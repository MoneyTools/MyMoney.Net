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
            ThreadPool.QueueUserWorkItem(GetChangeList, setupHost);
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

        private void GetChangeList(object state)
        {
            try
            {
                Uri host = (Uri)state;
                XDocument manifest = GetDocument(new Uri(host, "MyMoney.application"));
                if (manifest == null || manifest.Root == null)
                {
                    OnCompleted(null);
                    return;
                }

                XElement root = manifest.Root;
                XElement assemblyIdentity = root.Element(asmNamespace + "assemblyIdentity");
                if (assemblyIdentity == null || assemblyIdentity.Attribute("version") == null)
                {
                    OnCompleted(null);
                    return;
                }

                string version = (string)assemblyIdentity.Attribute("version");

                string folder = "MyMoney_" + version.Replace(".", "_");

                XDocument changelist = GetDocument(new Uri(host, "Application Files/" + folder + "/Setup/changes.xml.deploy"));

                string exe = ProcessHelper.MainExecutable;
                string currentVersion = NativeMethods.GetFileVersion(exe);

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
