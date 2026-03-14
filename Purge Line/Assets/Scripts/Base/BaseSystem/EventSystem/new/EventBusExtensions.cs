#nullable disable
using System;
using System.Collections.Generic;
using System.Threading;
using R3;
using UnityEngine;

namespace Base.BaseSystem.EventSystem.New
{
    public static class EventBusExtensions
    {
        public static IDisposable Subscribe<T>(
            this EventBus bus,
            EventKey<T> key,
            Func<T, bool> predicate,
            Action<T> onNext)
        {
            return bus.AsObservable(key)
                .Where(predicate)
                .Subscribe(onNext);
        }

        public static IDisposable Subscribe<T, TResult>(
            this EventBus bus,
            EventKey<T> key,
            Func<T, TResult> selector,
            Action<TResult> onNext)
        {
            return bus.AsObservable(key)
                .Select(selector)
                .Subscribe(onNext);
        }

        public static IDisposable SubscribeOnMainThread<T>(
            this EventBus bus,
            EventKey<T> key,
            Action<T> onNext)
        {
            return bus.AsObservable(key)
                .Subscribe(onNext);
        }

        public static IDisposable Subscribe<T>(
            this EventBus bus,
            EventKey<T> key,
            Action<T> onNext,
            GameObject gameObject)
        {
            var disposable = bus.Subscribe(key, onNext);
            if (gameObject != null)
            {
                var cts = gameObject.GetCancellationTokenOnDestroy();
                var linkedCts = new CancellationTokenSource();
                linkedCts.Token.Register(disposable.Dispose);
                cts.Register(() => linkedCts.Cancel());
            }
            return disposable;
        }

        public static IDisposable Subscribe<T>(
            this EventBus bus,
            EventKey<T> key,
            Action<T> onNext,
            CompositeDisposable container)
        {
            var disposable = bus.Subscribe(key, onNext);
            container.Add(disposable);
            return disposable;
        }

        public static IDisposable SubscribeOnce<T>(
            this EventBus bus,
            EventKey<T> key,
            Action<T> onNext)
        {
            var cts = new CancellationTokenSource();
            var disposable = default(IDisposable);

            disposable = bus.Subscribe(key, e =>
            {
                onNext(e);
                cts.Cancel();
            }, cts.Token);

            return Disposable.Create(() =>
            {
                cts.Cancel();
                cts.Dispose();
                disposable.Dispose();
            });
        }
    }
}
