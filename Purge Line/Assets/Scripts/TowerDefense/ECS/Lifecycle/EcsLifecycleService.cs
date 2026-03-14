using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Unity.Core;
using Unity.Entities;
using VContainer.Unity;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TowerDefense.ECS.Lifecycle
{
    /// <summary>
    /// 统一接管 ECS World 与系统组生命周期，提供手动启停与观测能力。
    /// </summary>
    public sealed class EcsLifecycleService : IEcsLifecycleService, IInitializable, ITickable, IDisposable
    {
        private const int SnapshotCapacity = 64;

        private readonly EcsRuntimeStatistics _runtimeStatistics = new EcsRuntimeStatistics();
        private readonly List<EcsLifecycleSnapshot> _snapshots = new List<EcsLifecycleSnapshot>(SnapshotCapacity);
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private ILogger _logger;
        private World _world;

        private InitializationSystemGroup _initializationGroup;
        private SimulationSystemGroup _simulationGroup;
        private PresentationSystemGroup _presentationGroup;

        private EcsLifecycleState _state = EcsLifecycleState.Uninitialized;
        private int _snapshotSequence;
        private double _elapsedTime;

        public World World => _world;

        public bool IsWorldReady => _world != null && _world.IsCreated;

        public EcsLifecycleState State => _state;

        public EcsRuntimeStatistics RuntimeStatistics => _runtimeStatistics;

        public void Initialize()
        {
            _logger = GameLogger.Create("EcsLifecycleService");
            TransitionTo(EcsLifecycleState.Ready, "service initialized");
            _logger.LogInformation("[EcsLifecycle] Ready for manual world start");
        }

        public bool StartWorld()
        {
            EnsureLogger();

            if (_state == EcsLifecycleState.Running)
            {
                _logger.LogWarning("[EcsLifecycle] StartWorld ignored: already running");
                return true;
            }

            if (_state == EcsLifecycleState.Starting || _state == EcsLifecycleState.Stopping || _state == EcsLifecycleState.Disposed)
            {
                _logger.LogWarning("[EcsLifecycle] StartWorld ignored: invalid state {State}", _state);
                return false;
            }

            TransitionTo(EcsLifecycleState.Starting, "manual start requested");

            try
            {
                DisposeCurrentWorldIfExists();

                _world = new World("PurgeLine.ManualGameplayWorld", WorldFlags.Game);
                var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(_world, systems);

                _runtimeStatistics.RegisteredSystemCount = systems.Count;
                _runtimeStatistics.LastError = null;

                _initializationGroup = _world.GetExistingSystemManaged<InitializationSystemGroup>();
                _simulationGroup = _world.GetExistingSystemManaged<SimulationSystemGroup>();
                _presentationGroup = _world.GetExistingSystemManaged<PresentationSystemGroup>();

                // 兼容旧代码：当前桥接层仍可能读取 DefaultGameObjectInjectionWorld。
                World.DefaultGameObjectInjectionWorld = _world;

                _elapsedTime = 0d;
                _runtimeStatistics.Frames = 0;
                _runtimeStatistics.AvgFrameMs = 0f;
                _runtimeStatistics.LastDeltaTime = 0f;
                _runtimeStatistics.LastFrameTiming = default;

                TransitionTo(EcsLifecycleState.Running, "world created and systems registered");
                _logger.LogInformation("[EcsLifecycle] World started with {Count} systems", systems.Count);
                return true;
            }
            catch (Exception ex)
            {
                _runtimeStatistics.LastError = ex.ToString();
                TransitionTo(EcsLifecycleState.Failed, "world start failed");
                _logger.LogError(ex, "[EcsLifecycle] Failed to start world");
                DisposeCurrentWorldIfExists();
                return false;
            }
        }

        public bool StopWorld()
        {
            EnsureLogger();

            if (_state == EcsLifecycleState.Stopped || _state == EcsLifecycleState.Ready)
                return true;

            if (_state == EcsLifecycleState.Stopping || _state == EcsLifecycleState.Disposed)
                return false;

            TransitionTo(EcsLifecycleState.Stopping, "manual stop requested");
            DisposeCurrentWorldIfExists();
            TransitionTo(EcsLifecycleState.Stopped, "world disposed");
            _logger.LogInformation("[EcsLifecycle] World stopped");
            return true;
        }

        public bool PauseWorld()
        {
            if (_state != EcsLifecycleState.Running)
                return false;

            TransitionTo(EcsLifecycleState.Paused, "manual pause requested");
            return true;
        }

        public bool ResumeWorld()
        {
            if (_state != EcsLifecycleState.Paused)
                return false;

            TransitionTo(EcsLifecycleState.Running, "manual resume requested");
            return true;
        }

        public void Tick()
        {
            if (_state != EcsLifecycleState.Running || !IsWorldReady)
                return;

            float deltaTime = UnityEngine.Time.deltaTime;
            _elapsedTime += deltaTime;
            _world.SetTime(new TimeData(_elapsedTime, deltaTime));

            float initMs = UpdateGroup(_initializationGroup);
            float simMs = UpdateGroup(_simulationGroup);
            float presMs = UpdateGroup(_presentationGroup);

            var timing = new EcsFrameTiming(initMs, simMs, presMs);
            _runtimeStatistics.LastFrameTiming = timing;
            _runtimeStatistics.LastDeltaTime = deltaTime;
            _runtimeStatistics.Frames += 1;

            float totalMs = timing.TotalMs;
            _runtimeStatistics.AvgFrameMs += (totalMs - _runtimeStatistics.AvgFrameMs) / _runtimeStatistics.Frames;

            // 每 30 帧采样一次，兼顾观测与开销。
            if ((_runtimeStatistics.Frames % 30) == 0)
            {
                _runtimeStatistics.LastEntityCount = _world.EntityManager.UniversalQuery.CalculateEntityCount();
            }
        }

        public EcsLifecycleSnapshot CaptureSnapshot(string note = null)
        {
            var snapshot = CreateSnapshot(note ?? "manual snapshot");
            PushSnapshot(snapshot);
            _logger.LogInformation(
                "[EcsLifecycle] Snapshot#{Sequence} state={State} entities={Entities} systems={Systems} note={Note}",
                snapshot.Sequence,
                snapshot.State,
                snapshot.EntityCount,
                snapshot.RegisteredSystemCount,
                snapshot.Note);
            return snapshot;
        }

        public IReadOnlyList<EcsLifecycleSnapshot> GetSnapshots()
        {
            return _snapshots;
        }

        public void Dispose()
        {
            StopWorld();
            TransitionTo(EcsLifecycleState.Disposed, "service disposed");
        }

        private float UpdateGroup(ComponentSystemGroup group)
        {
            if (group == null)
                return 0f;

            _stopwatch.Restart();
            group.Update();
            _stopwatch.Stop();
            return (float)(_stopwatch.Elapsed.TotalMilliseconds);
        }

        private void TransitionTo(EcsLifecycleState newState, string note)
        {
            _state = newState;
            PushSnapshot(CreateSnapshot(note));
        }

        private EcsLifecycleSnapshot CreateSnapshot(string note)
        {
            int entityCount = 0;
            string worldName = "<none>";

            if (IsWorldReady)
            {
                worldName = _world.Name;
                entityCount = _world.EntityManager.UniversalQuery.CalculateEntityCount();
            }

            return new EcsLifecycleSnapshot(
                sequence: ++_snapshotSequence,
                timestampUtc: DateTime.UtcNow,
                state: _state,
                worldName: worldName,
                entityCount: entityCount,
                registeredSystemCount: _runtimeStatistics.RegisteredSystemCount,
                managedMemoryBytes: GC.GetTotalMemory(false),
                lastFrameTiming: _runtimeStatistics.LastFrameTiming,
                note: note ?? string.Empty);
        }

        private void PushSnapshot(EcsLifecycleSnapshot snapshot)
        {
            if (_snapshots.Count >= SnapshotCapacity)
            {
                _snapshots.RemoveAt(0);
            }

            _snapshots.Add(snapshot);
        }

        private void DisposeCurrentWorldIfExists()
        {
            if (_world == null)
                return;

            try
            {
                if (_world.IsCreated)
                {
                    _world.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[EcsLifecycle] Error disposing world");
            }
            finally
            {
                if (World.DefaultGameObjectInjectionWorld == _world)
                {
                    World.DefaultGameObjectInjectionWorld = null;
                }

                _world = null;
                _initializationGroup = null;
                _simulationGroup = null;
                _presentationGroup = null;
                _runtimeStatistics.RegisteredSystemCount = 0;
                _runtimeStatistics.LastEntityCount = 0;
                _runtimeStatistics.LastFrameTiming = default;
                _runtimeStatistics.LastDeltaTime = 0f;
            }
        }

        private void EnsureLogger()
        {
            _logger ??= GameLogger.Create("EcsLifecycleService");
        }
    }
}


