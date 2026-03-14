using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace TowerDefense.Data.Blueprint
{
    public static class EntityBlueprintRuntimeFactory
    {
        private static readonly Dictionary<Type, MethodInfo> AddComponentDataMethods = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

        public static Entity Create(EntityManager entityManager, EntityBlueprintDocument document, float3 position)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            Entity entity = entityManager.CreateEntity();

            bool hasLocalTransform = false;
            for (int i = 0; i < document.components.Count; i++)
            {
                Type type = Type.GetType(document.components[i].componentTypeName);
                if (type == typeof(LocalTransform))
                {
                    hasLocalTransform = true;
                    break;
                }
            }

            if (!hasLocalTransform)
                entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));

            for (int i = 0; i < document.components.Count; i++)
            {
                ComponentRecord record = document.components[i];
                if (string.IsNullOrEmpty(record.componentTypeName))
                    continue;

                Type componentType = Type.GetType(record.componentTypeName);
                if (componentType == null)
                    continue;
                if (!typeof(IComponentData).IsAssignableFrom(componentType))
                    continue;
                if (!componentType.IsValueType)
                    continue;

                object componentValue = Activator.CreateInstance(componentType);
                ApplyFields(componentType, ref componentValue, record.fields, entity);
                AddComponentData(entityManager, entity, componentType, componentValue);
            }

            return entity;
        }

        private static void ApplyFields(Type componentType, ref object componentValue, List<FieldRecord> fieldRecords, Entity self)
        {
            if (!FieldCache.TryGetValue(componentType, out FieldInfo[] fields))
            {
                fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public);
                FieldCache[componentType] = fields;
            }

            for (int i = 0; i < fieldRecords.Count; i++)
            {
                FieldRecord fieldRecord = fieldRecords[i];
                if (string.IsNullOrEmpty(fieldRecord.fieldPath))
                    continue;

                for (int f = 0; f < fields.Length; f++)
                {
                    FieldInfo fieldInfo = fields[f];
                    if (!string.Equals(fieldInfo.Name, fieldRecord.fieldPath, StringComparison.Ordinal))
                        continue;

                    object value;
                    if (fieldInfo.FieldType == typeof(Entity) && string.Equals(fieldRecord.serializedValue, "$self", StringComparison.Ordinal))
                    {
                        value = self;
                    }
                    else if (!EntityBlueprintTypeUtility.TryDeserializeValue(fieldInfo.FieldType, fieldRecord.serializedValue, out value))
                    {
                        continue;
                    }

                    fieldInfo.SetValue(componentValue, value);
                    break;
                }
            }
        }

        private static void AddComponentData(EntityManager entityManager, Entity entity, Type componentType, object componentValue)
        {
            if (!AddComponentDataMethods.TryGetValue(componentType, out MethodInfo method))
            {
                method = typeof(EntityBlueprintRuntimeFactory)
                    .GetMethod(nameof(AddComponentDataGeneric), BindingFlags.Static | BindingFlags.NonPublic)
                    ?.MakeGenericMethod(componentType);
                AddComponentDataMethods[componentType] = method;
            }

            method?.Invoke(null, new[] { (object)entityManager, (object)entity, componentValue });
        }

        private static void AddComponentDataGeneric<T>(EntityManager entityManager, Entity entity, object value)
            where T : unmanaged, IComponentData
        {
            entityManager.AddComponentData(entity, (T)value);
        }
    }
}



