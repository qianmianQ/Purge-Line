using System;
using System.Globalization;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TowerDefense.Data.Blueprint
{
    public static class EntityBlueprintTypeUtility
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static bool IsSupportedFieldType(Type fieldType)
        {
            return fieldType == typeof(int)
                   || fieldType == typeof(uint)
                   || fieldType == typeof(short)
                   || fieldType == typeof(ushort)
                   || fieldType == typeof(long)
                   || fieldType == typeof(ulong)
                   || fieldType == typeof(float)
                   || fieldType == typeof(double)
                   || fieldType == typeof(bool)
                   || fieldType == typeof(byte)
                   || fieldType == typeof(sbyte)
                   || fieldType == typeof(string)
                   || fieldType == typeof(Vector2)
                   || fieldType == typeof(Vector3)
                   || fieldType == typeof(Vector4)
                   || fieldType == typeof(float2)
                   || fieldType == typeof(float3)
                   || fieldType == typeof(float4)
                   || fieldType == typeof(int2)
                   || fieldType == typeof(int3)
                   || fieldType == typeof(int4)
                   || fieldType == typeof(FixedString64Bytes)
                   || fieldType == typeof(Entity);
        }

        public static string SerializeValue(Type fieldType, object value)
        {
            if (fieldType == typeof(FixedString64Bytes))
                return ((FixedString64Bytes)value).ToString();

            if (fieldType == typeof(Entity))
            {
                var entity = (Entity)value;
                return $"{entity.Index}:{entity.Version}";
            }

            if (fieldType == typeof(Vector2))
            {
                var v = (Vector2)value;
                return $"{v.x.ToString(Culture)},{v.y.ToString(Culture)}";
            }

            if (fieldType == typeof(Vector3))
            {
                var v = (Vector3)value;
                return $"{v.x.ToString(Culture)},{v.y.ToString(Culture)},{v.z.ToString(Culture)}";
            }

            if (fieldType == typeof(Vector4))
            {
                var v = (Vector4)value;
                return $"{v.x.ToString(Culture)},{v.y.ToString(Culture)},{v.z.ToString(Culture)},{v.w.ToString(Culture)}";
            }

            if (fieldType == typeof(float2))
            {
                var v = (float2)value;
                return $"{v.x.ToString(Culture)},{v.y.ToString(Culture)}";
            }

            if (fieldType == typeof(float3))
            {
                var v = (float3)value;
                return $"{v.x.ToString(Culture)},{v.y.ToString(Culture)},{v.z.ToString(Culture)}";
            }

            if (fieldType == typeof(float4))
            {
                var v = (float4)value;
                return $"{v.x.ToString(Culture)},{v.y.ToString(Culture)},{v.z.ToString(Culture)},{v.w.ToString(Culture)}";
            }

            if (fieldType == typeof(int2))
            {
                var v = (int2)value;
                return $"{v.x},{v.y}";
            }

            if (fieldType == typeof(int3))
            {
                var v = (int3)value;
                return $"{v.x},{v.y},{v.z}";
            }

            if (fieldType == typeof(int4))
            {
                var v = (int4)value;
                return $"{v.x},{v.y},{v.z},{v.w}";
            }

            if (value == null)
                return string.Empty;

            if (value is IFormattable formattable)
                return formattable.ToString(null, Culture);

            return value.ToString() ?? string.Empty;
        }

        public static bool TryDeserializeValue(Type fieldType, string text, out object value)
        {
            try
            {
                if (fieldType == typeof(int)) { value = int.Parse(text, Culture); return true; }
                if (fieldType == typeof(uint)) { value = uint.Parse(text, Culture); return true; }
                if (fieldType == typeof(short)) { value = short.Parse(text, Culture); return true; }
                if (fieldType == typeof(ushort)) { value = ushort.Parse(text, Culture); return true; }
                if (fieldType == typeof(long)) { value = long.Parse(text, Culture); return true; }
                if (fieldType == typeof(ulong)) { value = ulong.Parse(text, Culture); return true; }
                if (fieldType == typeof(float)) { value = float.Parse(text, Culture); return true; }
                if (fieldType == typeof(double)) { value = double.Parse(text, Culture); return true; }
                if (fieldType == typeof(bool)) { value = bool.Parse(text); return true; }
                if (fieldType == typeof(byte)) { value = byte.Parse(text, Culture); return true; }
                if (fieldType == typeof(sbyte)) { value = sbyte.Parse(text, Culture); return true; }
                if (fieldType == typeof(string)) { value = text ?? string.Empty; return true; }

                if (fieldType == typeof(FixedString64Bytes))
                {
                    value = new FixedString64Bytes(text ?? string.Empty);
                    return true;
                }

                if (fieldType == typeof(Entity))
                {
                    var parts = (text ?? string.Empty).Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int index) && int.TryParse(parts[1], out int version))
                    {
                        value = new Entity { Index = index, Version = version };
                        return true;
                    }

                    value = Entity.Null;
                    return true;
                }

                if (fieldType == typeof(Vector2)) { value = ParseVector2(text); return true; }
                if (fieldType == typeof(Vector3)) { value = ParseVector3(text); return true; }
                if (fieldType == typeof(Vector4)) { value = ParseVector4(text); return true; }
                if (fieldType == typeof(float2)) { value = ParseFloat2(text); return true; }
                if (fieldType == typeof(float3)) { value = ParseFloat3(text); return true; }
                if (fieldType == typeof(float4)) { value = ParseFloat4(text); return true; }
                if (fieldType == typeof(int2)) { value = ParseInt2(text); return true; }
                if (fieldType == typeof(int3)) { value = ParseInt3(text); return true; }
                if (fieldType == typeof(int4)) { value = ParseInt4(text); return true; }
            }
            catch
            {
                // ignored
            }

            value = null;
            return false;
        }

        private static string[] SplitCsv(string text)
        {
            return (text ?? string.Empty).Split(',');
        }

        private static Vector2 ParseVector2(string text)
        {
            var p = SplitCsv(text);
            return new Vector2(
                p.Length > 0 ? float.Parse(p[0], Culture) : 0f,
                p.Length > 1 ? float.Parse(p[1], Culture) : 0f);
        }

        private static Vector3 ParseVector3(string text)
        {
            var p = SplitCsv(text);
            return new Vector3(
                p.Length > 0 ? float.Parse(p[0], Culture) : 0f,
                p.Length > 1 ? float.Parse(p[1], Culture) : 0f,
                p.Length > 2 ? float.Parse(p[2], Culture) : 0f);
        }

        private static Vector4 ParseVector4(string text)
        {
            var p = SplitCsv(text);
            return new Vector4(
                p.Length > 0 ? float.Parse(p[0], Culture) : 0f,
                p.Length > 1 ? float.Parse(p[1], Culture) : 0f,
                p.Length > 2 ? float.Parse(p[2], Culture) : 0f,
                p.Length > 3 ? float.Parse(p[3], Culture) : 0f);
        }

        private static float2 ParseFloat2(string text)
        {
            var p = SplitCsv(text);
            return new float2(
                p.Length > 0 ? float.Parse(p[0], Culture) : 0f,
                p.Length > 1 ? float.Parse(p[1], Culture) : 0f);
        }

        private static float3 ParseFloat3(string text)
        {
            var p = SplitCsv(text);
            return new float3(
                p.Length > 0 ? float.Parse(p[0], Culture) : 0f,
                p.Length > 1 ? float.Parse(p[1], Culture) : 0f,
                p.Length > 2 ? float.Parse(p[2], Culture) : 0f);
        }

        private static float4 ParseFloat4(string text)
        {
            var p = SplitCsv(text);
            return new float4(
                p.Length > 0 ? float.Parse(p[0], Culture) : 0f,
                p.Length > 1 ? float.Parse(p[1], Culture) : 0f,
                p.Length > 2 ? float.Parse(p[2], Culture) : 0f,
                p.Length > 3 ? float.Parse(p[3], Culture) : 0f);
        }

        private static int2 ParseInt2(string text)
        {
            var p = SplitCsv(text);
            return new int2(
                p.Length > 0 ? int.Parse(p[0], Culture) : 0,
                p.Length > 1 ? int.Parse(p[1], Culture) : 0);
        }

        private static int3 ParseInt3(string text)
        {
            var p = SplitCsv(text);
            return new int3(
                p.Length > 0 ? int.Parse(p[0], Culture) : 0,
                p.Length > 1 ? int.Parse(p[1], Culture) : 0,
                p.Length > 2 ? int.Parse(p[2], Culture) : 0);
        }

        private static int4 ParseInt4(string text)
        {
            var p = SplitCsv(text);
            return new int4(
                p.Length > 0 ? int.Parse(p[0], Culture) : 0,
                p.Length > 1 ? int.Parse(p[1], Culture) : 0,
                p.Length > 2 ? int.Parse(p[2], Culture) : 0,
                p.Length > 3 ? int.Parse(p[3], Culture) : 0);
        }
    }
}

