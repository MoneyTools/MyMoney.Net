using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            ExportResource("Microsoft.VisualStudio.Diagnostics.PerformanceProvider.MeasurementBlockManifest.xml", path);
            LoadManifest("Microsoft.VisualStudio.Diagnostics.PerformanceProvider.MeasurementBlockManifest.xml");
        }

        protected override EventArrivedEventArgs CreateEventArgs()
        {
            return new MeasurementBlockEventArgs();
        }
    }
}
