using System.Diagnostics.Tracing;
using Walkabout.PerformanceProvider.Grpc;

namespace Walkabout.PerformanceProvider
{
    [EventSource(Name = "Walkabout-PerformanceProvider")]
    public sealed class PerformanceEventSource : EventSource
    {
        public static readonly PerformanceEventSource Log = new PerformanceEventSource();

        private PerformanceEventSource() {            
        }

        [Event(1, Level = EventLevel.Informational, Keywords = (EventKeywords)0x1)]
        public void Begin(int component, int category, int measurement, long ticks, ulong size, double rate)
        {
            if (IsEnabled())
            {
                WriteEvent(1, component, category, measurement, ticks, size, rate);
            }
            if (PerformanceClient.Instance != null)
            {
                PerformanceClient.Instance.Send(new PerformanceMessage() {
                    EventId = 1,
                    Component = component,
                    Category = category,
                    Measurement = measurement,
                    Ticks = Environment.TickCount,
                    Size = size,
                    Rate = rate
                });
            }
        }

        [Event(2, Level = EventLevel.Informational, Keywords = (EventKeywords)0x1)]
        public void End(int component, int category, int measurement, long ticks, ulong size, double rate)
        {
            if (IsEnabled())
            {
                WriteEvent(2, component, category, measurement, ticks, size, rate);
            }
            if (PerformanceClient.Instance != null)
            {
                PerformanceClient.Instance.Send(new PerformanceMessage()
                {
                    EventId = 2,
                    Component = component,
                    Category = category,
                    Measurement = measurement,
                    Ticks = Environment.TickCount,
                    Size = size,
                    Rate = rate
                });
            }
        }

        [Event(3, Level = EventLevel.Informational, Keywords = (EventKeywords)0x1)]
        public void Step(int component, int category, int measurement, long ticks, ulong steps, double rate)
        {
            if (IsEnabled())
            {
                WriteEvent(3, component, category, measurement, ticks, steps, rate);
            }

            if (PerformanceClient.Instance != null)
            {
                PerformanceClient.Instance.Send(new PerformanceMessage()
                {
                    EventId = 3,
                    Component = component,
                    Category = category,
                    Measurement = measurement,
                    Ticks = Environment.TickCount,
                    Size = steps,
                    Rate = rate
                });
            }
        }

        [Event(4, Level = EventLevel.Informational, Keywords = (EventKeywords)0x1)]
        public void Mark(int component, int category, int measurement, long ticks, ulong size, double rate)
        {
            if (IsEnabled())
            {
                WriteEvent(4, component, category, measurement, ticks, size, rate);
            }

            if (PerformanceClient.Instance != null)
            {
                PerformanceClient.Instance.Send(new PerformanceMessage()
                {
                    EventId = 4,
                    Component = component,
                    Category = category,
                    Measurement = measurement,
                    Ticks = Environment.TickCount,
                    Size = size,
                    Rate = rate
                });
            }
        }
    }
}
