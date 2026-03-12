// ============================================================================
// PurgeLine.Resource — ResourceHandle.cs
// 资源句柄：不可变值类型，用于追踪资源引用的生命周期
// ============================================================================

using System;
using System.Runtime.CompilerServices;

namespace PurgeLine.Resource
{
    /// <summary>
    /// 资源句柄（值类型）。外部通过它引用已加载资源，释放时回传给 ResourceManager。
    /// </summary>
    /// <typeparam name="T">资源类型（UnityEngine.Object 子类、TextAsset 等）</typeparam>
    public readonly struct ResourceHandle<T> : IEquatable<ResourceHandle<T>>
    {
        /// <summary>全局唯一句柄 ID（0 = 无效句柄）</summary>
        public readonly uint Id;

        /// <summary>资源 Addressable 地址</summary>
        public readonly string Address;

        /// <summary>已加载的资源实例</summary>
        public readonly T Asset;

        /// <summary>句柄是否有效</summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Id != 0 && Asset != null;
        }

        /// <summary>无效句柄常量</summary>
        public static readonly ResourceHandle<T> Invalid = default;

        internal ResourceHandle(uint id, string address, T asset)
        {
            Id = id;
            Address = address;
            Asset = asset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ResourceHandle<T> other) => Id == other.Id;

        public override bool Equals(object obj) => obj is ResourceHandle<T> other && Equals(other);

        public override int GetHashCode() => (int)Id;

        public static bool operator ==(ResourceHandle<T> left, ResourceHandle<T> right) => left.Id == right.Id;

        public static bool operator !=(ResourceHandle<T> left, ResourceHandle<T> right) => left.Id != right.Id;

        public override string ToString() => $"Handle({Id}, {Address ?? "null"})";
    }
}

