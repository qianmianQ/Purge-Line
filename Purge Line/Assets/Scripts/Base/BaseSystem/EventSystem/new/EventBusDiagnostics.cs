#nullable disable
using System;
using System.Collections.Generic;

namespace Base.BaseSystem.EventSystem.New
{
    public class EventBusDiagnosticsOptions
    {
        public bool EnableDiagnostics { get; set; } = false;
        public bool TrackStackTrace { get; set; } = false;
        public bool TrackMemoryLeaks { get; set; } = false;
        public int MaxEventHistory { get; set; } = 1024;
    }

    public class LeakReport
    {
        public int SubscriptionId;
        public EventKey EventKey;
        public DateTime CreatedAt;
        public DateTime LeakedAt;
    }

    public class PerformanceReport
    {
        public long TotalPublishCount;
        public long TotalDispatchCount;
        public long TotalTimeMicroseconds;
    }
}
