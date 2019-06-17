using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Office.Interop.OneNote;
using System.Xml.Linq;
using System.IO;
using mshtml;
using System.Xml;
using System.Net;

namespace PublishHelp
{
    class Program
    {
        UniqueFileNames uniqueNames = new UniqueFileNames();
        Dictionary<string, PageInfo> pageIdMap;
        static string PublishPath;
        static string GitHubRoot = "https://github.com/clovett/MyMoney.Net/wiki/";

        static void PrintUsage()
        {
            Console.WriteLine("Usage: PublishHelp <targetdir>");
            Console.WriteLine("Publishes content of 'Money Specs' as Markdown to the target directory");
            Console.WriteLine("OneNote must be running and the Money Specs one note must be open.");
        }
        static void Main(string[] args)
        {
            Program p = new Program();

            if (args.Length > 0)
            {
                PublishPath = args[0];
            }
            else
            {
                PrintUsage();
                return;
            }


            bool export = true;
            bool markdown = true;
            if (export)
            { 
                string tempPath = Path.GetTempPath() + "OneNoteDocs\\";
                Directory.CreateDirectory(tempPath);

                p.ClearTargetDirectory(PublishPath);
                p.ClearTargetDirectory(System.IO.Path.Combine(PublishPath, "Images"));
                p.ClearTargetDirectory(tempPath);
                p.PublishPages(tempPath);
                p.FixupLinksAndSaveParts(PublishPath);
            }
            if (markdown)
            {
                p.GenerateMarkdown(PublishPath, GitHubRoot);
            }
            return;
        }

        private void GenerateMarkdown(string publishPath, string baseUri)
        {
            Console.WriteLine("Generating markdown...");
            foreach (string file in Directory.GetFiles(publishPath, "*.htm"))
            {
                Console.Write(Path.GetFileName(file) + "...");
                MarkdownConverter converter = new PublishHelp.MarkdownConverter();
                try
                {
                    converter.Convert(file, baseUri);
                    Console.WriteLine("ok");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private void ClearTargetDirectory(string path)
        {
            Directory.CreateDirectory(path);
            foreach (string file in Directory.GetFiles(path))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }

        private void SaveFileMap(string publishPath)
        {
            XDocument map = new XDocument(new XElement("FileMap", new XAttribute("BaseUri", publishPath)));

            foreach (PageInfo p in pageIdMap.Values)
            {
                map.Root.Add(new XElement("File", new XAttribute("Name", p.Name)));
            }

            map.Save(PublishPath + "filemap.xml");
        }

        

        private void FixupLinksAndSaveParts(string path)
        {
            foreach (PageInfo info in pageIdMap.Values)
            {
                string mhtFile = info.FileName;

                Console.Write("Fixing links " + Path.GetFileName(mhtFile) + "...");
                MimePackage pkg = MimePackage.Load(mhtFile);
                bool save = false;

                if (pkg.RootPart.IsHtml)
                {
                    save = FixHtmlLinks(pkg.RootPart);
                }

                if (pkg.Parts != null)
                {
                    foreach (MimePart part in pkg.Parts)
                    {
                        if (part.IsHtml)
                        {
                            save |= FixHtmlLinks(part);
                        }
                    }
                }

                string htm = Path.Combine(path, Path.GetFileNameWithoutExtension(mhtFile) + ".htm");
                pkg.SaveParts(htm, uniqueNames);
                
                Console.WriteLine("saved");                
            }
        }

        private bool FixHtmlLinks(MimePart part)
        {
            bool save = false;
            bool changed = false;
            var doc = part.HtmlDocument;
            // find any "a" tags with "href" attributes and fix them.
            foreach (XElement anchor in doc.Descendants(doc.Root.Name.Namespace + "a"))
            {
                string href = (string)anchor.Attribute("href");
                string newhref = MapUrl(href);
                if (href != newhref)
                {
                    anchor.SetAttributeValue("href", System.Uri.EscapeUriString(newhref));
                    changed = true;
                }
            }
            if (changed)
            {
                part.Body = doc.ToString();
                save = true;
            }
            return save;
        }


        private void PublishPages(string directory)
        {
            Microsoft.Office.Interop.OneNote.Application app = new Microsoft.Office.Interop.OneNote.Application();

            string xml;
            app.GetHierarchy("", HierarchyScope.hsNotebooks, out xml);

            XDocument doc = XDocument.Parse(xml);

            foreach (XElement e in doc.Root.Elements())
            {
                string name = (string)e.Attribute("name");
                if (name == "Money Specs")
                {
                    // this is the one!
                    string id = (string)e.Attribute("ID");

                    app.GetHierarchy(id, HierarchyScope.hsPages, out xml);

                    doc = XDocument.Parse(xml);

                    foreach (XElement section in doc.Root.Elements())
                    {
                        string sectionName = (string)section.Attribute("name");
                        string sectionId = (string)section.Attribute("ID");

                        if (section.Name.LocalName == "Section" && sectionName == "Documentation")
                        {
                            pageIdMap = new Dictionary<string, PageInfo>();

                            foreach (XElement page in section.Elements())
                            {
                                string pageName = (string)page.Attribute("name");
                                Console.Write("Publishing " + pageName + "...");
                                string pageId = (string)page.Attribute("ID");
                                string fileName = directory + "\\" + pageName + ".mht";

                                PageInfo info = new PageInfo()
                                {
                                    Name = pageName,
                                    Id = pageId,
                                    FileName = fileName
                                };

                                pageIdMap[pageName] = info;

                                if (File.Exists(fileName))
                                {
                                    File.Delete(fileName);
                                }
                                app.Publish(pageId, fileName, PublishFormat.pfMHTML, "");
                                Console.WriteLine("done");
                            }                            
                        }
                    }
                }
            }
        }

        const string OneNoteScheme = "onenote:";

        private string MapUrl(string href)
        {
            if (href.IndexOf("SkyDrive", 0, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("### Error: Reference to skydrive probably not intended: " + href);
            }

            if (!string.IsNullOrEmpty(href) && href.StartsWith(OneNoteScheme, StringComparison.InvariantCultureIgnoreCase))
            {
                int i = href.IndexOf('#');
                if (i > 0)
                {
                    href = href.Substring(i + 1);
                }
                else
                {
                    href = href.Substring(OneNoteScheme.Length + 1);
                }

                string[] parts = href.Split('&');

                string name = null;
                string sectionId = null;
                string pageId = null;
                string basePath = null;

                foreach (string s in parts)
                {
                    int equals = s.IndexOf('=');
                    if (equals >= 0)
                    {
                        string varname = s.Substring(0, equals);
                        string value = s.Substring(equals + 1);
                        switch (varname)
                        {
                            case "section-id":
                                sectionId = value;
                                break;
                            case "page-id":
                                pageId = value;
                                break;
                            case "base-path":
                                basePath = value;
                                break;
                        }
                    }
                    else if (name == null)
                    {
                        name = s;
                    }
                }

                name = name.Replace("%20", " ");

                PageInfo pi = null;
                if (pageIdMap.TryGetValue(name, out pi))
                {
                    return System.IO.Path.GetFileNameWithoutExtension(pi.FileName) + ".htm";
                }
            }

            return href;
        }
    }
}
