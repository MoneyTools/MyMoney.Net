using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    public class XmlStore : IDatabase
    {
        private readonly string filename;
        private string backup;
        private string password;

        public XmlStore(string filename, string password)
        {
            this.filename = filename;
            this.password = password;
        }

        public virtual bool SupportsUserLogin => false;
        public virtual string Server { get; set; }

        public virtual string DatabasePath { get { return this.filename; } }

        public virtual string ConnectionString { get { return null; } }

        public virtual string BackupPath { get { return this.backup; } set { this.backup = value; } }

        public virtual DbFlavor DbFlavor { get { return Data.DbFlavor.Xml; } }

        public virtual string UserId { get; set; }

        public virtual string Password
        {
            get { return this.password; }
            set { this.password = value; }
        }

        public virtual bool Exists
        {
            get
            {
                return File.Exists(this.filename);
            }
        }

        public virtual string GetDatabaseFullPath()
        {
            return this.DatabasePath;
        }

        public virtual void Create()
        {
        }

        public virtual void Disconnect()
        {

        }

        public virtual void Delete()
        {
            if (this.Exists)
            {
                File.Delete(this.filename);
            }
        }

        public bool UpgradeRequired
        {
            get
            {
                return false;
            }
        }

        public void Upgrade()
        {
        }

        public virtual MyMoney Load(IStatusService status)
        {
            MyMoney money = null;

            DataContractSerializer serializer = new DataContractSerializer(typeof(MyMoney));
            if (!File.Exists(this.filename))
            {
                money = new MyMoney();
            }
            else
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();

                using (XmlReader r = XmlReader.Create(this.filename))
                {
                    money = (MyMoney)serializer.ReadObject(r);
                }

                watch.Stop();
                Debug.WriteLine("Loaded XML store in " + watch.Elapsed.TotalSeconds + " seconds");
            }

            money.PostDeserializeFixup();

            return money;
        }

        public virtual string GetLog()
        {
            return "";
        }

        public virtual DataSet QueryDataSet(string cmd)
        {
            DataSet result = new DataSet();
            result.ReadXml(this.filename);
            return result;
        }


        public virtual void Save(MyMoney money)
        {
            PrepareSave(money);

            Stopwatch watch = new Stopwatch();
            watch.Start();

            DataContractSerializer serializer = new DataContractSerializer(typeof(MyMoney), MyMoney.GetKnownTypes());
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(this.filename, settings))
            {
                serializer.WriteObject(w, money);
            }

            Debug.WriteLine("Saved XML store in " + watch.Elapsed.TotalSeconds + " seconds");
        }

        internal static void PrepareSave(MyMoney money)
        {
            // time to cleanup deleted items
            money.Accounts.RemoveDeleted();
            money.OnlineAccounts.RemoveDeleted();
            money.Aliases.RemoveDeleted();
            money.Categories.RemoveDeleted();
            money.Payees.RemoveDeleted();
            money.Securities.RemoveDeleted();
            money.StockSplits.RemoveDeleted();
            money.Currencies.RemoveDeleted();
            money.Buildings.RemoveDeleted();
            money.LoanPayments.RemoveDeleted();
            money.Transactions.RemoveDeleted();

            foreach (Transaction t in money.Transactions)
            {
                if (t.IsSplit)
                {
                    t.Splits.RemoveDeleted();
                }
            }
        }

        public virtual void Backup(string path)
        {
            File.Copy(this.filename, path);
            this.backup = path;
        }
    }

    /// <summary>
    /// This class loads/saves the MyMoney objects to an encrypted binary XML file.
    /// </summary>
    public class BinaryXmlStore : XmlStore
    {
        private readonly string filename;

        public BinaryXmlStore(string filename, string password)
            : base(filename, password)
        {
            this.filename = filename;
        }

        public override DbFlavor DbFlavor { get { return Data.DbFlavor.BinaryXml; } }


        public override MyMoney Load(IStatusService status)
        {
            MyMoney money = null;

            DataContractSerializer serializer = new DataContractSerializer(typeof(MyMoney));
            if (!File.Exists(this.filename))
            {
                money = new MyMoney();
            }
            else
            {
                string path = this.filename;
                string tempPath = null;
                bool encrypted = !string.IsNullOrWhiteSpace(this.Password);

                if (encrypted)
                {
                    // Decrypt the file.
                    Encryption e = new Encryption();
                    tempPath = Path.GetTempFileName();
                    e.DecryptFile(this.filename, this.Password, tempPath);
                    path = tempPath;
                }

                using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using (GZipStream zip = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        // binary reader doesn't add any speed - which is odd, so there's no point using it.
                        // we get a better result by just zipping the XML text.
                        // using (BinaryXmlReader binary = new BinaryXmlReader(zip)) {}
                        using (XmlReader r = XmlReader.Create(zip))
                        {
                            money = (MyMoney)serializer.ReadObject(r);
                        }
                    }
                }

                if (encrypted)
                {
                    File.Delete(tempPath);
                }
            }

            money.PostDeserializeFixup();
            money.OnLoaded();

            return money;
        }

        public override void Save(MyMoney money)
        {
            PrepareSave(money);

            DataContractSerializer serializer = new DataContractSerializer(typeof(MyMoney));

            string path = this.filename;
            bool encrypted = !string.IsNullOrWhiteSpace(this.Password);

            if (encrypted)
            {
                // save to temp file, then encrypt it.
                path = Path.GetTempFileName();
            }

            using (Stream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (GZipStream zip = new GZipStream(stream, CompressionMode.Compress))
                {
                    // using (BinaryXmlWriter binary = new BinaryXmlWriter(zip)) {}
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Encoding = Encoding.UTF8;
                    settings.Indent = true;
                    using (XmlWriter w = XmlWriter.Create(zip, settings))
                    {
                        serializer.WriteObject(w, money);
                    }
                }
            }

            if (encrypted)
            {
                // now encrypt the file.
                Encryption e = new Encryption();
                e.EncryptFile(path, this.Password, this.filename);
                File.Delete(path);
            }


        }

    }

    #region BinaryXmlReader/Writer

    // These are currently not in use, but may come in handy in the future.

    // Tokens used by BinaryXmlReader and BinaryXmlWriter
    internal enum XmlNodeTypeToken
    {
        None,
        Element,
        Attribute,
        Text,
        CDATA,
        EntityReference,
        Entity,
        ProcessingInstruction,
        Comment,
        Document,
        DocumentType,
        DocumentFragment,
        Notation,
        Whitespace,
        SignificantWhitespace,
        EndElement,
        EndEntity,
        XmlDeclaration,
        // Extended tokens.
        Base64,
        RawText,
        RawChars,
        CharacterEntity,
        Chars,
        StartAttribute,
        EndAttribute,
        EndDocument,
        SurrogateCharEntity,

        // primitive types
        Bool,
        DateTime,
        Decimal,
        Double,
        Float,
        Int,
        Long,
        EndStartTag,
        EmptyEndStartTag
    }


    public class BinaryXmlWriter : XmlWriter
    {
        private readonly Stream stream;
        private readonly BinaryWriter writer;
        private readonly XmlNamespaceManager mgr;
        private readonly NameTable nameTable;
        private WriteState state = WriteState.Start;

        #region Auto Namespaces

        private class XmlNamespaceDefinition
        {
            public string Prefix;
            public string NamespaceUri;
        }

        private readonly List<XmlNamespaceDefinition> namespaceStack = new List<XmlNamespaceDefinition>();
        private int namespacePos;

        private void PushNewNamespace(string prefix, string namespaceUri)
        {
            XmlNamespaceDefinition def;
            if (this.namespacePos < this.namespaceStack.Count)
            {
                def = this.namespaceStack[this.namespacePos];
            }
            else
            {
                def = new XmlNamespaceDefinition();
                this.namespaceStack.Add(def);
            }
            def.Prefix = prefix;
            def.NamespaceUri = namespaceUri;
            this.namespacePos++;
        }

        private void SetNamespaceDefined(string prefix, string nsuri)
        {
            for (int i = 0; i < this.namespacePos; i++)
            {
                XmlNamespaceDefinition def = this.namespaceStack[i];
                if (def.Prefix == prefix && def.NamespaceUri == nsuri)
                {
                    this.namespaceStack.RemoveAt(i);
                    return;
                }
            }
        }

        private void WriteAutoNamespaces()
        {
            if (this.namespacePos > 0)
            {
                // need to write out some extra namespace definitions
                for (int i = 0; i < this.namespacePos; i++)
                {
                    XmlNamespaceDefinition d = this.namespaceStack[i];

                    this.writer.Write((short)XmlNodeTypeToken.StartAttribute);
                    if (string.IsNullOrEmpty(d.Prefix))
                    {
                        this.writer.Write(string.Empty);
                        this.writer.Write("xmlns");
                    }
                    else
                    {
                        this.writer.Write("xmlns");
                        this.writer.Write(d.Prefix);
                    }
                    this.writer.Write("http://www.w3.org/2000/xmlns/");

                    this.writer.Write((short)XmlNodeTypeToken.Text);
                    this.writer.Write(d.NamespaceUri);

                    this.writer.Write((short)XmlNodeTypeToken.EndAttribute);

                    this.mgr.AddNamespace(d.Prefix ?? string.Empty, d.NamespaceUri);
                }
                this.namespacePos = 0;
            }
        }
        #endregion

        public BinaryXmlWriter(string filename)
        {
            this.stream = new FileStream(filename, FileMode.OpenOrCreate | FileMode.Truncate, FileAccess.ReadWrite, FileShare.None);
            this.writer = new BinaryWriter(this.stream, Encoding.UTF8);
            this.nameTable = new NameTable();
            this.mgr = new XmlNamespaceManager(this.nameTable);
        }

        public BinaryXmlWriter(Stream stream)
        {
            this.stream = stream;
            this.writer = new BinaryWriter(stream, Encoding.UTF8);
            this.nameTable = new NameTable();
            this.mgr = new XmlNamespaceManager(this.nameTable);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this.writer.Dispose();
            this.stream.Dispose();
        }

        public override void Close()
        {
            this.writer.Close();
            this.stream.Close();
            this.state = WriteState.Closed;
        }

        public override void Flush()
        {
            this.stream.Flush();
        }

        public override string LookupPrefix(string ns)
        {
            string result = this.mgr.LookupPrefix(this.nameTable.Add(ns));
            return result;
        }

        private void EndStartTag(bool isEmpty)
        {
            if (this.state == System.Xml.WriteState.Element)
            {
                this.WriteAutoNamespaces();
                this.state = System.Xml.WriteState.Content;
                this.writer.Write(isEmpty ? (short)XmlNodeTypeToken.EmptyEndStartTag : (short)XmlNodeTypeToken.EndStartTag);
            }
        }

        public override void WriteBase64(byte[] buffer, int index, int count)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            int outsize = (count - index) * 4 / 3;
            char[] chars = new char[outsize + 4];
            Convert.ToBase64CharArray(buffer, index, count, chars, 0);
            this.writer.Write((short)XmlNodeTypeToken.Base64);
            this.writer.Write(count);
            this.writer.Write(chars, 0, count);
        }

        public override void WriteCData(string text)
        {
            this.EndStartTag(false);
            this.writer.Write((short)XmlNodeTypeToken.CDATA);
            this.writer.Write(text);
        }

        public override void WriteCharEntity(char ch)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.CharacterEntity);
            this.writer.Write(ch);
        }

        public override void WriteChars(char[] buffer, int index, int count)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.Chars);
            this.writer.Write(count);
            this.writer.Write(buffer, index, count);
        }

        public override void WriteComment(string text)
        {
            this.EndStartTag(false);
            this.state = System.Xml.WriteState.Content;
            this.writer.Write((short)XmlNodeTypeToken.Comment);
            this.writer.Write(text);
        }

        public override void WriteDocType(string name, string pubid, string sysid, string subset)
        {
            this.state = System.Xml.WriteState.Prolog;
            this.writer.Write((short)XmlNodeTypeToken.DocumentType);
            this.writer.Write(name);
            this.writer.Write(pubid);
            this.writer.Write(sysid);
            this.writer.Write(subset);
        }

        public override void WriteEndAttribute()
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                throw new Exception("Cannot write EndAttribute since we are not in 'Attribute' state");
            }
            this.writingNamespace = false;
            this.state = System.Xml.WriteState.Element;
            this.writer.Write((short)XmlNodeTypeToken.EndAttribute);
        }

        public override void WriteEndDocument()
        {
            this.EndStartTag(true);
            this.writer.Write((short)XmlNodeTypeToken.EndDocument);
        }

        public override void WriteEndElement()
        {
            this.EndStartTag(true);
            this.mgr.PopScope();
            this.writer.Write((short)XmlNodeTypeToken.EndElement);
        }

        public override void WriteEntityRef(string name)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.EntityReference);
            this.writer.Write(name);
        }

        public override void WriteFullEndElement()
        {
            this.EndStartTag(false);
            this.WriteEndElement();
        }

        public override void WriteProcessingInstruction(string name, string text)
        {
            this.EndStartTag(false);
            this.writer.Write((short)XmlNodeTypeToken.ProcessingInstruction);
            this.writer.Write(name);
            this.writer.Write(text);
        }

        public override void WriteRaw(string data)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.RawText);
            this.writer.Write(data);
        }

        public override void WriteRaw(char[] buffer, int index, int count)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.RawChars);
            this.writer.Write(count);
            this.writer.Write(buffer, index, count);
        }

        private bool writingNamespace;
        private string prefix;

        public override void WriteStartAttribute(string prefix, string localName, string ns)
        {
            this.writingNamespace = false;
            if (prefix == "xmlns")
            {
                this.writingNamespace = true;
                this.prefix = this.nameTable.Add(localName);
            }
            this.state = System.Xml.WriteState.Attribute;

            this.writer.Write((short)XmlNodeTypeToken.StartAttribute);
            this.writer.Write(prefix ?? string.Empty);
            this.writer.Write(localName ?? string.Empty);
            this.writer.Write(ns ?? string.Empty);
        }

        public override void WriteStartDocument(bool standalone)
        {
            this.state = System.Xml.WriteState.Prolog;
            this.writer.Write((short)XmlNodeTypeToken.Document);
        }

        public override void WriteStartDocument()
        {
            this.state = System.Xml.WriteState.Prolog;
            this.writer.Write((short)XmlNodeTypeToken.Document);
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            this.EndStartTag(false);
            this.state = System.Xml.WriteState.Element;
            this.writer.Write((short)XmlNodeTypeToken.Element);
            this.mgr.PushScope();
            if (!string.IsNullOrEmpty(ns) && prefix == null)
            {
                prefix = this.LookupPrefix(ns);
                if (prefix == null)
                {
                    this.PushNewNamespace(prefix, ns);
                }
            }
            this.writer.Write(prefix ?? string.Empty);
            this.writer.Write(localName ?? string.Empty);
            this.writer.Write(ns ?? string.Empty);
        }

        public override WriteState WriteState
        {
            get { return this.state; }
        }

        public override void WriteString(string text)
        {
            if (!string.IsNullOrWhiteSpace(text) || this.state == System.Xml.WriteState.Attribute)
            {
                if (this.writingNamespace)
                {
                    this.SetNamespaceDefined(this.prefix, text);
                    this.mgr.AddNamespace(this.prefix, this.nameTable.Add(text));
                }
                if (this.state != System.Xml.WriteState.Attribute)
                {
                    this.EndStartTag(false);
                }
                this.writer.Write((short)XmlNodeTypeToken.Text);
                this.writer.Write(text ?? string.Empty);
            }
        }

        public override void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.SurrogateCharEntity);
            this.writer.Write(lowChar);
            this.writer.Write(highChar);
        }

        public override void WriteWhitespace(string ws)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.Whitespace);
            this.writer.Write(ws ?? string.Empty);
        }

        public override void WriteValue(bool value)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.Bool);
            this.writer.Write(value);
        }

        public override void WriteValue(DateTime value)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.DateTime);
            this.writer.Write(value.Ticks);
        }

        public override void WriteValue(decimal value)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.Decimal);
            this.writer.Write(value);
        }

        public override void WriteValue(double value)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.Double);
            this.writer.Write(value);
        }
        public override void WriteValue(float value)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.Float);
            this.writer.Write(value);
        }

        public override void WriteValue(int value)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.Int);
            this.writer.Write(value);
        }

        public override void WriteValue(long value)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            this.writer.Write((short)XmlNodeTypeToken.Long);
            this.writer.Write(value);
        }

        public override void WriteValue(object value)
        {
            throw new NotImplementedException();
        }

        public override void WriteValue(string value)
        {
            if (this.state != System.Xml.WriteState.Attribute)
            {
                this.EndStartTag(false);
            }
            if (!string.IsNullOrWhiteSpace(value) || this.state == System.Xml.WriteState.Attribute)
            {
                this.writer.Write((short)XmlNodeTypeToken.Text);
                this.writer.Write(value ?? string.Empty);
            }
        }
    }

    public class BinaryXmlReader : XmlReader
    {
        private readonly string baseUri;
        private readonly Stream stream;
        private BinaryXmlElement currentElement;
        private int attributePos; // position in MoveToNextAttribute
        private BinaryXmlAttribute currentAttribute; // in MoveToNextAttribute
        private object value;
        private readonly List<BinaryXmlElement> elementStack = new List<BinaryXmlElement>();
        private int elementDepth;
        private bool isElementEmpty;
        private readonly BinaryReader reader;
        private XmlNodeType nodeType = XmlNodeType.None;
        private ReadState state = ReadState.Initial;
        private XmlNodeTypeToken token = XmlNodeTypeToken.None;
        private readonly XmlNameTable nameTable = new NameTable();
        private readonly XmlNamespaceManager mgr;
        private readonly BinaryXmlElement leafNode = new BinaryXmlElement();

        private class BinaryXmlAttribute
        {
            public string Prefix;
            public string Name;
            public string Namespace;
            public object Value;
            public XmlNodeTypeToken Token;
        }

        private class BinaryXmlElement
        {
            public string Prefix;
            public string Name;
            public string NamespaceUri;
            public List<BinaryXmlAttribute> Attributes = new List<BinaryXmlAttribute>();
            public int AttributeCount; // actual number of attributes on current element.

            // This stack operates as a high water mark kind of stack so we keep BinaryXmlAttributes allocated so we can reuse them efficiently.
            public BinaryXmlAttribute PushAttribute()
            {
                if (this.AttributeCount < this.Attributes.Count)
                {
                    return this.Attributes[this.AttributeCount++];
                }
                BinaryXmlAttribute result = new BinaryXmlAttribute();
                this.Attributes.Add(result);
                this.AttributeCount++;
                return result;
            }
        }

        public BinaryXmlReader(string filename)
        {
            this.baseUri = filename;
            this.stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            this.reader = new BinaryReader(this.stream, Encoding.UTF8);
            this.mgr = new XmlNamespaceManager(this.nameTable);
        }

        public BinaryXmlReader(Stream stream)
        {
            this.stream = stream;
            this.reader = new BinaryReader(stream, Encoding.UTF8);
            this.mgr = new XmlNamespaceManager(this.nameTable);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this.reader.Dispose();
            this.stream.Dispose();
        }

        public override void Close()
        {
            this.reader.Close();
            this.stream.Close();
            this.state = ReadState.Closed;
        }

        public override int Depth
        {
            get { return this.elementDepth; }
        }

        public override bool EOF
        {
            get { return this.state == System.Xml.ReadState.EndOfFile; }
        }

        public override int AttributeCount
        {
            get { return this.currentElement != null ? this.currentElement.AttributeCount : 0; }
        }

        public override string BaseURI
        {
            get { return this.baseUri; }
        }

        public override string GetAttribute(int i)
        {
            if ((this.token == XmlNodeTypeToken.Element || this.currentAttribute != null) && i <= this.currentElement.AttributeCount)
            {
                return this.currentElement.Attributes[i].Value.ToString();
            }
            return null;
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            if ((this.token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.currentElement.AttributeCount > 0)
            {
                string tname = this.nameTable.Add(name);
                string tnamespace = this.nameTable.Add(namespaceURI ?? string.Empty);
                foreach (BinaryXmlAttribute a in this.currentElement.Attributes)
                {
                    if (a.Name == (object)tname && a.Namespace == (object)tnamespace)
                    {
                        return a.Value.ToString();
                    }
                }
            }
            return null;
        }

        public override string GetAttribute(string name)
        {
            if ((this.token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.currentElement.AttributeCount > 0)
            {
                string tname = this.nameTable.Add(name);
                foreach (BinaryXmlAttribute a in this.currentElement.Attributes)
                {
                    if (a.Name == (object)tname)
                    {
                        return a.Value.ToString();
                    }
                }
            }
            return null;
        }

        public override bool IsEmptyElement
        {
            get
            {
                return this.isElementEmpty;
            }
        }

        public override string LocalName
        {
            get
            {
                if (this.token == XmlNodeTypeToken.Attribute)
                {
                    return this.currentAttribute.Name;
                }
                return this.currentElement == null ? null : this.currentElement.Name;
            }
        }

        public override string LookupNamespace(string prefix)
        {
            return this.mgr.LookupNamespace(prefix);
        }

        public override bool MoveToAttribute(string name, string namespaceURI)
        {
            if ((this.token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.currentElement.AttributeCount > 0)
            {
                string tname = this.nameTable.Add(name);
                string tnamespace = this.nameTable.Add(namespaceURI ?? string.Empty);
                int i = 0;
                foreach (BinaryXmlAttribute a in this.currentElement.Attributes)
                {
                    if (a.Name == (object)tname && a.Namespace == (object)tnamespace)
                    {
                        this.SetCurrentAttribute(i);
                        return true;
                    }
                    i++;
                }
            }
            return false;
        }

        private string SetCurrentAttribute(int i)
        {
            Debug.Assert(this.currentElement != null);
            if (this.token == XmlNodeTypeToken.Element)
            {
                this.elementDepth++;
            }
            this.attributePos = i;
            this.currentAttribute = this.currentElement.Attributes[i];
            this.value = this.currentAttribute.Value;
            this.token = XmlNodeTypeToken.Attribute;
            this.nodeType = XmlNodeType.Attribute;
            return this.currentAttribute.Name;
        }


        public override bool MoveToAttribute(string name)
        {
            if ((this.token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.currentElement.AttributeCount > 0)
            {
                string tname = this.nameTable.Add(name);
                int i = 0;
                foreach (BinaryXmlAttribute a in this.currentElement.Attributes)
                {
                    if (a.Name == (object)tname)
                    {
                        this.SetCurrentAttribute(i);
                        return true;
                    }
                    i++;
                }
            }
            return false;
        }

        public override bool MoveToElement()
        {
            if (this.token == XmlNodeTypeToken.EndElement || this.state == ReadState.Initial || this.token == XmlNodeTypeToken.Element)
            {
                return false;
            }
            if (this.currentAttribute != null)
            {
                this.elementDepth--;
                this.currentAttribute = null;
                this.token = XmlNodeTypeToken.Element;
                this.nodeType = XmlNodeType.Element;
                return true;
            }

            while (this.Read())
            {
                if (this.nodeType == XmlNodeType.Element)
                {
                    return true;
                }
            }
            return false;
        }

        public override bool MoveToFirstAttribute()
        {
            if ((this.token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.currentElement.AttributeCount > 0)
            {
                this.SetCurrentAttribute(0);
                return true;
            }
            return false;
        }

        public override bool MoveToNextAttribute()
        {
            if ((this.token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.attributePos + 1 < this.currentElement.AttributeCount)
            {
                this.attributePos++;
                this.SetCurrentAttribute(this.attributePos);
                return true;
            }
            return false;
        }

        public override XmlNameTable NameTable
        {
            get { return this.nameTable; }
        }

        public override string NamespaceURI
        {
            get
            {
                if (this.token == XmlNodeTypeToken.Attribute)
                {
                    return this.currentAttribute.Namespace;
                }
                return this.currentElement == null ? null : this.currentElement.NamespaceUri;
            }
        }

        public override XmlNodeType NodeType
        {
            get { return this.nodeType; }
        }

        public override string Prefix
        {
            get
            {
                if (this.token == XmlNodeTypeToken.Attribute)
                {
                    return this.currentAttribute.Prefix;
                }
                return this.currentElement == null ? null : this.currentElement.Prefix;
            }
        }

        public override bool Read()
        {
            if (this.state == System.Xml.ReadState.EndOfFile || this.state == System.Xml.ReadState.Error || this.state == System.Xml.ReadState.Closed)
            {
                return false;
            }

            if (this.nodeType == XmlNodeType.Element || this.nodeType == XmlNodeType.Attribute)
            {
                this.elementDepth++;
            }

            this.currentElement = null;
            XmlNodeTypeToken previous = this.token;
            try
            {
                this.token = (XmlNodeTypeToken)this.reader.ReadInt16();
            }
            catch (EndOfStreamException)
            {
                this.state = System.Xml.ReadState.EndOfFile;
                this.token = XmlNodeTypeToken.EndDocument;
                return false;
            }

            if (this.isElementEmpty && this.token == XmlNodeTypeToken.EndElement)
            {
                this.PopElement();
                // have to swallow this end element.
                this.token = (XmlNodeTypeToken)this.reader.ReadInt16();
            }
            if (this.token == XmlNodeTypeToken.Document)
            {
                this.token = (XmlNodeTypeToken)this.reader.ReadInt16();
            }
            if (this.token == XmlNodeTypeToken.None || this.token == XmlNodeTypeToken.EndDocument)
            {
                this.nodeType = XmlNodeType.None;
                this.state = System.Xml.ReadState.EndOfFile;
                return false;
            }
            this.state = System.Xml.ReadState.Interactive;
            switch (this.token)
            {
                case XmlNodeTypeToken.Element: // begining of start tag
                    this.ReadElement();
                    break;
                case XmlNodeTypeToken.Text:
                    this.nodeType = XmlNodeType.Text;
                    this.value = this.reader.ReadString();
                    break;
                case XmlNodeTypeToken.CDATA:
                    this.nodeType = XmlNodeType.CDATA;
                    this.value = this.reader.ReadString();
                    break;
                case XmlNodeTypeToken.ProcessingInstruction:
                    this.nodeType = XmlNodeType.ProcessingInstruction;
                    this.currentElement = this.leafNode;
                    this.currentElement.Name = this.nameTable.Add(this.ReadString());
                    this.value = this.reader.ReadString();
                    break;
                case XmlNodeTypeToken.Comment:
                    this.nodeType = XmlNodeType.Comment;
                    this.currentElement = this.leafNode;
                    this.currentElement.Name = this.nameTable.Add("#comment");
                    this.value = this.reader.ReadString();
                    break;
                case XmlNodeTypeToken.DocumentType:
                    this.nodeType = XmlNodeType.DocumentType;
                    this.currentElement = new BinaryXmlElement();
                    this.currentElement.Name = this.nameTable.Add(this.reader.ReadString());
                    BinaryXmlAttribute pubid = this.currentElement.PushAttribute();
                    pubid.Name = "Public";
                    pubid.Value = this.reader.ReadString();
                    BinaryXmlAttribute sysid = this.currentElement.PushAttribute();
                    sysid.Name = "SystemLiteral";
                    sysid.Value = this.reader.ReadString();
                    BinaryXmlAttribute subset = this.currentElement.PushAttribute();
                    subset.Name = "InternalSubset";
                    subset.Value = this.reader.ReadString();
                    break;
                case XmlNodeTypeToken.Whitespace:
                case XmlNodeTypeToken.SignificantWhitespace:
                    this.nodeType = (XmlNodeType)this.token;
                    this.currentElement = this.leafNode;
                    this.currentElement.Name = this.nameTable.Add("#whitespace");
                    this.value = this.reader.ReadString();
                    break;
                case XmlNodeTypeToken.EndElement:
                    this.PopElement();
                    break;
                case XmlNodeTypeToken.Base64:
                    this.SetTextNode(this.ReadBase64());
                    return true;
                case XmlNodeTypeToken.RawText:
                case XmlNodeTypeToken.RawChars:
                case XmlNodeTypeToken.Chars:
                    this.SetTextNode(this.ReadRaw());
                    break;
                case XmlNodeTypeToken.Bool:
                    this.SetTextNode(this.reader.ReadBoolean());
                    break;
                case XmlNodeTypeToken.DateTime:
                    this.SetTextNode(new DateTime(this.reader.ReadInt64()));
                    break;
                case XmlNodeTypeToken.Decimal:
                    this.SetTextNode(this.reader.ReadDecimal());
                    break;
                case XmlNodeTypeToken.Double:
                    this.SetTextNode(this.reader.ReadDouble());
                    break;
                case XmlNodeTypeToken.Float:
                    this.SetTextNode(this.reader.ReadSingle());
                    break;
                case XmlNodeTypeToken.Int:
                    this.SetTextNode(this.reader.ReadInt32());
                    break;
                case XmlNodeTypeToken.Long:
                    this.SetTextNode(this.reader.ReadInt64());
                    break;
                default:
                    this.state = System.Xml.ReadState.Error;
                    throw new Exception(string.Format("Binary stream contains unpexpected token '{0}'", (int)this.token));
            }
            return true;
        }

        public override bool ReadAttributeValue()
        {
            if (this.token == XmlNodeTypeToken.Attribute && this.currentAttribute != null)
            {
                this.value = this.currentAttribute.Value;
                this.token = this.currentAttribute.Token;
                return true;
            }
            return false;
        }

        public override ReadState ReadState
        {
            get { return this.state; }
        }

        public override void ResolveEntity()
        {
            throw new NotImplementedException();
        }

        public override string Value
        {
            get
            {
                if (this.value == null)
                {
                    return null;
                }
                if (this.value is DateTime)
                {
                    return XmlConvert.ToString((DateTime)this.value, "yyyy-MM-ddTHH:mm:ss");
                }
                else if (this.value is Boolean)
                {
                    return XmlConvert.ToString((bool)this.value);
                }
                return this.value.ToString();
            }
        }

        public override object ReadContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver)
        {
            throw new NotImplementedException();
        }

        private int bufferPos;

        public override int ReadContentAsBase64(byte[] buffer, int index, int count)
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.Base64 && (this.value is byte[]))
            {
                byte[] data = (byte[])this.value;
                int available = data.Length - this.bufferPos;
                int returned = Math.Min(available, count);
                if (returned != 0)
                {
                    Array.Copy(data, this.bufferPos, buffer, index, returned);
                    this.bufferPos += returned;
                }
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return returned;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a Bas64 token");
            }
        }

        public override bool ReadContentAsBoolean()
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.Bool)
            {
                var rc = (bool)this.value;
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return rc;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a bool token");
            }
        }

        public override DateTime ReadContentAsDateTime()
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.DateTime)
            {
                var rc = (DateTime)this.value;
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return rc;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a DateTime token");
            }
        }

        public override decimal ReadContentAsDecimal()
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.Decimal)
            {
                var rc = (decimal)this.value;
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return rc;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a decimal token");
            }
        }

        public override double ReadContentAsDouble()
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.Double)
            {
                var rc = (double)this.value;
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return rc;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a double token");
            }
        }

        public override float ReadContentAsFloat()
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.Float)
            {
                var rc = (float)this.value;
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return rc;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a float token");
            }
        }

        public override int ReadContentAsInt()
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.Int)
            {
                var rc = (int)this.value;
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return rc;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a int token");
            }
        }

        public override long ReadContentAsLong()
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.Long)
            {
                var rc = (long)this.value;
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return rc;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a long token");
            }
        }

        public override object ReadContentAsObject()
        {
            throw new NotImplementedException();
        }

        public override string ReadContentAsString()
        {
            if (this.token == XmlNodeTypeToken.Attribute)
            {
                this.ReadAttributeValue();
            }
            if (this.token == XmlNodeTypeToken.Text)
            {
                var rc = (string)this.value;
                if (this.currentAttribute == null)
                {
                    this.Read();
                }
                return rc;
            }
            else
            {
                throw new InvalidOperationException("Reader is not positioned on a string token");
            }
        }


        public override void MoveToAttribute(int i)
        {
            if (this.token == XmlNodeTypeToken.Element && i < this.currentElement.AttributeCount)
            {
                this.SetCurrentAttribute(i);
            }
        }

        public override string Name
        {
            get
            {
                if (this.token == XmlNodeTypeToken.Attribute)
                {
                    return !string.IsNullOrEmpty(this.currentAttribute.Prefix) ? this.currentAttribute.Prefix + ":" + this.currentAttribute.Name : this.currentAttribute.Name;
                }
                else if (this.currentElement != null)
                {
                    return !string.IsNullOrEmpty(this.currentElement.Prefix) ? this.currentElement.Prefix + ":" + this.currentElement.Name : this.currentElement.Name;
                }
                return null;
            }
        }

        private void PopElement()
        {
            if (this.nodeType == XmlNodeType.Attribute)
            {
                this.elementDepth--;
            }
            this.nodeType = XmlNodeType.EndElement;
            this.elementDepth--;
            this.currentElement = (this.elementDepth >= 0) ? this.elementStack[this.elementDepth] : null;
            this.value = null;
            this.isElementEmpty = false;
        }

        private void SetTextNode(object value)
        {
            this.nodeType = XmlNodeType.Text;
            this.currentElement = this.leafNode;
            this.currentElement.Name = null;
            this.value = value;
        }

        private void ReadElement()
        {
            BinaryXmlElement e = this.PushElement();
            this.nodeType = XmlNodeType.Element;
            e.Prefix = this.nameTable.Add(this.reader.ReadString());
            e.Name = this.nameTable.Add(this.reader.ReadString());
            e.NamespaceUri = this.nameTable.Add(this.reader.ReadString());
            this.ReadAttributes(e);
            this.currentElement = e;
        }

        #region Attributes

        private void ReadAttributes(BinaryXmlElement e)
        {
            this.value = null;
            this.attributePos = -1;
            this.currentAttribute = null;
            e.AttributeCount = 0;
            XmlNodeTypeToken next = (XmlNodeTypeToken)this.reader.ReadInt16();
            while (next != XmlNodeTypeToken.EndStartTag && next != XmlNodeTypeToken.EmptyEndStartTag)
            {
                Debug.Assert(next == XmlNodeTypeToken.StartAttribute);
                BinaryXmlAttribute a = e.PushAttribute();
                a.Prefix = this.nameTable.Add(this.reader.ReadString());
                a.Name = this.nameTable.Add(this.reader.ReadString());
                a.Namespace = this.nameTable.Add(this.reader.ReadString());

                next = (XmlNodeTypeToken)this.reader.ReadInt16();
                while (next != XmlNodeTypeToken.EndAttribute)
                {
                    a.Token = next;
                    switch (next)
                    {
                        case XmlNodeTypeToken.Text:
                            a.Value = this.reader.ReadString();
                            break;
                        case XmlNodeTypeToken.Base64:
                            a.Value = this.ReadBase64();
                            break;
                        case XmlNodeTypeToken.RawText:
                            a.Value = this.ReadString();
                            break;
                        case XmlNodeTypeToken.RawChars:
                        case XmlNodeTypeToken.Chars:
                            a.Value = this.ReadRaw();
                            break;
                        case XmlNodeTypeToken.Bool:
                            a.Value = this.reader.ReadBoolean();
                            break;
                        case XmlNodeTypeToken.DateTime:
                            a.Value = new DateTime(this.reader.ReadInt64());
                            break;
                        case XmlNodeTypeToken.Decimal:
                            a.Value = this.reader.ReadDecimal();
                            break;
                        case XmlNodeTypeToken.Double:
                            a.Value = this.reader.ReadDouble();
                            break;
                        case XmlNodeTypeToken.Float:
                            a.Value = this.reader.ReadSingle();
                            break;
                        case XmlNodeTypeToken.Int:
                            a.Value = this.reader.ReadInt32();
                            break;
                        case XmlNodeTypeToken.Long:
                            a.Value = this.reader.ReadInt64();
                            break;
                        default:
                            throw new Exception(string.Format("Unexpected token '{0}' reading attributes", (int)next));
                    }

                    next = (XmlNodeTypeToken)this.reader.ReadInt16();
                }
                if (a.Prefix == "xmlns")
                {
                    a.Namespace = "http://www.w3.org/2000/xmlns/";
                    this.mgr.AddNamespace(a.Name, (string)a.Value);
                }
                else if (string.IsNullOrEmpty(a.Prefix) && a.Name == "xmlns")
                {
                    a.Namespace = "http://www.w3.org/2000/xmlns/";
                    this.mgr.AddNamespace(string.Empty, (string)a.Value);
                }

                next = (XmlNodeTypeToken)this.reader.ReadInt16();// consume EndAttribute
            }

            this.isElementEmpty = next == XmlNodeTypeToken.EmptyEndStartTag;
        }

        private byte[] ReadBase64()
        {
            this.bufferPos = 0;
            int count = this.reader.ReadInt32();
            char[] buffer = this.reader.ReadChars(count);
            return Convert.FromBase64CharArray(buffer, 0, count);
        }

        private char[] ReadRaw()
        {
            this.bufferPos = 0;
            int count = this.reader.ReadInt32();
            return this.reader.ReadChars(count);
        }

        // This stack operates as a high water mark kind of stack so we keep BinaryXmlElements allocated so we can reuse them efficiently.
        private BinaryXmlElement PushElement()
        {
            if (this.elementDepth < this.elementStack.Count)
            {
                return this.elementStack[this.elementDepth];
            }
            BinaryXmlElement e = new BinaryXmlElement();
            this.elementStack.Add(e);
            return e;
        }

        #endregion
    }
    #endregion 
}
