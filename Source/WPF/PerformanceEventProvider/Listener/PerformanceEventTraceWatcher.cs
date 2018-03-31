//-----------------------------------------------------------------------
// <copyright file="PerformanceEventTraceWatcher.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    public sealed class PerformanceEventTraceWatcher : EventTraceWatcher
    {
        public PerformanceEventTraceWatcher(EventTraceSession session)
            : base(typeof(PerformanceBlock).GUID, session)
        {
            LoadManifest("Microsoft.VisualStudio.Diagnostics.PerformanceProvider.PerformanceProviderManifest.xml");
        }

        protected override EventArrivedEventArgs CreateEventArgs()
        {
            return new PerformanceEventArrivedEventArgs();
        }
    }
}