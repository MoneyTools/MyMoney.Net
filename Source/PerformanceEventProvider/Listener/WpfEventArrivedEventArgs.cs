//-----------------------------------------------------------------------
// <copyright file="WpfEventArrivedEventArgs.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    public enum MIL_PRESENTATION_RESULTS
    {
        MIL_PRESENTATION_VSYNC,
        MIL_PRESENTATION_NOPRESENT,
        MIL_PRESENTATION_VSYNC_UNSUPPORTED,
        MIL_PRESENTATION_DWM,
        MIL_PRESENTATION_FORCE_DWORD = unchecked((int)0xffffffff)
    }; 

    public class WpfEventArrivedEventArgs : EventArrivedEventArgs
    {
        public WpfEvent Event { get; set; }

        public override void ParseUserData(uint eventId, BinaryReader reader)
        {
            int id = (int)eventId;
            Event = WpfEventTraceWatcher.GetEvent(id);
            if (Event != null)
            {
                if (Event.StartId == id)
                {
                    this.EventId = 1; // bugbug
                }
                else if (Event.StopId == id)
                {
                    this.EventId = 2; // bugbug
                }
            }
        }

        public static long QPCFrequency
        {
            get
            {
                if (frequency == 0)
                {
                    QueryPerformanceFrequency(out frequency);
                }
                return frequency;
            }
        }

        static long frequency = 0;

        [DllImport("Kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);
    }
}
