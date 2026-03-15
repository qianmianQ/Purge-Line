#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using MemoryPack;
using TowerDefense.Data.Blueprint;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace TowerDefense.Editor.Blueprint
{
    // Blueprint 静态编译器（Editor Only）
    //
    // 职责：将 EntityBlueprintDocument（含字符串字段描述）编译为 CompiledBlueprint（字节数组）
    //   反射 **仅在此处发生**，运行时 BlueprintRegistry.TryLoad 中仅执行一次 Type.GetType，
    //   此后全走 TypeIndex 索引，零反射。
    //
    // 调用方式：
    //   var (ok, warnings, compiled) = BlueprintCompiler.Compile(doc);
    //   byte[] bytes = MemoryPackSerializer.Serialize(compiled);
    //   File.WriteAllBytes(outputPath, bytes);  // 路径由外部写入
    //
    // 架构限制（以下组件类型跳过并输出警告，不会阻断编译）：
    //   - 非 IComponentData 的类型（包括 IBufferElementData，不支持）
    //   - 含 managed 字段的类型（不是 blittable 值类型）
    //   - 类型名称无效或无法通过 Type.GetType() 解析的条目
    //   - Entity 字段：编译时写入 Entity.Null，运行时无法持久化 Entity 引用
    //
    // 资产保存时自动触发：暂未实现（由外部决定调用时机）
    public static class BlueprintCompiler
    {
        public static (bool success, string warnings, CompiledBlueprint compiled) Compile(EntityBlueprintDocument doc)
        {
            return Compile(doc, string.Empty);
        }

        // 编译结果（使用值元组返回，避免为单一用途引入额外类型）
        // success    : 整体是否成功（false 表示 doc 本身无效，无法产出 CompiledBlueprint）
        // warnings   : 非致命警告（跳过的组件说明），null 表示无警告
        // compiled   : 编译结果，success == false 时为 null
        public static (bool success, string warnings, CompiledBlueprint compiled) Compile(
            EntityBlueprintDocument doc, string blueprintHash)
        {
            if (doc == null)
                return (false, null, null);

            if (string.IsNullOrWhiteSpace(doc.blueprintName))
                return (false, "blueprintName is empty or null", null);

            var compiled = new CompiledBlueprint
            {
                blueprintId = doc.blueprintName,
                blueprintHash = blueprintHash ?? string.Empty,
                version     = CompiledBlueprint.CurrentVersion
            };

            var records  = new List<CompiledComponentRecord>();
            var warnings = new List<string>();

            foreach (var rec in doc.components)
            {
                if (rec == null || string.IsNullOrWhiteSpace(rec.componentTypeName))
                {
                    warnings.Add("Skipped: record has empty componentTypeName");
                    continue;
                }

                // ── 类型加载失败：降级跳过，不崩溃 ──────────────────────────────────
                Type type = Type.GetType(rec.componentTypeName);
                if (type == null)
                {
                    string msg = $"Type not found, skipped: {rec.componentTypeName}";
                    warnings.Add(msg);
                    Debug.LogWarning($"[BlueprintCompiler] {msg}");
                    continue;
                }

                if (!type.IsValueType)
                {
                    string msg = $"Not a value type, skipped: {rec.componentTypeName}";
                    warnings.Add(msg);
                    Debug.LogWarning($"[BlueprintCompiler] {msg}");
                    continue;
                }

                // IBufferElementData 完全不支持（无法通过 rawBytes memcpy 初始化 DynamicBuffer）
                // ISharedComponentData 同样不走此路径
                if (!typeof(IComponentData).IsAssignableFrom(type))
                {
                    string msg = $"Not IComponentData (e.g. IBufferElementData is not supported), skipped: {rec.componentTypeName}";
                    warnings.Add(msg);
                    Debug.LogWarning($"[BlueprintCompiler] {msg}");
                    continue;
                }

                // ── 检测 Tag 组件（无实例字段 == 零大小）────────────────────────────
                // Tag 组件在 DOTS TypeManager 中 TypeSize == 0，无需（也不能）写入数据
                // 仅通过 Archetype 包含，rawBytes 置 null，isTagComponent = true
                FieldInfo[] instanceFields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                bool isTag = instanceFields.Length == 0;

                byte[] rawBytes = null;

                if (!isTag)
                {
                    // 用字段描述还原 struct 值（此处是反射发生的唯一位置）
                    object value = Activator.CreateInstance(type);
                    ApplyFieldsForCompile(type, ref value, rec.fields);

                    // 将 struct 内存 dump 成字节数组（blittable 契约保证内存布局跨 IL2CPP/Burst 稳定）
                    int size = UnsafeUtility.SizeOf(type);
                    if (size <= 0)
                    {
                        // UnsafeUtility.SizeOf 报告 0，视为 Tag 组件（防御性处理）
                        isTag = true;
                        string msg = $"UnsafeUtility.SizeOf == 0 for {type.FullName}, treated as tag component";
                        warnings.Add(msg);
                        Debug.LogWarning($"[BlueprintCompiler] {msg}");
                    }
                    else
                    {
                        rawBytes = new byte[size];
                        GCHandle pin = GCHandle.Alloc(value, GCHandleType.Pinned);
                        try
                        {
                            Marshal.Copy(pin.AddrOfPinnedObject(), rawBytes, 0, size);
                        }
                        finally
                        {
                            pin.Free();
                        }
                    }
                }

                records.Add(new CompiledComponentRecord
                {
                    typeName      = type.AssemblyQualifiedName, // 存全名，TypeIndex 不可跨构建持久化
                    rawBytes      = rawBytes,
                    isTagComponent = isTag
                });
            }

            compiled.records = records.ToArray();

            string warningText = warnings.Count > 0 ? string.Join("\n", warnings) : null;
            return (true, warningText, compiled);
        }

        // 便捷方法：直接返回序列化后的字节（供直接写文件使用）
        // 有警告时通过 Debug.LogWarning 输出，不影响返回
        public static byte[] CompileToBytes(EntityBlueprintDocument doc, string blueprintHash)
        {
            var (success, warningText, compiled) = Compile(doc, blueprintHash);
            if (!success)
                throw new InvalidOperationException(
                    $"[BlueprintCompiler] Compile failed: {warningText ?? "EntityBlueprintDocument is null or invalid"}");

            if (!string.IsNullOrWhiteSpace(warningText))
                Debug.LogWarning($"[BlueprintCompiler] Compile warnings for '{doc.blueprintName}':\n{warningText}");

            return MemoryPackSerializer.Serialize(compiled);
        }

        public static byte[] CompileToBytes(EntityBlueprintDocument doc)
        {
            return CompileToBytes(doc, string.Empty);
        }

        // ── 字段反射还原（Editor Only，仅 Compile 时调用一次）──────────────────────
        // 与 EntityBlueprintRuntimeFactory 的逻辑对应，但不处理 $self（编译时无有效 Entity）
        private static void ApplyFieldsForCompile(
            Type componentType, ref object componentValue, List<FieldRecord> fieldRecords)
        {
            if (fieldRecords == null || fieldRecords.Count == 0)
                return;

            FieldInfo[] publicFields = componentType.GetFields(
                BindingFlags.Instance | BindingFlags.Public);

            foreach (var fieldRecord in fieldRecords)
            {
                if (string.IsNullOrEmpty(fieldRecord.fieldPath))
                    continue;

                foreach (var fieldInfo in publicFields)
                {
                    if (!string.Equals(fieldInfo.Name, fieldRecord.fieldPath, StringComparison.Ordinal))
                        continue;

                    // Entity 字段：编译期写入 Entity.Null（运行时无法持久化 Entity 引用）
                    if (fieldInfo.FieldType == typeof(Entity))
                    {
                        fieldInfo.SetValue(componentValue, Entity.Null);
                        break;
                    }

                    if (EntityBlueprintTypeUtility.TryDeserializeValue(
                            fieldInfo.FieldType, fieldRecord.serializedValue, out object value))
                    {
                        fieldInfo.SetValue(componentValue, value);
                    }

                    break;
                }
            }
        }
    }
}
#endif
