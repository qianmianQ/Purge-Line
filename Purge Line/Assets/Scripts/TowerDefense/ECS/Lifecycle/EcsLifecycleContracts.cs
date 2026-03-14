using System;
using System.Collections.Generic;
using Unity.Entities;

namespace TowerDefense.ECS.Lifecycle
{
    public enum EcsLifecycleState
    {
        Uninitialized = 0,
        Ready,
        Starting,
        Running,
        Paused,
        Stopping,
        Stopped,
        Failed,
        Disposed
    }

    public readonly struct EcsFrameTiming
    {
        public readonly float InitializationMs;
        public readonly float SimulationMs;
        public readonly float PresentationMs;

        public EcsFrameTiming(float initializationMs, float simulationMs, float presentationMs)
        {
            InitializationMs = initializationMs;
            SimulationMs = simulationMs;
            PresentationMs = presentationMs;
        }

        public float TotalMs => InitializationMs + SimulationMs + PresentationMs;
    }

    public readonly struct EcsLifecycleSnapshot
    {
        public readonly int Sequence;
        public readonly DateTime TimestampUtc;
        public readonly EcsLifecycleState State;
        public readonly string WorldName;
        public readonly int EntityCount;
        public readonly int RegisteredSystemCount;
        public readonly long ManagedMemoryBytes;
        public readonly EcsFrameTiming LastFrameTiming;
        public readonly string Note;

        public EcsLifecycleSnapshot(
            int sequence,
            DateTime timestampUtc,
            EcsLifecycleState state,
            string worldName,
            int entityCount,
            int registeredSystemCount,
            long managedMemoryBytes,
            EcsFrameTiming lastFrameTiming,
            string note)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            State = state;
            WorldName = worldName;
            EntityCount = entityCount;
            RegisteredSystemCount = registeredSystemCount;
            ManagedMemoryBytes = managedMemoryBytes;
            LastFrameTiming = lastFrameTiming;
            Note = note;
        }
    }

    public sealed class EcsRuntimeStatistics
    {
        public int RegisteredSystemCount { get; internal set; }
        public int LastEntityCount { get; internal set; }
        public float LastDeltaTime { get; internal set; }
        public EcsFrameTiming LastFrameTiming { get; internal set; }
        public float AvgFrameMs { get; internal set; }
        public int Frames { get; internal set; }
        public string LastError { get; internal set; }
    }

    public interface IEcsWorldAccessor
    {
        World World { get; }
        bool IsWorldReady { get; }
        EcsLifecycleState State { get; }
    }

    public interface IEcsLifecycleService : IEcsWorldAccessor
    {
        EcsRuntimeStatistics RuntimeStatistics { get; }
        bool StartWorld();
        bool StopWorld();
        bool PauseWorld();
        bool ResumeWorld();
        EcsLifecycleSnapshot CaptureSnapshot(string note = null);
        IReadOnlyList<EcsLifecycleSnapshot> GetSnapshots();
    }
}

