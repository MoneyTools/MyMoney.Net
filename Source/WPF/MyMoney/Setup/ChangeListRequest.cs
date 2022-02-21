using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading;
using System.Net;
using System.IO;
using Walkabout.Utilities;
using Walkabout.Configuration;
using System.Threading.Tasks;

namespace Walkabout.Setup
{
    public class SetupRequestEventArgs : EventArgs
    {
        public XDocument Changes { get; set; }
        public bool NewVersionAvailable { get; set; }
    }

    /// <summary>
    /// This class downloads the latest changes.xml from a click once deployment location.
    /// </summary>
    public class ChangeListRequest
    {
        Settings settings;
        XDocument changeList;
        EventHandlerCollection<SetupRequestEventArgs> handlers;

        public ChangeListRequest(Settings settings)
        {
            this.settings = settings;
        }

        public XDocument Changes { get { return this.changeList; } }

        public void BeginGetChangeList(Uri setupHost)
        {
            Task.Run(() => GetChangeList(setupHost));
        }

        public event EventHandler<SetupRequestEventArgs> Completed
        {
            add
            {
                if (handlers == null)
                {
                    handlers = new EventHandlerCollection<SetupRequestEventArgs>();
                }
                handlers.AddHandler(value);
            }
            remove
            {
                if (handlers != null)
                {
                    handlers.RemoveHandler(value);
                }
            }
        }

        private void OnCompleted(XDocument doc, bool newVersion = false)
        {
            changeList = doc;

            if (handlers != null && handlers.HasListeners)
            {
                handlers.RaiseEvent(this, new SetupRequestEventArgs() { Changes = doc, NewVersionAvailable = newVersion });
            }
        }

        static XNamespace asmNamespace = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");

        private void GetChangeList(Uri host)
        {
            try
            {
                XDocument changelist = GetDocument(new Uri(host, "changes.xml"));
                if (changelist == null || changelist.Root == null)
                {
                    OnCompleted(null);
                    return;
                }

                string exe = ProcessHelper.MainExecutable;
                string currentVersion = NativeMethods.GetFileVersion(exe);

                XElement first = changelist.Root.Element("change");
                string version = (string)(first?.Attribute("version"));
                Version latest = Version.Parse(version);
                Version current = Version.Parse(currentVersion);
                
                OnCompleted(changelist, current < latest);
            }
            catch
            {
                OnCompleted(null);
                return;
            }
        }

        private XDocument GetDocument(Uri url)
        {
            try
            {
                WebRequest request = WebRequest.Create(url);
                WebResponse response = request.GetResponse();
                using (Stream s = response.GetResponseStream())
                {
                    XDocument doc = XDocument.Load(s);
                    return doc;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
