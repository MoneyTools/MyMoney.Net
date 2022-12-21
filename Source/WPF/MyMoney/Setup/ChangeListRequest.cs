using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Walkabout.Configuration;
using Walkabout.Utilities;

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
        private readonly Settings settings;
        private XDocument changeList;
        private EventHandlerCollection<SetupRequestEventArgs> handlers;

        public ChangeListRequest(Settings settings)
        {
            this.settings = settings;
        }

        public XDocument Changes { get { return this.changeList; } }

        public void BeginGetChangeList(Uri setupHost)
        {
            Task.Run(() => this.GetChangeList(setupHost));
        }

        public event EventHandler<SetupRequestEventArgs> Completed
        {
            add
            {
                if (this.handlers == null)
                {
                    this.handlers = new EventHandlerCollection<SetupRequestEventArgs>();
                }
                this.handlers.AddHandler(value);
            }
            remove
            {
                if (this.handlers != null)
                {
                    this.handlers.RemoveHandler(value);
                }
            }
        }

        private void OnCompleted(XDocument doc, bool newVersion = false)
        {
            this.changeList = doc;

            if (this.handlers != null && this.handlers.HasListeners)
            {
                this.handlers.RaiseEvent(this, new SetupRequestEventArgs() { Changes = doc, NewVersionAvailable = newVersion });
            }
        }

        private static readonly XNamespace asmNamespace = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");

        private void GetChangeList(Uri host)
        {
            try
            {
                XDocument changelist = this.GetDocument(new Uri(host, "changes.xml"));
                if (changelist == null || changelist.Root == null)
                {
                    this.OnCompleted(null);
                    return;
                }

                string exe = ProcessHelper.MainExecutable;
                string currentVersion = NativeMethods.GetFileVersion(exe);

                XElement first = changelist.Root.Element("change");
                string version = (string)(first?.Attribute("version"));
                Version latest = Version.Parse(version);
                Version current = Version.Parse(currentVersion);

                this.OnCompleted(changelist, current < latest);
            }
            catch
            {
                this.OnCompleted(null);
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
