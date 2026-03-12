using System;
using PurgeLine.Events;
using R3;

namespace Base.BaseSystem.EventSystem
{
    /// <summary>
    /// 事件域：每个域持有独立的 EventBus，实现真正的域隔离。
    /// - 无参事件通过 TEnum 枚举约束限制在本域
    /// - 有参事件通过独立 Bus 保证不跨域触发
    /// </summary>
    public sealed class EventDomain : IDisposable
    {
        private readonly EventBus _bus;
        private readonly Type _enumType;
        private bool _disposed;

        internal EventDomain(EventBus bus, Type enumType)
        {
            _bus = bus;
            _enumType = enumType;
            if (!enumType.IsEnum)
                throw new ArgumentException("Provided type is not an Enum.", nameof(enumType));
        }

        // ── 无参枚举事件 ─────────────────────────────────────────

        /// <summary>返回 Observable，支持 Rx 链式操作</summary>
        public Observable<Unit> OnEvent(Enum e)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventDomain));
            ValidateEvent(e);
            return _bus.GetOrCreateEnum(e);
        }

        /// <summary>命令式订阅，返回 IDisposable 用于取消</summary>
        public IDisposable AddListener(Enum e, Action callback)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventDomain));
            ValidateEvent(e);
            return _bus.GetOrCreateEnum(e).Subscribe(_ => callback());
        }

        /// <summary>派发无参事件</summary>
        public void Dispatch(Enum e)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventDomain));
            ValidateEvent(e);
            _bus.Emit(e);
        }

        // ── 有参事件 ─────────────────────────────────────────────

        /// <summary>返回 Observable&lt;T&gt;，支持 Rx 链式操作</summary>
        public Observable<T> OnEvent<T>()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventDomain));
            return _bus.GetOrCreate<T>();
        }

        /// <summary>命令式订阅，返回 IDisposable 用于取消</summary>
        public IDisposable AddListener<T>(Action<T> callback)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventDomain));
            return _bus.GetOrCreate<T>().Subscribe(callback);
        }

        /// <summary>派发有参事件</summary>
        public void Dispatch<T>(T eventData)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventDomain));
            _bus.Emit(eventData);
        }

        // ── 生命周期 ──────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Dispose();
        }

        // ── 私有校验 ──────────────────────────────────────────────

        /// <summary>
        /// 拒绝使用默认枚举值（None = 0）派发或订阅事件。
        /// </summary>
        private void ValidateEvent(Enum e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            if (e.GetType() != _enumType)
                throw new ArgumentException($"[EventSystem] 枚举类型不匹配，要求 {_enumType.Name}，实际 {e.GetType().Name}。", nameof(e));
            var intVal = Convert.ToInt32(e);
            if (intVal == 0)
                throw new ArgumentException(
                    $"[EventSystem] 不允许使用默认值 0（None）作为事件。请定义具体的事件枚举值。",
                    nameof(e));
        }
    }
}
