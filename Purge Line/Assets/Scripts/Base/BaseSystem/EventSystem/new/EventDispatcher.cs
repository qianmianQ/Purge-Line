#nullable disable
#nullable disable
using System;
using System.Collections.Generic;
using System.Threading;
using R3;
using UnityEngine;

namespace Base.BaseSystem.EventSystem.New
{
    internal sealed class EventDispatcher
    {
        [ThreadStatic]
        private static Queue<WorkItem> _localQueue;

        private readonly RingBuffer<WorkItem> _globalQueue;
        private readonly int _batchSize = 32;

        private struct WorkItem
        {
            public EventKey Key;
            public object EventArgs;
            public DispatchMode Mode;
        }

        public EventDispatcher()
        {
            _globalQueue = new RingBuffer<WorkItem>();
        }

        public void Dispatch<T>(EventKey key, T eventArgs, DispatchMode mode, HandlerArray<T> handlers)
        {
            switch (mode)
            {
                case DispatchMode.Immediate:
                    DispatchImmediate(key, eventArgs, handlers);
                    break;
                case DispatchMode.Queue:
                    EnqueueToLocal(key, eventArgs, handlers);
                    break;
                case DispatchMode.MainThread:
                    DispatchToMainThread(key, eventArgs, handlers);
                    break;
                case DispatchMode.ThreadPool:
                    DispatchToThreadPool(key, eventArgs, handlers);
                    break;
                case DispatchMode.CustomScheduler:
                default:
                    DispatchImmediate(key, eventArgs, handlers);
                    break;
            }
        }

        private void DispatchImmediate<T>(EventKey key, T eventArgs, HandlerArray<T> handlers)
        {
            var span = handlers.AsReadOnlySpan();
            var version = handlers.Version;

            int removedCount = 0;
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var node = ref span[i];

                if (node.Flags.HasFlag(HandlerFlags.Removed))
                {
                    removedCount++;
                    continue;
                }

                if (node.Token.IsCancellationRequested)
                {
                    handlers.MarkForRemoval(node.SubscriptionId);
                    removedCount++;
                    continue;
                }

                try
                {
                    node.Handler(eventArgs);

                    if (node.Flags.HasFlag(HandlerFlags.Once))
                    {
                        handlers.MarkForRemoval(node.SubscriptionId);
                        removedCount++;
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex, key);
                }

                if (handlers.Version != version)
                {
                    span = handlers.AsReadOnlySpan();
                    version = handlers.Version;
                }
            }

            if (removedCount >= span.Length / 4)
            {
                ScheduleCompaction(handlers);
            }
        }

        [ThreadStatic]
        private static bool _isDispatching;

        private void EnqueueToLocal<T>(EventKey key, T eventArgs, HandlerArray<T> handlers)
        {
            if (_localQueue == null)
                _localQueue = new Queue<WorkItem>();

            if (!_isDispatching)
            {
                _isDispatching = true;
                try
                {
                    DispatchImmediate(key, eventArgs, handlers);
                    while (_localQueue.Count > 0)
                    {
                        var workItem = _localQueue.Dequeue();
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }
            else
            {
                _localQueue.Enqueue(new WorkItem
                {
                    Key = key,
                    EventArgs = eventArgs,
                    Mode = DispatchMode.Immediate
                });
            }
        }

        private void DispatchToMainThread<T>(EventKey key, T eventArgs, HandlerArray<T> handlers)
        {
            var mainThreadDispatcher = new UnityMainThreadDispatcher();
            mainThreadDispatcher.Enqueue(() => DispatchImmediate(key, eventArgs, handlers));
        }

        private class UnityMainThreadDispatcher : MonoBehaviour
        {
            private static UnityMainThreadDispatcher _instance;
            private static readonly object _lock = new object();
            private static readonly Queue<Action> _actions = new Queue<Action>();

            public static UnityMainThreadDispatcher Instance
            {
                get
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            var go = new GameObject("UnityMainThreadDispatcher");
                            go.hideFlags = HideFlags.HideAndDontSave;
                            _instance = go.AddComponent<UnityMainThreadDispatcher>();
                            DontDestroyOnLoad(go);
                        }
                        return _instance;
                    }
                }
            }

            public void Enqueue(Action action)
            {
                lock (_lock)
                {
                    _actions.Enqueue(action);
                }
            }

            private void Update()
            {
                while (_actions.Count > 0)
                {
                    Action action;
                    lock (_lock)
                    {
                        action = _actions.Dequeue();
                    }
                    action();
                }
            }
        }

        private void DispatchToThreadPool<T>(EventKey key, T eventArgs, HandlerArray<T> handlers)
        {
            System.Threading.Tasks.Task.Run(() => DispatchImmediate(key, eventArgs, handlers));
        }

        private void ScheduleCompaction<T>(HandlerArray<T> handlers)
        {
            System.Threading.Tasks.Task.Run(() => handlers.Compact());
        }

        private void HandleException(Exception ex, EventKey key)
        {
            Debug.LogError("EventBus error: " + ex);
        }
    }
}
