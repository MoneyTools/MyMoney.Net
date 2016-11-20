//-----------------------------------------------------------------------
// <copyright file="EventArrivedEventArgs.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
// Author: Daniel Vasquez Lopez
//------------------------------------------------------------------------------
using System;
using System.IO;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    public class EventArrivedEventArgs : EventArgs 
    {
        internal EventArrivedEventArgs()
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal EventArrivedEventArgs(Exception exception)
            : this(Guid.Empty, 0, string.Empty, new PropertyBag()) 
        {
            this.EventException = exception;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal EventArrivedEventArgs(Guid providerId, uint id, string eventName, PropertyBag properties) 
        {
            this.EventName = eventName;
            this.EventId = id;
            this.ProviderId = providerId;
            this.Properties = properties;
        }

        public long Timestamp { get; set; }
        public uint EventId { get; set; }
        public Exception EventException { get; set; }
        public string EventName { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public PropertyBag Properties { get; set; }
        public Guid ProviderId { get; set; }

        public virtual void ParseUserData(uint eventId, BinaryReader reader)
        {
        }
    }
}
