using System;
using System;

namespace Walkabout.PerformanceProvider
{
    // Simplified EventInfo used for Trace.WriteLine when ETW is disabled.
    public class EventInfo
    {
        public ComponentId ComponentId { get; private set; }

        public CategoryId CategoryId { get; private set; }

        public MeasurementId MeasurementId { get; private set; }

        public ulong Size { get; private set; }

        public EventInfo(ComponentId componentId, CategoryId categoryId, MeasurementId measurementId, ulong size)
        {
            ComponentId = componentId;
            CategoryId = categoryId;
            MeasurementId = measurementId;
            Size = size;
        }

        public override string ToString()
        {
            return $"PerformanceEvent: Component={ComponentId}, Category={CategoryId}, Measurement={MeasurementId}, Size={Size}";
        }
    }
}
