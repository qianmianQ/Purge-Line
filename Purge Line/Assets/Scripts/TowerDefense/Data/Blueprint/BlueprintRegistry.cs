using System;
using System.Collections.Generic;
using System.Reflection;
using MemoryPack;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace TowerDefense.Data.Blueprint
{
    // 运行时蓝图的内存表示（主线程持有，不落盘）
    // 注意：TypeIndices / RawBytes 是托管数组，不能直接传入 Job
    // Job 安全访问入口：BlueprintRegistry.GetJobReadOnly()
    public struct ReadyBlueprint
    {
        // 已解析的 TypeIndex 数组（索引对应 CompiledBlueprint.records）
        // TypeIndex.Null 表示该位置的类型解析失败（已跳过）
        public TypeIndex[] TypeIndices;

        // 对应各组件的原始字节（Tag 组件或解析失败的位置为 null）
        public byte[][] RawBytes;

        // Warmup 后缓存的 Prefab Entity，供 EntityManager.Instantiate 使用
        public Entity PrefabEntity;
    }

    // 运行时蓝图注册表
    //
    // 设计说明：
    //   主线程负责 Load()（反射解析 + Prefab Entity 创建），全部 Load 完成后
    //   调用 FreezeForJobAccess() 将 PrefabEntity 固化到 NativeParallelHashMap
    //   Job 通过 GetJobReadOnly() 安全访问，无需 Dictionary（非线程安全）
    //
    // 架构限制（以下功能架构不支持）：
    //   - IBufferElementData、BlobAssetReference、含 managed 字段的组件：见 CompiledBlueprint 注释
    //   - FreezeForJobAccess 的 key（blueprintId）最长 61 字节 UTF-8（FixedString64Bytes 限制）
    //     超出长度的蓝图无法加入 NativeView，但主线程 TryGetPrefab / GetPrefab 仍可正常访问
    public class BlueprintRegistry : IDisposable
    {
        // 主线程读写（非线程安全，不能在 Job 中访问）
        private readonly Dictionary<string, ReadyBlueprint> _ready = new();

        // Job 安全只读视图（FreezeForJobAccess 后生效）
        // Key: blueprintId（FixedString64Bytes，最长 61 字节 UTF-8）
        // Value: PrefabEntity（Instantiate 只需要 Entity，TypeIndices 不进 Job）
        private NativeParallelHashMap<FixedString64Bytes, Entity> _nativeView;
        private bool _frozen;
        private bool _disposed;

        // EntityManager.GetComponentDataRawRW / SetComponentDataRaw 在 Unity.Entities 1.x 中是 internal，
        // 此处通过泛型辅助方法 + 反射调用 public em.SetComponentData<T>()
        // 每种组件类型缓存一次 MethodInfo，Warmup 一次性开销
        private static readonly Dictionary<Type, MethodInfo> s_setDataMethods = new();

        // 加载并 Warmup 一个蓝图
        // 主线程调用，每个 blueprintId 只需调用一次
        // bin：MemoryPack 序列化的 CompiledBlueprint 字节（路径由外部写入，此处只收字节）
        // 返回 false 时通过 error 输出原因，不抛异常
        public bool TryLoad(string blueprintId, byte[] bin, EntityManager em, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(blueprintId))
            {
                error = "blueprintId is null or empty";
                Debug.LogError($"[BlueprintRegistry] {error}");
                return false;
            }

            if (bin == null || bin.Length == 0)
            {
                error = $"Binary data is null or empty for blueprint '{blueprintId}'";
                Debug.LogError($"[BlueprintRegistry] {error}");
                return false;
            }

            // ── 反序列化 ────────────────────────────────────────────────────────────
            CompiledBlueprint compiled;
            try
            {
                compiled = MemoryPackSerializer.Deserialize<CompiledBlueprint>(bin);
            }
            catch (Exception ex)
            {
                error = $"Deserialization failed for '{blueprintId}': {ex.Message}";
                Debug.LogError($"[BlueprintRegistry] {error}");
                return false;
            }

            if (compiled == null)
            {
                error = $"Deserialized result is null for '{blueprintId}'";
                Debug.LogError($"[BlueprintRegistry] {error}");
                return false;
            }

            // ── version 校验 ─────────────────────────────────────────────────────────
            if (compiled.version != CompiledBlueprint.CurrentVersion)
            {
                error = $"Version mismatch for '{blueprintId}': expected {CompiledBlueprint.CurrentVersion},"
                      + $" got {compiled.version}. Please recompile the blueprint.";
                Debug.LogError($"[BlueprintRegistry] {error}");
                return false;
            }

            var records = compiled.records ?? Array.Empty<CompiledComponentRecord>();
            int n = records.Length;

            if (n == 0)
                Debug.LogWarning($"[BlueprintRegistry] Blueprint '{blueprintId}' has no component records,"
                               + " will create empty prefab entity");

            // ── 解析 TypeIndex（仅此一次 Type.GetType + TypeManager 反射）──────────
            var typeIndices   = new TypeIndex[n];
            var resolvedTypes = new Type[n];      // 缓存 Type，供 WriteComponentFromBytes 使用
            var rawBytesArr   = new byte[n][];
            int validCount    = 0;

            for (int i = 0; i < n; i++)
            {
                var rec = records[i];

                if (rec == null || string.IsNullOrWhiteSpace(rec.typeName))
                {
                    Debug.LogWarning($"[BlueprintRegistry] Record[{i}] in blueprint '{blueprintId}'"
                                   + " has empty typeName, skipped");
                    continue;
                }

                // 类型加载失败：降级跳过，不崩溃
                Type t = Type.GetType(rec.typeName);
                if (t == null)
                {
                    Debug.LogError($"[BlueprintRegistry] Type not found: '{rec.typeName}'"
                                 + $" in blueprint '{blueprintId}', component skipped");
                    continue;
                }

                // TypeIndex 获取失败（类型未注册到 TypeManager）：降级跳过
                TypeIndex ti;
                try
                {
                    ti = TypeManager.GetTypeIndex(t);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BlueprintRegistry] TypeManager.GetTypeIndex failed for '{rec.typeName}'"
                                 + $" in blueprint '{blueprintId}': {ex.Message}, component skipped");
                    continue;
                }

                if (ti == TypeIndex.Null)
                {
                    Debug.LogError($"[BlueprintRegistry] TypeIndex is Null for '{rec.typeName}'"
                                 + $" in blueprint '{blueprintId}', component skipped");
                    continue;
                }

                // rawBytes 尺寸校验（Tag 组件跳过；防止旧 .bytes 与新代码内存布局不一致导致越界）
                if (!rec.isTagComponent && rec.rawBytes != null && rec.rawBytes.Length > 0)
                {
                    int expectedSize = TypeManager.GetTypeInfo(ti).TypeSize;
                    if (expectedSize > 0 && rec.rawBytes.Length != expectedSize)
                    {
                        // 尺寸不匹配：零内存降级，打印警告提示重新编译
                        Debug.LogWarning($"[BlueprintRegistry] rawBytes size mismatch for '{rec.typeName}'"
                                       + $" in blueprint '{blueprintId}':"
                                       + $" expected {expectedSize} bytes, got {rec.rawBytes.Length}."
                                       + " Blueprint may be outdated. Component data reset to zero default.");
                        rawBytesArr[i] = null;
                    }
                    else
                    {
                        rawBytesArr[i] = rec.rawBytes;
                    }
                }
                // Tag 组件或空 rawBytes：不写入数据（Archetype 已包含组件类型即可）

                typeIndices[i]   = ti;
                resolvedTypes[i] = t;
                validCount++;
            }

            // ── 创建 Prefab Entity（预建 Archetype）────────────────────────────────
            var compTypesArr = new NativeArray<ComponentType>(validCount, Allocator.Temp);
            int compIdx = 0;
            for (int i = 0; i < n; i++)
            {
                if (typeIndices[i] != TypeIndex.Null)
                    compTypesArr[compIdx++] = ComponentType.FromTypeIndex(typeIndices[i]);
            }

            Entity prefab;
            try
            {
                prefab = em.CreateEntity(em.CreateArchetype(compTypesArr));
                em.AddComponent<Prefab>(prefab);
            }
            catch (Exception ex)
            {
                error = $"Failed to create prefab entity for '{blueprintId}': {ex.Message}";
                Debug.LogError($"[BlueprintRegistry] {error}");
                return false;
            }
            finally
            {
                compTypesArr.Dispose();
            }

            // ── 写入默认组件值 ────────────────────────────────────────────────────────
            // EntityManager.GetComponentDataRawRW / SetComponentDataRaw 在 Unity.Entities 1.x 中是 internal。
            // 此处通过泛型辅助方法 WriteComponentGeneric<T> + MethodInfo 缓存绕过：
            //   bytes → T value（unsafe MemCpy）→ em.SetComponentData<T>（public API）
            // Warmup 是一次性开销，每种类型缓存一次 MethodInfo，后续无额外反射。
            //
            // Tag 组件（零大小，isTagComponent == true）：已通过 Archetype 包含，无需写入数据；
            // 零长度 rawBytes 的 MemCpy 虽无崩溃风险，但属冗余操作，统一跳过。
            for (int i = 0; i < n; i++)
            {
                if (typeIndices[i] == TypeIndex.Null || resolvedTypes[i] == null)
                    continue;

                var rec = records[i];

                if (rec.isTagComponent || rawBytesArr[i] == null || rawBytesArr[i].Length == 0)
                    continue;

                WriteComponentFromBytes(em, prefab, resolvedTypes[i], rawBytesArr[i]);
            }

            _ready[blueprintId] = new ReadyBlueprint
            {
                TypeIndices  = typeIndices,
                RawBytes     = rawBytesArr,
                PrefabEntity = prefab
            };

            // Load 后需重新 FreezeForJobAccess，旧的 NativeView 已过期
            _frozen = false;

            return true;
        }

        // 主线程访问：获取 PrefabEntity（不存在时返回 false）
        public bool TryGetPrefab(string id, out Entity prefab)
        {
            if (_ready.TryGetValue(id, out var blueprint))
            {
                prefab = blueprint.PrefabEntity;
                return true;
            }

            prefab = Entity.Null;
            return false;
        }

        // 主线程访问：获取 PrefabEntity（不存在时抛出异常）
        public Entity GetPrefab(string id)
        {
            if (!_ready.TryGetValue(id, out var blueprint))
                throw new KeyNotFoundException($"Blueprint '{id}' not loaded in BlueprintRegistry");
            return blueprint.PrefabEntity;
        }

        // 将所有已加载蓝图的 PrefabEntity 固化为 NativeParallelHashMap，供 Job 安全只读访问
        //
        // 调用时机：所有 TryLoad() 调用完成后、第一个依赖蓝图的 Job 调度前（主线程执行）
        // 每次新增 Load() 后需重新调用此方法
        //
        // 注意：blueprintId 超过 61 字节 UTF-8 的蓝图无法进入 NativeView
        //       主线程 TryGetPrefab / GetPrefab 仍可正常访问这类蓝图
        public void FreezeForJobAccess()
        {
            if (_nativeView.IsCreated)
                _nativeView.Dispose();

            _nativeView = new NativeParallelHashMap<FixedString64Bytes, Entity>(
                _ready.Count, Allocator.Persistent);

            foreach (var kv in _ready)
            {
                FixedString64Bytes key;
                try
                {
                    key = new FixedString64Bytes(kv.Key);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BlueprintRegistry] blueprintId '{kv.Key}' too long for"
                                   + $" FixedString64Bytes ({ex.Message}), excluded from NativeView."
                                   + " Use TryGetPrefab() on main thread for this blueprint.");
                    continue;
                }

                _nativeView.TryAdd(key, kv.Value.PrefabEntity);
            }

            _frozen = true;
        }

        // 返回供 Job 只读访问的视图（必须在 FreezeForJobAccess() 后调用）
        // Job 字段声明示例：
        //   [ReadOnly] public NativeParallelHashMap<FixedString64Bytes, Entity>.ReadOnly BlueprintView;
        public NativeParallelHashMap<FixedString64Bytes, Entity>.ReadOnly GetJobReadOnly()
        {
            if (!_frozen || !_nativeView.IsCreated)
                throw new InvalidOperationException(
                    "[BlueprintRegistry] Must call FreezeForJobAccess() before GetJobReadOnly()");
            return _nativeView.AsReadOnly();
        }

        public bool IsLoaded(string id) => _ready.ContainsKey(id);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_nativeView.IsCreated)
                _nativeView.Dispose();
        }

        // ── 内部：bytes → T → em.SetComponentData<T>（绕过 internal raw API）────────

        private static void WriteComponentFromBytes(
            EntityManager em, Entity entity, Type componentType, byte[] rawBytes)
        {
            if (!s_setDataMethods.TryGetValue(componentType, out var method))
            {
                method = typeof(BlueprintRegistry)
                    .GetMethod(nameof(WriteComponentGeneric),
                        BindingFlags.Static | BindingFlags.NonPublic)
                    ?.MakeGenericMethod(componentType);
                s_setDataMethods[componentType] = method;
            }

            if (method == null)
            {
                Debug.LogError($"[BlueprintRegistry] Failed to create WriteComponentGeneric<{componentType.Name}>,"
                             + " component data will remain at default zero value");
                return;
            }

            method.Invoke(null, new object[] { em, entity, rawBytes });
        }

        // 泛型辅助：将 rawBytes 以 unsafe MemCpy 写入 T，再通过 public SetComponentData 提交
        // T 约束为 unmanaged, IComponentData，确保 MemCpy 安全（blittable 契约）
        private static unsafe void WriteComponentGeneric<T>(
            EntityManager em, Entity entity, byte[] rawBytes)
            where T : unmanaged, IComponentData
        {
            T value = default;
            fixed (byte* src = rawBytes)
                UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref value), src, rawBytes.Length);
            em.SetComponentData(entity, value);
        }
    }
}
