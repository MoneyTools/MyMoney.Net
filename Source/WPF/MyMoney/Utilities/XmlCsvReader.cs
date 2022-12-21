/*
* 
* An XmlReader implementation for loading comma delimited files (.csv files)
*
* Copyright (c) 2001-2005 Microsoft Corporation. All rights reserved.
*
* Chris Lovett
* 
*/

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace Walkabout.Utilities
{
    enum State
    {
        Initial,
        Root,
        Row,
        Field,
        FieldValue,
        EndField,
        EndRow,
        Attr,
        AttrValue,
        EndRoot,
        Eof
    }


    /// <summary>
    /// Summary description for XmlCsvReader.
    /// </summary>
    public class XmlCsvReader : XmlReader
    {
        CsvReader _csvReader;
        Uri _baseUri;
        Uri _href;
        string _root = "root";
        string _rowname = "row";
        XmlNameTable _nt;
        string[] _names;
        State _state = State.Initial;
        int _attr = 0;
        bool _asAttrs = false;
        bool _firstRowHasColumnNames = false;
        char _delimiter;
        string _proxy;
        Encoding _encoding;

        void Init()
        {
            this._state = State.Initial;
            this._attr = 0;
        }

        /// <summary>
        /// Construct XmlCsvReader.  You must specify an HRef
        /// location or a TextReader before calling Read().
        /// </summary>
        public XmlCsvReader()
        {
            this._nt = new NameTable();
            this._encoding = Encoding.Default;
        }

        /// <summary>
        /// Construct an XmlCsvReader.
        /// </summary>
        /// <param name="location">The location of the .csv file</param>
        /// <param name="nametable">The nametable to use for atomizing element names</param>
        public XmlCsvReader(Uri location, Encoding encoding, XmlNameTable nametable)
        {
            this._baseUri = location;
            this._encoding = encoding;
            this._nt = nametable;
            if (nametable == null)
            {
                this._nt = new NameTable();
            }

            this._csvReader = new CsvReader(location, encoding, null, 4096);
        }

        /// <summary>
        /// Construct an XmlCsvReader.
        /// </summary>
        /// <param name="input">The .csv input stream</param>
        /// <param name="baseUri">The base URI of the .csv.</param>
        /// <param name="nametable">The nametable to use for atomizing element names</param>
        public XmlCsvReader(Stream input, Encoding encoding, Uri baseUri, XmlNameTable nametable)
        {
            this._baseUri = baseUri;
            this._encoding = encoding;
            this._csvReader = new CsvReader(input, encoding, 4096);
            this._nt = nametable;
        }

        /// <summary>
        /// Construct an XmlCsvReader.
        /// </summary>
        /// <param name="input">The .csv input text reader</param>
        /// <param name="baseUri">The base URI of the .csv.</param>
        /// <param name="nametable">The nametable to use for atomizing element names</param>
        public XmlCsvReader(TextReader input, Uri baseUri, XmlNameTable nametable)
        {
            this._baseUri = baseUri;
            this._encoding = Encoding.Default;
            this._csvReader = new CsvReader(input, 4096);
            this._nt = nametable;
        }

        /// <summary>
        /// Specifies the encoding to use when loading the .csv file.
        /// </summary>
        public Encoding Encoding
        {
            get { return this._encoding == null ? System.Text.Encoding.Default : this._encoding; }
            set { this._encoding = value; }
        }

        /// <summary>
        /// Specifies the URI location of the .csv file to parse.
        /// This can also be a local file name.
        /// This can be a relative URI if a BaseUri has been provided.
        /// You must specify either this property or a TextReader as input
        /// before calling Read.
        /// </summary>
        public string Href
        {
            get { return this._href == null ? "" : this._href.AbsoluteUri; }
            set
            {
                if (this._baseUri != null)
                {
                    this._href = new Uri(this._baseUri, value);
                }
                else
                {
                    try
                    {
                        this._href = new Uri(value);
                    }
                    catch (Exception)
                    {
                        string file = Path.GetFullPath(value);
                        this._href = new Uri(file);
                    }
                    this._baseUri = this._href;
                }
                this._csvReader = null;
                this.Init();
            }
        }

        /// <summary>
        /// Specifies the proxy server.  This is only needed for internet HTTP requests
        /// where the caller is behind a proxy server internet gateway. 
        /// </summary>
        public string Proxy
        {
            get { return this._proxy; }
            set { this._proxy = value; }
        }

        /// <summary>
        /// Returns the TextReader that contains the .csv file contents.
        /// </summary>
        public TextReader TextReader
        {
            get { return this._csvReader == null ? null : this._csvReader.Reader; }
            set
            {
                this._csvReader = new CsvReader(value, 4096);
                this._csvReader.Delimiter = this.Delimiter;
                this.Init();
            }
        }

        /// <summary>
        /// Specifies the name of the root element, the default is "root".
        /// </summary>
        public string RootName
        {
            get { return this._root; }
            set { this._root = this._nt.Add(value); }
        }

        /// <summary>
        /// Specifies the name of the XML element generated for each row
        /// of .csv data.  The default is "row".
        /// </summary>
        public string RowName
        {
            get { return this._rowname; }
            set { this._rowname = this._nt.Add(value); }
        }

        /// <summary>
        /// Specifies whether the first row contains column names.
        /// Default is false.
        /// </summary>
        public bool FirstRowHasColumnNames
        {
            get { return this._firstRowHasColumnNames; }
            set { this._firstRowHasColumnNames = value; }
        }

        /// <summary>
        /// Specifies whether to return the columns as attributes
        /// or as child elements.  Default is false.
        /// </summary>
        public bool ColumnsAsAttributes
        {
            get { return this._asAttrs; }
            set { this._asAttrs = value; }
        }

        /// <summary>
        /// Instead of reading the column names from the stream you can also
        /// provide the column names yourself.
        /// </summary>
        public string[] ColumnNames
        {
            get { return this._names; }
            set
            {
                // atomize the names.
                ArrayList copy = new ArrayList();
                for (int i = 0; i < value.Length; i++)
                {
                    copy.Add(this._nt.Add(value[i]));
                }
                this._names = (string[])copy.ToArray(typeof(string));
            }
        }

        /// <summary>
        /// Gets or sets the column delimiter.  Default is '\0' which means 
        /// the reader will auto detect the delimiter.
        /// </summary>
        public char Delimiter
        {
            get { return this._delimiter; }
            set
            {
                this._delimiter = value; if (this._csvReader != null)
                {
                    this._csvReader.Delimiter = value;
                }
            }
        }

        void ReadColumnNames()
        {
            if (this._csvReader.Read())
            {
                // If column names were already provided then we just skip this row.
                if (this._names == null)
                {
                    this._names = new string[this._csvReader.FieldCount];
                    for (int i = 0; i < this._csvReader.FieldCount; i++)
                    {
                        this._names[i] = this._nt.Add(this._csvReader[i]);
                    }
                }
            }
        }

        public override XmlNodeType NodeType
        {
            get
            {
                switch (this._state)
                {
                    case State.Initial:
                    case State.Eof:
                        return XmlNodeType.None;
                    case State.Root:
                    case State.Row:
                    case State.Field:
                        return XmlNodeType.Element;
                    case State.Attr:
                        return XmlNodeType.Attribute;
                    case State.AttrValue:
                    case State.FieldValue:
                        return XmlNodeType.Text;
                    default:
                        return XmlNodeType.EndElement;
                }
            }
        }

        public override string Name
        {
            get
            {
                return this.LocalName;
            }
        }

        public override string LocalName
        {
            get
            {
                switch (this._state)
                {
                    case State.Attr:
                    case State.Field:
                    case State.EndField:
                        if (this._names == null || this._attr >= this._names.Length)
                        {
                            return this._nt.Add("a" + this._attr);
                        }
                        return XmlConvert.EncodeLocalName(this._names[this._attr]);
                    case State.Root:
                    case State.EndRoot:
                        return this._root;
                    case State.Row:
                    case State.EndRow:
                        return this._rowname;
                }
                return string.Empty;
            }
        }

        public override string NamespaceURI
        {
            get
            {
                return String.Empty;
            }
        }

        public override string Prefix
        {
            get
            {
                return String.Empty;
            }
        }

        public override bool HasValue
        {
            get
            {
                if (this._state == State.Attr || this._state == State.AttrValue || this._state == State.FieldValue)
                {
                    return this.Value != String.Empty;
                }
                return false;
            }
        }

        public override string Value
        {
            get
            {
                if (this._state == State.Attr || this._state == State.AttrValue || this._state == State.FieldValue)
                {
                    return this._csvReader[this._attr];
                }
                return null;
            }
        }

        public override int Depth
        {
            get
            {
                switch (this._state)
                {
                    case State.Row:
                    case State.EndRow:
                        return 1;
                    case State.Attr:
                    case State.Field:
                    case State.EndField:
                        return 2;
                    case State.AttrValue:
                    case State.FieldValue:
                        return 3;
                }
                return 0;
            }
        }

        public override string BaseURI
        {
            get
            {
                return this._baseUri.AbsolutePath;
            }
        }

        public override bool IsEmptyElement
        {
            get
            {
                if (this._state == State.Row && this._asAttrs)
                {
                    return true;
                }

                if (this._state == State.Field && this._csvReader[this._attr] == String.Empty)
                {
                    return true;
                }

                return false;
            }
        }
        public override bool IsDefault
        {
            get
            {
                return false;
            }
        }
        public override char QuoteChar
        {
            get
            {
                return this._csvReader.QuoteChar;
            }
        }

        public override XmlSpace XmlSpace
        {
            get
            {
                return XmlSpace.Default;
            }
        }

        public override string XmlLang
        {
            get
            {
                return String.Empty;
            }
        }

        public override int AttributeCount
        {
            get
            {
                if (!this._asAttrs)
                {
                    return 0;
                }

                if (this._state == State.Row || this._state == State.Attr || this._state == State.AttrValue)
                {
                    return this._csvReader.FieldCount;
                }
                return 0;
            }
        }

        public override string GetAttribute(string name)
        {
            if (!this._asAttrs)
            {
                return null;
            }

            if (this._state == State.Row || this._state == State.Attr || this._state == State.AttrValue)
            {
                int i = this.GetOrdinal(name);
                if (i >= 0)
                {
                    return this.GetAttribute(i);
                }
            }
            return null;
        }

        int GetOrdinal(string name)
        {
            if (this._names != null)
            {
                string n = this._nt.Add(name);
                for (int i = 0; i < this._names.Length; i++)
                {
                    if (this._names[i] == (object)n)
                    {
                        return i;
                    }
                }
                throw new Exception("Attribute '" + name + "' not found.");
            }
            // names are assigned a0, a1, a2, ...
            return Int32.Parse(name.Substring(1));
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            if (namespaceURI != string.Empty && namespaceURI != null)
            {
                return null;
            }

            return this.GetAttribute(name);
        }

        public override string GetAttribute(int i)
        {
            if (!this._asAttrs)
            {
                return null;
            }

            if (this._state == State.Row || this._state == State.Attr || this._state == State.AttrValue)
            {
                return this._csvReader[i];
            }
            return null;
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
            if (!this._asAttrs)
            {
                return false;
            }

            if (this._state == State.Row || this._state == State.Attr || this._state == State.AttrValue)
            {
                int i = this.GetOrdinal(name);
                if (i < 0)
                {
                    return false;
                }

                this.MoveToAttribute(i);
            }
            return false;
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            if (ns != string.Empty && ns != null)
            {
                return false;
            }

            return this.MoveToAttribute(name);
        }

        public override void MoveToAttribute(int i)
        {
            if (this._asAttrs)
            {
                if (this._state == State.Row || this._state == State.Attr || this._state == State.AttrValue)
                {
                    this._state = State.Attr;
                    this._attr = i;
                }
            }
        }

        public override bool MoveToFirstAttribute()
        {
            if (!this._asAttrs)
            {
                return false;
            }

            if (this.AttributeCount > 0)
            {
                this._attr = 0;
                this._state = State.Attr;
                return true;
            }
            return false;
        }

        public override bool MoveToNextAttribute()
        {
            if (!this._asAttrs)
            {
                return false;
            }

            if (this._attr < this.AttributeCount - 1)
            {
                this._attr = (this._state == State.Attr || this._state == State.AttrValue) ? this._attr + 1 : 0;
                this._state = State.Attr;
                return true;
            }
            return false;
        }

        public override bool MoveToElement()
        {
            if (!this._asAttrs)
            {
                return true;
            }

            if (this._state == State.Root || this._state == State.EndRoot || this._state == State.Row)
            {
                return true;
            }
            else if (this._state == State.Attr || this._state == State.AttrValue)
            {
                this._state = State.Row;
                return true;
            }
            return false;
        }

        public override bool Read()
        {
            switch (this._state)
            {
                case State.Initial:
                    if (this._csvReader == null)
                    {
                        if (this._href == null)
                        {
                            throw new Exception("You must provide an input location via the Href property, or provide an input stream via the TextReader property.");
                        }
                        this._csvReader = new CsvReader(this._href, this._encoding, this._proxy, 4096);
                        this._csvReader.Delimiter = this.Delimiter;
                    }
                    if (this._firstRowHasColumnNames)
                    {
                        this.ReadColumnNames();
                    }
                    this._state = State.Root;
                    return true;
                case State.Eof:
                    return false;
                case State.Root:
                case State.EndRow:
                    if (this._csvReader.Read())
                    {
                        this._state = State.Row;
                        return true;
                    }
                    this._state = State.EndRoot;
                    return true;
                case State.EndRoot:
                    this._state = State.Eof;
                    return false;
                case State.Row:
                    if (this._asAttrs)
                    {
                        this._attr = 0;
                        goto case State.EndRow;
                    }
                    else
                    {
                        this._state = State.Field;
                        this._attr = 0;
                        return true;
                    }
                case State.Field:
                    if (!this.IsEmptyElement)
                    {
                        this._state = State.FieldValue;
                    }
                    else
                    {
                        goto case State.EndField;
                    }
                    return true;
                case State.FieldValue:
                    this._state = State.EndField;
                    return true;
                case State.EndField:
                    if (this._attr < this._csvReader.FieldCount - 1)
                    {
                        this._attr++;
                        this._state = State.Field;
                        return true;
                    }
                    this._state = State.EndRow;
                    return true;
                case State.Attr:
                case State.AttrValue:
                    this._state = State.Root;
                    this._attr = 0;
                    goto case State.Root;
            }
            return false;
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
            this._csvReader.Close();
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
            if (this._state == State.AttrValue || this._state == State.Attr)
            {
                return this._csvReader[this._attr];
            }
            return String.Empty;
        }

        public override string ReadInnerXml()
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xw = new XmlTextWriter(sw);
            xw.Formatting = Formatting.Indented;
            while (!this.EOF && this.NodeType != XmlNodeType.EndElement)
            {
                xw.WriteNode(this, true);
            }
            xw.Close();
            return sw.ToString();
        }

        public override string ReadOuterXml()
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xw = new XmlTextWriter(sw);
            xw.Formatting = Formatting.Indented;
            xw.WriteNode(this, true);
            xw.Close();
            return sw.ToString();
        }

        public override XmlNameTable NameTable
        {
            get
            {
                return this._nt;
            }
        }

        public override string LookupNamespace(string prefix)
        {
            return null;
        }

        public override void ResolveEntity()
        {
            throw new NotImplementedException();
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
            throw new Exception("Not on an attribute.");
        }

    }

    class CsvReader
    {
        TextReader _r;
        char[] _buffer;
        int _pos;
        int _used;

        // assumes end of record delimiter is {CR}{LF}, {CR}, or {LF}
        // possible values are {CR}{LF}, {CR}, {LF}, ';', ',', '\t'
        // char _recDelim;

        char _colDelim; // possible values ',', ';', '\t', '|'
        char _quoteChar;

        ArrayList _values;
        int _fields;

        public CsvReader(Uri location, Encoding encoding, string proxy, int bufsize)
        {  // the location of the .csv file
            if (location.IsFile)
            {
                this._r = new StreamReader(location.LocalPath, encoding, true);
            }
            else
            {
                WebRequest wr = WebRequest.Create(location);
                if (proxy != null && proxy != "")
                {
                    wr.Proxy = new WebProxy(proxy);
                }

                wr.Credentials = CredentialCache.DefaultCredentials;
                Stream stm = wr.GetResponse().GetResponseStream();
                this._r = new StreamReader(stm, encoding, true);
            }
            this._buffer = new char[bufsize];
            this._values = new ArrayList();
        }

        public CsvReader(Stream stm, Encoding encoding, int bufsize)
        {  // the location of the .csv file
            this._r = new StreamReader(stm, encoding, true);
            this._buffer = new char[bufsize];
            this._values = new ArrayList();
        }
        public CsvReader(TextReader stm, int bufsize)
        {  // the location of the .csv file
            this._r = stm;
            this._buffer = new char[bufsize];
            this._values = new ArrayList();
        }

        public TextReader Reader
        {
            get { return this._r; }
        }

        const int EOF = 0xffff;

        public bool Read()
        { // read a record.
            this._fields = 0;
            char ch = this.ReadChar();
            if (ch == 0)
            {
                return false;
            }

            while (ch != 0 && ch == '\r' || ch == '\n' || ch == ' ')
            {
                ch = this.ReadChar();
            }

            if (ch == 0)
            {
                return false;
            }

            while (ch != 0 && ch != '\r' && ch != '\n')
            {
                StringBuilder sb = this.AddField();
                if (ch == '\'' || ch == '"')
                {
                    this._quoteChar = ch;
                    char c = this.ReadChar();
                    bool done = false;
                    while (!done && c != 0)
                    {
                        while (c != 0 && c != ch)
                        { // scan literal.
                            sb.Append(c);
                            c = this.ReadChar();
                        }
                        if (c == ch)
                        {
                            done = true;
                            char next = this.ReadChar(); // consume end quote
                            if (next == ch)
                            {
                                // it was an escaped quote sequence "" inside the literal
                                // so append a single " and consume the second end quote.
                                done = false;
                                sb.Append(next);
                                c = this.ReadChar();
                                if (this._colDelim != 0 && c == this._colDelim)
                                {
                                    // bad form, but this is probably a record separator.
                                    done = true;
                                }
                            }
                            else if (this._colDelim != 0 && next != this._colDelim && next != 0 && next != ' ' && next != '\n' && next != '\r')
                            {
                                // it was an un-escaped quote embedded inside a string literal
                                // in this case the quote is probably just part of the text so ignore it.
                                done = false;
                                sb.Append(c);
                                sb.Append(next);
                                c = this.ReadChar();
                            }
                            else
                            {
                                c = next;
                            }
                        }
                    }
                    ch = c;
                }
                else
                {
                    // scan number, date, time, float, etc.
                    while (ch != 0 && ch != '\n' && ch != '\r')
                    {
                        if (ch == this._colDelim || (this._colDelim == '\0' && (ch == ',' || ch == ';' || ch == '\t' || ch == '|')))
                        {
                            break;
                        }

                        sb.Append(ch);
                        ch = this.ReadChar();
                    }
                }
                if (ch == this._colDelim || (this._colDelim == '\0' && (ch == ',' || ch == ';' || ch == '\t' || ch == '|')))
                {
                    this._colDelim = ch;
                    ch = this.ReadChar();
                    if (ch == '\n' || ch == '\r')
                    {
                        sb = this.AddField(); // blank field.
                    }
                }
            }
            return true;
        }

        public char QuoteChar { get { return this._quoteChar; } }
        public char Delimiter { get { return this._colDelim; } set { this._colDelim = value; } }

        public int FieldCount { get { return this._fields; } }

        public string this[int i] { get { return ((StringBuilder)this._values[i]).ToString(); } }

        char ReadChar()
        {
            if (this._pos == this._used)
            {
                this._pos = 0;
                this._used = this._r.Read(this._buffer, 0, this._buffer.Length);
            }
            if (this._pos == this._used)
            {
                return (char)0;
            }
            return this._buffer[this._pos++];
        }

        StringBuilder AddField()
        {
            if (this._fields == this._values.Count)
            {
                this._values.Add(new StringBuilder());
            }
            StringBuilder sb = (StringBuilder)this._values[this._fields++];
            sb.Length = 0;
            return sb;
        }

        public void Close()
        {
            this._r.Close();
        }
    }

}