#nullable disable
using System;
using System.Threading;
using R3;

namespace Base.BaseSystem.EventSystem.New
{
    public class EventBusOptions
    {
        public int InitialHandlerCapacity { get; set; } = 8;
        public int HotCacheSize { get; set; } = 16;
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(5);
        public int IdleCleanupThreshold { get; set; } = 3;
        public DispatchMode DefaultDispatchMode { get; set; } = DispatchMode.Immediate;
        public bool IsolateExceptions { get; set; } = true;
        public Action<Exception, EventKey> ExceptionHandler { get; set; }
        public EventBusDiagnosticsOptions Diagnostics { get; set; } = new();
    }

    public sealed class EventBus : IDisposable
    {
        public static EventBus Default { get; } = new EventBus();

        private readonly EventHandlerStorage _storage;
        private readonly EventDispatcher _dispatcher;
        private readonly EventBusOptions _options;
        private int _nextSubscriptionId;
        private bool _disposed;

        public EventBus() : this(new EventBusOptions()) { }

        public EventBus(EventBusOptions options)
        {
            _options = options ?? new EventBusOptions();
            _storage = new EventHandlerStorage();
            _dispatcher = new EventDispatcher();
        }

        public void Publish<T>(T message)
            => Publish(EventKeyFactory.Default<T>(), message);

        public void Publish<T>(EventKey<T> key, T message)
            => Publish(key, message, _options.DefaultDispatchMode);

        public void Publish<T>(EventKey<T> key, T message, DispatchMode mode)
        {
            if (_disposed) return;
            if (_storage.TryGetArray<T>(key, out var handlers))
            {
                _dispatcher.Dispatch(key, message, mode, handlers);
            }
        }

        public IDisposable Subscribe<T>(Action<T> onNext)
            => Subscribe(EventKeyFactory.Default<T>(), onNext);

        public IDisposable Subscribe<T>(EventKey<T> key, Action<T> onNext)
        {
            return Subscribe(key, onNext, CancellationToken.None);
        }

        public IDisposable Subscribe<T>(EventKey<T> key, Action<T> onNext, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventBus));

            var handlers = _storage.GetOrCreateArray<T>(key);

            var node = new HandlerNode<T>(
                handler: onNext,
                token: cancellationToken,
                subscriptionId: Atomic.Increment(ref _nextSubscriptionId),
                priority: 0,
                flags: HandlerFlags.None);

            var subscriptionId = handlers.Add(node);

            var unsubscriber = new Unsubscriber<T>(this, key, subscriptionId);
            return unsubscriber;
        }

        private sealed class Unsubscriber<T> : IDisposable
        {
            private readonly WeakReference<EventBus> _busRef;
            private readonly EventKey<T> _key;
            private readonly int _subscriptionId;
            private int _disposed;

            public Unsubscriber(EventBus bus, EventKey<T> key, int subscriptionId)
            {
                _busRef = new WeakReference<EventBus>(bus);
                _key = key;
                _subscriptionId = subscriptionId;
            }

            public void Dispose()
            {
                if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    if (_busRef.TryGetTarget(out var bus))
                    {
                        bus.Unsubscribe(_key, _subscriptionId);
                    }
                }
            }
        }

        public void Unsubscribe<T>(EventKey<T> key, int subscriptionId)
        {
            if (_storage.TryGetArray<T>(key, out var handlers))
            {
                handlers.MarkForRemoval(subscriptionId);
            }
        }

        public Observable<T> AsObservable<T>()
            => AsObservable(EventKeyFactory.Default<T>());

        public Observable<T> AsObservable<T>(EventKey<T> key)
        {
            return Observable.Create<T>(observer =>
            {
                var cts = new CancellationTokenSource();
                var disposable = Subscribe(key, observer.OnNext, cts.Token);

                return Disposable.Create(() =>
                {
                    cts.Cancel();
                    cts.Dispose();
                    disposable.Dispose();
                });
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
