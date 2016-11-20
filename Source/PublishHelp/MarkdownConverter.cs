using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace PublishHelp
{
    /// <summary>
    /// Converts HTML to markdown.
    /// </summary>
    class MarkdownConverter
    {
        string baseUri;

        public void Convert(string path, string baseUri)
        {
            this.baseUri = baseUri;
            XDocument html = XDocument.Load(path);

            string mdFile = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".md");
            using (StreamWriter sw = new StreamWriter(mdFile))
            {

                XNamespace ns = html.Root.Name.Namespace;
                XElement body = html.Root.Element(ns + "body");
                if (body == null)
                {
                    throw new Exception("Missing html body");
                }

                WalkElements(body, null, sw);
            }

            File.Delete(path);
        }

        bool ignoreFirstParagraph = true;

        void WalkElements(XElement e, CssAttributes inherited, StreamWriter writer)
        {
            double beforeSize = (inherited != null) ? inherited.GetFontSize() : 0;
            CssAttributes attrs = CssAttributes.Parse((string)e.Attribute("style"), inherited);
            double size = attrs.GetFontSize();

            if (ignoreFirstParagraph && e.Name.LocalName == "p")
            {
                // ignore first paragraph.
                ignoreFirstParagraph = false;
                return;
            }

            if (beforeSize == 11 && size > 11 && !e.Name.LocalName.StartsWith("h"))
            {
                if (size == 12)
                {
                    writer.Write("### ");
                }
                else if (size >= 13 && size < 17)
                {
                    writer.Write("## ");
                }
                else if (size >= 17)
                {
                    writer.Write("# ");
                }
                else
                {
                    Console.WriteLine("Font size changed to {0} on element {1}", size, e.Name.LocalName);
                }
            }

            foreach (XNode child in e.Nodes())
            {
                if (child is XElement)
                {
                    XElement ce = (XElement)child;
                    switch (ce.Name.LocalName)
                    {
                        case "a":
                            writer.Write("[");
                            WalkElements((XElement)child, attrs, writer);
                            writer.Write("](");
                            WriteAnchor((string)ce.Attribute("href"), writer);
                            writer.Write(")");
                            break;
                        case "h3":
                            writer.Write("### ");
                            WalkElements((XElement)child, attrs, writer);
                            writer.WriteLine();
                            break;
                        case "h2":
                            writer.Write("## ");
                            WalkElements((XElement)child, attrs, writer);
                            writer.WriteLine();
                            break;
                        case "h1":
                            writer.Write("# ");
                            WalkElements((XElement)child, attrs, writer);
                            writer.WriteLine();
                            break;
                        case "div":
                        case "p":
                            WalkElements((XElement)child, attrs, writer);
                            writer.WriteLine();
                            break;
                        case "li":
                            string listType = attrs.Find("list");
                            if (listType == "ordered")
                            {
                                writer.Write("1. ");
                            }
                            else
                            {
                                writer.Write("* ");
                            }
                            WalkElements((XElement)child, attrs, writer);
                            writer.WriteLine();
                            break;
                        case "span":
                            bool bold = attrs.Find("fond-weight") == "bold";
                            if (bold) {
                                writer.Write("** ");
                            }
                            WalkElements((XElement)child, attrs, writer);
                            break;
                        case "img":
                            writer.Write("![");
                            writer.Write("](");
                            WriteAnchor((string)ce.Attribute("src"), writer);
                            writer.Write(")");
                            break;
                        case "ol":
                            attrs.Push("list", "ordered");
                            WalkElements((XElement)child, attrs, writer);
                            attrs.Pop("list");
                            break;
                        case "ul":
                            attrs.Push("list", "unordered");
                            WalkElements((XElement)child, attrs, writer);
                            attrs.Pop("list");
                            break;
                        case "br":
                            writer.WriteLine();
                            break;
                        case "table":
                            throw new Exception("HTML Table doesn't translate to markdown");

                        default:
                            Console.WriteLine("Ignoring element: " + ce.Name.LocalName);
                            break;
                    }
                }
                else if (child is XText)
                {
                    XText text = (XText)child;
                    WriteText(text.Value, writer);
                }
            }
        }

        private void WriteAnchor(string href, StreamWriter writer)
        {
            if (!href.Contains("://")) {
                // relative links are to markdown files, so no .htm suffix
                if (href.EndsWith(".htm"))
                {
                    href = href.Substring(0, href.Length - 4);
                }
                href = this.baseUri + href;
            }
            WriteText(href, writer);
        }

        static char[] whitespace = new char[] { ' ', '\t', '\r', '\n' };

        void WriteText(string value, StreamWriter writer)
        {
            if (value == "Created with Microsoft OneNote 2016.")
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            bool carriageReturn = false;

            for (int i = 0, length = value.Length; i < length; i++)
            {
                char ch = value[i];
                if (ch == '&')
                {
                    i++;
                    int j = value.IndexOf(';', i);
                    if (j > i)
                    {
                        string name = value.Substring(i, j - i);
                        switch (name)
                        {
                            case "amp":
                                writer.Write('&');
                                break;
                            case "lt":
                                writer.Write('<');
                                break;
                            case "gt":
                                writer.Write('>');
                                break;
                            case "apos":
                                writer.Write('\'');
                                break;
                            case "quot":
                                writer.Write('"');
                                break;
                            default:
                                throw new Exception("Unsupported entity: " + name);
                        }
                    }
                }
                else if (ch == '\r')
                {
                    carriageReturn = true;
                }
                else if (ch == '\n')
                {
                    carriageReturn = false;
                    writer.WriteLine();
                }
                else
                {
                    if (carriageReturn)
                    {
                        carriageReturn = false;
                        writer.WriteLine();
                    }
                    writer.Write(ch);
                }
            }
        }
    }

    class CssAttributes
    {
        CssAttributes inherited;
        Dictionary<string, string> map = new Dictionary<string, string>();

        public static CssAttributes Parse(string style, CssAttributes inherited)
        {
            if (string.IsNullOrWhiteSpace(style))
            {
                return inherited;
            }
            CssAttributes result = new CssAttributes(inherited);

            foreach (var part in style.Split(';'))
            {
                string[] nvalue = part.Split(':');
                if (nvalue.Length == 2)
                {
                    result.map[nvalue[0]] = nvalue[1];
                }
                else
                {
                    Console.WriteLine("Ignoring CSS attribute: " + part);
                }
            }

            return result;
        }

        public string Find(string name)
        {
            string result = null;
            if (!map.TryGetValue(name, out result))
            {
                if (inherited != null)
                {
                    return inherited.Find(name);
                }
            }
            return result;
        }

        internal double GetFontSize()
        {
            string value = Find("font-size");
            if (!string.IsNullOrEmpty(value))
            {
                if (value.EndsWith("pt"))
                {
                    value = value.Substring(0, value.Length - 2);
                    return double.Parse(value);
                }
                else
                {
                    throw new Exception("Only supports point sizes");
                }
            }
            return 0;
        }

        internal void Push(string name, string value)
        {
            map[name] = value;
        }

        internal void Pop(string name)
        {
            map.Remove(name);
        }

        public CssAttributes(CssAttributes inherited)
        {
            this.inherited = inherited;
        }
    }
}
