using System;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;


namespace Walkabout.Utilities
{

    static internal class InternetExplorer
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "rc")]
        public static void OpenUrl(IntPtr owner, Uri url)
        {
            Uri baseUri = new Uri(ProcessHelper.StartupPath);
            Uri resolved = new Uri(baseUri, url);
            
            // todo: support showing embedded pack:// resources in a popup page (could be useful for help content).
            int rc = NativeMethods.ShellExecute(owner, "open", resolved.AbsoluteUri, null, ProcessHelper.StartupPath, NativeMethods.SW_SHOWNORMAL);
        }

        public static void OpenUrl(IntPtr owner, string url)
        {
            OpenUrl(owner, new Uri(url, UriKind.RelativeOrAbsolute));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "rc")]
        public static void EditUrl(IntPtr owner, string url)
        {
            int rc = NativeMethods.ShellExecute(owner, "edit", url, null, ProcessHelper.StartupPath, NativeMethods.SW_SHOWNORMAL);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "rc")]
        public static void EditTransform(IntPtr owner, string url)
        {
            string path = Transform(url);
            EditUrl(owner, path);
        }

        static string Transform(string path)
        {

            Uri uri = new Uri(path);
            XmlDocument doc = new XmlDocument();
            doc.Load(path);

            XmlProcessingInstruction pi = doc.SelectSingleNode("processing-instruction('xml-stylesheet')") as XmlProcessingInstruction;
            if (pi != null)
            {
                XmlElement e = doc.CreateElement("xml-stylesheet");
                e.InnerXml = "<pi " + pi.Data + "/>";
                e = (XmlElement)e.FirstChild;

                string href = e.GetAttribute("href");
                Uri xsl = new Uri(uri, href);

                if (!System.IO.File.Exists(xsl.LocalPath))
                {
                    xsl = new Uri(xsl, "Reports\\" + href);
                }

                System.Xml.Xsl.XslCompiledTransform xt = new System.Xml.Xsl.XslCompiledTransform();
                xt.Load(xsl.AbsoluteUri);

                string name = System.IO.Path.GetFileNameWithoutExtension(path) + ".htm";
                Uri output = new Uri(uri, name);

                path = output.LocalPath;
                using (XmlTextWriter writer = new XmlTextWriter(path, Encoding.UTF8))
                {
                    writer.Formatting = Formatting.Indented;
                    xt.Transform(doc, null, writer, new XmlUrlResolver());
                }
                return path;
            }
            else
            {
                return path;
            }
        }


        public static Hyperlink GetOpenFileHyperlink(string label, string path)
        {
            Uri uri = new Uri(path, UriKind.RelativeOrAbsolute);            

            Hyperlink link = new Hyperlink(new Run(label))
            {
                NavigateUri = uri,
            };

            link.PreviewMouseDown += new MouseButtonEventHandler((s, e) =>
            {
                InternetExplorer.OpenUrl(IntPtr.Zero, uri);
            });

            link.MouseEnter += new MouseEventHandler((s, e) =>
            {
                link.Foreground = Brushes.Blue;
            });

            link.MouseLeave += new MouseEventHandler((s, e) =>
            {
                link.SetValue(Hyperlink.ForegroundProperty, DependencyProperty.UnsetValue);
            });

            link.Cursor = Cursors.Arrow;

            return link;
        }


    }



  
}
