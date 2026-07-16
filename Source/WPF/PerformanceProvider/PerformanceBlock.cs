namespace Walkabout.PerformanceProvider
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.Runtime.InteropServices;
    using System.Threading;

    [Guid("45a86a61-e4f2-4a65-b908-4408ead653fd")]
    public class PerformanceBlock : IDisposable
    {
        private static PerformanceEventSource etwProvider = PerformanceEventSource.Log;

        private static TraceSwitch memReportingSwitch = new TraceSwitch("MemoryReporting", "defined in config file");

        private static bool IsEnabled = true; // switch override

        private ComponentId component;

        private CategoryId category;

        private MeasurementId measurementId;

        private Stopwatch timer = new Stopwatch();

        private static PerformanceBlock cache;

        internal PerformanceBlock(ComponentId component, CategoryId category, MeasurementId measurementId, ulong size)
        {
            Start(component, category, measurementId, size);
        }

        private void Start(ComponentId component, CategoryId category, MeasurementId measurementId, ulong size)
        {
            this.component = component;
            this.category = category;
            this.measurementId = measurementId;
            if (IsEnabled || !memReportingSwitch.TraceInfo)
            {
                timer.Start();
                etwProvider.Begin((int)component, (int)category, (int)measurementId, 0L, size, 0.0);
            }
            else
            {
                EventInfo value = new EventInfo(component, category, measurementId, size);
                Trace.WriteLine(value);
            }
        }

        public static void Mark(ComponentId component, CategoryId category, MeasurementId measurementId, ulong size = 0uL, double rate = 0.0)
        {
            etwProvider.Mark((int)component, (int)category, (int)measurementId, 0L, size, rate);
        }

        public static PerformanceBlock Create(ComponentId component, CategoryId category, MeasurementId measurementId, ulong size = 0uL)
        {
            PerformanceBlock performanceBlock = Interlocked.Exchange(ref cache, null);
            if (performanceBlock != null)
            {
                performanceBlock.Start(component, category, measurementId, size);
                return performanceBlock;
            }
            return new PerformanceBlock(component, category, measurementId, size);
        }

        public void Step(int steps)
        {
            etwProvider.Step((int)component, (int)category, (int)measurementId, 0L, (ulong)steps, 0.0);
        }

        public void Dispose()
        {
            if (IsEnabled || !memReportingSwitch.TraceInfo)
            {
                timer.Stop();
                etwProvider.End((int)component, (int)category, (int)measurementId, (long)timer.ElapsedTicks, 0uL, 0.0);
            }
            else
            {
                EventInfo value = new EventInfo(component, category, measurementId, 0uL);
                Trace.WriteLine(value);
            }
            Interlocked.CompareExchange(ref cache, null, this);
        }
    }

}
