#nullable disable
using System;

namespace Base.BaseSystem.EventSystem.New
{
    public class EventBusBuilder
    {
        private readonly EventBusOptions _options = new();

        public EventBusBuilder WithInitialCapacity(int capacity)
        {
            _options.InitialHandlerCapacity = capacity;
            return this;
        }

        public EventBusBuilder WithHotCacheSize(int size)
        {
            _options.HotCacheSize = size;
            return this;
        }

        public EventBusBuilder WithCleanupInterval(TimeSpan interval)
        {
            _options.CleanupInterval = interval;
            return this;
        }

        public EventBusBuilder WithDefaultDispatchMode(DispatchMode mode)
        {
            _options.DefaultDispatchMode = mode;
            return this;
        }

        public EventBusBuilder WithExceptionHandler(Action<Exception, EventKey> handler)
        {
            _options.ExceptionHandler = handler;
            return this;
        }

        public EventBusBuilder EnableDiagnostics()
        {
            _options.Diagnostics.EnableDiagnostics = true;
            return this;
        }

        public EventBusBuilder EnableLeakDetection()
        {
            _options.Diagnostics.EnableDiagnostics = true;
            _options.Diagnostics.TrackMemoryLeaks = true;
            return this;
        }

        public EventBus Build()
        {
            return new EventBus(_options);
        }
    }
}
