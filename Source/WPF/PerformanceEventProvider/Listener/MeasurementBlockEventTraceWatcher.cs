using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    [Guid("143a31db-0372-40b6-b8f1-b4b16adb5f54")]
    public class MeasurementBlockEventTraceWatcher : EventTraceWatcher
    {
        public MeasurementBlockEventTraceWatcher(EventTraceSession session)
            : base(typeof(MeasurementBlockEventTraceWatcher).GUID, session)
        {
            string path = Path.Combine(Path.GetTempPath(), "Microsoft.VisualStudio.Diagnostics.Measurement.Native.dll");
            this.ExportResource("Microsoft.VisualStudio.Diagnostics.PerformanceProvider.MeasurementBlockManifest.xml", path);
            this.LoadManifest("Microsoft.VisualStudio.Diagnostics.PerformanceProvider.MeasurementBlockManifest.xml");
        }

        protected override EventArrivedEventArgs CreateEventArgs()
        {
            return new MeasurementBlockEventArgs();
        }
    }
}
