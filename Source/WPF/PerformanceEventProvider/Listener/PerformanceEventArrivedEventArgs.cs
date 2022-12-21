//-----------------------------------------------------------------------
// <copyright file="PerformanceEventArrivedEventArgs.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.IO;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    public sealed class PerformanceEventArrivedEventArgs : EventArrivedEventArgs
    {
        ComponentId component;
        CategoryId category;
        MeasurementId measurement;
        const int BeginEvent = 1;

        public PerformanceEventArrivedEventArgs()
        {
        }

        public override void ParseUserData(uint eventId, BinaryReader reader)
        {
            this.Component = (ComponentId)reader.ReadUInt32();
            this.Category = (CategoryId)reader.ReadUInt32();
            this.Measurement = (MeasurementId)reader.ReadUInt32();
            this.Ticks = reader.ReadUInt64();
            this.Size = reader.ReadUInt64();
            this.Rate = reader.ReadDouble();
        }

        public ComponentId Component { get { return this.component; } set { this.component = value; this.ComponentName = value.ToString(); } }
        public CategoryId Category { get { return this.category; } set { this.category = value; this.CategoryName = value.ToString(); } }
        public MeasurementId Measurement { get { return this.measurement; } set { this.measurement = value; this.MeasurementName = value.ToString(); } }
        public ulong Size { get; set; }
        public ulong Ticks { get; set; }
        public double Rate { get; set; }
        public string ComponentName { get; set; }
        public string CategoryName { get; set; }
        public string MeasurementName { get; set; }

    }
}
