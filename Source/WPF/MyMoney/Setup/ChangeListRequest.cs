using System;
using System.Net.Http;
using System.Threading;
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
            CancellationTokenSource src = new CancellationTokenSource();
            Task.Run(() => this.GetChangeList(setupHost, src.Token));
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
                    if (!this.handlers.HasListeners)
                    {
                        this.handlers = null;
                    }
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

        private async Task GetChangeList(Uri host, CancellationToken token)
        {
            try
            {
                XDocument changelist = await this.GetDocument(new Uri(host, "changes.xml"), token);
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

        private async Task<XDocument> GetDocument(Uri url, CancellationToken token)
        {
            try
            {
                HttpClient client = new HttpClient();
                var msg = await client.GetAsync(url);
                using (var stm = await msg.Content.ReadAsStreamAsync())
                {
                    XDocument doc = await XDocument.LoadAsync(stm, LoadOptions.None, token);
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
