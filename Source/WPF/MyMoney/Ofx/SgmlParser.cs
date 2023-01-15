using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace Walkabout.Sgml
{
    public enum LiteralType
    {
        CDATA, SDATA, PI
    };

    public class Entity
    {
        public const char EOF = (char)65535;
        public string Proxy;

        public Entity(string name, string pubid, string uri, string proxy)
        {
            this.Name = name;
            this.PublicId = pubid;
            this.Uri = uri;
            this.Proxy = proxy;
        }

        public Entity(string name, string literal)
        {
            this.Name = name;
            this.Literal = literal;
            this.Internal = true;
        }

        public Entity(string name, Uri baseUri, TextReader stm, string proxy)
        {
            this.Name = name;
            this.Internal = true;
            this._stm = stm;
            this._resolvedUri = baseUri;
            this.Proxy = proxy;
        }

        public string Name;
        public bool Internal;
        public string PublicId;
        public string Uri;
        public string Literal;
        public LiteralType LiteralType;
        public Entity Parent;

        public Uri ResolvedUri
        {
            get
            {
                if (this._resolvedUri != null)
                {
                    return this._resolvedUri;
                }
                else if (this.Parent != null)
                {
                    return this.Parent.ResolvedUri;
                }

                return null;
            }
        }

        private Uri _resolvedUri;
        private TextReader _stm;
        private bool _weOwnTheStream;

        public int Line;
        private int _LineStart;
        private int _absolutePos;
        public char Lastchar;
        public bool IsWhitespace;

        public int LinePosition
        {
            get { return this._absolutePos - this._LineStart + 1; }
        }

        public char ReadChar()
        {
            char ch = (char)this._stm.Read();
            if (ch == 0)
            {
                // convert nulls to whitespace, since they are not valid in XML anyway.
                ch = ' ';
            }
            this._absolutePos++;
            if (ch == 0xa)
            {
                this.IsWhitespace = true;
                this._LineStart = this._absolutePos + 1;
                this.Line++;
            }
            else if (ch == ' ' || ch == '\t')
            {
                this.IsWhitespace = true;
                if (this.Lastchar == 0xd)
                {
                    this._LineStart = this._absolutePos;
                    this.Line++;
                }
            }
            else if (ch == 0xd)
            {
                this.IsWhitespace = true;
            }
            else
            {
                this.IsWhitespace = false;
                if (this.Lastchar == 0xd)
                {
                    this.Line++;
                    this._LineStart = this._absolutePos;
                }
            }
            this.Lastchar = ch;
            return ch;
        }

        public void Open(Entity parent, Uri baseUri)
        {
            this.Parent = parent;
            this.Line = 1;
            if (this.Internal)
            {
                if (this.Literal != null)
                {
                    this._stm = new StringReader(this.Literal);
                }
            }
            else if (this.Uri == null)
            {
                this.Error("Unresolvable entity '{0}'", this.Name);
            }
            else
            {
                if (baseUri != null)
                {
                    // bugbug: new Uri(baseUri, this.Uri) when baseUri is
                    // file://currentdirectory and this.Uri is "\temp\test.htm"
                    // resolves to a UNC with LocalPath starting with \\ which
                    // is wrong!
                    if (baseUri.Scheme == "file")
                    {
                        // bugbug: Path.Combine looses the base path's drive name!
                        string path = baseUri.LocalPath;
                        int i = path.IndexOf(":");
                        string drive = string.Empty;
                        if (i > 0)
                        {
                            drive = path.Substring(0, i + 1);
                        }
                        string s = Path.Combine(baseUri.LocalPath, this.Uri);
                        if (s.Substring(1, 2) == ":\\")
                        {
                            drive = string.Empty;
                        }

                        string uri = "file:///" + drive + s;
                        this._resolvedUri = new Uri(uri);
                    }
                    else
                    {
                        this._resolvedUri = new Uri(baseUri, this.Uri);
                    }
                }
                else
                {
                    this._resolvedUri = new Uri(this.Uri);
                }
                switch (this._resolvedUri.Scheme)
                {
                    case "file":
                        {
                            string path = this._resolvedUri.LocalPath;
                            this._stm = new StreamReader(
                                new FileStream(path, FileMode.Open, FileAccess.Read),
                                Encoding.Default, true);
                            this._weOwnTheStream = true;
                        }
                        break;
                    default:
                        //Console.WriteLine("Fetching:" + ResolvedUri.AbsoluteUri);
                        HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(this.ResolvedUri);
                        wr.Timeout = 10000; // in case this is running in an ASPX page.
                        if (this.Proxy != null)
                        {
                            wr.Proxy = new WebProxy(this.Proxy);
                        }

                        wr.PreAuthenticate = false;
                        // Pass the credentials of the process. 
                        wr.Credentials = CredentialCache.DefaultCredentials;

                        WebResponse resp = wr.GetResponse();
                        Uri actual = resp.ResponseUri;
                        if (actual.AbsoluteUri != this._resolvedUri.AbsoluteUri)
                        {
                            this._resolvedUri = actual;
                        }
                        string contentType = resp.ContentType.ToLower();
                        int i = contentType.IndexOf("charset");
                        Encoding e = Encoding.Default;
                        if (i >= 0)
                        {
                            int j = contentType.IndexOf("=", i);
                            int k = contentType.IndexOf(";", j);
                            if (k < 0)
                            {
                                k = contentType.Length;
                            }

                            if (j > 0)
                            {
                                j++;
                                string charset = contentType.Substring(j, k - j).Trim();
                                try
                                {
                                    e = Encoding.GetEncoding(charset);
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }
                        this._stm = new StreamReader(resp.GetResponseStream(), e, true);
                        this._weOwnTheStream = true;
                        break;

                }
            }
        }

        public void Close()
        {
            if (this._weOwnTheStream)
            {
                this._stm.Close();
                this._stm.Dispose();
            }
        }

        public char SkipWhitespace()
        {
            char ch = this.Lastchar;
            while (ch != Entity.EOF && (ch == ' ' || ch == '\r' || ch == '\n' || ch == '\t'))
            {
                ch = this.ReadChar();
            }
            return ch;
        }

        public string ScanToken(StringBuilder sb, string term, bool nmtoken)
        {
            sb.Length = 0;
            char ch = this.Lastchar;
            if (nmtoken && ch != '_' && !char.IsLetter(ch))
            {
                throw new Exception(
                    string.Format("Invalid name start character '{0}'", ch));
            }
            while (ch != Entity.EOF && term.IndexOf(ch) < 0)
            {
                if (!nmtoken || ch == '_' || ch == '.' || ch == '-' || ch == ':' || char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                }
                else
                {
                    throw new Exception(
                        string.Format("Invalid name character '{0}'", ch));
                }
                ch = this.ReadChar();
            }
            return sb.ToString();
        }

        public string ScanLiteral(StringBuilder sb, char quote)
        {
            sb.Length = 0;
            char ch = this.ReadChar();
            while (ch != Entity.EOF && ch != quote)
            {
                if (ch == '&')
                {
                    ch = this.ReadChar();
                    if (ch == '#')
                    {
                        string charent = this.ExpandCharEntity();
                        sb.Append(charent);
                    }
                    else
                    {
                        sb.Append('&');
                        sb.Append(ch);
                    }
                }
                else
                {
                    sb.Append(ch);
                }
                ch = this.ReadChar();
            }
            this.ReadChar(); // consume end quote.           
            return sb.ToString();
        }

        public string ScanToEnd(StringBuilder sb, string type, string terminators)
        {
            if (sb != null)
            {
                sb.Length = 0;
            }

            int start = this.Line;
            // This method scans over a chunk of text looking for the
            // termination sequence specified by the 'terminators' parameter.
            char ch = this.ReadChar();
            int state = 0;
            char next = terminators[state];
            while (ch != Entity.EOF)
            {
                if (ch == next)
                {
                    state++;
                    if (state >= terminators.Length)
                    {
                        // found it!
                        break;
                    }
                    next = terminators[state];
                }
                else if (state > 0)
                {
                    // char didn't match, so go back and see how much does still match.
                    int i = state - 1;
                    int newstate = 0;
                    while (i >= 0 && newstate == 0)
                    {
                        if (terminators[i] == ch)
                        {
                            // character is part of the terminators pattern, ok, so see if we can
                            // match all the way back to the beginning of the pattern.
                            int j = 1;
                            while (i - j >= 0)
                            {
                                if (terminators[i - j] != terminators[state - j])
                                {
                                    break;
                                }

                                j++;
                            }
                            if (j > i)
                            {
                                newstate = i + 1;
                            }
                        }
                        else
                        {
                            i--;
                        }
                    }
                    if (sb != null)
                    {
                        i = (i < 0) ? 1 : 0;
                        for (int k = 0; k <= state - newstate - i; k++)
                        {
                            sb.Append(terminators[k]);
                        }
                        if (i > 0) // see if we've matched this char or not
                        {
                            sb.Append(ch); // if not then append it to buffer.
                        }
                    }
                    state = newstate;
                    next = terminators[newstate];
                }
                else
                {
                    if (sb != null)
                    {
                        sb.Append(ch);
                    }
                }
                ch = this.ReadChar();
            }
            if (ch == 0)
            {
                this.Error(type + " starting on line {0} was never closed", start);
            }

            this.ReadChar(); // consume last char in termination sequence.
            if (sb != null)
            {
                return sb.ToString();
            }

            return string.Empty;
        }

        public string ExpandCharEntity()
        {
            char ch = this.ReadChar();
            int v = 0;
            if (ch == 'x' || ch == 'X')
            {
                ch = this.ReadChar();

                for (; ch != Entity.EOF && ch != ';'; ch = this.ReadChar())
                {
                    int p = 0;
                    if (ch >= '0' && ch <= '9')
                    {
                        p = ch - '0';
                    }
                    else if (ch >= 'a' && ch <= 'f')
                    {
                        p = ch - 'a' + 10;
                    }
                    else if (ch >= 'A' && ch <= 'F')
                    {
                        p = ch - 'A' + 10;
                    }
                    else
                    {
                        break;//we must be done!
                        //Error("Hex digit out of range '{0}'", (int)ch);
                    }
                    v = (v * 16) + p;
                }
            }
            else
            {
                for (; ch != Entity.EOF && ch != ';'; ch = this.ReadChar())
                {
                    if (ch >= '0' && ch <= '9')
                    {
                        v = (v * 10) + (ch - '0');
                    }
                    else
                    {
                        break; // we must be done!
                        //Error("Decimal digit out of range '{0}'", (int)ch);
                    }
                }
            }
            if (ch == 0)
            {
                this.Error("Premature {0} parsing entity reference", ch);
            }

            return Convert.ToChar(v).ToString();
        }

        private static readonly int[] CtrlMap = new int[] {
            // This is the windows-1252 mapping of the code points 0x80 through 0x9f.
            8364, 129, 8218, 402, 8222, 8230, 8224, 8225, 710, 8240, 352, 8249, 338, 141,
            381, 143, 144, 8216, 8217, 8220, 8221, 8226, 8211, 8212, 732, 8482, 353, 8250,
            339, 157, 382, 376
        };

        public void Error(string msg)
        {
            throw new Exception(msg);
        }

        public void Error(string msg, char ch)
        {
            string str = (ch == Entity.EOF) ? "EOF" : char.ToString(ch);
            throw new Exception(string.Format(msg, str));
        }

        public void Error(string msg, int x)
        {
            throw new Exception(string.Format(msg, x));
        }

        public void Error(string msg, string arg)
        {
            throw new Exception(string.Format(msg, arg));
        }

        public string Context()
        {
            Entity p = this;
            StringBuilder sb = new StringBuilder();
            while (p != null)
            {
                string msg;
                if (p.Internal)
                {
                    msg = string.Format("\nReferenced on line {0}, position {1} of internal entity '{2}'", p.Line, p.LinePosition, p.Name);
                }
                else
                {
                    msg = string.Format("\nReferenced on line {0}, position {1} of '{2}' entity at [{3}]", p.Line, p.LinePosition, p.Name, p.ResolvedUri.AbsolutePath);
                }
                sb.Append(msg);
                p = p.Parent;
            }
            return sb.ToString();
        }

        public static bool IsLiteralType(string token)
        {
            return token == "CDATA" || token == "SDATA" || token == "PI";
        }

        public void SetLiteralType(string token)
        {
            switch (token)
            {
                case "CDATA":
                    this.LiteralType = LiteralType.CDATA;
                    break;
                case "SDATA":
                    this.LiteralType = LiteralType.SDATA;
                    break;
                case "PI":
                    this.LiteralType = LiteralType.PI;
                    break;
            }
        }
    }

    public class ElementDecl
    {
        public ElementDecl(string name, bool sto, bool eto, ContentModel cm, string[] inclusions, string[] exclusions)
        {
            this.Name = name;
            this.StartTagOptional = sto;
            this.EndTagOptional = eto;
            this.ContentModel = cm;
            this.Inclusions = inclusions;
            this.Exclusions = exclusions;
        }

        public string Name;
        public bool StartTagOptional;
        public bool EndTagOptional;
        public ContentModel ContentModel;
        public string[] Inclusions;
        public string[] Exclusions;

        public AttList AttList;

        public void AddAttDefs(AttList list)
        {
            if (this.AttList == null)
            {
                this.AttList = list;
            }
            else
            {
                foreach (AttDef a in list)
                {
                    if (this.AttList[a.Name] == null)
                    {
                        this.AttList.Add(a);
                    }
                }
            }
        }

        public bool CanContain(string name, SgmlDtd dtd)
        {
            bool ignoreCase = dtd.IgnoreCase;

            // return true if this element is allowed to contain the given element.
            if (this.Exclusions != null)
            {
                foreach (string s in this.Exclusions)
                {
                    if (s == (object)name) // XmlNameTable optimization
                    {
                        return false;
                    }

                    if (ignoreCase && string.Compare(s, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return true;
                    }
                }
            }
            if (this.Inclusions != null)
            {
                foreach (string s in this.Inclusions)
                {
                    if (s == (object)name) // XmlNameTable optimization
                    {
                        return true;
                    }

                    if (ignoreCase && string.Compare(s, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return true;
                    }
                }
            }
            return this.ContentModel.CanContain(name, dtd);
        }
    }

    public enum DeclaredContent
    {
        Default, CDATA, RCDATA, EMPTY
    }

    public class ContentModel
    {
        public DeclaredContent DeclaredContent;
        public int CurrentDepth;
        public Group Model;

        public ContentModel()
        {
            this.Model = new Group(null);
        }

        public void PushGroup()
        {
            this.Model = new Group(this.Model);
            this.CurrentDepth++;
        }

        public int PopGroup()
        {
            if (this.CurrentDepth == 0)
            {
                return -1;
            }

            this.CurrentDepth--;
            this.Model.Parent.AddGroup(this.Model);
            this.Model = this.Model.Parent;
            return this.CurrentDepth;
        }

        public void AddSymbol(string sym)
        {
            this.Model.AddSymbol(sym);
        }

        public void AddConnector(char c)
        {
            this.Model.AddConnector(c);
        }

        public void AddOccurrence(char c)
        {
            this.Model.AddOccurrence(c);
        }

        public void SetDeclaredContent(string dc)
        {
            switch (dc)
            {
                case "EMPTY":
                    this.DeclaredContent = DeclaredContent.EMPTY;
                    break;
                case "RCDATA":
                    this.DeclaredContent = DeclaredContent.RCDATA;
                    break;
                case "CDATA":
                    this.DeclaredContent = DeclaredContent.CDATA;
                    break;
                default:
                    throw new Exception(
                        string.Format("Declared content type '{0}' is not supported", dc));
            }
        }

        public bool CanContain(string name, SgmlDtd dtd)
        {
            if (this.DeclaredContent != DeclaredContent.Default)
            {
                return false; // empty or text only node.
            }

            return this.Model.CanContain(name, dtd);
        }
    }

    public enum GroupType
    {
        None, And, Or, Sequence
    };

    public enum Occurrence
    {
        Required, Optional, ZeroOrMore, OneOrMore
    }

    public class Group
    {
        public Group Parent;
        public ArrayList Members;
        public GroupType GroupType;
        public Occurrence Occurrence;
        public bool Mixed;

        public bool TextOnly
        {
            get { return this.Mixed && this.Members.Count == 0; }
        }

        public Group(Group parent)
        {
            this.Parent = parent;
            this.Members = new ArrayList();
            this.GroupType = GroupType.None;
            this.Occurrence = Occurrence.Required;
        }
        public void AddGroup(Group g)
        {
            this.Members.Add(g);
        }
        public void AddSymbol(string sym)
        {
            if (sym == "#PCDATA")
            {
                this.Mixed = true;
            }
            else
            {
                this.Members.Add(sym);
            }
        }
        public void AddConnector(char c)
        {
            if (!this.Mixed && this.Members.Count == 0)
            {
                throw new Exception(
                    string.Format("Missing token before connector '{0}'.", c)
                    );
            }
            GroupType gt = GroupType.None;
            switch (c)
            {
                case ',':
                    gt = GroupType.Sequence;
                    break;
                case '|':
                    gt = GroupType.Or;
                    break;
                case '&':
                    gt = GroupType.And;
                    break;
            }
            if (this.GroupType != GroupType.None && this.GroupType != gt)
            {
                throw new Exception(
                    string.Format("Connector '{0}' is inconsistent with {1} group.", c, this.GroupType.ToString())
                    );
            }
            this.GroupType = gt;
        }

        public void AddOccurrence(char c)
        {
            Occurrence o = Occurrence.Required;
            switch (c)
            {
                case '?':
                    o = Occurrence.Optional;
                    break;
                case '+':
                    o = Occurrence.OneOrMore;
                    break;
                case '*':
                    o = Occurrence.ZeroOrMore;
                    break;
            }
            this.Occurrence = o;
        }

        // Rough approximation - this is really assuming an "Or" group
        public bool CanContain(string name, SgmlDtd dtd)
        {
            // Do a simple search of members.
            foreach (object obj in this.Members)
            {
                if (obj is string)
                {
                    string s = (string)obj;
                    if (s == name || (dtd.IgnoreCase && string.Compare(s, name, StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        return true;
                    }
                }
            }
            // didn't find it, so do a more expensive search over child elements
            // that have optional start tags and over child groups.
            foreach (object obj in this.Members)
            {
                if (obj is string)
                {
                    string s = (string)obj;
                    ElementDecl e = dtd.FindElement(s);
                    if (e != null)
                    {
                        if (e.StartTagOptional)
                        {
                            // tricky case, the start tag is optional so element may be
                            // allowed inside this guy!
                            if (e.CanContain(name, dtd))
                            {
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    Group m = (Group)obj;
                    if (m.CanContain(name, dtd))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public enum AttributeType
    {
        DEFAULT, CDATA, ENTITY, ENTITIES, ID, IDREF, IDREFS, NAME, NAMES, NMTOKEN, NMTOKENS,
        NUMBER, NUMBERS, NUTOKEN, NUTOKENS, NOTATION, ENUMERATION
    }

    public enum AttributePresence
    {
        DEFAULT, FIXED, REQUIRED, IMPLIED
    }

    public class AttDef
    {
        public string Name;
        public AttributeType Type;
        public string[] EnumValues;
        public string Default;
        public AttributePresence Presence;

        public AttDef(string name)
        {
            this.Name = name;
        }


        public void SetType(string type)
        {
            switch (type)
            {
                case "CDATA":
                    this.Type = AttributeType.CDATA;
                    break;
                case "ENTITY":
                    this.Type = AttributeType.ENTITY;
                    break;
                case "ENTITIES":
                    this.Type = AttributeType.ENTITIES;
                    break;
                case "ID":
                    this.Type = AttributeType.ID;
                    break;
                case "IDREF":
                    this.Type = AttributeType.IDREF;
                    break;
                case "IDREFS":
                    this.Type = AttributeType.IDREFS;
                    break;
                case "NAME":
                    this.Type = AttributeType.NAME;
                    break;
                case "NAMES":
                    this.Type = AttributeType.NAMES;
                    break;
                case "NMTOKEN":
                    this.Type = AttributeType.NMTOKEN;
                    break;
                case "NMTOKENS":
                    this.Type = AttributeType.NMTOKENS;
                    break;
                case "NUMBER":
                    this.Type = AttributeType.NUMBER;
                    break;
                case "NUMBERS":
                    this.Type = AttributeType.NUMBERS;
                    break;
                case "NUTOKEN":
                    this.Type = AttributeType.NUTOKEN;
                    break;
                case "NUTOKENS":
                    this.Type = AttributeType.NUTOKENS;
                    break;
                default:
                    throw new Exception("Attribute type '" + type + "' is not supported");
            }
        }

        public bool SetPresence(string token)
        {
            bool hasDefault = true;
            if (token == "FIXED")
            {
                this.Presence = AttributePresence.FIXED;
            }
            else if (token == "REQUIRED")
            {
                this.Presence = AttributePresence.REQUIRED;
                hasDefault = false;
            }
            else if (token == "IMPLIED")
            {
                this.Presence = AttributePresence.IMPLIED;
                hasDefault = false;
            }
            else
            {
                throw new Exception(string.Format("Attribute value '{0}' not supported", token));
            }
            return hasDefault;
        }
    }

    public class AttList : IEnumerable
    {
        private readonly Hashtable AttDefs;

        public AttList(bool ignoreCase)
        {
            if (ignoreCase)
            {
                this.AttDefs = new Hashtable(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this.AttDefs = new Hashtable();
            }
        }

        public void Add(AttDef a)
        {
            this.AttDefs.Add(a.Name, a);
        }

        public AttDef this[string name]
        {
            get
            {
                return (AttDef)this.AttDefs[name];
            }
        }

        public IEnumerator GetEnumerator()
        {
            return this.AttDefs.Values.GetEnumerator();
        }
    }

    public class SgmlDtd
    {
        public string Name;
        private readonly Hashtable _elements;
        private readonly Hashtable _pentities;
        private readonly Hashtable _entities;
        private readonly StringBuilder _sb;
        private Entity _current;
        private readonly XmlNameTable _nt;
        private readonly bool _ignoreCase;

        public SgmlDtd(string name, XmlNameTable nt, bool ignoreCase = false)
        {
            this._ignoreCase = ignoreCase;
            this._nt = nt;
            if (name != null)
            {
                this.Name = this._nt.Add(name);
            }

            if (ignoreCase)
            {
                this._elements = new Hashtable(StringComparer.OrdinalIgnoreCase);
                this._pentities = new Hashtable(StringComparer.OrdinalIgnoreCase);
                this._entities = new Hashtable(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this._elements = new Hashtable();
                this._pentities = new Hashtable();
                this._entities = new Hashtable();
            }
            this._sb = new StringBuilder();
        }

        internal bool IgnoreCase { get { return this._ignoreCase; } }

        public XmlNameTable NameTable { get { return this._nt; } }

        public static SgmlDtd Parse(Uri baseUri, string name, string pubid, string url, string subset, string proxy, XmlNameTable nt, bool ignoreCase = false)
        {
            SgmlDtd dtd = new SgmlDtd(name, nt, ignoreCase);
            if (url != null && url != string.Empty)
            {
                dtd.PushEntity(baseUri, new Entity(dtd.Name, pubid, url, proxy));
            }
            if (subset != null && subset != string.Empty)
            {
                dtd.PushEntity(baseUri, new Entity(name, subset));
            }
            try
            {
                dtd.Parse();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message + dtd._current.Context());
            }
            return dtd;
        }
        public static SgmlDtd Parse(Uri baseUri, string name, string pubid, TextReader input, string subset, string proxy, XmlNameTable nt)
        {
            SgmlDtd dtd = new SgmlDtd(name, nt);
            dtd.PushEntity(baseUri, new Entity(dtd.Name, baseUri, input, proxy));
            if (subset != null && subset != string.Empty)
            {
                dtd.PushEntity(baseUri, new Entity(name, subset));
            }
            try
            {
                dtd.Parse();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message + dtd._current.Context());
            }
            return dtd;
        }

        public Entity FindEntity(string name)
        {
            return (Entity)this._entities[name];
        }

        public ElementDecl FindElement(string name)
        {
            return (ElementDecl)this._elements[name];
        }

        //-------------------------------- Parser -------------------------
        private void PushEntity(Uri baseUri, Entity e)
        {
            e.Open(this._current, baseUri);
            this._current = e;
            this._current.ReadChar();
        }

        private void PopEntity()
        {
            if (this._current != null)
            {
                this._current.Close();
            }

            if (this._current.Parent != null)
            {
                this._current = this._current.Parent;
            }
            else
            {
                this._current = null;
            }
        }

        private void Parse()
        {
            char ch = this._current.Lastchar;
            while (true)
            {
                switch (ch)
                {
                    case Entity.EOF:
                        this.PopEntity();
                        if (this._current == null)
                        {
                            return;
                        }

                        ch = this._current.Lastchar;
                        break;
                    case ' ':
                    case '\n':
                    case '\r':
                    case '\t':
                        ch = this._current.ReadChar();
                        break;
                    case '<':
                        this.ParseMarkup();
                        ch = this._current.ReadChar();
                        break;
                    case '%':
                        Entity e = this.ParseParameterEntity(_ws);
                        try
                        {
                            this.PushEntity(this._current.ResolvedUri, e);
                        }
                        catch (Exception ex)
                        {
                            // bugbug - need an error log.
                            Console.WriteLine(ex.Message + this._current.Context());
                        }
                        ch = this._current.Lastchar;
                        break;
                    default:
                        this._current.Error("Unexpected character '{0}'", ch);
                        break;
                }
            }
        }

        private void ParseMarkup()
        {
            char ch = this._current.ReadChar();
            if (ch != '!')
            {
                this._current.Error("Found '{0}', but expecing declaration starting with '<!'");
                return;
            }
            ch = this._current.ReadChar();
            if (ch == '-')
            {
                ch = this._current.ReadChar();
                if (ch != '-')
                {
                    this._current.Error("Expecting comment '<!--' but found {0}", ch);
                }

                this._current.ScanToEnd(this._sb, "Comment", "-->");
            }
            else if (ch == '[')
            {
                this.ParseMarkedSection();
            }
            else
            {
                string token = this._current.ScanToken(this._sb, _ws, true);
                switch (token)
                {
                    case "ENTITY":
                        this.ParseEntity();
                        break;
                    case "ELEMENT":
                        this.ParseElementDecl();
                        break;
                    case "ATTLIST":
                        this.ParseAttList();
                        break;
                    default:
                        this._current.Error("Invalid declaration '<!{0}'.  Expecting 'ENTITY', 'ELEMENT' or 'ATTLIST'.", token);
                        break;
                }
            }
        }

        private char ParseDeclComments()
        {
            char ch = this._current.Lastchar;
            while (ch == '-')
            {
                ch = this.ParseDeclComment(true);
            }
            return ch;
        }

        private char ParseDeclComment(bool full)
        {
            int start = this._current.Line;
            // -^-...--
            // This method scans over a comment inside a markup declaration.
            char ch = this._current.ReadChar();
            if (full && ch != '-')
            {
                this._current.Error("Expecting comment delimiter '--' but found {0}", ch);
            }

            this._current.ScanToEnd(this._sb, "Markup Comment", "--");
            return this._current.SkipWhitespace();
        }

        private void ParseMarkedSection()
        {
            // <![^ name [ ... ]]>
            this._current.ReadChar(); // move to next char.
            string name = this.ScanName("[");
            if (name == "INCLUDE")
            {
                ParseIncludeSection();
            }
            else if (name == "IGNORE")
            {
                this.ParseIgnoreSection();
            }
            else
            {
                this._current.Error("Unsupported marked section type '{0}'", name);
            }
        }

        private static void ParseIncludeSection()
        {
            throw new NotImplementedException("Include Section");
        }

        private void ParseIgnoreSection()
        {
            int start = this._current.Line;
            // <!-^-...-->
            char ch = this._current.SkipWhitespace();
            if (ch != '[')
            {
                this._current.Error("Expecting '[' but found {0}", ch);
            }

            this._current.ScanToEnd(this._sb, "Conditional Section", "]]>");
        }

        private string ScanName(string term)
        {
            // skip whitespace, scan name (which may be parameter entity reference
            // which is then expanded to a name)
            char ch = this._current.SkipWhitespace();
            if (ch == '%')
            {
                Entity e = this.ParseParameterEntity(term);
                ch = this._current.Lastchar;
                // bugbug - need to support external and nested parameter entities
                if (!e.Internal)
                {
                    throw new NotSupportedException("External parameter entity resolution");
                }

                return e.Literal.Trim();
            }
            else
            {
                return this._current.ScanToken(this._sb, term, true);
            }
        }

        private Entity ParseParameterEntity(string term)
        {
            // almost the same as _current.ScanToken, except we also terminate on ';'
            char ch = this._current.ReadChar();
            string name = this._current.ScanToken(this._sb, ";" + term, false);
            name = this._nt.Add(name);
            if (this._current.Lastchar == ';')
            {
                this._current.ReadChar();
            }

            Entity e = this.GetParameterEntity(name);
            return e;
        }

        private Entity GetParameterEntity(string name)
        {
            Entity e = (Entity)this._pentities[name];
            if (e == null)
            {
                this._current.Error("Reference to undefined parameter entity '{0}'", name);
            }

            return e;
        }

        private static readonly string _ws = " \r\n\t";

        private void ParseEntity()
        {
            char ch = this._current.SkipWhitespace();
            bool pe = ch == '%';
            if (pe)
            {
                // parameter entity.
                this._current.ReadChar(); // move to next char
                ch = this._current.SkipWhitespace();
            }
            string name = this._current.ScanToken(this._sb, _ws, true);
            name = this._nt.Add(name);
            ch = this._current.SkipWhitespace();
            Entity e = null;
            if (ch == '"' || ch == '\'')
            {
                string literal = this._current.ScanLiteral(this._sb, ch);
                e = new Entity(name, literal);
            }
            else
            {
                string pubid = null;
                string extid = null;
                string tok = this._current.ScanToken(this._sb, _ws, true);
                if (Entity.IsLiteralType(tok))
                {
                    ch = this._current.SkipWhitespace();
                    string literal = this._current.ScanLiteral(this._sb, ch);
                    e = new Entity(name, literal);
                    e.SetLiteralType(tok);
                }
                else
                {
                    extid = tok;
                    if (extid == "PUBLIC")
                    {
                        ch = this._current.SkipWhitespace();
                        if (ch == '"' || ch == '\'')
                        {
                            pubid = this._current.ScanLiteral(this._sb, ch);
                        }
                        else
                        {
                            this._current.Error("Expecting public identifier literal but found '{0}'", ch);
                        }
                    }
                    else if (extid != "SYSTEM")
                    {
                        this._current.Error("Invalid external identifier '{0}'.  Expecing 'PUBLIC' or 'SYSTEM'.", extid);
                    }
                    string uri = null;
                    ch = this._current.SkipWhitespace();
                    if (ch == '"' || ch == '\'')
                    {
                        uri = this._current.ScanLiteral(this._sb, ch);
                    }
                    else if (ch != '>')
                    {
                        this._current.Error("Expecting system identifier literal but found '{0}'", ch);
                    }
                    e = new Entity(name, pubid, uri, this._current.Proxy);
                }
            }
            ch = this._current.SkipWhitespace();
            if (ch == '-')
            {
                ch = this.ParseDeclComments();
            }

            if (ch != '>')
            {
                this._current.Error("Expecting end of entity declaration '>' but found '{0}'", ch);
            }
            if (pe)
            {
                this._pentities[e.Name] = e;
            }
            else
            {
                this._entities[e.Name] = e;
            }
        }

        private void ParseElementDecl()
        {
            char ch = this._current.SkipWhitespace();
            string[] names = this.ParseNameGroup(ch, true);
            bool sto = char.ToLower(this._current.SkipWhitespace()) == 'o'; // start tag optional?   
            this._current.ReadChar();
            bool eto = char.ToLower(this._current.SkipWhitespace()) == 'o'; // end tag optional? 
            this._current.ReadChar();
            ch = this._current.SkipWhitespace();
            ContentModel cm = this.ParseContentModel(ch);
            ch = this._current.SkipWhitespace();

            string[] exclusions = null;
            string[] inclusions = null;

            if (ch == '-')
            {
                ch = this._current.ReadChar();
                if (ch == '(')
                {
                    exclusions = this.ParseNameGroup(ch, true);
                    ch = this._current.SkipWhitespace();
                }
                else if (ch == '-')
                {
                    ch = this.ParseDeclComment(false);
                }
                else
                {
                    this._current.Error("Invalid syntax at '{0}'", ch);
                }
            }

            if (ch == '-')
            {
                ch = this.ParseDeclComments();
            }

            if (ch == '+')
            {
                ch = this._current.ReadChar();
                if (ch != '(')
                {
                    this._current.Error("Expecting inclusions name group", ch);
                }
                inclusions = this.ParseNameGroup(ch, true);
                ch = this._current.SkipWhitespace();
            }

            if (ch == '-')
            {
                ch = this.ParseDeclComments();
            }

            if (ch != '>')
            {
                this._current.Error("Expecting end of ELEMENT declaration '>' but found '{0}'", ch);
            }

            foreach (string name in names)
            {
                string atom = this._nt.Add(name);
                this._elements.Add(atom, new ElementDecl(atom, sto, eto, cm, inclusions, exclusions));
            }
        }

        private static readonly string _ngterm = " \r\n\t|,)";

        private string[] ParseNameGroup(char ch, bool nmtokens)
        {
            ArrayList names = new ArrayList();
            if (ch == '(')
            {
                ch = this._current.ReadChar();
                ch = this._current.SkipWhitespace();
                while (ch != ')')
                {
                    // skip whitespace, scan name (which may be parameter entity reference
                    // which is then expanded to a name)                    
                    ch = this._current.SkipWhitespace();
                    if (ch == '%')
                    {
                        Entity e = this.ParseParameterEntity(_ngterm);
                        this.PushEntity(this._current.ResolvedUri, e);
                        this.ParseNameList(names, nmtokens);
                        this.PopEntity();
                        ch = this._current.Lastchar;
                    }
                    else
                    {
                        string token = this._current.ScanToken(this._sb, _ngterm, nmtokens);
                        string atom = this._nt.Add(token);
                        names.Add(atom);
                    }
                    ch = this._current.SkipWhitespace();
                    if (ch == '|' || ch == ',')
                    {
                        ch = this._current.ReadChar();
                    }
                }
                this._current.ReadChar(); // consume ')'
            }
            else
            {
                string name = this._current.ScanToken(this._sb, _ws, nmtokens);
                name = this._nt.Add(name);
                names.Add(name);
            }
            return (string[])names.ToArray(typeof(string));
        }

        private void ParseNameList(ArrayList names, bool nmtokens)
        {
            char ch = this._current.Lastchar;
            ch = this._current.SkipWhitespace();
            while (ch != Entity.EOF)
            {
                string name;
                if (ch == '%')
                {
                    Entity e = this.ParseParameterEntity(_ngterm);
                    this.PushEntity(this._current.ResolvedUri, e);
                    this.ParseNameList(names, nmtokens);
                    this.PopEntity();
                    ch = this._current.Lastchar;
                }
                else
                {
                    name = this._current.ScanToken(this._sb, _ngterm, true);
                    name = this._nt.Add(name);
                    names.Add(name);
                }
                ch = this._current.SkipWhitespace();
                if (ch == '|')
                {
                    ch = this._current.ReadChar();
                    ch = this._current.SkipWhitespace();
                }
            }
        }

        private static readonly string _dcterm = " \r\n\t>";

        private ContentModel ParseContentModel(char ch)
        {
            ContentModel cm = new ContentModel();
            if (ch == '(')
            {
                this._current.ReadChar();
                this.ParseModel(')', cm);
                ch = this._current.ReadChar();
                if (ch == '?' || ch == '+' || ch == '*')
                {
                    cm.AddOccurrence(ch);
                    this._current.ReadChar();
                }
            }
            else if (ch == '%')
            {
                Entity e = this.ParseParameterEntity(_dcterm);
                this.PushEntity(this._current.ResolvedUri, e);
                cm = this.ParseContentModel(this._current.Lastchar);
                this.PopEntity(); // bugbug should be at EOF.
            }
            else
            {
                string dc = this.ScanName(_dcterm);
                cm.SetDeclaredContent(dc);
            }
            return cm;
        }

        private static readonly string _cmterm = " \r\n\t,&|()?+*";

        private void ParseModel(char cmt, ContentModel cm)
        {
            // Called when part of the model is made up of the contents of a parameter entity
            int depth = cm.CurrentDepth;
            char ch = this._current.Lastchar;
            ch = this._current.SkipWhitespace();
            while (ch != cmt || cm.CurrentDepth > depth) // the entity must terminate while inside the content model.
            {
                if (ch == Entity.EOF)
                {
                    this._current.Error("Content Model was not closed");
                }
                if (ch == '%')
                {
                    Entity e = this.ParseParameterEntity(_cmterm);
                    this.PushEntity(this._current.ResolvedUri, e);
                    this.ParseModel(Entity.EOF, cm);
                    this.PopEntity();
                    ch = this._current.SkipWhitespace();
                }
                else if (ch == '(')
                {
                    cm.PushGroup();
                    this._current.ReadChar();// consume '('
                    ch = this._current.SkipWhitespace();
                }
                else if (ch == ')')
                {
                    ch = this._current.ReadChar();// consume ')'
                    if (ch == '*' || ch == '+' || ch == '?')
                    {
                        cm.AddOccurrence(ch);
                        ch = this._current.ReadChar();
                    }
                    if (cm.PopGroup() < depth)
                    {
                        this._current.Error("Parameter entity cannot close a paren outside it's own scope");
                    }
                    ch = this._current.SkipWhitespace();
                }
                else if (ch == ',' || ch == '|' || ch == '&')
                {
                    cm.AddConnector(ch);
                    this._current.ReadChar(); // skip connector
                    ch = this._current.SkipWhitespace();
                }
                else
                {
                    string token;
                    if (ch == '#')
                    {
                        ch = this._current.ReadChar();
                        token = "#" + this._current.ScanToken(this._sb, _cmterm, true); // since '#' is not a valid name character.
                    }
                    else
                    {
                        token = this._current.ScanToken(this._sb, _cmterm, true);
                    }
                    token = this._nt.Add(token);// atomize it.
                    ch = this._current.Lastchar;
                    if (ch == '?' || ch == '+' || ch == '*')
                    {
                        cm.PushGroup();
                        cm.AddSymbol(token);
                        cm.AddOccurrence(ch);
                        cm.PopGroup();
                        this._current.ReadChar(); // skip connector
                        ch = this._current.SkipWhitespace();
                    }
                    else
                    {
                        cm.AddSymbol(token);
                        ch = this._current.SkipWhitespace();
                    }
                }
            }
        }

        private void ParseAttList()
        {
            char ch = this._current.SkipWhitespace();
            string[] names = this.ParseNameGroup(ch, true);
            AttList attlist = new AttList(this._ignoreCase);
            this.ParseAttList(attlist, '>');
            foreach (string name in names)
            {
                ElementDecl e = (ElementDecl)this._elements[name];
                if (e == null)
                {
                    this._current.Error("ATTLIST references undefined ELEMENT {0}", name);
                }
                e.AddAttDefs(attlist);
            }
        }

        private static readonly string _peterm = " \t\r\n>";

        private void ParseAttList(AttList list, char term)
        {
            char ch = this._current.SkipWhitespace();
            while (ch != term)
            {
                if (ch == '%')
                {
                    Entity e = this.ParseParameterEntity(_peterm);
                    this.PushEntity(this._current.ResolvedUri, e);
                    this.ParseAttList(list, Entity.EOF);
                    this.PopEntity();
                    ch = this._current.SkipWhitespace();
                }
                else if (ch == '-')
                {
                    ch = this.ParseDeclComments();
                }
                else
                {
                    AttDef a = this.ParseAttDef(ch);
                    list.Add(a);
                }
                ch = this._current.SkipWhitespace();
            }
        }

        private AttDef ParseAttDef(char ch)
        {
            ch = this._current.SkipWhitespace();
            string name = this._nt.Add(this.ScanName(_ws));
            AttDef attdef = new AttDef(name);

            ch = this._current.SkipWhitespace();
            if (ch == '-')
            {
                ch = this.ParseDeclComments();
            }

            this.ParseAttType(ch, attdef);

            ch = this._current.SkipWhitespace();
            if (ch == '-')
            {
                ch = this.ParseDeclComments();
            }

            this.ParseAttDefault(ch, attdef);

            ch = this._current.SkipWhitespace();
            if (ch == '-')
            {
                ch = this.ParseDeclComments();
            }

            return attdef;

        }

        private void ParseAttType(char ch, AttDef attdef)
        {
            if (ch == '%')
            {
                Entity e = this.ParseParameterEntity(_ws);
                this.PushEntity(this._current.ResolvedUri, e);
                this.ParseAttType(this._current.Lastchar, attdef);
                this.PopEntity(); // bugbug - are we at the end of the entity?
                ch = this._current.Lastchar;
                return;
            }

            if (ch == '(')
            {
                attdef.EnumValues = this.ParseNameGroup(ch, false);
                attdef.Type = AttributeType.ENUMERATION;
            }
            else
            {
                string token = this.ScanName(_ws);
                if (token == "NOTATION")
                {
                    ch = this._current.SkipWhitespace();
                    if (ch != '(')
                    {
                        this._current.Error("Expecting name group '(', but found '{0}'", ch);
                    }
                    attdef.Type = AttributeType.NOTATION;
                    attdef.EnumValues = this.ParseNameGroup(ch, true);
                }
                else
                {
                    attdef.SetType(token);
                }
            }
        }

        private void ParseAttDefault(char ch, AttDef attdef)
        {
            if (ch == '%')
            {
                Entity e = this.ParseParameterEntity(_ws);
                this.PushEntity(this._current.ResolvedUri, e);
                this.ParseAttDefault(this._current.Lastchar, attdef);
                this.PopEntity(); // bugbug - are we at the end of the entity?
                ch = this._current.Lastchar;
                return;
            }

            bool hasdef = true;
            if (ch == '#')
            {
                this._current.ReadChar();
                string token = this._current.ScanToken(this._sb, _ws, true);
                hasdef = attdef.SetPresence(token);
                ch = this._current.SkipWhitespace();
            }
            if (hasdef)
            {
                if (ch == '\'' || ch == '"')
                {
                    string lit = this._current.ScanLiteral(this._sb, ch);
                    attdef.Default = lit;
                    ch = this._current.SkipWhitespace();
                }
                else
                {
                    string name = this._current.ScanToken(this._sb, _ws, false);
                    name = this._nt.Add(name);
                    attdef.Default = name; // bugbug - must be one of the enumerated names.
                    ch = this._current.SkipWhitespace();
                }
            }
        }
    }
}
