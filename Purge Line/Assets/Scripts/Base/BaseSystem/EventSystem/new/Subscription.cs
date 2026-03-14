#nullable disable
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using R3;
using UnityEngine;

namespace Base.BaseSystem.EventSystem.New
{
    public static class DisposableExtensions
    {
        public static T AddTo<T>(this T disposable, GameObject gameObject) where T : IDisposable
        {
            if (gameObject == null)
            {
                disposable.Dispose();
                return disposable;
            }

            var cts = GetOrCreateCancellationTokenSource(gameObject);
            cts.Token.Register(disposable.Dispose);
            return disposable;
        }

        public static T AddTo<T>(this T disposable, Component component) where T : IDisposable
        {
            if (component == null)
            {
                disposable.Dispose();
                return disposable;
            }
            return disposable.AddTo(component.gameObject);
        }

        public static T AddTo<T>(this T disposable, CompositeDisposable container) where T : IDisposable
        {
            container.Add(disposable);
            return disposable;
        }

        private static readonly ConditionalWeakTable<GameObject, CancellationTokenSource> _ctsTable =
            new ConditionalWeakTable<GameObject, CancellationTokenSource>();

        private static CancellationTokenSource GetOrCreateCancellationTokenSource(GameObject gameObject)
        {
            if (!_ctsTable.TryGetValue(gameObject, out var cts))
            {
                cts = new CancellationTokenSource();

                var hook = gameObject.AddComponent<DestroyHook>();
                hook.Initialize(cts);
                _ctsTable.Add(gameObject, cts);
            }
            return cts;
        }

        public static CancellationToken GetCancellationTokenOnDestroy(this GameObject gameObject)
        {
            return GetOrCreateCancellationTokenSource(gameObject).Token;
        }

        private sealed class DestroyHook : MonoBehaviour
        {
            private CancellationTokenSource? _cts;

            public void Initialize(CancellationTokenSource cts)
            {
                _cts = cts;
            }

            private void OnDestroy()
            {
                try
                {
                    _cts?.Cancel();
                    _cts?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Destroy hook error: {ex}");
                }
            }
        }
    }
}
