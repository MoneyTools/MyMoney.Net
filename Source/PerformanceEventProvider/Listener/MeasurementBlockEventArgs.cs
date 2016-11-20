using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    public class MeasurementBlockEventArgs : EventArrivedEventArgs
    {
        const int BeginEvent = 1;
        const int EndEvent = 2;
        const int StepEvent = 3;
        const int MarkEvent = 4;

        public override void ParseUserData(uint eventId, BinaryReader reader)
        {
            this.Component = reader.ReadUInt32();
            this.Ticks = reader.ReadUInt64();
            this.CpuTicks = reader.ReadUInt64();
            this.Size = reader.ReadUInt64();
            this.CorrelationId = new Guid(reader.ReadBytes(16));
            this.SequenceNumber = reader.ReadUInt32();
            this.NestingLevel = reader.ReadUInt32();
            this.ParentCorrelationId = new Guid(reader.ReadBytes(16));
            this.ParentSequenceNumber = reader.ReadUInt32();
            this.Category = ReadLittleEndianString(reader);
        }

        public uint Component { get; set; }
        public string Category { get; set; }
        public Guid CorrelationId { get; set; }
        public uint SequenceNumber{ get; set; }
        public Guid ParentCorrelationId { get; set; }
        public uint ParentSequenceNumber { get; set; }
        public uint NestingLevel{ get; set; }
        public ulong Size { get; set; }
        public ulong Ticks { get; set; }
        public ulong CpuTicks { get; set; }
        public double Rate { get; set; }

        /// <summary>
        /// BinaryReader.ReadString doesn't work because it was written by native provider as a binary blob dumped from a pinned
        /// string buffer.  So the bytes were not written in the usual order matching BinaryWriter.Write string.
        /// </summary>
        string ReadLittleEndianString(BinaryReader reader)
        {
            StringBuilder sb = new StringBuilder();
            int next;
            while ((next = reader.ReadUInt16()) != 0)
            {
                char ch = Convert.ToChar(next);
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
