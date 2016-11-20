using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Collections.Specialized;
using System.Xml;
using Walkabout.Sgml;

namespace PublishHelp
{
    /// <summary>
    /// This class parses a MIME encoded file format as per http://tools.ietf.org/html/rfc2557 
    /// </summary>
    class MimePackage
    {
        public string MimeVersion { get; set; }

        public MimePart RootPart { get; set; }

        public bool IsMultipart { get; set; }
        
        public List<MimePart> Parts { get; set; }  // if multipart

        public static MimePackage Load(string fileName)
        {
            MimePackage result = new MimePackage();
            
            using (StreamReader sr = new StreamReader(fileName, Encoding.UTF8))
            {
                result.Parse(sr);
            }

            return result;
        }

        StreamReader reader;
        int lineNumber;
        string boundary;
        string lastBoundaryMarker;

        public string ReadLine()
        {
            string line = reader.ReadLine();
            if (line != null)
            {
                lineNumber++;
            }
            return line;
        }

        public void Error(string message)
        {
            throw new Exception("Error on line " + lineNumber + ": " + message);
        }

        private void Parse(StreamReader sr)
        {
            reader = sr;
            ParseVersion();

            RootPart = ParseHeader();

            if (RootPart.Type == "multipart")
            {
                IsMultipart = true;
                Parts = new List<MimePart>();

                boundary = RootPart.ContentTypeParameters["boundary"];
                if (string.IsNullOrEmpty(boundary))
                {
                    Error("Missing 'boundary' parameter on root Content-Type");
                }

                RootPart.RawBody = ParseBody();

                string terminator = "--" + boundary + "--";

                while (lastBoundaryMarker != terminator)
                {
                    MimePart part = ParseHeader();
                    part.RawBody = ParseBody();
                    Parts.Add(part);
                }

            }
            else
            {
                RootPart.RawBody = reader.ReadToEnd();
            }
        
        }

        internal const string MimeVersionPrefix = "MIME-Version:";
        internal const string ContentTypePrefix = "Content-Type:";
        internal const string ContentEncodingPrefix = "Content-Transfer-Encoding:";
        internal const string ContentLocationPrefix = "Content-Location:";
        internal const string ContentIdPrefix = "Content-ID:";
        internal const string ContentDescriptionPrefix = "Content-Description:";

        private void ParseVersion()
        {
            string line = ReadLine();

            if (line == null || !line.StartsWith(MimeVersionPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                Error("Missing MIME-Version header");
            }

            // todo: this version may contain comments inside parentheses that may need to be stripped out in order
            // to do property version number comparisons.
            MimeVersion = line.Substring(MimeVersionPrefix.Length).Trim();
        }

        private MimePart ParseHeader()
        {
            MimePart result = new MimePart();
            // header is terminated by an empty line.
            string line = ReadLine();
            while (line != null && line != "")
            {
                if (line.StartsWith(ContentTypePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseContentType(result, line.Substring(ContentTypePrefix.Length).Trim());
                }
                else if (line.StartsWith(ContentEncodingPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    result.ContentEncoding = line.Substring(ContentEncodingPrefix.Length).Trim();
                }
                else if (line.StartsWith(ContentLocationPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    result.ContentLocation = line.Substring(ContentLocationPrefix.Length).Trim();
                }
                else if (line.StartsWith(ContentIdPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    result.ContentLocation = line.Substring(ContentIdPrefix.Length).Trim();
                }
                else if (line.StartsWith(ContentDescriptionPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    result.ContentLocation = line.Substring(ContentDescriptionPrefix.Length).Trim();
                }
                else
                {
                    // ignore unknown headers.
                }

                line = ReadLine();
            }

            return result;
        }
        
        public void ParseContentType(MimePart part, string s)
        {
            int i = s.IndexOf('/');
            if (i < 0)
            {
                Error("Malformed content type missing subtype after '/'");
            }

            part.Type = s.Substring(0, i).Trim();

            s = s.Substring(i + 1);

            i = s.IndexOf(';');
            if (i < 0)
            {
                part.SubType = s.Trim();
                return;
            }
            else
            {
                part.SubType = s.Substring(0, i).Trim();
            }

            // parameters: attribute "=" value
            while (i >= 0)
            {
                s = s.Substring(i + 1);
                i = s.IndexOf('=');
                if (i < 0)
                {
                    Error(part.Type + " parameter missing equals '='");
                }

                string paramName = s.Substring(0, i).Trim();
                
                bool quoted = false;
                s = s.Substring(i + 1);
                string paramValue = s.Trim();

                if (paramValue.Length > 1 && paramValue[0] == '"')
                {
                    quoted= true;
                    // quoted value.
                    int endQuote = paramValue.IndexOf('"', 1);
                    if (endQuote > 0)
                    {
                        s = paramValue.Substring(endQuote+1);
                        paramValue = paramValue.Substring(1, endQuote - 1);
                    }
                    else
                    {
                        Error("Missing end quote on value for parameter " + paramName);
                    }
                }


                i = s.IndexOf(';');

                if (! quoted && i >= 0)
                {                    
                    paramValue = paramValue.Substring(0, i).Trim();
                }

                part.ContentTypeParameters.Add(paramName, paramValue);
            }

        }


        private string ParseBody()
        {
            string boundaryPrefix = "--" + boundary;
            StringBuilder sb = new StringBuilder();
            string line = ReadLine();
            while (line != null)
            {
                if (line.StartsWith(boundaryPrefix))
                {
                    lastBoundaryMarker = line;
                    break;
                }
                sb.Append(line);
                sb.AppendLine();
                line = ReadLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Save as a single file web file (.mht or .mhtml file).
        /// This is a multipart MIME format.
        /// </summary>
        /// <param name="mhtFile"></param>
        internal void SaveMht(string mhtFile)
        {
            using (StreamWriter writer = new StreamWriter(mhtFile, false, Encoding.UTF8))
            {
                Write(writer);
            }
        }

        /// <summary>
        /// Save as a separeate HTML and image files where images are stored in an "Images" 
        /// subdirectory
        /// </summary>
        /// <param name="htmlFile">The full path of the html file to save (assumes only one per MimePackage)</param>
        /// <param name="fileNames">The object used to ensure unique file names for the images.</param>
        internal void SaveParts(string htmlFile, UniqueFileNames fileNames)
        {
            Uri baseUri = new Uri(htmlFile);
            if (RootPart.IsHtml)
            {
                using (StreamWriter sw = new StreamWriter(htmlFile, false, Encoding.UTF8))
                {
                    sw.Write(RootPart.HtmlDocument.ToString());
                }
            }
            else if (IsMultipart)
            {
                XDocument doc = null;
                Uri location = null;
                Dictionary<string, string> map = new Dictionary<string,string>(); // from Content-Location to image file name.
                string baseName = Path.GetFileNameWithoutExtension(htmlFile);

                // Compute file names for the parts
                foreach (MimePart part in Parts)
                {
                    if (part.IsHtml)
                    {
                        location = new Uri(part.ContentLocation);
                        doc = part.HtmlDocument;
                    }
                    else if (part.Type == "image")
                    {
                        string imageName = fileNames.Add(baseName);
                        Uri imageUri = new Uri(baseUri, "Images/" + imageName + "." + part.SubType);
                        part.FileName = imageUri.LocalPath;
                        map[part.ContentLocation] = part.FileName;
                        Directory.CreateDirectory(Path.GetDirectoryName(part.FileName));

                        using (FileStream fs = new FileStream(part.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            if (part.ContentEncoding == "base64")
                            {
                                byte[] decoded = Convert.FromBase64String(part.RawBody);
                                fs.Write(decoded, 0, decoded.Length);
                            }
                            else 
                            {
                                throw new Exception("Unknown image encoding: " + part.ContentEncoding);
                            }
                        }
                    }
                }

                if (doc == null) 
                {
                    throw new Exception("Multipart parts missing an HTML document");
                }

                // fix image tags to point to 
                foreach (XElement img in doc.Root.Descendants(doc.Root.Name.Namespace + "img"))
                {
                    string src = (string)img.Attribute("src");
                    if (!string.IsNullOrEmpty(src)) 
                    {
                        Uri resolved = new Uri(location, src);
                        string absolute = resolved.AbsoluteUri;
                        // find this part
                        string fileName = null;
                        if (map.TryGetValue(absolute, out fileName))
                        {
                            Uri uri = new Uri(fileName);
                            Uri relative = baseUri.MakeRelativeUri(uri);
                            src = relative.ToString();
                            img.SetAttributeValue("src", src);
                        }
                        else
                        {
                            Console.WriteLine("### Error: could not find image for part " + src);
                        }
                    }
                }

                doc.Save(htmlFile);
            }
        }


        internal void Write(TextWriter writer)
        {
            writer.Write(MimeVersionPrefix);
            writer.WriteLine(MimeVersion);
            if (RootPart != null)
            {
                RootPart.Write(writer);
            }

            if (IsMultipart)
            {
                boundary = RootPart.ContentTypeParameters["boundary"];
                if (string.IsNullOrEmpty(boundary))
                {
                    Error("Missing 'boundary' parameter on root Content-Type");
                }

                foreach (MimePart part in Parts)
                {
                    writer.Write("--");
                    writer.WriteLine(boundary);
                    part.Write(writer);
                }

                // write terminator
                writer.Write("--");
                writer.Write(boundary);
                writer.WriteLine("--");
            }

        }
    }

    class MimePart
    {
        // sub-parts of ContentType
        public string Type { get; set; }                    // text, image, audio, video, application, message, multipart
        public string SubType { get; set; }                 // the second part, tml, xml, jpg, or for composite type "related", etc

        public StringDictionary ContentTypeParameters { get; set; }    // name=value pairs

        public string ContentLocation { get; set; }
        public string ContentEncoding { get; set; }
        public string ContentId { get; set; }
        public string ContentDescription { get; set; }
        
        public string RawBody { get; set; }

        public string FileName { get; set; } // if multi-part is being saved as separate files.

        public MimePart()
        {
            ContentTypeParameters = new StringDictionary();
        }

        public string Body
        {
            get
            {
                // get the decoded body.
                if (string.Compare(ContentEncoding, "quoted-printable", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    return DecodeQuotedPrintable(RawBody);
                }
                else if (string.Compare(ContentEncoding, "base64", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    throw new Exception("Content is base64 encoded, which probably means it is binary data, not a string, please use Binary property instead");
                }
                return RawBody;
            }
            set
            {
                if (string.Compare(ContentEncoding, "quoted-printable", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    RawBody = EncodeQuotedPrintable(value);
                    return;
                }
                else if (string.Compare(ContentEncoding, "base64", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    throw new Exception("Content is base64 encoded, which probably means it is binary data, not a string, please use Binary property instead");
                }
                RawBody = value;
            }
        }

        public byte[] Binary
        {
            get
            {                
                if (string.Compare(ContentEncoding, "base64", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    return Convert.FromBase64String(RawBody);
                }
                else
                {
                    throw new Exception("Content is not base64 encoded, which probably means it is not binary data, please use Body property instead");
                }
            }
        }

        public string DecodeQuotedPrintable(string s)
        {
            int lineNumber = 0;
            StringBuilder result = new StringBuilder();
            for (int i = 0, n = s.Length; i < n; i++)
            {
                char ch = s[i];
                if (ch == '=' && i + 1 < n)
                {
                    i++;
                    char next = s[i];
                    if (next == '\r')
                    {
                        // skip this new line, it was inserted to make the lines shorter.
                        lineNumber++;
                        if (i +1 < n && s[i + 1] == '\n')
                        {
                            // skip crlf pair
                            i++;
                        }
                    }
                    else if (i + 1 < n)
                    {
                        result.Append(Utilities.FromOctet(s[i], s[i + 1]));
                        i++;
                    }
                }
                else
                {
                    if (ch == '\n')
                    {
                        lineNumber++;
                    }
                    result.Append(ch);
                }
            }
            return result.ToString();
        }

        private string EncodeQuotedPrintable(string body)
        {
            StringBuilder sb = new StringBuilder();
            int col = 0;
            for (int i = 0, n = body.Length; i < n; i++)
            {
                char ch = body[i];
                if (ch == '\r' || ch == '\n')
                {
                    sb.Append(ch);
                    col = 0;
                }
                else if (ch == '=')
                {
                    if (col + 3 < 76)
                    {
                        sb.Append("=3D");
                        col += 3;
                    }
                    else
                    {
                        // soft new line
                        sb.Append('=');
                        sb.AppendLine();
                        sb.Append("=3D");
                        col = 2;
                    }
                }
                else if (col < 76)
                {
                    sb.Append(ch);
                    col++;
                }
                else
                {
                    // soft new line
                    sb.Append('=');
                    sb.AppendLine();
                    sb.Append(ch);
                    col = 1;
                }                
            }
            return sb.ToString();
        }

        public bool IsHtml
        {
            get
            {
                return string.Compare(this.Type, "text", StringComparison.InvariantCultureIgnoreCase) == 0 &&
                    string.Compare(this.SubType, "html", StringComparison.InvariantCultureIgnoreCase) == 0;
            }
        }

        static SgmlDtd HtmlDtd = null;

        public XDocument HtmlDocument
        {
            get
            {
                if (IsHtml)
                {
                    SgmlReader reader = new SgmlReader();
                    reader.InputStream = new StringReader(this.Body);
                    reader.DocType = "HTML";
                    if (HtmlDtd == null)
                    {
                        string dtdFile = System.IO.Path.GetTempPath() + "\\" + "html.dtd";
                        Utilities.ExtractEmbeddedResourceAsFile("PublishHelp.Html.dtd", dtdFile);
                        HtmlDtd = SgmlDtd.Parse(new Uri(dtdFile), "HTML", null, dtdFile, "", "", reader.NameTable, true);
                    }
                    reader.Dtd = HtmlDtd;

                    return XDocument.Load(reader);
                }
                throw new Exception("Body is not of type 'text/html'");
            }
        }

        internal void Write(TextWriter writer)
        {
            if (!string.IsNullOrEmpty(ContentLocation))
            {
                writer.Write(MimePackage.ContentLocationPrefix);
                writer.Write(" ");
                writer.WriteLine(ContentLocation);
            }
            if (!string.IsNullOrEmpty(ContentEncoding))
            {
                writer.Write(MimePackage.ContentEncodingPrefix);
                writer.Write(" ");
                writer.WriteLine(ContentEncoding);
            }
            if (!string.IsNullOrEmpty(ContentId))
            {
                writer.Write(MimePackage.ContentIdPrefix);
                writer.Write(" ");
                writer.WriteLine(ContentId);
            }
            if (!string.IsNullOrEmpty(ContentDescription))
            {
                writer.Write(MimePackage.ContentDescriptionPrefix);
                writer.Write(" ");
                writer.WriteLine(ContentDescription);
            }
            if (string.IsNullOrEmpty(Type))
            {
                throw new Exception("Missing required content Type");
            }
            if (string.IsNullOrEmpty(SubType))
            {
                throw new Exception("Missing required content SubType");
            }

            writer.Write(MimePackage.ContentTypePrefix);
            writer.Write(Type);
            writer.Write("/");
            writer.Write(SubType);

            if (ContentTypeParameters != null && ContentTypeParameters.Count > 0)
            {
                writer.Write("; ");
                foreach (string key in ContentTypeParameters.Keys)
                {
                    string value = ContentTypeParameters[key];
                    writer.Write(key);
                    writer.Write("=");
                    writer.Write(value);
                }
            }

            // CRLFCRLF sequence separates the body.
            writer.WriteLine();
            writer.WriteLine();

            writer.WriteLine(RawBody);            

            writer.WriteLine();
            return;
        }

    }
}
