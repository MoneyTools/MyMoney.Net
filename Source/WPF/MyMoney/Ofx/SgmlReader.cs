/*
* 
* An XmlReader implementation for loading HTML as if it was XHTML.
*
* Copyright (c) 2002 Microsoft Corporation. All rights reserved.
*
* Chris Lovett
* 
*/

using System;
using System.IO;
using System.Text;
using System.Xml;

namespace Walkabout.Sgml
{
    internal class Attribute
    {
        public string Name;
        public string LocalName;
        public string Prefix;
        public AttDef DtdType;
        public char QuoteChar;
        public string NamespaceURI { get; set; }

        public const string XmlnsUri = "http://www.w3.org/2000/xmlns/";

        public Attribute(string name, string value, char quote)
        {
            this.Name = name;
            this.LocalName = name;
            this.ParseXmlns(name);

            this._value = value;
            this.QuoteChar = quote;
        }

        private void ParseXmlns(string name)
        {
            this.LocalName = name;
            this.Prefix = "";
            this.NamespaceURI = "";
            if (name == "xmlns")
            {
                this.LocalName = "xmlns";
                this.NamespaceURI = XmlnsUri;
            }
            else if (name.StartsWith("xmlns:"))
            {
                this.Prefix = "xmlns";
                this.LocalName = name.Substring(6);
                this.NamespaceURI = XmlnsUri;
            }
            else
            {
                int i = name.IndexOf(':');
                if (i > 0)
                {
                    this.Prefix = name.Substring(0, i);
                    this.LocalName = name.Substring(i + 1);
                }
            }
        }

        public void Reset(string name, string value, char quote)
        {
            this.Name = name;
            this._value = value;
            this.QuoteChar = quote;
            this.DtdType = null;
            this.ParseXmlns(name);
        }

        public string Value
        {
            get
            {
                if (this._value != null)
                {
                    return this._value;
                }

                if (this.DtdType != null)
                {
                    return this.DtdType.Default;
                }

                return null;
            }
            set
            {
                this._value = value;
            }
        }

        public bool IsDefault
        {
            get
            {
                return this._value == null;
            }
        }

        private string _value;

    }

    internal class Node
    {
        public XmlNodeType NodeType;
        public string Value;
        public XmlSpace Space;
        public string XmlLang;
        public bool IsEmpty;
        public string Name;
        public string LocalName;
        public string Prefix;
        public string NamespaceURI;
        public ElementDecl DtdType;
        public State CurrentState;
        private Attribute[] _attributes;
        private int _attsize;
        private int _attcount;

        public Node(string name, XmlNodeType nt, string value)
        {
            this.Name = name;
            this.NodeType = nt;
            this.Value = value;
            this.IsEmpty = true;
            this.ParseNamespacePrefix(name);
        }

        private void ParseNamespacePrefix(string name)
        {
            this.Prefix = "";
            this.LocalName = "";
            this.NamespaceURI = "";

            if (name != null)
            {
                int i = name.IndexOf(':');
                if (i > 0)
                {
                    this.Prefix = name.Substring(0, i);
                    this.LocalName = name.Substring(i + 1);
                }
                else
                {
                    this.Prefix = "";
                    this.LocalName = name;
                }
            }
        }

        public void Reset(string name, XmlNodeType nt, string value)
        {
            this.Value = value;
            this.Name = name;
            this.NodeType = nt;
            this.Space = XmlSpace.None;
            this.XmlLang = null;
            this.IsEmpty = true;
            this._attcount = 0;
            this.DtdType = null;
            this.ParseNamespacePrefix(name);

        }
        public Attribute AddAttribute(string name, string value, char quotechar, bool ignoreCase)
        {
            if (this._attcount == this._attsize)
            {
                int newsize = this._attsize + 10;
                Attribute[] newarray = new Attribute[newsize];
                if (this._attributes != null)
                {
                    Array.Copy(this._attributes, newarray, this._attsize);
                }

                this._attsize = newsize;
                this._attributes = newarray;
            }
            for (int i = 0; i < this._attcount; i++)
            {
                if ((ignoreCase && string.Compare(this._attributes[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    || this._attributes[i].Name == (object)name)
                {
                    return null; // ignore duplicates!
                }
            }
            Attribute a = this._attributes[this._attcount];
            if (a == null)
            {
                a = new Attribute(name, value, quotechar);
                this._attributes[this._attcount] = a;
            }
            else
            {
                a.Reset(name, value, quotechar);
            }
            this._attcount++;
            return a;
        }

        public void RemoveAttribute(string name)
        {
            for (int i = 0; i < this._attcount; i++)
            {
                if (this._attributes[i].Name == name)
                {
                    this._attributes[i] = null;
                    Array.Copy(this._attributes, i + 1, this._attributes, i, this._attcount - i - 1);
                    this._attcount--;
                    return;
                }
            }
        }
        public void CopyAttributes(Node n, bool ignoreCase)
        {
            for (int i = 0; i < n._attcount; i++)
            {
                Attribute a = n._attributes[i];
                Attribute na = this.AddAttribute(a.Name, a.Value, a.QuoteChar, ignoreCase);
                na.DtdType = a.DtdType;
            }
        }

        public int AttributeCount
        {
            get
            {
                return this._attcount;
            }
        }

        public int GetAttribute(string name)
        {
            if (this._attcount > 0)
            {
                for (int i = 0; i < this._attcount; i++)
                {
                    Attribute a = this._attributes[i];
                    if (a.Name == name)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        public Attribute GetAttribute(int i)
        {
            if (i >= 0 && i < this._attcount)
            {
                Attribute a = this._attributes[i];
                return a;
            }
            return null;
        }
    }

    internal enum State
    {
        Initial,
        Markup,
        EndTag,
        Attr,
        AttrValue,
        Text,
        PartialTag,
        AutoClose,
        AutoCloseTextOnly,
        CData,
        PartialText,
        Eof
    }


    /// <summary>
    /// SgmlReader is an XmlReader API over any SGML document (including built in support for HTML).  
    /// </summary>
    public class SgmlReader : XmlReader
    {
        private SgmlDtd _dtd;
        private Entity _current;
        private State _state;
        private readonly XmlNameTable _nametable;
        private readonly XmlNamespaceManager _mgr;
        private char _partial;
        private object _endTag;
        private Node[] _stack;
        private int _depth;
        private int _size;
        private Node _node; // current node (except for attributes)
                            // Attributes are handled separately using these members.

        private Attribute _a;
        private int _apos; // which attribute are we positioned on in the collection.

        private Uri _baseUri;
        private StringBuilder _sb;
        private StringBuilder _name;
        private TextWriter _log;

        // autoclose support
        private Node _newnode;
        private int _poptodepth;
        private int _rootCount;
        private string _href;
        private string _ErrorLogFile;
        private Entity _lastError;
        private string _proxy;
        private TextReader _inputStream;
        private string _syslit;
        private string _pubid;
        private string _subset;
        private string _docType;
        private WhitespaceHandling _whitespaceHandling;

        public SgmlReader()
        {
            this._nametable = new NameTable();
            this._mgr = new XmlNamespaceManager(this._nametable);
            this.Init();
        }

        public bool IgnoreCase { get; set; }

        /// <summary>
        /// Specify the SgmlDtd object directly.  This allows you to cache the Dtd and share
        /// it across multipl SgmlReaders.  To load a DTD from a URL use the SystemLiteral property.
        /// </summary>
        public SgmlDtd Dtd
        {
            get
            {
                this.LazyLoadDtd(this._baseUri);
                return this._dtd;
            }
            set { this._dtd = value; }
        }

        private void LazyLoadDtd(Uri baseUri)
        {
            if (this._dtd == null)
            {
                if (this._syslit == null || this._syslit == string.Empty)
                {

                }
                else
                {
                    if (this._syslit.IndexOf("://") > 0)
                    {
                        baseUri = new Uri(this._syslit);
                    }
                    else
                    {
                        // probably a local filename.
                        baseUri = new Uri("file://" + this._syslit.Replace("\\", "/"));
                    }
                    this._dtd = SgmlDtd.Parse(baseUri, this._docType, this._pubid, this._syslit, this._subset, this._proxy, this._nametable);
                }
            }
        }

        /// <summary>
        /// The name of root element specified in the DOCTYPE tag.
        /// </summary>
        public string DocType
        {
            get { return this._docType; }
            set { this._docType = value; }
        }

        /// <summary>
        /// The PUBLIC identifier in the DOCTYPE tag
        /// </summary>
        public string PublicIdentifier
        {
            get { return this._pubid; }
            set { this._pubid = value; }
        }

        /// <summary>
        /// The SYSTEM literal in the DOCTYPE tag identifying the location of the DTD.
        /// </summary>
        public string SystemLiteral
        {
            get { return this._syslit; }
            set { this._syslit = value; }
        }

        /// <summary>
        /// The DTD internal subset in the DOCTYPE tag
        /// </summary>
        public string InternalSubset
        {
            get { return this._subset; }
            set { this._subset = value; }
        }

        /// <summary>
        /// The input stream containing SGML data to parse.
        /// You must specify this property or the Href property before calling Read().
        /// </summary>
        public TextReader InputStream
        {
            get { return this._inputStream; }
            set { this._inputStream = value; this.Init(); }
        }

        /// <summary>
        /// Sometimes you need to specify a proxy server in order to load data via HTTP
        /// from outside the firewall.  For example: "itgproxy:80".
        /// </summary>
        public string WebProxy
        {
            get { return this._proxy; }
            set { this._proxy = value; }
        }

        /// <summary>
        /// The base Uri is used to resolve relative Uri's like the SystemLiteral and
        /// Href properties.  This is a method because BaseURI is a read-only
        /// property on the base XmlReader class.
        /// </summary>
        public void SetBaseUri(string uri)
        {
            this._baseUri = new Uri(uri);
        }

        /// <summary>
        /// Specify the location of the input SGML document as a URL.
        /// </summary>
        public string Href
        {
            get { return this._href; }
            set
            {
                this._href = value;
                this.Init();
                if (this._baseUri == null)
                {
                    if (this._href.IndexOf("://") > 0)
                    {
                        this._baseUri = new Uri(this._href);
                    }
                    else
                    {
                        this._baseUri = new Uri("file:///" + Directory.GetCurrentDirectory() + "//");
                    }
                }
            }
        }

        /// <summary>
        /// DTD validation errors are written to this stream.
        /// </summary>
        public TextWriter ErrorLog
        {
            get { return this._log; }
            set { this._log = value; }
        }

        /// <summary>
        /// DTD validation errors are written to this log file.
        /// </summary>
        public string ErrorLogFile
        {
            get { return this._ErrorLogFile; }
            set
            {
                this._ErrorLogFile = value;
                this.ErrorLog = new StreamWriter(value);
            }
        }

        private void Log(string msg, params string[] args)
        {
            if (this.ErrorLog != null)
            {
                string err = string.Format(msg, args);
                if (this._lastError != this._current)
                {
                    err = err + "    " + this._current.Context();
                    this._lastError = this._current;
                    this.ErrorLog.WriteLine("### Error:" + err);
                }
                else
                {
                    string path = string.Empty;
                    if (this._current.ResolvedUri != null)
                    {
                        path = this._current.ResolvedUri.AbsolutePath;
                    }
                    this.ErrorLog.WriteLine("### Error in " +
                        path + "#" +
                        this._current.Name +
                        ", line " + this._current.Line + ", position " + this._current.LinePosition + ": " +
                        err);
                }
            }
        }

        private void Log(string msg, char ch)
        {
            this.Log(msg, ch.ToString());
        }

        private void Init()
        {
            this._state = State.Initial;
            this._stack = new Node[10];
            this._size = 10;
            this._depth = 0;
            this._node = this.Push(null, XmlNodeType.Document, null);
            this._node.IsEmpty = false;
            this._sb = new StringBuilder();
            this._name = new StringBuilder();
            this._poptodepth = 0;
            this._current = null;
            this._partial = '\0';
            this._endTag = null;
            this._a = null;
            this._apos = 0;
            this._newnode = null;
            this._poptodepth = 0;
            this._rootCount = 0;
        }

        private void Grow()
        {
            int inc = 10;
            int newsize = this._size + inc;
            Node[] narray = new Node[newsize];
            Array.Copy(this._stack, narray, this._size);
            this._size = newsize;
            this._stack = narray;
        }

        private Node Push(string name, XmlNodeType nt, string value)
        {
            if (this._depth == this._size)
            {
                this.Grow();
            }

            Node result;
            if (this._stack[this._depth] == null)
            {
                result = new Node(name, nt, value);
                this._stack[this._depth] = result;
            }
            else
            {
                result = this._stack[this._depth];
                result.Reset(name, nt, value);
            }
            this._depth++;
            this._node = result;
            this._mgr.PushScope();
            return result;
        }

        private Node Push(Node n)
        {
            // we have to do a deep clone of the Node object because
            // it is reused in the stack.
            Node n2 = this.Push(n.Name, n.NodeType, n.Value);
            n2.DtdType = n.DtdType;
            n2.IsEmpty = n.IsEmpty;
            n2.Space = n.Space;
            n2.XmlLang = n.XmlLang;
            n2.CurrentState = n.CurrentState;
            n2.CopyAttributes(n, this.IgnoreCase);
            this._node = n2;
            return n2;
        }

        private void Pop()
        {
            if (this._depth > 1)
            {
                this._depth--;
                this._node = this._stack[this._depth - 1];
                this._mgr.PopScope();
            }
        }

        public override XmlNodeType NodeType
        {
            get
            {
                if (this._state == State.Attr)
                {
                    return XmlNodeType.Attribute;
                }
                else if (this._state == State.AttrValue)
                {
                    return XmlNodeType.Text;
                }
                else if (this._state == State.EndTag || this._state == State.AutoClose || this._state == State.AutoCloseTextOnly)
                {
                    return XmlNodeType.EndElement;
                }
                return this._node.NodeType;
            }
        }

        public override string Name
        {
            get
            {
                string result = null;
                if (this._state == State.Attr)
                {
                    result = this._a.Name;
                }
                else if (this._state == State.AttrValue)
                {
                    result = null;
                }
                else
                {
                    result = this._node.Name;
                }

                return result;
            }
        }

        public override string LocalName
        {
            get
            {
                string result = null;
                if (this._state == State.Attr)
                {
                    result = this._a.LocalName;
                }
                else if (this._state == State.AttrValue)
                {
                    result = null;
                }
                else
                {
                    result = this._node.LocalName;
                }

                return result;
            }
        }

        public override string NamespaceURI
        {
            get
            {
                string result = string.Empty;
                if (this._state == State.Attr)
                {
                    result = this._a.NamespaceURI;
                }
                else
                {
                    result = this._node.NamespaceURI;
                }
                return result;
            }
        }

        public override string Prefix
        {
            get
            {
                string result = string.Empty;
                if (this._state == State.Attr)
                {
                    result = this._a.Prefix;
                }
                else
                {
                    result = this._node.Prefix;
                }
                return result;
            }
        }

        public override bool HasValue
        {
            get
            {
                if (this._state == State.Attr || this._state == State.AttrValue)
                {
                    return true;
                }
                return this._node.Value != null;
            }
        }

        public override string Value
        {
            get
            {
                if (this._state == State.Attr || this._state == State.AttrValue)
                {
                    return this._a.Value;
                }
                return this._node.Value;
            }
        }

        public override int Depth
        {
            get
            {
                if (this._state == State.Attr)
                {
                    return this._depth;
                }
                else if (this._state == State.AttrValue)
                {
                    return this._depth + 1;
                }
                return this._depth - 1;
            }
        }

        public override string BaseURI
        {
            get
            {
                return this._baseUri == null ? string.Empty : this._baseUri.AbsoluteUri;
            }
        }

        public override bool IsEmptyElement
        {
            get
            {
                if (this._state == State.Markup || this._state == State.Attr || this._state == State.AttrValue)
                {
                    return this._node.IsEmpty;
                }
                return false;
            }
        }
        public override bool IsDefault
        {
            get
            {
                if (this._state == State.Attr || this._state == State.AttrValue)
                {
                    return this._a.IsDefault;
                }

                return false;
            }
        }
        public override char QuoteChar
        {
            get
            {
                if (this._a != null)
                {
                    return this._a.QuoteChar;
                }

                return '\0';
            }
        }

        public override XmlSpace XmlSpace
        {
            get
            {
                for (int i = this._depth - 1; i > 1; i--)
                {
                    XmlSpace xs = this._stack[i].Space;
                    if (xs != XmlSpace.None)
                    {
                        return xs;
                    }
                }
                return XmlSpace.None;
            }
        }

        public override string XmlLang
        {
            get
            {
                for (int i = this._depth - 1; i > 1; i--)
                {
                    string xmllang = this._stack[i].XmlLang;
                    if (xmllang != null)
                    {
                        return xmllang;
                    }
                }
                return string.Empty;
            }
        }

        public WhitespaceHandling WhitespaceHandling
        {
            get
            {
                return this._whitespaceHandling;
            }
            set
            {
                this._whitespaceHandling = value;
            }
        }

        public override int AttributeCount
        {
            get
            {
                if (this._state == State.Attr || this._state == State.AttrValue)
                {
                    return 0;
                }

                if (this._node.NodeType == XmlNodeType.Element ||
                    this._node.NodeType == XmlNodeType.DocumentType)
                {
                    return this._node.AttributeCount;
                }

                return 0;
            }
        }

        public override string GetAttribute(string name)
        {
            if (this._state != State.Attr && this._state != State.AttrValue)
            {
                int i = this._node.GetAttribute(name);
                if (i >= 0)
                {
                    return this.GetAttribute(i);
                }
            }
            return null;
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            return this.GetAttribute(name); // SGML has no namespaces.
        }

        public override string GetAttribute(int i)
        {
            if (this._state != State.Attr && this._state != State.AttrValue)
            {
                Attribute a = this._node.GetAttribute(i);
                if (a != null)
                {
                    return a.Value;
                }
            }
            throw new IndexOutOfRangeException();
        }

        public override string this[int i]
        {
            get
            {
                return this.GetAttribute(i);
            }
        }

        public override string this[string name]
        {
            get
            {
                return this.GetAttribute(name);
            }
        }

        public override string this[string name, string namespaceURI]
        {
            get
            {
                return this.GetAttribute(name, namespaceURI);
            }
        }

        public override bool MoveToAttribute(string name)
        {
            int i = this._node.GetAttribute(name);
            if (i >= 0)
            {
                this.MoveToAttribute(i);
                return true;
            }
            return false;
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            return this.MoveToAttribute(name);
        }

        public override void MoveToAttribute(int i)
        {
            Attribute a = this._node.GetAttribute(i);
            if (a != null)
            {
                this._apos = i;
                this._a = a;
                this._node.CurrentState = this._state;//save current state.
                this._state = State.Attr;
                return;
            }
            throw new IndexOutOfRangeException();
        }

        public override bool MoveToFirstAttribute()
        {
            if (this._node.AttributeCount > 0)
            {
                this.MoveToAttribute(0);
                return true;
            }
            return false;
        }

        public override bool MoveToNextAttribute()
        {
            if (this._state != State.Attr && this._state != State.AttrValue)
            {
                return this.MoveToFirstAttribute();
            }
            if (this._apos < this._node.AttributeCount - 1)
            {
                this.MoveToAttribute(this._apos + 1);
                return true;
            }
            return false;
        }

        public override bool MoveToElement()
        {
            if (this._state == State.Attr || this._state == State.AttrValue)
            {
                this._state = this._node.CurrentState;
                this._a = null;
                return true;
            }
            return this._node.NodeType == XmlNodeType.Element;
        }

        private bool IsTextOnly
        {
            get
            {
                if (this._node != null)
                {
                    ElementDecl e = this._node.DtdType;
                    if (e != null)
                    {
                        ContentModel m = e.ContentModel;
                        if (m != null)
                        {
                            Group g = m.Model;
                            if (g != null)
                            {
                                return g.TextOnly;
                            }
                        }
                    }
                }
                return false;
            }
        }


        public override bool Read()
        {
            if (this._current == null)
            {
                this.LazyLoadDtd(this._baseUri);

                if (this.Href != null)
                {
                    this._current = new Entity("#document", null, this._href, this._proxy);
                }
                else if (this._inputStream != null)
                {
                    this._current = new Entity("#document", null, this._inputStream, this._proxy);
                }
                else
                {
                    throw new InvalidOperationException("You must specify input either via Href or InputStream properties");
                }
                this._current.Open(null, this._baseUri);
                this._baseUri = this._current.ResolvedUri;
            }

            bool foundnode = false;
            while (!foundnode)
            {
                switch (this._state)
                {
                    case State.Initial:
                        this._state = State.Markup;
                        this._current.ReadChar();
                        goto case State.Markup;
                    case State.Eof:
                        if (this._current.Parent != null)
                        {
                            this._current.Close();
                            this._current = this._current.Parent;
                        }
                        else
                        {
                            return false;
                        }
                        break;
                    case State.EndTag:
                        if (this._endTag == (object)this._node.Name)
                        {
                            this.Pop(); // we're done!
                            this._state = State.Markup;
                            goto case State.Markup;
                        }
                        this.Pop(); // close one element
                        foundnode = true;// return another end element.
                        break;
                    case State.Markup:
                        if (this._node.IsEmpty)
                        {
                            this.Pop();
                        }
                        foundnode = this.ParseMarkup();
                        break;
                    case State.PartialTag:
                        this.Pop(); // remove text node.
                        this._state = State.Markup;
                        if (this._partial != '/' && this.IsTextOnly)
                        {
                            this._state = State.AutoCloseTextOnly;
                            foundnode = true;
                        }
                        else
                        {
                            foundnode = this.ParseTag(this._partial);
                        }
                        break;
                    case State.AutoCloseTextOnly:
                        this.Pop(); // remove start tag.
                        this._state = State.Markup;
                        foundnode = this.ParseTag(this._partial);
                        break;
                    case State.AutoClose:
                        this.Pop(); // close next node.
                        if (this._depth <= this._poptodepth)
                        {
                            this._state = State.Markup;
                            var n = this.Push(this._newnode); // now we're ready to start the new node.
                            n.NamespaceURI = this._mgr.LookupNamespace(this._newnode.Prefix);
                            this._state = State.Markup;
                        }
                        foundnode = true;
                        break;
                    case State.CData:
                        foundnode = this.ParseCData();
                        break;
                    case State.Attr:
                        goto case State.AttrValue;
                    case State.AttrValue:
                        this._state = State.Markup;
                        goto case State.Markup;
                    case State.Text:
                        this.Pop();
                        goto case State.Markup;
                    case State.PartialText:
                        if (this.ParseText(this._current.Lastchar, false))
                        {
                            this._node.NodeType = XmlNodeType.Whitespace;
                        }
                        foundnode = true;
                        break;
                }
                if (foundnode && this._node.NodeType == XmlNodeType.Whitespace && this._whitespaceHandling == WhitespaceHandling.None)
                {
                    // strip out whitespace (caller is probably pretty printing the XML).
                    foundnode = false;
                }
            }
            return true;
        }

        private bool ParseMarkup()
        {
            char ch = this._current.Lastchar;
            if (ch == '<')
            {
                ch = this._current.ReadChar();
                return this.ParseTag(ch);
            }
            else if (ch != Entity.EOF)
            {
                if (this._node.DtdType != null && this._node.DtdType.ContentModel.DeclaredContent == DeclaredContent.CDATA)
                {
                    // e.g. SCRIPT or STYLE tags which contain unparsed character data.
                    this._partial = '\0';
                    this._state = State.CData;
                    return false;
                }
                else if (this.ParseText(ch, true))
                {
                    this._node.NodeType = XmlNodeType.Whitespace;
                }
                return true;
            }
            this._state = State.Eof;
            return false;
        }

        private static readonly string _declterm = " \t\r\n>";

        private bool ParseTag(char ch)
        {
            if (ch == '!')
            {
                ch = this._current.ReadChar();
                if (ch == '-')
                {
                    return this.ParseComment();
                }
                else if (ch != '_' && !char.IsLetter(ch))
                {
                    // perhaps it's one of those nasty office document hacks like '<![if ! ie ]>'
                    string value = this._current.ScanToEnd(this._sb, "Recovering", ">"); // skip it
                    this.Log("Ignoring invalid markup '<!" + value + ">");
                    return false;
                }
                else
                {
                    string name = this._current.ScanToken(this._sb, _declterm, false);
                    if (name == "DOCTYPE")
                    {
                        this.ParseDocType();
                        // In SGML DOCTYPE SYSTEM attribute is optional, but in XML it is required,
                        // therefore if there is a PUBLIC identifier, but no SYSTEM literal then
                        // remove the PUBLIC identifier.
                        if (this.GetAttribute("SYSTEM") == null && this.GetAttribute("PUBLIC") != null)
                        {
                            this._node.RemoveAttribute("PUBLIC");
                        }
                        this._node.NodeType = XmlNodeType.DocumentType;
                        return true;
                    }
                    else
                    {
                        this.Log("Invalid declaration '<!{0}...'.  Expecting '<!DOCTYPE' only.", name);
                        this._current.ScanToEnd(null, "Recovering", ">"); // skip it
                        return false;
                    }
                }
            }
            else if (ch == '?')
            {
                this._current.ReadChar();// consume the '?' character.
                this.ParsePI();
            }
            else if (ch == '/')
            {
                return this.ParseEndTag();
            }
            else
            {
                return this.ParseStartTag(ch);
            }
            return true;
        }

        private static readonly string _tagterm = " \t\r\n/>";
        private static readonly string _aterm = " \t\r\n=/>";
        private static readonly string _avterm = " \t\r\n>";

        private bool ParseStartTag(char ch)
        {
            if (_tagterm.IndexOf(ch) >= 0)
            {
                this._sb.Length = 0;
                this._sb.Append('<');
                this._state = State.PartialText;
                return false;
            }
            string name = this._current.ScanToken(this._sb, _tagterm, false);
            name = this._nametable.Add(name);
            Node n = this.Push(name, XmlNodeType.Element, null);
            n.IsEmpty = false;

            this.Validate(n);
            ch = this._current.SkipWhitespace();
            while (ch != Entity.EOF && ch != '>')
            {
                if (ch == '/')
                {
                    n.IsEmpty = true;
                    ch = this._current.ReadChar();
                    if (ch != '>')
                    {
                        this.Log("Expected empty start tag '/>' sequence instead of '{0}'", ch);
                        this._current.ScanToEnd(null, "Recovering", ">");
                        return false;
                    }
                    break;
                }
                else if (ch == '<')
                {
                    this.Log("Start tag '{0}' is missing '>'", name);
                    break;
                }
                string aname = this._current.ScanToken(this._sb, _aterm, false);
                ch = this._current.SkipWhitespace();
                string value = null;
                char quote = '\0';
                if (ch == '=')
                {
                    this._current.ReadChar();
                    ch = this._current.SkipWhitespace();
                    if (ch == '\'' || ch == '\"')
                    {
                        quote = ch;
                        value = this.ScanLiteral(this._sb, ch);
                    }
                    else if (ch != '>')
                    {
                        string term = _avterm;
                        value = this._current.ScanToken(this._sb, term, false);
                    }
                }
                aname = this._nametable.Add(aname);
                Attribute a = n.AddAttribute(aname, value, quote, this.IgnoreCase);
                if (a == null)
                {
                    this.Log("Duplicate attribute '{0}' ignored", aname);
                }
                else
                {
                    ValidateAttribute(n, a);
                }
                if (a.NamespaceURI == Attribute.XmlnsUri)
                {
                    string prefix = a.LocalName;
                    if (prefix == "xmlns")
                    {
                        prefix = "";
                    }
                    this._mgr.AddNamespace(prefix, a.Value);
                }
                ch = this._current.SkipWhitespace();
            }


            n.NamespaceURI = this._mgr.LookupNamespace(n.Prefix);

            if (ch == Entity.EOF)
            {
                this._current.Error("Unexpected EOF parsing start tag '{0}'", name);
            }
            else if (ch == '>')
            {
                this._current.ReadChar(); // consume '>'
            }
            if (this.Depth == 1)
            {
                if (this._rootCount == 1)
                {
                    // Hmmm, we found another root level tag, soooo, the only
                    // thing we can do to keep this a valid XML document is stop
                    this._state = State.Eof;
                    return false;
                }
                this._rootCount++;
            }
            this.ValidateContent(n);
            return true;
        }

        private bool ParseEndTag()
        {
            this._state = State.EndTag;
            this._current.ReadChar(); // consume '/' char.
            string name = this._current.ScanToken(this._sb, _tagterm, false);
            char ch = this._current.SkipWhitespace();
            if (ch != '>')
            {
                this.Log("Expected empty start tag '/>' sequence instead of '{0}'", ch);
                this._current.ScanToEnd(null, "Recovering", ">");
            }
            this._current.ReadChar(); // consume '>'

            // Make sure there's a matching start tag for it.
            this._endTag = this._nametable.Add(name);
            this._node = this._stack[this._depth - 1];
            for (int i = this._depth - 1; i > 0; i--)
            {
                if ((object)this._stack[i].Name == this._endTag)
                {
                    return true;
                }
            }
            this.Log("No matching start tag for '</{0}>'", name);
            this._state = State.Markup;
            return false;
        }

        private bool ParseComment()
        {
            char ch = this._current.ReadChar();
            if (ch != '-')
            {
                this.Log("Expecting comment '<!--' but found {0}", ch);
                this._current.ScanToEnd(null, "Comment", ">");
                return false;
            }
            string value = this._current.ScanToEnd(this._sb, "Comment", "-->");

            // Make sure it's a valid comment!
            int i = value.IndexOf("--");
            while (i >= 0)
            {
                int j = i + 2;
                while (j < value.Length && value[j] == '-')
                {
                    j++;
                }

                if (i > 0)
                {
                    value = value.Substring(0, i - 1) + "-" + value.Substring(j);
                }
                else
                {
                    value = "-" + value.Substring(j);
                }
                i = value.IndexOf("--");
            }
            if (value.Length > 0 && value[value.Length - 1] == '-')
            {
                value += " "; // '-' cannot be last character
            }
            this.Push(null, XmlNodeType.Comment, value);
            return true;
        }

        private static readonly string _dtterm = " \t\r\n>";

        private void ParseDocType()
        {
            char ch = this._current.SkipWhitespace();
            string name = this._current.ScanToken(this._sb, _dtterm, false);
            name = this._nametable.Add(name);
            this.Push(name, XmlNodeType.DocumentType, null);
            ch = this._current.SkipWhitespace();
            if (ch != '>')
            {
                string subset = string.Empty;
                string pubid = string.Empty;
                string syslit = string.Empty;

                if (ch != '[')
                {
                    string token = this._current.ScanToken(this._sb, _dtterm, false);
                    token = this._nametable.Add(token.ToUpper());
                    if (token == "PUBLIC")
                    {
                        ch = this._current.SkipWhitespace();
                        if (ch == '\"' || ch == '\'')
                        {
                            pubid = this._current.ScanLiteral(this._sb, ch);
                            this._node.AddAttribute(token, pubid, ch, this.IgnoreCase);
                        }
                    }
                    else if (token != "SYSTEM")
                    {
                        this.Log("Unexpected token in DOCTYPE '{0}'", token);
                        this._current.ScanToEnd(null, "DOCTYPE", ">");
                    }
                    ch = this._current.SkipWhitespace();
                    if (ch == '\"' || ch == '\'')
                    {
                        token = this._nametable.Add("SYSTEM");
                        syslit = this._current.ScanLiteral(this._sb, ch);
                        this._node.AddAttribute(token, syslit, ch, this.IgnoreCase);
                    }
                    ch = this._current.SkipWhitespace();
                }
                if (ch == '[')
                {
                    subset = this._current.ScanToEnd(this._sb, "Internal Subset", "]");
                    this._node.Value = subset;
                }
                ch = this._current.SkipWhitespace();
                if (ch != '>')
                {
                    this.Log("Expecting end of DOCTYPE tag, but found '{0}'", ch);
                    this._current.ScanToEnd(null, "DOCTYPE", ">");
                }

                if (this._dtd == null)
                {
                    this._docType = name;
                    this._pubid = pubid;
                    this._syslit = syslit;
                    this._subset = subset;
                    this.LazyLoadDtd(this._current.ResolvedUri);
                }
            }
            this._current.ReadChar();
        }

        private static readonly string _piterm = " \t\r\n?";

        private bool ParsePI()
        {
            string name = this._current.ScanToken(this._sb, _piterm, false);
            string value = null;
            if (this._current.Lastchar != '?')
            {
                value = this._current.ScanToEnd(this._sb, "Processing Instruction", "?>");
            }
            else
            {
                // error recovery.
                value = this._current.ScanToEnd(this._sb, "Processing Instruction", ">");
            }
            this.Push(this._nametable.Add(name), XmlNodeType.ProcessingInstruction, value);
            return true;
        }

        private bool ParseText(char ch, bool newtext)
        {
            bool ws = !newtext || this._current.IsWhitespace;
            if (newtext)
            {
                this._sb.Length = 0;
            }
            //_sb.Append(ch);
            //ch = _current.ReadChar();
            this._state = State.Text;
            while (ch != Entity.EOF)
            {
                if (ch == '<')
                {
                    ch = this._current.ReadChar();
                    if (ch == '/' || ch == '!' || ch == '?' || char.IsLetter(ch))
                    {
                        // Hit a tag, so return XmlNodeType.Text token
                        // and remember we partially started a new tag.
                        this._state = State.PartialTag;
                        this._partial = ch;
                        break;
                    }
                    else
                    {
                        // not a tag, so just proceed.
                        this._sb.Append('<');
                        this._sb.Append(ch);
                        ws = false;
                        ch = this._current.ReadChar();
                    }
                }
                else if (ch == '&')
                {
                    this.ExpandEntity(this._sb, '<');
                    ws = false;
                    ch = this._current.Lastchar;
                }
                else
                {
                    if (!this._current.IsWhitespace)
                    {
                        ws = false;
                    }

                    this._sb.Append(ch);
                    ch = this._current.ReadChar();
                }
            }
            string value = this._sb.ToString();
            this.Push(null, XmlNodeType.Text, value);
            return ws;
        }

        // This version is slightly different from Entity.ScanLiteral in that
        // it also expands entities.
        public string ScanLiteral(StringBuilder sb, char quote)
        {
            sb.Length = 0;
            char ch = this._current.ReadChar();
            while (ch != Entity.EOF && ch != quote)
            {
                if (ch == '&')
                {
                    this.ExpandEntity(this._sb, quote);
                    ch = this._current.Lastchar;
                }
                else
                {
                    sb.Append(ch);
                    ch = this._current.ReadChar();
                }
            }
            this._current.ReadChar(); // consume end quote.          
            return sb.ToString();
        }

        private bool ParseCData()
        {
            // Like ParseText(), only it doesn't allow elements in the content.  
            // It allows comments and processing instructions and text only and
            // text is not returned as text but CDATA (since it may contain angle brackets).
            // And initial whitespace is ignored.  It terminates when we hit the
            // end tag for the current CDATA node (e.g. </style>).
            bool ws = this._current.IsWhitespace;
            this._sb.Length = 0;
            char ch;
            if (this._partial != '\0')
            {
                this.Pop(); // pop the CDATA
                switch (this._partial)
                {
                    case '!':
                        this._partial = ' '; // and pop the comment next time around
                        return this.ParseComment();
                    case '?':
                        this._partial = ' '; // and pop the PI next time around
                        return this.ParsePI();
                    case '/':
                        this._state = State.EndTag;
                        return true;    // we are done!
                    case ' ':
                        break; // means we just needed to pop the CDATA.
                }
            }
            // if _partial == '!' then parse the comment and return
            // if _partial == '?' then parse the processing instruction and return.
            ch = this._current.ReadChar();
            while (ch != Entity.EOF)
            {
                if (ch == '<')
                {
                    ch = this._current.ReadChar();
                    if (ch == '!')
                    {
                        ch = this._current.ReadChar();
                        if (ch == '-')
                        {
                            // return what CDATA we have accumulated so far
                            // then parse the comment and return to here.
                            if (ws)
                            {
                                this._partial = ' '; // pop comment next time through
                                return this.ParseComment();
                            }
                            else
                            {
                                // return what we've accumulated so far then come
                                // back in and parse the comment.
                                this._partial = '!';
                                break;
                            }
                        }
                        else
                        {
                            // not a comment, so ignore it and continue on.
                            this._sb.Append('<');
                            this._sb.Append('!');
                            this._sb.Append(ch);
                            ws = false;
                        }
                    }
                    else if (ch == '?')
                    {
                        // processing instruction.
                        this._current.ReadChar();// consume the '?' character.
                        if (ws)
                        {
                            this._partial = ' '; // pop PI next time through
                            return this.ParsePI();
                        }
                        else
                        {
                            this._partial = '?';
                            break;
                        }
                    }
                    else if (ch == '/')
                    {
                        // see if this is the end tag for this CDATA node.
                        string temp = this._sb.ToString();
                        if (this.ParseEndTag() && this._endTag == (object)this._node.Name)
                        {
                            if (ws)
                            {
                                // we are done!
                                return true;
                            }
                            else
                            {
                                // return CDATA text then the end tag
                                this._partial = '/';
                                this._sb.Length = 0; // restore buffer!
                                this._sb.Append(temp);
                                this._state = State.CData;
                                break;
                            }
                        }
                        else
                        {
                            // wrong end tag, so continue on.
                            this._sb.Length = 0; // restore buffer!
                            this._sb.Append(temp);
                            this._sb.Append("</" + this._endTag + ">");
                            ws = false;
                        }
                    }
                    else
                    {
                        // must be just part of the CDATA block, so proceed.
                        this._sb.Append('<');
                        this._sb.Append(ch);
                        ws = false;
                    }
                }
                else
                {
                    if (!this._current.IsWhitespace && ws)
                    {
                        ws = false;
                    }

                    this._sb.Append(ch);
                }
                ch = this._current.ReadChar();
            }
            string value = this._sb.ToString();
            this.Push(null, XmlNodeType.CDATA, value);
            if (this._partial == '\0')
            {
                this._partial = ' ';// force it to pop this CDATA next time in.
            }

            return true;
        }

        private void ExpandEntity(StringBuilder sb, char terminator)
        {
            char ch = this._current.ReadChar();
            if (ch == '#')
            {
                string charent = this._current.ExpandCharEntity();
                sb.Append(charent);
                ch = this._current.ReadChar();
            }
            else
            {
                this._name.Length = 0;
                while (ch != Entity.EOF &&
                    (char.IsLetter(ch) || ch == '_' || ch == '-'))
                {
                    this._name.Append(ch);
                    ch = this._current.ReadChar();
                }
                string name = this._name.ToString();
                if (this._dtd != null && name != string.Empty)
                {
                    Entity e = this._dtd.FindEntity(name);
                    if (e != null)
                    {
                        if (e.Internal)
                        {
                            sb.Append(e.Literal);
                            if (ch != terminator)
                            {
                                ch = this._current.ReadChar();
                            }

                            return;
                        }
                        else
                        {
                            Entity ex = new Entity(name, e.PublicId, e.Uri, this._current.Proxy);
                            e.Open(this._current, new Uri(e.Uri));
                            this._current = ex;
                            this._current.ReadChar();
                            return;
                        }
                    }
                    else
                    {
                        this.Log("Undefined entity '{0}'", name);
                    }
                }
                // Entity is not defined, so just keep it in with the rest of the
                // text.
                sb.Append("&");
                sb.Append(name);
                if (ch != terminator)
                {
                    sb.Append(ch);
                    ch = this._current.ReadChar();
                }
            }
        }

        public override bool EOF
        {
            get
            {
                return this._state == State.Eof;
            }
        }

        public override void Close()
        {
            if (this._current != null)
            {
                this._current.Close();
                this._current = null;
            }
            if (this._log != null)
            {
                this._log.Close();
                this._log = null;
            }
        }

        public override ReadState ReadState
        {
            get
            {
                if (this._state == State.Initial)
                {
                    return ReadState.Initial;
                }
                else if (this._state == State.Eof)
                {
                    return ReadState.EndOfFile;
                }

                return ReadState.Interactive;
            }
        }

        public override string ReadString()
        {
            if (this._node.NodeType == XmlNodeType.Element)
            {
                this._sb.Length = 0;
                while (this.Read())
                {
                    switch (this.NodeType)
                    {
                        case XmlNodeType.CDATA:
                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.Whitespace:
                        case XmlNodeType.Text:
                            this._sb.Append(this._node.Value);
                            break;
                        default:
                            return this._sb.ToString();
                    }
                }
                return this._sb.ToString();
            }
            return this._node.Value;
        }


        public override string ReadInnerXml()
        {
            using (StringWriter sw = new StringWriter())
            {
                XmlTextWriter xw = new XmlTextWriter(sw);
                xw.Formatting = Formatting.Indented;
                switch (this.NodeType)
                {
                    case XmlNodeType.Element:
                        this.Read();
                        while (!this.EOF && this.NodeType != XmlNodeType.EndElement)
                        {
                            xw.WriteNode(this, true);
                        }
                        this.Read(); // consume the end tag
                        break;
                    case XmlNodeType.Attribute:
                        sw.Write(this.Value);
                        break;
                    default:
                        // return empty string according to XmlReader spec.
                        break;
                }
                xw.Close();
                return sw.ToString();
            }
        }

        public override string ReadOuterXml()
        {
            using (StringWriter sw = new StringWriter())
            {
                XmlTextWriter xw = new XmlTextWriter(sw);
                xw.Formatting = Formatting.Indented;
                xw.WriteNode(this, true);
                xw.Close();
                return sw.ToString();
            }
        }

        public override XmlNameTable NameTable
        {
            get
            {
                return this._nametable;
            }
        }

        public override string LookupNamespace(string prefix)
        {
            return null;// there are no namespaces in SGML.
        }

        public override void ResolveEntity()
        {
            // We never return any entity reference nodes, so this should never be called.
            throw new InvalidOperationException("Not on an entity reference.");
        }

        public override bool ReadAttributeValue()
        {
            if (this._state == State.Attr)
            {
                this._state = State.AttrValue;
                return true;
            }
            else if (this._state == State.AttrValue)
            {
                return false;
            }
            throw new InvalidOperationException("Not on an attribute.");
        }

        private void Validate(Node node)
        {
            if (this._dtd != null)
            {
                ElementDecl e = this._dtd.FindElement(node.Name);
                if (e != null)
                {
                    node.DtdType = e;
                    if (e.ContentModel.DeclaredContent == DeclaredContent.EMPTY)
                    {
                        node.IsEmpty = true;
                    }
                }
            }
        }

        private static void ValidateAttribute(Node node, Attribute a)
        {
            ElementDecl e = node.DtdType;
            if (e != null)
            {
                AttDef ad = e.AttList[a.Name];
                if (ad != null)
                {
                    a.DtdType = ad;
                }
            }
        }

        private void ValidateContent(Node node)
        {
            if (this._dtd != null)
            {
                // See if this element is allowed inside the current element.
                // If it isn't, then auto-close elements until we find one
                // that it's allowed to be in.  

                string name = node.Name;
                int i = 0;
                int top = this._depth - 2;
                if (this._dtd.FindElement(name) != null)
                {
                    // it is a known element, let's see if it's allowed in the
                    // current context.
                    for (i = top; i > 0; i--)
                    {
                        Node n = this._stack[i];
                        ElementDecl f = n.DtdType;
                        if (f != null)
                        {
                            if (f.Name == this._dtd.Name)
                            {
                                break; // can't pop the root element.
                            }

                            if (f.CanContain(name, this._dtd))
                            {
                                break;
                            }
                            else if (!f.EndTagOptional)
                            {
                                // If the end tag is not optional then we can't
                                // auto-close it.  We'll just have to live with the
                                // junk we've found and move on.
                                break;
                            }
                        }
                        else
                        {
                            // Since we don't understand this tag anyway,
                            // we might as well allow this content!
                            break;
                        }
                    }
                }
                if (i == 0)
                {
                    // Tag was not found or is not allowed anywhere, ignore it and 
                    // continue on.
                }
                else if (i < top)
                {
                    if (i == top - 1 && name == this._stack[top].Name)
                    {
                        // e.g. p not allowed inside p, not an interesting error.
                    }
                    else
                    {
                        string closing = string.Empty;
                        for (int k = top; k >= i + 1; k--)
                        {
                            if (closing != string.Empty)
                            {
                                closing += ",";
                            }

                            closing += "<" + this._stack[k].Name + ">";
                        }
                        this.Log("Element '{0}' not allowed inside '{1}', closing {2}.",
                            name, this._stack[top].Name, closing);
                    }
                    this._state = State.AutoClose;
                    this._newnode = node;
                    this.Pop(); // save this new node until we pop the others
                    this._poptodepth = i + 1;
                }
            }
        }
    }
}
