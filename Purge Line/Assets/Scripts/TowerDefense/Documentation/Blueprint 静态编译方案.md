# Blueprint 静态编译方案

Unity DOTS · EntityBlueprintDocument → MemoryPack 二进制 → 极速实体实例化

## 1. 设计目标

编辑器保留完整的反射+二进制蓝图工作流，运行时彻底绕开反射，实现与手写 Prefab 等价的零开销实例化。

|角色|说明|
|---|---|
|编辑器路径|EntityBlueprintDocument → 反射 → 可读字段（当前方案不变）|
|运行时路径|CompiledBlueprint.bin → TypeIndex 数组 + rawBytes 数组 → Instantiate|
|编译触发点|资产保存 / Build 时，由 AssetPostprocessor 自动触发|
|反射次数|运行时启动仅执行一次 Type.GetType()，之后全走索引|
## 2. 为什么 rawBytes → Chunk 是安全的

核心依据：**IComponentData 强制要求 blittable 值类型**。

- blittable 的定义是「托管/非托管内存布局完全一致，可直接 memcpy」。

- TypeManager 在注册每个组件类型时做 blittable 校验，非 blittable 的类型根本无法成为 IComponentData。

- Chunk 内每列组件数据本身就是连续的 blittable 字节数组（SOA 布局）。

- EntityManager.Instantiate 内部也是相同的 chunk-level memcpy，WriteComponentRaw 与其完全等价。

IL2CPP / Burst 不改变 blittable struct 的内存布局，这是 DOTS 的基础契约。

唯一需要注意：TypeIndex 是运行时动态分配的，不能跨构建持久化。持久化时存类型全名，运行时启动时解析一次即可。

## 3. 数据结构

### 3.1 CompiledBlueprint（MemoryPack 序列化目标）

```C#

[MemoryPackable]
public partial class CompiledBlueprint
{
    public string blueprintId;
    public int    version;          // 用于校验，防止旧 .bin 被新代码读
    public ComponentRecord[] records;

    [MemoryPackable]
    public partial struct ComponentRecord
    {
        public string typeName;     // 类型全名，跨构建稳定
        public byte[] rawBytes;     // blittable struct 的原始内存字节
    }
}
```

### 3.2 ReadyBlueprint（运行时内存结构，不落盘）

```C#

public struct ReadyBlueprint
{
    public TypeIndex[] TypeIndices;   // 已解析，直接用于 API 调用
    public byte[][]    RawBytes;       // 对应组件默认值
    public Entity      PrefabEntity;   // Warmup 后缓存的 Prefab Entity
}
```

## 4. 编译器（Editor Only）

BlueprintCompiler 在保存资产时运行，把反射的结果「固化」成字节数组。反射只在这里发生，运行时完全不涉及。

```C#

#if UNITY_EDITOR
public static class BlueprintCompiler
{
    public static CompiledBlueprint Compile(EntityBlueprintDocument doc)
    {
        var compiled = new CompiledBlueprint { blueprintId = doc.id, version = 1 };
        var records  = new List<CompiledBlueprint.ComponentRecord>();

        foreach (var rec in doc.components)
        {
            Type type = Type.GetType(rec.componentTypeName);
            if (type == null || !type.IsValueType) continue;
            if (!typeof(IComponentData).IsAssignableFrom(type)) continue;

            // 用现有反射逻辑还原组件值
            object value = Activator.CreateInstance(type);
            ApplyFields(type, ref value, rec.fields, Entity.Null);

            // 把 struct 内存直接 dump 成字节数组
            int size = UnsafeUtility.SizeOf(type);
            byte[] bytes = new byte[size];
            GCHandle pin = GCHandle.Alloc(value, GCHandleType.Pinned);
            Marshal.Copy(pin.AddrOfPinnedObject(), bytes, 0, size);
            pin.Free();

            records.Add(new CompiledBlueprint.ComponentRecord
            {
                typeName = type.AssemblyQualifiedName,  // 存全名，不存 TypeIndex
                rawBytes = bytes
            });
        }

        compiled.records = records.ToArray();
        return compiled;
    }

    // 资产保存时自动触发
    class AutoCompile : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                var doc = AssetDatabase.LoadAssetAtPath<EntityBlueprintDocument>(path);
                if (doc == null) continue;
                var bin = MemoryPackSerializer.Serialize(Compile(doc));
                File.WriteAllBytes(path.Replace(".asset", ".bin"), bin);
            }
        }
    }
}
#endif
```

## 5. 运行时加载与 Warmup

启动时对每个 Blueprint 执行一次加载，反射只在这里出现一次；Warmup 结束后所有操作零反射。

```C#

public class BlueprintRegistry
{
    private readonly Dictionary<string, ReadyBlueprint> _ready = new();

    public void Load(string blueprintId, byte[] bin, EntityManager em)
    {
        var compiled = MemoryPackSerializer.Deserialize<CompiledBlueprint>(bin);

        // ── 解析 TypeIndex（仅此一次反射）──────────────────────────
        int n = compiled.records.Length;
        var typeIndices = new TypeIndex[n];
        var rawBytes    = new byte[n][];

        for (int i = 0; i < n; i++)
        {
            Type t = Type.GetType(compiled.records[i].typeName);
            typeIndices[i] = TypeManager.GetTypeIndex(t);
            rawBytes[i]    = compiled.records[i].rawBytes;
        }

        // ── 创建 Prefab Entity（预建 Archetype + 写入默认值）──────
        var compTypes = new NativeArray<ComponentType>(n, Allocator.Temp);
        for (int i = 0; i < n; i++)
            compTypes[i] = ComponentType.FromTypeIndex(typeIndices[i]);

        Entity prefab = em.CreateEntity(em.CreateArchetype(compTypes));
        compTypes.Dispose();
        em.AddComponent<Prefab>(prefab);

        // ── 写入默认组件值（unsafe memcpy）──────────────────────────
        unsafe
        {
            for (int i = 0; i < n; i++)
            {
                void* dst = em.GetComponentDataRawRW(prefab, typeIndices[i]);
                fixed (byte* src = rawBytes[i])
                    UnsafeUtility.MemCpy(dst, src, rawBytes[i].Length);
            }
        }

        _ready[blueprintId] = new ReadyBlueprint
        {
            TypeIndices  = typeIndices,
            RawBytes     = rawBytes,
            PrefabEntity = prefab
        };
    }

    public Entity Prefab(string id) => _ready[id].PrefabEntity;
}
```

## 6. 批量实例化

Warmup 完成后，每次生成 Entity 只需两步：Instantiate（chunk memcpy）+ SetComponentData 修改位置。

```C#

public static void SpawnBatch(
    EntityManager em, Entity prefab,
    NativeArray<float3> positions, NativeArray<Entity> outEntities)
{
    // chunk-level memcpy，极快
    em.Instantiate(prefab, outEntities);

    // 仅 patch 位置（各 Entity 不同）
    for (int i = 0; i < outEntities.Length; i++)
        em.SetComponentData(outEntities[i], LocalTransform.FromPosition(positions[i]));
}
```

如需更高吞吐，把 position patch 搬进 IJobParallelFor + Burst：

```C#

[BurstCompile]
struct PatchTransformJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3>    Positions;
    [ReadOnly] public NativeArray<Entity>    Entities;
    public ComponentLookup<LocalTransform>   Transforms;

    public void Execute(int i)
        => Transforms[Entities[i]] = LocalTransform.FromPosition(Positions[i]);
}
```

## 7. 完整数据流

### 【编辑器】

```Plain Text

EntityBlueprintDocument（.asset）
    ↓  保存时 AssetPostprocessor 触发
    ↓  BlueprintCompiler.Compile()   ← 反射在此发生，仅一次
    ↓  MemoryPackSerializer.Serialize()
    →  compiled/{id}.bin             ← 唯一的运行时输入
```

### 【运行时启动】

```Plain Text

File.ReadAllBytes / Addressables
    ↓  MemoryPackSerializer.Deserialize<CompiledBlueprint>()
    ↓  Type.GetType() + TypeManager.GetTypeIndex()  ← 仅此一次反射
    ↓  CreateArchetype + Prefab Entity + unsafe MemCpy
    →  ReadyBlueprint（TypeIndex[] + byte[][] + PrefabEntity）
```

### 【每次生成 Entity】

```Plain Text

EntityManager.Instantiate(prefab, outEntities)  ← chunk memcpy，零反射
SetComponentData / PatchTransformJob             ← 仅 patch 位置
    →  Entity 在 World 中就位
```

## 8. 注意事项

|注意点|处理方式|
|---|---|
|TypeIndex 不可持久化|存 AssemblyQualifiedName，启动时解析一次，之后全走 TypeIndex 索引|
|rawBytes 尺寸校验|Load 时用 TypeManager.GetTypeInfo(ti).TypeSize 与 rawBytes.Length 对比，不匹配则报错|
|version 字段|编辑器改变组件结构后递增 version，运行时检测不匹配时拒绝加载并提示重新编译|
|含 LocalTransform|Warmup 时记录 LocalTransform 的位置，SpawnBatch 里 SetComponentData 覆盖即可|
|BlobAsset 引用|rawBytes 无法携带 BlobAssetReference，BlobAsset 需在 Warmup 后单独写入|
|仅限 blittable 组件|含 managed 字段（string、class）的组件无法走此路径，需单独处理或转为 FixedString|
---

编辑器保留反射，出包后零反射。  编译期的工作做得越多，运行时的代价就越低。