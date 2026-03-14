#nullable disable
using System;
using System.Threading;
using R3;
using UnityEngine;

namespace Base.BaseSystem.EventSystem.New
{
    public static class EventBusUsageExamples
    {
        public class PlayerEvent
        {
            public int PlayerId { get; set; }
            public string EventType { get; set; }
            public int Health { get; set; }
        }

        public enum GameEvent
        {
            WaveCompleted = 1,
            EnemySpawned = 2,
            TowerPlaced = 3
        }

        public static void BasicUsage()
        {
            var bus = EventBus.Default;
            var key = EventKeyFactory.Default<PlayerEvent>();

            bus.Subscribe(key, e => Debug.Log($"Received event: PlayerId={e.PlayerId}, Health={e.Health}"));
            bus.Publish(key, new PlayerEvent { PlayerId = 1, EventType = "Damage", Health = 85 });
        }

        public static void R3StyleUsage()
        {
            var bus = EventBus.Default;
            var key = EventKeyFactory.Default<PlayerEvent>();

            var disposable = bus.AsObservable(key)
                .Where(e => e.Health > 0)
                .Select(e => e.PlayerId)
                .Subscribe(id => UpdateUI(id));

            CompositeDisposable disposables = new CompositeDisposable();
            disposable.AddTo(disposables);
        }

        public static void AdvancedUsage()
        {
            var bus = new EventBusBuilder()
                .WithInitialCapacity(32)
                .WithHotCacheSize(32)
                .WithDefaultDispatchMode(DispatchMode.Immediate)
                .Build();

            var key = EventKeyFactory.Create<PlayerEvent>();

            bus.Publish(key, new PlayerEvent { PlayerId = 1, EventType = "Damage", Health = 85 });
            bus.Publish(key, new PlayerEvent { PlayerId = 1, EventType = "Heal", Health = 95 }, DispatchMode.MainThread);
            bus.Publish(key, new PlayerEvent { PlayerId = 1, EventType = "PowerUp", Health = 100 }, DispatchMode.ThreadPool);
        }

        public static void GameObjectLifecycle()
        {
            GameObject gameObject = new GameObject();

            var disposable = EventBus.Default.Subscribe<PlayerEvent>(e =>
            {
                Debug.Log($"GameObject received event: {e.EventType}");
            });

            if (disposable is IDisposable sub)
            {
                var cts = gameObject.GetCancellationTokenOnDestroy();
                var linkedCts = new CancellationTokenSource();
                linkedCts.Token.Register(sub.Dispose);
                cts.Register(() => linkedCts.Cancel());
            }
        }

        private static void UpdateUI(int playerId)
        {
            Debug.Log($"UI update for player {playerId}");
        }
    }
}
