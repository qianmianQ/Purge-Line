#if UNITY_EDITOR
using System;
using System.Globalization;
using TowerDefense.Data.Blueprint;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerDefense.Editor.Blueprint
{
    internal static class EntityBlueprintFieldDrawer
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static VisualElement BuildField(FieldRecord field, Action onFieldChanged)
        {
            Type fieldType = Type.GetType(field.fieldTypeName);
            if (fieldType == null)
                return BuildFallbackField(field, onFieldChanged);

            if (fieldType == typeof(bool))
            {
                bool value = bool.TryParse(field.serializedValue, out bool b) && b;
                var toggle = new Toggle(field.fieldPath) { value = value };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    field.serializedValue = evt.newValue.ToString();
                    onFieldChanged?.Invoke();
                });
                return toggle;
            }

            if (fieldType == typeof(int))
            {
                int.TryParse(field.serializedValue, NumberStyles.Integer, Culture, out int value);
                var input = new IntegerField(field.fieldPath) { value = value };
                input.RegisterValueChangedCallback(evt =>
                {
                    field.serializedValue = evt.newValue.ToString(Culture);
                    onFieldChanged?.Invoke();
                });
                return input;
            }

            if (fieldType == typeof(float))
            {
                float.TryParse(field.serializedValue, NumberStyles.Float, Culture, out float value);
                var input = new FloatField(field.fieldPath) { value = value };
                input.RegisterValueChangedCallback(evt =>
                {
                    field.serializedValue = evt.newValue.ToString(Culture);
                    onFieldChanged?.Invoke();
                });
                return input;
            }

            if (fieldType == typeof(string) || fieldType == typeof(FixedString64Bytes))
            {
                var input = new TextField(field.fieldPath) { value = field.serializedValue ?? string.Empty };
                input.RegisterValueChangedCallback(evt =>
                {
                    field.serializedValue = evt.newValue;
                    onFieldChanged?.Invoke();
                });
                return input;
            }

            if (fieldType == typeof(Entity))
            {
                var input = new TextField($"{field.fieldPath} (index:version or $self)")
                {
                    value = field.serializedValue ?? "0:0"
                };
                input.RegisterValueChangedCallback(evt =>
                {
                    field.serializedValue = evt.newValue;
                    onFieldChanged?.Invoke();
                });
                return input;
            }

            if (fieldType == typeof(Vector2))
                return BuildVector2(field, onFieldChanged);
            if (fieldType == typeof(Vector3))
                return BuildVector3(field, onFieldChanged);
            if (fieldType == typeof(Vector4))
                return BuildVector4(field, onFieldChanged);
            if (fieldType == typeof(float2))
                return BuildFloat2(field, onFieldChanged);
            if (fieldType == typeof(float3))
                return BuildFloat3(field, onFieldChanged);
            if (fieldType == typeof(float4))
                return BuildFloat4(field, onFieldChanged);

            return BuildFallbackField(field, onFieldChanged);
        }

        private static VisualElement BuildVector2(FieldRecord field, Action onFieldChanged)
        {
            EntityBlueprintTypeUtility.TryDeserializeValue(typeof(Vector2), field.serializedValue, out object value);
            var vector = value is Vector2 v ? v : Vector2.zero;
            var input = new Vector2Field(field.fieldPath) { value = vector };
            input.RegisterValueChangedCallback(evt =>
            {
                field.serializedValue = EntityBlueprintTypeUtility.SerializeValue(typeof(Vector2), evt.newValue);
                onFieldChanged?.Invoke();
            });
            return input;
        }

        private static VisualElement BuildVector3(FieldRecord field, Action onFieldChanged)
        {
            EntityBlueprintTypeUtility.TryDeserializeValue(typeof(Vector3), field.serializedValue, out object value);
            var vector = value is Vector3 v ? v : Vector3.zero;
            var input = new Vector3Field(field.fieldPath) { value = vector };
            input.RegisterValueChangedCallback(evt =>
            {
                field.serializedValue = EntityBlueprintTypeUtility.SerializeValue(typeof(Vector3), evt.newValue);
                onFieldChanged?.Invoke();
            });
            return input;
        }

        private static VisualElement BuildVector4(FieldRecord field, Action onFieldChanged)
        {
            EntityBlueprintTypeUtility.TryDeserializeValue(typeof(Vector4), field.serializedValue, out object value);
            var vector = value is Vector4 v ? v : Vector4.zero;
            var input = new Vector4Field(field.fieldPath) { value = vector };
            input.RegisterValueChangedCallback(evt =>
            {
                field.serializedValue = EntityBlueprintTypeUtility.SerializeValue(typeof(Vector4), evt.newValue);
                onFieldChanged?.Invoke();
            });
            return input;
        }

        private static VisualElement BuildFloat2(FieldRecord field, Action onFieldChanged)
        {
            EntityBlueprintTypeUtility.TryDeserializeValue(typeof(float2), field.serializedValue, out object value);
            var data = value is float2 v ? v : float2.zero;
            var input = new Vector2Field(field.fieldPath) { value = new Vector2(data.x, data.y) };
            input.RegisterValueChangedCallback(evt =>
            {
                field.serializedValue = EntityBlueprintTypeUtility.SerializeValue(typeof(float2), new float2(evt.newValue.x, evt.newValue.y));
                onFieldChanged?.Invoke();
            });
            return input;
        }

        private static VisualElement BuildFloat3(FieldRecord field, Action onFieldChanged)
        {
            EntityBlueprintTypeUtility.TryDeserializeValue(typeof(float3), field.serializedValue, out object value);
            var data = value is float3 v ? v : float3.zero;
            var input = new Vector3Field(field.fieldPath) { value = new Vector3(data.x, data.y, data.z) };
            input.RegisterValueChangedCallback(evt =>
            {
                field.serializedValue = EntityBlueprintTypeUtility.SerializeValue(typeof(float3), new float3(evt.newValue.x, evt.newValue.y, evt.newValue.z));
                onFieldChanged?.Invoke();
            });
            return input;
        }

        private static VisualElement BuildFloat4(FieldRecord field, Action onFieldChanged)
        {
            EntityBlueprintTypeUtility.TryDeserializeValue(typeof(float4), field.serializedValue, out object value);
            var data = value is float4 v ? v : float4.zero;
            var input = new Vector4Field(field.fieldPath) { value = new Vector4(data.x, data.y, data.z, data.w) };
            input.RegisterValueChangedCallback(evt =>
            {
                field.serializedValue = EntityBlueprintTypeUtility.SerializeValue(typeof(float4), new float4(evt.newValue.x, evt.newValue.y, evt.newValue.z, evt.newValue.w));
                onFieldChanged?.Invoke();
            });
            return input;
        }

        private static VisualElement BuildFallbackField(FieldRecord field, Action onFieldChanged)
        {
            var input = new TextField($"{field.fieldPath} ({field.fieldTypeName})")
            {
                value = field.serializedValue ?? string.Empty
            };
            input.RegisterValueChangedCallback(evt =>
            {
                field.serializedValue = evt.newValue;
                onFieldChanged?.Invoke();
            });
            return input;
        }
    }
}
#endif


