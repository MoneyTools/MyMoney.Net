//-----------------------------------------------------------------------
// <copyright file="PerformanceData.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Microsoft.VisualStudio.PerformanceGraph
{
    internal class PerformanceData
    {
        private List<PerformanceEventArrivedEventArgs> data = new List<PerformanceEventArrivedEventArgs>();
        private HashSet<string> componentNames = new HashSet<string>();
        private HashSet<string> categoryNames = new HashSet<string>();
        private HashSet<string> measurements = new HashSet<string>();

        public const int BeginEvent = 1;
        public const int EndEvent = 2;
        public const int Step = 3;
        public const int Mark = 4;

        public TimeSpan HistoryLength = TimeSpan.FromSeconds(60);
        public long PerformanceFrequency;
        public int PurgeThreshold = 10000; // start purging after we cross this threshold
        private uint lastCheck;

        public void Add(PerformanceEventArrivedEventArgs e)
        {
            lock (this.data)
            {
                this.data.Add(e);

                this.componentNames.Add(e.ComponentName);
                this.categoryNames.Add(e.CategoryName);
                this.measurements.Add(e.MeasurementName);
                this.LazyCleanup(e.Timestamp);
            }
        }

        private void LazyCleanup(long latest)
        {
            uint now = (uint)Environment.TickCount;
            if (now > this.lastCheck + 1000 && this.data.Count > this.PurgeThreshold)
            {
                double seconds = latest / (double)this.PerformanceFrequency;
                double deleteDate = seconds - this.HistoryLength.TotalSeconds;
                for (int i = 0; i < this.data.Count; i++)
                {
                    PerformanceEventArrivedEventArgs args = this.data[i];
                    seconds = args.Timestamp / (double)this.PerformanceFrequency;
                    if (seconds > deleteDate)
                    {
                        if (i > 0)
                        {
                            this.data.RemoveRange(0, i);
                        }
                        break;
                    }
                }

                this.lastCheck = now;
            }
        }

        public void Clear()
        {
            this.data = new List<PerformanceEventArrivedEventArgs>();
        }

        public IEnumerable<string> Components { get { return this.componentNames; } }
        public IEnumerable<string> Measurements { get { return this.measurements; } }
        public IEnumerable<string> Categoryies { get { return this.categoryNames; } }


        public List<PerformanceEventArrivedEventArgs> GetMatchingEvents(string component, string category, string measurement)
        {
            List<PerformanceEventArrivedEventArgs> result = new List<PerformanceEventArrivedEventArgs>();
            lock (this.data)
            {
                if (this.data.Count > 0)
                {
                    foreach (PerformanceEventArrivedEventArgs e in this.data)
                    {
                        if ((component == null || e.ComponentName == component) &&
                            (category == null || e.CategoryName == category) &&
                            (measurement == null || e.MeasurementName == measurement))
                        {
                            result.Add(e);
                        }
                    }
                }
            }
            return result;
        }

        public string Units
        {
            get;
            set;
        }

        internal void WriteXml(string filename, List<PerformanceEventArrivedEventArgs> events)
        {
            XmlWriterSettings s = new XmlWriterSettings();
            s.Indent = true;

            using (XmlWriter w = XmlWriter.Create(filename, s))
            {
                long start = 0;
                w.WriteStartElement("PerformanceData");
                if (events.Count > 0)
                {
                    start = events[0].Timestamp;
                    long end = events[events.Count - 1].Timestamp;
                    w.WriteAttributeString("Duration", (end - start).ToString());
                }

                Dictionary<PerformanceEventArrivedEventArgsKey, PerformanceEventArrivedEventArgs> open = new Dictionary<PerformanceEventArrivedEventArgsKey, PerformanceEventArrivedEventArgs>();

                foreach (var e in events)
                {
                    PerformanceEventArrivedEventArgsKey key = new PerformanceEventArrivedEventArgsKey(e);
                    if (e.EventId == BeginEvent)
                    {
                        open[key] = e;
                    }
                    else if (e.EventId == EndEvent)
                    {
                        w.WriteStartElement("Event");
                        this.WriteEventCategory(w, e);
                        w.WriteAttributeString("Timestamp", (e.Timestamp - start).ToString())
                            ;
                        PerformanceEventArrivedEventArgs begin = null;
                        if (open.TryGetValue(key, out begin))
                        {
                            long eventDuration = e.Timestamp - begin.Timestamp;
                            w.WriteAttributeString("Duration", eventDuration.ToString());
                            w.WriteEndElement();
                        }
                    }
                    else
                    {
                        w.WriteStartElement("Event");
                        this.WriteEventCategory(w, e);
                        w.WriteAttributeString("Timestamp", (e.Timestamp - start).ToString());
                        w.WriteEndElement();
                    }
                }

                w.WriteEndElement();
            }
        }

        private void WriteEventCategory(XmlWriter w, PerformanceEventArrivedEventArgs e)
        {
            w.WriteAttributeString("Component", e.ComponentName);
            w.WriteAttributeString("Category", e.CategoryName);
            w.WriteAttributeString("Measurement", e.MeasurementName);
        }
    }

    internal class PerformanceEventArrivedEventArgsKey : IEquatable<PerformanceEventArrivedEventArgsKey>
    {
        public string Component { get; set; }
        public string Category { get; set; }
        public string Measurement { get; set; }

        public PerformanceEventArrivedEventArgsKey(PerformanceEventArrivedEventArgs data)
        {
            this.Component = data.ComponentName;
            this.Category = data.CategoryName;
            this.Measurement = data.MeasurementName;

            if (this.Component == null && !string.IsNullOrEmpty(this.Category))
            {
                string[] parts = this.Category.Split('.');
                if (parts.Length == 2)
                {
                    this.Component = parts[0];
                    this.Category = parts[1];
                }
            }
        }

        public override int GetHashCode()
        {
            int result = 0;
            if (this.Component != null)
            {
                result += this.Component.GetHashCode();
            }
            if (this.Category != null)
            {
                result = (result * 100) + this.Category.GetHashCode();
            }
            if (this.Measurement != null)
            {
                result = (result * 10) + this.Measurement.GetHashCode();
            }
            return result;
        }

        public override bool Equals(object obj)
        {
            return this.Equals((PerformanceEventArrivedEventArgsKey)obj);
        }

        public bool Equals(PerformanceEventArrivedEventArgsKey other)
        {
            return this.Component == other.Component && this.Category == other.Category && this.Measurement == other.Measurement;
        }

        public string Label
        {
            get
            {
                if (string.IsNullOrEmpty(this.Measurement))
                {
                    if (string.IsNullOrEmpty(this.Category))
                    {
                        return this.Component;
                    }
                    return this.Category;
                }
                return this.Measurement;
            }
        }
    }
}
