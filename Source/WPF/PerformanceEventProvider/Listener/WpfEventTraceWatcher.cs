//-----------------------------------------------------------------------
// <copyright file="WpfEventTraceWatcher.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener
{
    [Guid("E13B77A8-14B6-11DE-8069-001B212B5009")]
    public class WpfEventTraceWatcher : EventTraceWatcher
    {
        public WpfEventTraceWatcher(EventTraceSession session)
            : base(typeof(WpfEventTraceWatcher).GUID, session)
        {
            //C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll
            string path = typeof(System.Environment).Assembly.Location;
            if (path.Contains("Framework64"))
            {
                path = path.Replace("Framework64", "Framework");
            }
            Uri uri = new Uri(path);
            Uri wpfman = new Uri(uri, @"wpf\wpf-etw.man");
            if (File.Exists(wpfman.LocalPath))
            {
                this.LoadManifest(wpfman);
                FindEvents(wpfman.LocalPath);
            }
        }

        static Dictionary<int, WpfEvent> events;
        static Dictionary<string, WpfEvent> tasks = new Dictionary<string, WpfEvent>();

        private static void FindEvents(string manifest)
        {
            // extract these start/stop pairs
            // <event value="3005"  level="win:Informational" task="DeleteInkNote"               opcode="win:Start"       template="Template_0"          symbol="DeleteInkNoteBegin"                    version="2" channel="DefaultChannel" keywords="KeywordAnnotation"  />
            // <event value="3006"  level="win:Informational" task="DeleteInkNote"               opcode="win:Stop"        template="Template_0"          symbol="DeleteInkNoteEnd"                      version="2" channel="DefaultChannel" keywords="KeywordAnnotation"  />

            if (events == null)
            {
                events = new Dictionary<int, WpfEvent>();
                XDocument doc = XDocument.Load(manifest);
                XNamespace ns = doc.Root.Name.Namespace;
                foreach (XElement e in doc.Root.Descendants(ns + "event"))
                {
                    ParseEvent(e);
                }
            }
        }

        private static void ParseEvent(XElement e)
        {
            int id = (int)e.Attribute("value");
            string task = (string)e.Attribute("task");
            string opcode = (string)e.Attribute("opcode");
            string symbol = (string)e.Attribute("symbol");

            if (id > 0 && !string.IsNullOrEmpty(task) && !string.IsNullOrEmpty(opcode) && !string.IsNullOrEmpty(symbol))
            {
                if (opcode.EndsWith("Start") || opcode.EndsWith("Stop"))
                {
                    Addevent(id, task, opcode, symbol);
                }
            }
        }

        private static WpfEvent Addevent(int id, string task, string opcode, string symbol)
        {
            WpfEvent evt = null;
            if (!tasks.TryGetValue(task, out evt))
            {
                evt = new WpfEvent() { Task = task, Symbol = symbol };
                tasks[task] = evt;
            }
            if (opcode.EndsWith("Start"))
            {
                evt.StartId = id;
                events[id] = evt;
            }
            else if (opcode.EndsWith("Stop"))
            {
                evt.StopId = id;
                events[id] = evt;
            }
            return evt;
        }

        internal static WpfEvent GetEvent(int id)
        {
            WpfEvent e = null;
            events.TryGetValue(id, out e);
            return e;
        }

        protected override EventArrivedEventArgs CreateEventArgs()
        {
            return new WpfEventArrivedEventArgs();
        }
    }

    public class WpfEvent
    {
        public int StartId;
        public int StopId;
        public string Task;
        public string Symbol;
    }

}
