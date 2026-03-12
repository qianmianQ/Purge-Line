using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using R3;

namespace PurgeLine.Events
{
    /// <summary>
    /// 底层事件总线。
    /// - 无参枚举事件：Subject&lt;Unit&gt;，key 为 (Type, int)，int 值通过 Unsafe.As 零装箱读取
    /// - 有参结构体事件：Subject&lt;T&gt;，key 为 typeof(T)
    /// 注意：所有枚举类型必须为 int 底层类型（项目约定）。
    /// </summary>
    internal sealed class EventBus : IDisposable
    {
        // 有参事件池：typeof(T) → Subject<T>（装箱存 object）
        private readonly Dictionary<Type, object> _typedPool = new();

        // 无参枚举事件池：(enumType, intValue) → Subject<Unit>
        private readonly Dictionary<(Type, int), Subject<Unit>> _enumPool = new();

        private bool _disposed;

        // ── 无参枚举事件 ────────────────────────────────────────

        /// <summary>
        /// 获取或创建枚举事件的 Observable。
        /// 使用 Unsafe.As 零装箱读取枚举整数值（要求枚举为 int 底层类型）。
        /// </summary>
        public Observable<Unit> GetOrCreateEnum<TEnum>(TEnum enumValue)
            where TEnum : Enum
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventBus));

#if UNITY_EDITOR || DEBUG
            if (System.Enum.GetUnderlyingType(typeof(TEnum)) != typeof(int))
                throw new System.InvalidOperationException(
                    $"[EventBus] {typeof(TEnum).Name} 的底层类型必须为 int（当前为 {System.Enum.GetUnderlyingType(typeof(TEnum)).Name}）。");
#endif

            var intVal = Unsafe.As<TEnum, int>(ref enumValue);
            var key = (typeof(TEnum), intVal);
            if (!_enumPool.TryGetValue(key, out var subject))
            {
                subject = new Subject<Unit>();
                _enumPool[key] = subject;
            }
            return subject.AsObservable();
        }

        /// <summary>
        /// 获取或创建枚举事件的 Observable（非泛型，支持 Enum 参数）。
        /// </summary>
        public Observable<Unit> GetOrCreateEnum(Enum enumValue)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventBus));
            var enumType = enumValue.GetType();
            var intVal = Convert.ToInt32(enumValue);
            var key = (enumType, intVal);
            if (!_enumPool.TryGetValue(key, out var subject))
            {
                subject = new Subject<Unit>();
                _enumPool[key] = subject;
            }
            return subject.AsObservable();
        }

        /// <summary>
        /// 派发无参枚举事件。无订阅者时不创建 Subject。
        /// 使用 Unsafe.As 零装箱读取枚举整数值。
        /// </summary>
        public void EmitEnum<TEnum>(TEnum enumValue)
            where TEnum : Enum
        {
            if (_disposed) return;

#if UNITY_EDITOR || DEBUG
            if (System.Enum.GetUnderlyingType(typeof(TEnum)) != typeof(int))
                throw new System.InvalidOperationException(
                    $"[EventBus] {typeof(TEnum).Name} 的底层类型必须为 int（当前为 {System.Enum.GetUnderlyingType(typeof(TEnum)).Name}）。");
#endif

            var intVal = Unsafe.As<TEnum, int>(ref enumValue);
            var key = (typeof(TEnum), intVal);
            if (_enumPool.TryGetValue(key, out var subject))
                subject.OnNext(Unit.Default);
        }

        /// <summary>
        /// 派发无参枚举事件（非泛型，支持 Enum 参数）。
        /// </summary>
        public void Emit(Enum enumValue)
        {
            if (_disposed) return;
            var enumType = enumValue.GetType();
            var intVal = Convert.ToInt32(enumValue);
            var key = (enumType, intVal);
            if (_enumPool.TryGetValue(key, out var subject))
                subject.OnNext(Unit.Default);
        }

        // ── 有参事件 ────────────────────────────────────────────

        /// <summary>
        /// 获取或创建有参事件的 Observable。
        /// 返回 AsObservable() 封装，防止调用方反向转型为 Subject&lt;T&gt;。
        /// </summary>
        public Observable<T> GetOrCreate<T>()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventBus));

            var type = typeof(T);
            if (!_typedPool.TryGetValue(type, out var boxed))
            {
                var s = new Subject<T>();
                _typedPool[type] = s;
                return s.AsObservable();
            }
            return ((Subject<T>)boxed).AsObservable();
        }

        /// <summary>
        /// 派发有参事件。无订阅者时不创建 Subject。
        /// 仅处理非枚举类型；枚举事件请使用 EmitEnum&lt;TEnum&gt;。
        /// </summary>
        public void Emit<T>(T eventData)
        {
            if (_disposed) return;

#if UNITY_EDITOR || DEBUG
            if (typeof(T).IsEnum)
                throw new System.ArgumentException(
                    $"[EventBus] 枚举类型 {typeof(T).Name} 请使用 EmitEnum<TEnum>() 派发，而非 Emit<T>()。");
#endif

            if (_typedPool.TryGetValue(typeof(T), out var boxed))
                ((Subject<T>)boxed).OnNext(eventData);
        }

        // ── 生命周期 ─────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 统一策略：先 OnCompleted 通知订阅者流结束，再 Dispose 释放内部资源
            foreach (var s in _enumPool.Values)
            {
                s.OnCompleted();
                ((IDisposable)s).Dispose();
            }
            // R3 的 Subject<T>.Dispose() 内部会自动发出 OnCompleted，
            // 因此此处无需显式调用 OnCompleted()，与 _enumPool 路径行为等价。
            foreach (var s in _typedPool.Values)
            {
                if (s is IDisposable d) d.Dispose();
            }

            _enumPool.Clear();
            _typedPool.Clear();
        }
    }
}
