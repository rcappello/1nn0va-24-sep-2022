using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.Tracing;
using System.Linq;

namespace DevicesSimulatorClient
{
    public sealed class ConsoleEventListener : EventListener
    {
        private readonly string[] _eventFilters;
        private readonly ILogger _logger;
        private readonly object _lock = new object();

        public ConsoleEventListener(string filter, ILogger logger)
        {
            _eventFilters = new string[1];
            _eventFilters[0] = filter ?? throw new ArgumentNullException(nameof(filter));
            _logger = logger;

            InitializeEventSources();
        }

        public ConsoleEventListener(string[] filters, ILogger logger)
        {
            _eventFilters = filters ?? throw new ArgumentNullException(nameof(filters));
            if (_eventFilters.Length == 0) throw new ArgumentException("Filters cannot be empty", nameof(filters));

            foreach (string filter in _eventFilters)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    throw new ArgumentNullException(nameof(filters));
                }
            }
            _logger = logger;

            InitializeEventSources();
        }

        private void InitializeEventSources()
        {
            foreach (EventSource source in EventSource.GetSources())
            {
                EnableEvents(source, EventLevel.LogAlways);
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);
            EnableEvents(
                eventSource,
                EventLevel.LogAlways
#if !NET451
                , EventKeywords.All
#endif
                );
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (_eventFilters == null) return;

            lock (_lock)
            {
                if (_eventFilters.Any(ef => eventData.EventSource.Name.StartsWith(ef, StringComparison.Ordinal)))
                {
                    string eventIdent;
#if NET451
                    // net451 doesn't have EventName, so we'll settle for EventId
                    eventIdent = eventData.EventId.ToString(CultureInfo.InvariantCulture);
#else
                    eventIdent = eventData.EventName;
#endif
                    string text = $"[{eventData.EventSource.Name}-{eventIdent}]{(eventData.Payload != null ? $" ({string.Join(", ", eventData.Payload)})." : "")}";
                    _logger.LogTrace(text);
                }
            }
        }
    }
}