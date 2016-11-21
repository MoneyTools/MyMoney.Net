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
        StreamWriter writer;

        public void Convert(string path, string baseUri)
        {
            this.baseUri = baseUri;
            XDocument html = XDocument.Load(path);

            string mdFile = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".md");
            using (StreamWriter sw = new StreamWriter(mdFile))
            {
                this.writer = sw;
                XNamespace ns = html.Root.Name.Namespace;
                XElement body = html.Root.Element(ns + "body");
                if (body == null)
                {
                    throw new Exception("Missing html body");
                }

                WalkElements(body, null);
            }

            File.Delete(path);
        }

        bool ignoreFirstParagraph = true;

        void WalkElements(XElement e, CssAttributes inherited)
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

            bool heading = false;

            if (beforeSize != size)
            {
                if (size == 12)
                {
                    writer.Write("### ");
                    heading = true;
                }
                else if (size >= 13 && size < 17)
                {
                    writer.Write("## ");
                    heading = true;
                }
                else if (size >= 17)
                {
                    writer.Write("# ");
                    heading = true;
                }
                else
                {
                    Console.WriteLine("Font size changed to {0} on element {1}", size, e.Name.LocalName);
                }
            }
            if (heading) {
                attrs.Push("heading", "true");
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
                            WalkElements(ce, attrs);
                            writer.Write("](");
                            WriteAnchor((string)ce.Attribute("href"), attrs);
                            writer.Write(")");
                            break;
                        case "h3":
                            CheckNewLine();
                            if (!string.IsNullOrWhiteSpace(ce.Value))
                            {
                                attrs.Push("font-size", "12pt");
                                attrs.Push("heading", "true");
                                writer.Write("### ");
                                WalkElements(ce, attrs);
                                attrs.Pop("font-size");
                                attrs.Pop("heading");
                                WriteLine();
                            }
                            break;
                        case "h2":
                            CheckNewLine();
                            if (!string.IsNullOrWhiteSpace(ce.Value))
                            {
                                attrs.Push("font-size", "13pt");
                                attrs.Push("heading", "true");
                                writer.Write("## ");
                                WalkElements(ce, attrs);
                                attrs.Pop("font-size");
                                attrs.Pop("heading");
                                WriteLine();
                            }
                            break;
                        case "h1":
                            CheckNewLine();
                            if (!string.IsNullOrWhiteSpace(ce.Value))
                            {
                                attrs.Push("font-size", "17pt");
                                attrs.Push("heading", "true");
                                writer.Write("# ");
                                WalkElements(ce, attrs);
                                attrs.Pop("font-size");
                                attrs.Pop("heading");
                                WriteLine();
                            }
                            break;
                        case "div":
                        case "p":
                            CheckNewLine();
                            WalkElements(ce, attrs);
                            WriteLine();
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
                            WalkElements(ce, attrs);
                            WriteLine();
                            break;
                        case "span":

                            if (!string.IsNullOrEmpty(ce.Value))
                            {
                                bool bold = attrs.Find("fond-weight") == "bold";
                                if (bold)
                                {
                                    writer.Write("** ");
                                }
                                WalkElements(ce, attrs);
                                if (bold)
                                {
                                    writer.Write("** ");
                                }
                            }
                            break;
                        case "img":
                            CheckNewLine();
                            writer.Write("![");
                            writer.Write("](");
                            WriteAnchor((string)ce.Attribute("src"), attrs);
                            writer.Write(")");
                            break;
                        case "ol":
                            CheckNewLine();
                            attrs.Push("list", "ordered");
                            WalkElements(ce, attrs);
                            attrs.Pop("list");
                            WriteLine();
                            break;
                        case "ul":
                            CheckNewLine();
                            attrs.Push("list", "unordered");
                            WalkElements(ce, attrs);
                            attrs.Pop("list");
                            WriteLine();
                            break;
                        case "br":
                            WriteLine();
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
                    WriteText(text.Value, attrs);
                }
            }

            if (heading)
            {
                attrs.Pop("heading");
            }
        }

        int linePos = 0;

        void WriteLine()
        {
            writer.WriteLine();
            linePos = 0;
        }

        void CheckNewLine()
        {
            if (linePos > 0)
            {
                WriteLine();
                WriteLine();
            }
        }

        private void WriteAnchor(string href, CssAttributes attrs)
        {
            if (!href.Contains("://"))
            {
                // relative links are to markdown files, so no .htm suffix
                if (href.EndsWith(".htm"))
                {
                    href = href.Substring(0, href.Length - 4);
                }
                href = this.baseUri + href;
            }
            attrs.Push("attribute", "true");
            WriteText(href, attrs);
            attrs.Pop("attribute");
        }

        static char[] whitespace = new char[] { ' ', '\t', '\r', '\n' };

        void WriteText(string value, CssAttributes attrs)
        {
            if (value == "Created with Microsoft OneNote 2016.")
            {
                return;
            }
            bool isAttribute = attrs.Find("attribute") == "true";
            if (!isAttribute)
            {
                if (attrs.Find("heading") == "true")
                {
                    // markdown can't handle newlines in a heading.
                    value = value.Replace("\n", " ");
                }
                if (attrs.GetMarginLeft() > 0.3)
                {
                    value = value.Replace("\n", " ");
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        writer.Write("    ");
                        linePos += 4;
                    }
                }
                if (attrs.Find("font-weight", false) == "bold")
                {
                    writer.Write("** ");
                }
            }
            if (value.Replace("\n", " ").Contains("Did not get the expected response"))
            {
                Console.WriteLine("found it");
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
                                this.linePos++;
                                break;
                            case "lt":
                                writer.Write('<');
                                this.linePos++;
                                break;
                            case "gt":
                                writer.Write('>');
                                this.linePos++;
                                break;
                            case "apos":
                                writer.Write('\'');
                                this.linePos++;
                                break;
                            case "quot":
                                writer.Write('"');
                                this.linePos++;
                                break;
                            default:
                                throw new Exception("Unsupported entity: " + name);
                        }
                    }
                    else
                    {
                        writer.Write('&');
                        this.linePos++;
                    }
                }
                else if (ch == '\r')
                {
                    carriageReturn = true;
                }
                else if (ch == '\n')
                {
                    carriageReturn = false;
                    WriteLine();
                }
                else if ((ch == ' ' || ch == '\t' || ch == 0xA0) && linePos == 0)
                {
                    continue;
                }
                else
                {
                    if (carriageReturn)
                    {
                        carriageReturn = false;
                        WriteLine();
                    }
                    writer.Write(ch);
                    this.linePos++;
                }
            }
            if (!isAttribute)
            {
                if (attrs.Find("font-weight", false) == "bold")
                {
                    writer.Write("** ");
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

        public string Find(string name, bool searchInheritance = true)
        {
            string result = null;
            if (!map.TryGetValue(name, out result))
            {
                if (inherited != null && searchInheritance)
                {
                    return inherited.Find(name, searchInheritance);
                }
            }
            return result;
        }

        internal double GetFontSize(bool searchInheritance = false)
        {
            string value = Find("font-size", searchInheritance);
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
        internal double GetMarginLeft(bool searchInheritance = false)
        {
            string value = Find("margin-left", searchInheritance);
            if (!string.IsNullOrEmpty(value))
            {
                if (value.EndsWith("in"))
                {
                    value = value.Substring(0, value.Length - 2);
                    return double.Parse(value);
                }
                else
                {
                    throw new Exception("Only supports margin in inches");
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
