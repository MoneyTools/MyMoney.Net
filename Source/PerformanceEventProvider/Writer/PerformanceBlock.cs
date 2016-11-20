//-----------------------------------------------------------------------
// <copyright file="PerformanceBlock.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider
{

    /// <summary>
    /// Helper class to encapsulate data that is sent through ETW events by default
    /// </summary>
    public class EventInfo
    {
        public EventProvider EventProvider { get; private set; }
        public EventDescriptor EventDescriptor { get; private set; }
        public ComponentId ComponentId { get; private set; }
        public CategoryId CategoryId { get; private set; }
        public MeasurementId MeasurementId { get; private set; }
        public ulong Size { get; private set; }

        public EventInfo(EventProvider eventProvider,
            EventDescriptor eventDescriptor,
            ComponentId componentId,
            CategoryId categoryId,
            MeasurementId measurementId,
            ulong size)
        {
            EventProvider = eventProvider;
            EventDescriptor = eventDescriptor;
            ComponentId = componentId;
            CategoryId = categoryId;
            MeasurementId = measurementId;
            Size = size;
        }

        public static EventDescriptor BeginEvent 
        {
            get { return new EventDescriptor(1, 0, 0, 2, 0, 0, 0); }
        }

        public static EventDescriptor EndEvent
        {
            get { return new EventDescriptor(2, 0, 0, 2, 0, 0, 0); }
        }
    }

    [Guid("45a86a61-e4f2-4a65-b908-4408ead653fd")]
    public class PerformanceBlock : IDisposable
    {
        //int id, byte version, byte channel, byte level, byte opcode, int task, long keywords
        private static EventDescriptor beginEvent = new EventDescriptor(1, 0, 0, 2, 0, 0, 0);
        private static EventDescriptor  endEvent  = new EventDescriptor(2, 0, 0, 2, 0, 0, 0);
        private static EventDescriptor  stepEvent = new EventDescriptor(3, 0, 0, 2, 0, 0, 0);
        private static EventDescriptor  markEvent = new EventDescriptor(4, 0, 0, 2, 0, 0, 0);

        //private static string performanceBlockProviderName = "Microsoft.VisualStudio.PerformanceBlock.4.0.0.0";

        // Instantiate event provider.
        private static EventProvider etwProvider = new EventProvider(typeof(PerformanceBlock).GUID);

        private static TraceSwitch memReportingSwitch = new TraceSwitch("MemoryReporting", "defined in config file");
        private ComponentId component;
        private CategoryId category;
        private MeasurementId measurementId;
        private Stopwatch timer = new Stopwatch();

        internal PerformanceBlock(ComponentId component, CategoryId category, MeasurementId measurementId, ulong size)
        {
            Start(component, category, measurementId, size);
        }

        private void Start(ComponentId component, CategoryId category, MeasurementId measurementId, ulong size)
        {
            this.component = component;
            this.category = category;
            this.measurementId = measurementId;
            if (!memReportingSwitch.TraceInfo)  // the default
            {
                this.timer.Start();
                etwProvider.WriteEvent(ref beginEvent, (uint)component, (uint)category, (uint)measurementId, (ulong)0, size, (double)0);
            }
            else
            {
                EventInfo eventInfo = new EventInfo(etwProvider, beginEvent, component, category, measurementId, size);
                Trace.WriteLine(eventInfo);
            }
        }

        public static void Mark(ComponentId component, CategoryId category, MeasurementId measurementId, ulong size = 0, double rate = 0)
        {
            etwProvider.WriteEvent(ref markEvent, (uint)component, (uint)category, (uint)measurementId, (ulong)0, size, rate);
        }

        private static PerformanceBlock cache;
        /// <summary>
        /// Create a new PerformanceBlock object
        /// </summary>
        /// <param name="component">The component containing the event</param>
        /// <param name="category">The category of what is being measured</param>
        /// <param name="measurementId">The specific measurement being taken</param>
        /// <param name="size">An optional size indicating the size of the work being performed</param>
        /// <returns></returns>
        public static PerformanceBlock Create(ComponentId component, CategoryId category, MeasurementId measurementId, ulong size = 0)
        {
            PerformanceBlock free = System.Threading.Interlocked.Exchange<PerformanceBlock>(ref cache, null);
            if (free != null)
            {
                free.Start(component, category, measurementId, size);
                return free;
            }
            return new PerformanceBlock(component, category, measurementId, size);
        }

        /// <summary>
        /// Call this if you want to indicate progress towards the "Size" given in the Create method.
        /// </summary>
        public void Step(int steps)
        {
            etwProvider.WriteEvent(ref stepEvent, (uint)component, (uint)category, (uint)measurementId, (ulong)0, (ulong)steps, (double)0);
        }

        public void Dispose()
        {
            if (!memReportingSwitch.TraceInfo)  // the default
            {
                this.timer.Stop();
                etwProvider.WriteEvent(ref endEvent, (uint)component, (uint)category, (uint)measurementId, (ulong)this.timer.ElapsedTicks, (ulong)0, (double)0);
            }
            else
            {
                EventInfo eventInfo = new EventInfo(etwProvider, endEvent, component, category, measurementId, 0);
                Trace.WriteLine(eventInfo);
            }
            System.Threading.Interlocked.CompareExchange<PerformanceBlock>(ref cache, null, this);
        }
    }
}
