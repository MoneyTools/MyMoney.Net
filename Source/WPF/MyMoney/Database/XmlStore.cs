using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Security;
using System.IO.Compression;
using Walkabout.Utilities;
using System.Xml.Linq;
using System.Data;

namespace Walkabout.Data
{
    public class XmlStore : IDatabase
    {
        string filename;
        string backup;
        string password;

        public XmlStore(string filename, string password)
        {
            this.filename = filename;
            this.password = password;
        }

        public virtual bool SupportsUserLogin => false;
        public virtual string Server { get; set; }

        public virtual string DatabasePath { get { return filename; } }

        public virtual string ConnectionString { get { return null; } }

        public virtual string BackupPath { get { return backup; } set { backup = value; } }

        public virtual DbFlavor DbFlavor { get { return Data.DbFlavor.Xml; } }

        public virtual string UserId { get; set; }

        public virtual string Password
        {
            get { return password; }
            set { password = value; }
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
            if (Exists)
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
            if (!File.Exists(filename))
            {
                money = new MyMoney();
            }
            else
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();

                using (XmlReader r = XmlReader.Create(filename))
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
            result.ReadXml(filename);
            return result;
        }


        public virtual void Save(MyMoney money)
        {
            PrepareSave(money);

            Stopwatch watch = new Stopwatch();
            watch.Start();

            DataContractSerializer serializer = new DataContractSerializer(typeof(MyMoney));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(filename, settings))
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
            File.Copy(filename, path);
            this.backup = path;
        }
    }

    /// <summary>
    /// This class loads/saves the MyMoney objects to an encrypted binary XML file.
    /// </summary>
    public class BinaryXmlStore : XmlStore
    {
        string filename;

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
            if (!File.Exists(filename))
            {
                money = new MyMoney();
            }
            else
            {
                string path = this.filename;
                string tempPath = null;
                bool encrypted = !string.IsNullOrWhiteSpace(Password);

                if (encrypted)
                {
                    // Decrypt the file.
                    Encryption e = new Encryption();
                    tempPath = Path.GetTempFileName();
                    e.DecryptFile(this.filename, Password, tempPath);
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
            bool encrypted = !string.IsNullOrWhiteSpace(Password);

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
                e.EncryptFile(path, Password, this.filename);
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
        Stream stream;
        BinaryWriter writer;
        XmlNamespaceManager mgr;
        NameTable nameTable;
        WriteState state = WriteState.Start;

        #region Auto Namespaces

        class XmlNamespaceDefinition
        {
            public string Prefix;
            public string NamespaceUri;
        }

        List<XmlNamespaceDefinition> namespaceStack = new List<XmlNamespaceDefinition>();
        int namespacePos;

        void PushNewNamespace(string prefix, string namespaceUri)
        {
            XmlNamespaceDefinition def;
            if (namespacePos < namespaceStack.Count)
            {
                def = namespaceStack[namespacePos];
            }
            else
            {
                def = new XmlNamespaceDefinition();
                namespaceStack.Add(def);
            }
            def.Prefix = prefix;
            def.NamespaceUri = namespaceUri;
            namespacePos++;
        }

        void SetNamespaceDefined(string prefix, string nsuri)
        {
            for (int i = 0; i < namespacePos; i++)
            {
                XmlNamespaceDefinition def = namespaceStack[i];
                if (def.Prefix == prefix && def.NamespaceUri == nsuri)
                {
                    namespaceStack.RemoveAt(i);
                    return;
                }
            }
        }

        void WriteAutoNamespaces()
        {
            if (namespacePos > 0)
            {
                // need to write out some extra namespace definitions
                for (int i = 0; i < namespacePos; i++)
                {
                    XmlNamespaceDefinition d = namespaceStack[i];

                    writer.Write((short)XmlNodeTypeToken.StartAttribute);
                    if (string.IsNullOrEmpty(d.Prefix))
                    {
                        writer.Write(string.Empty);
                        writer.Write("xmlns");
                    }
                    else
                    {
                        writer.Write("xmlns");
                        writer.Write(d.Prefix);
                    }
                    writer.Write("http://www.w3.org/2000/xmlns/");

                    writer.Write((short)XmlNodeTypeToken.Text);
                    writer.Write(d.NamespaceUri);

                    writer.Write((short)XmlNodeTypeToken.EndAttribute);

                    mgr.AddNamespace(d.Prefix ?? string.Empty, d.NamespaceUri);
                }
                namespacePos = 0;
            }
        }
        #endregion

        public BinaryXmlWriter(string filename)
        {
            stream = new FileStream(filename, FileMode.OpenOrCreate | FileMode.Truncate, FileAccess.ReadWrite, FileShare.None);
            writer = new BinaryWriter(stream, Encoding.UTF8);
            nameTable = new NameTable();
            mgr = new XmlNamespaceManager(nameTable);
        }

        public BinaryXmlWriter(Stream stream)
        {
            this.stream = stream;
            writer = new BinaryWriter(stream, Encoding.UTF8);
            nameTable = new NameTable();
            mgr = new XmlNamespaceManager(nameTable);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            writer.Dispose();
            stream.Dispose();
        }

        public override void Close()
        {
            writer.Close();
            stream.Close();
            state = WriteState.Closed;
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override string LookupPrefix(string ns)
        {
            string result = mgr.LookupPrefix(nameTable.Add(ns));
            return result;
        }

        private void EndStartTag(bool isEmpty)
        {
            if (state == System.Xml.WriteState.Element)
            {
                WriteAutoNamespaces();
                state = System.Xml.WriteState.Content;
                writer.Write(isEmpty ? (short)XmlNodeTypeToken.EmptyEndStartTag : (short)XmlNodeTypeToken.EndStartTag);
            }
        }

        public override void WriteBase64(byte[] buffer, int index, int count)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            int outsize = ((count - index) * 4) / 3;
            char[] chars = new char[outsize + 4];
            Convert.ToBase64CharArray(buffer, index, count, chars, 0);
            writer.Write((short)XmlNodeTypeToken.Base64);
            writer.Write(count);
            writer.Write(chars, 0, count);
        }

        public override void WriteCData(string text)
        {
            EndStartTag(false);
            writer.Write((short)XmlNodeTypeToken.CDATA);
            writer.Write(text);
        }

        public override void WriteCharEntity(char ch)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.CharacterEntity);
            writer.Write(ch);
        }

        public override void WriteChars(char[] buffer, int index, int count)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.Chars);
            writer.Write(count);
            writer.Write(buffer, index, count);
        }

        public override void WriteComment(string text)
        {
            EndStartTag(false);
            state = System.Xml.WriteState.Content;
            writer.Write((short)XmlNodeTypeToken.Comment);
            writer.Write(text);
        }

        public override void WriteDocType(string name, string pubid, string sysid, string subset)
        {
            state = System.Xml.WriteState.Prolog;
            writer.Write((short)XmlNodeTypeToken.DocumentType);
            writer.Write(name);
            writer.Write(pubid);
            writer.Write(sysid);
            writer.Write(subset);
        }

        public override void WriteEndAttribute()
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                throw new Exception("Cannot write EndAttribute since we are not in 'Attribute' state");
            }
            writingNamespace = false;
            state = System.Xml.WriteState.Element;
            writer.Write((short)XmlNodeTypeToken.EndAttribute);
        }

        public override void WriteEndDocument()
        {
            EndStartTag(true);
            writer.Write((short)XmlNodeTypeToken.EndDocument);
        }

        public override void WriteEndElement()
        {
            EndStartTag(true);
            mgr.PopScope();
            writer.Write((short)XmlNodeTypeToken.EndElement);
        }

        public override void WriteEntityRef(string name)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.EntityReference);
            writer.Write(name);
        }

        public override void WriteFullEndElement()
        {
            EndStartTag(false);
            WriteEndElement();
        }

        public override void WriteProcessingInstruction(string name, string text)
        {
            EndStartTag(false);
            writer.Write((short)XmlNodeTypeToken.ProcessingInstruction);
            writer.Write(name);
            writer.Write(text);
        }

        public override void WriteRaw(string data)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.RawText);
            writer.Write(data);
        }

        public override void WriteRaw(char[] buffer, int index, int count)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.RawChars);
            writer.Write(count);
            writer.Write(buffer, index, count);
        }

        bool writingNamespace;
        string prefix;

        public override void WriteStartAttribute(string prefix, string localName, string ns)
        {
            writingNamespace = false;
            if (prefix == "xmlns")
            {
                writingNamespace = true;
                this.prefix = nameTable.Add(localName);
            }
            state = System.Xml.WriteState.Attribute;

            writer.Write((short)XmlNodeTypeToken.StartAttribute);
            writer.Write(prefix ?? string.Empty);
            writer.Write(localName ?? string.Empty);
            writer.Write(ns ?? string.Empty);
        }

        public override void WriteStartDocument(bool standalone)
        {
            state = System.Xml.WriteState.Prolog;
            writer.Write((short)XmlNodeTypeToken.Document);
        }

        public override void WriteStartDocument()
        {
            state = System.Xml.WriteState.Prolog;
            writer.Write((short)XmlNodeTypeToken.Document);
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            EndStartTag(false);
            state = System.Xml.WriteState.Element;
            writer.Write((short)XmlNodeTypeToken.Element);
            mgr.PushScope();
            if (!string.IsNullOrEmpty(ns) && prefix == null)
            {
                prefix = LookupPrefix(ns);
                if (prefix == null)
                {
                    PushNewNamespace(prefix, ns);
                }
            }
            writer.Write(prefix ?? string.Empty);
            writer.Write(localName ?? string.Empty);
            writer.Write(ns ?? string.Empty);
        }

        public override WriteState WriteState
        {
            get { return state; }
        }

        public override void WriteString(string text)
        {
            if (!string.IsNullOrWhiteSpace(text) || state == System.Xml.WriteState.Attribute)
            {
                if (writingNamespace)
                {
                    SetNamespaceDefined(prefix, text);
                    mgr.AddNamespace(prefix, nameTable.Add(text));
                }
                if (state != System.Xml.WriteState.Attribute)
                {
                    EndStartTag(false);
                }
                writer.Write((short)XmlNodeTypeToken.Text);
                writer.Write(text ?? string.Empty);
            }
        }

        public override void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.SurrogateCharEntity);
            writer.Write(lowChar);
            writer.Write(highChar);
        }

        public override void WriteWhitespace(string ws)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.Whitespace);
            writer.Write(ws ?? string.Empty);
        }

        public override void WriteValue(bool value)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.Bool);
            writer.Write(value);
        }

        public override void WriteValue(DateTime value)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.DateTime);
            writer.Write(value.Ticks);
        }

        public override void WriteValue(decimal value)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.Decimal);
            writer.Write(value);
        }

        public override void WriteValue(double value)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.Double);
            writer.Write(value);
        }
        public override void WriteValue(float value)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.Float);
            writer.Write(value);
        }

        public override void WriteValue(int value)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.Int);
            writer.Write(value);
        }

        public override void WriteValue(long value)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            writer.Write((short)XmlNodeTypeToken.Long);
            writer.Write(value);
        }

        public override void WriteValue(object value)
        {
            throw new NotImplementedException();
        }

        public override void WriteValue(string value)
        {
            if (state != System.Xml.WriteState.Attribute)
            {
                EndStartTag(false);
            }
            if (!string.IsNullOrWhiteSpace(value) || state == System.Xml.WriteState.Attribute)
            {
                writer.Write((short)XmlNodeTypeToken.Text);
                writer.Write(value ?? string.Empty);
            }
        }
    }

    public class BinaryXmlReader : XmlReader
    {
        string baseUri;
        Stream stream;
        BinaryXmlElement currentElement;
        int attributePos; // position in MoveToNextAttribute
        BinaryXmlAttribute currentAttribute; // in MoveToNextAttribute
        object value;
        List<BinaryXmlElement> elementStack = new List<BinaryXmlElement>();
        int elementDepth;
        bool isElementEmpty;
        BinaryReader reader;
        XmlNodeType nodeType = XmlNodeType.None;
        ReadState state = ReadState.Initial;
        XmlNodeTypeToken token = XmlNodeTypeToken.None;
        XmlNameTable nameTable = new NameTable();
        XmlNamespaceManager mgr;
        BinaryXmlElement leafNode = new BinaryXmlElement();

        class BinaryXmlAttribute
        {
            public string Prefix;
            public string Name;
            public string Namespace;
            public object Value;
            public XmlNodeTypeToken Token;
        }

        class BinaryXmlElement
        {
            public string Prefix;
            public string Name;
            public string NamespaceUri;
            public List<BinaryXmlAttribute> Attributes = new List<BinaryXmlAttribute>();
            public int AttributeCount; // actual number of attributes on current element.

            // This stack operates as a high water mark kind of stack so we keep BinaryXmlAttributes allocated so we can reuse them efficiently.
            public BinaryXmlAttribute PushAttribute()
            {
                if (this.AttributeCount < Attributes.Count)
                {
                    return Attributes[AttributeCount++];
                }
                BinaryXmlAttribute result = new BinaryXmlAttribute();
                Attributes.Add(result);
                AttributeCount++;
                return result;
            }
        }

        public BinaryXmlReader(string filename)
        {
            this.baseUri = filename;
            stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = new BinaryReader(stream, Encoding.UTF8);
            mgr = new XmlNamespaceManager(nameTable);
        }

        public BinaryXmlReader(Stream stream)
        {
            this.stream = stream;
            reader = new BinaryReader(stream, Encoding.UTF8);
            mgr = new XmlNamespaceManager(nameTable);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            reader.Dispose();
            stream.Dispose();
        }

        public override void Close()
        {
            reader.Close();
            stream.Close();
            state = ReadState.Closed;
        }

        public override int Depth
        {
            get { return elementDepth; }
        }

        public override bool EOF
        {
            get { return state == System.Xml.ReadState.EndOfFile; }
        }

        public override int AttributeCount
        {
            get { return currentElement != null ? currentElement.AttributeCount : 0; }
        }

        public override string BaseURI
        {
            get { return baseUri; }
        }

        public override string GetAttribute(int i)
        {
            if ((token == XmlNodeTypeToken.Element || this.currentAttribute != null) && i <= currentElement.AttributeCount)
            {
                return currentElement.Attributes[i].Value.ToString();
            }
            return null;
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            if ((token == XmlNodeTypeToken.Element || this.currentAttribute != null) && currentElement.AttributeCount > 0)
            {
                string tname = nameTable.Add(name);
                string tnamespace = nameTable.Add(namespaceURI ?? string.Empty);
                foreach (BinaryXmlAttribute a in currentElement.Attributes)
                {
                    if ((object)a.Name == (object)tname && (object)a.Namespace == (object)tnamespace)
                    {
                        return a.Value.ToString();
                    }
                }
            }
            return null;
        }

        public override string GetAttribute(string name)
        {
            if ((token == XmlNodeTypeToken.Element || this.currentAttribute != null) && currentElement.AttributeCount > 0)
            {
                string tname = nameTable.Add(name);
                foreach (BinaryXmlAttribute a in currentElement.Attributes)
                {
                    if ((object)a.Name == (object)tname)
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
                return isElementEmpty;
            }
        }

        public override string LocalName
        {
            get
            {
                if (token == XmlNodeTypeToken.Attribute)
                {
                    return this.currentAttribute.Name;
                }
                return this.currentElement == null ? null : this.currentElement.Name;
            }
        }

        public override string LookupNamespace(string prefix)
        {
            return mgr.LookupNamespace(prefix);
        }

        public override bool MoveToAttribute(string name, string namespaceURI)
        {
            if ((token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.currentElement.AttributeCount > 0)
            {
                string tname = nameTable.Add(name);
                string tnamespace = nameTable.Add(namespaceURI ?? string.Empty);
                int i = 0;
                foreach (BinaryXmlAttribute a in currentElement.Attributes)
                {
                    if ((object)a.Name == (object)tname && (object)a.Namespace == (object)tnamespace)
                    {
                        SetCurrentAttribute(i);
                        return true;
                    }
                    i++;
                }
            }
            return false;
        }

        private string SetCurrentAttribute(int i)
        {
            Debug.Assert(currentElement != null);
            if (token == XmlNodeTypeToken.Element)
            {
                elementDepth++;
            }
            this.attributePos = i;
            this.currentAttribute = this.currentElement.Attributes[i];
            value = this.currentAttribute.Value;
            token = XmlNodeTypeToken.Attribute;
            nodeType = XmlNodeType.Attribute;
            return this.currentAttribute.Name;
        }


        public override bool MoveToAttribute(string name)
        {
            if ((token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.currentElement.AttributeCount > 0)
            {
                string tname = nameTable.Add(name);
                int i = 0;
                foreach (BinaryXmlAttribute a in this.currentElement.Attributes)
                {
                    if ((object)a.Name == (object)tname)
                    {
                        SetCurrentAttribute(i);
                        return true;
                    }
                    i++;
                }
            }
            return false;
        }

        public override bool MoveToElement()
        {
            if (token == XmlNodeTypeToken.EndElement || state == ReadState.Initial || token == XmlNodeTypeToken.Element)
            {
                return false;
            }
            if (this.currentAttribute != null)
            {
                elementDepth--;
                this.currentAttribute = null;
                token = XmlNodeTypeToken.Element;
                nodeType = XmlNodeType.Element;
                return true;
            }

            while (Read())
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
            if ((token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.currentElement.AttributeCount > 0)
            {
                SetCurrentAttribute(0);
                return true;
            }
            return false;
        }

        public override bool MoveToNextAttribute()
        {
            if ((token == XmlNodeTypeToken.Element || this.currentAttribute != null) && this.attributePos + 1 < this.currentElement.AttributeCount)
            {
                this.attributePos++;
                SetCurrentAttribute(this.attributePos);
                return true;
            }
            return false;
        }

        public override XmlNameTable NameTable
        {
            get { return nameTable; }
        }

        public override string NamespaceURI
        {
            get
            {
                if (token == XmlNodeTypeToken.Attribute)
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
                if (token == XmlNodeTypeToken.Attribute)
                {
                    return this.currentAttribute.Prefix;
                }
                return this.currentElement == null ? null : this.currentElement.Prefix;
            }
        }

        public override bool Read()
        {
            if (state == System.Xml.ReadState.EndOfFile || state == System.Xml.ReadState.Error || state == System.Xml.ReadState.Closed)
            {
                return false;
            }

            if (nodeType == XmlNodeType.Element || nodeType == XmlNodeType.Attribute)
            {
                elementDepth++;
            }

            currentElement = null;
            XmlNodeTypeToken previous = token;
            try
            {
                token = (XmlNodeTypeToken)reader.ReadInt16();
            }
            catch (EndOfStreamException)
            {
                state = System.Xml.ReadState.EndOfFile;
                token = XmlNodeTypeToken.EndDocument;
                return false;
            }

            if (isElementEmpty && token == XmlNodeTypeToken.EndElement)
            {
                PopElement();
                // have to swallow this end element.
                token = (XmlNodeTypeToken)reader.ReadInt16();
            }
            if (token == XmlNodeTypeToken.Document)
            {
                token = (XmlNodeTypeToken)reader.ReadInt16();
            }
            if (token == XmlNodeTypeToken.None || token == XmlNodeTypeToken.EndDocument)
            {
                nodeType = XmlNodeType.None;
                state = System.Xml.ReadState.EndOfFile;
                return false;
            }
            state = System.Xml.ReadState.Interactive;
            switch (token)
            {
                case XmlNodeTypeToken.Element: // begining of start tag
                    ReadElement();
                    break;
                case XmlNodeTypeToken.Text:
                    nodeType = XmlNodeType.Text;
                    value = reader.ReadString();
                    break;
                case XmlNodeTypeToken.CDATA:
                    nodeType = XmlNodeType.CDATA;
                    value = reader.ReadString();
                    break;
                case XmlNodeTypeToken.ProcessingInstruction:
                    nodeType = XmlNodeType.ProcessingInstruction;
                    currentElement = leafNode;
                    currentElement.Name = nameTable.Add(ReadString());
                    value = reader.ReadString();
                    break;
                case XmlNodeTypeToken.Comment:
                    nodeType = XmlNodeType.Comment;
                    currentElement = leafNode;
                    currentElement.Name = nameTable.Add("#comment");
                    value = reader.ReadString();
                    break;
                case XmlNodeTypeToken.DocumentType:
                    this.nodeType = XmlNodeType.DocumentType;
                    currentElement = new BinaryXmlElement();
                    currentElement.Name = nameTable.Add(reader.ReadString());
                    BinaryXmlAttribute pubid = currentElement.PushAttribute();
                    pubid.Name = "Public";
                    pubid.Value = reader.ReadString();
                    BinaryXmlAttribute sysid = currentElement.PushAttribute();
                    sysid.Name = "SystemLiteral";
                    sysid.Value = reader.ReadString();
                    BinaryXmlAttribute subset = currentElement.PushAttribute();
                    subset.Name = "InternalSubset";
                    subset.Value = reader.ReadString();
                    break;
                case XmlNodeTypeToken.Whitespace:
                case XmlNodeTypeToken.SignificantWhitespace:
                    nodeType = (XmlNodeType)token;
                    currentElement = leafNode;
                    currentElement.Name = nameTable.Add("#whitespace");
                    value = reader.ReadString();
                    break;
                case XmlNodeTypeToken.EndElement:
                    PopElement();
                    break;
                case XmlNodeTypeToken.Base64:
                    SetTextNode(ReadBase64());
                    return true;
                case XmlNodeTypeToken.RawText:
                case XmlNodeTypeToken.RawChars:
                case XmlNodeTypeToken.Chars:
                    SetTextNode(ReadRaw());
                    break;
                case XmlNodeTypeToken.Bool:
                    SetTextNode(reader.ReadBoolean());
                    break;
                case XmlNodeTypeToken.DateTime:
                    SetTextNode(new DateTime(reader.ReadInt64()));
                    break;
                case XmlNodeTypeToken.Decimal:
                    SetTextNode(reader.ReadDecimal());
                    break;
                case XmlNodeTypeToken.Double:
                    SetTextNode(reader.ReadDouble());
                    break;
                case XmlNodeTypeToken.Float:
                    SetTextNode(reader.ReadSingle());
                    break;
                case XmlNodeTypeToken.Int:
                    SetTextNode(reader.ReadInt32());
                    break;
                case XmlNodeTypeToken.Long:
                    SetTextNode(reader.ReadInt64());
                    break;
                default:
                    state = System.Xml.ReadState.Error;
                    throw new Exception(string.Format("Binary stream contains unpexpected token '{0}'", (int)token));
            }
            return true;
        }

        public override bool ReadAttributeValue()
        {
            if (this.token == XmlNodeTypeToken.Attribute && currentAttribute != null)
            {
                this.value = currentAttribute.Value;
                this.token = currentAttribute.Token;
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
                if (value is DateTime)
                {
                    return XmlConvert.ToString((DateTime)value, "yyyy-MM-ddTHH:mm:ss");
                }
                else if (value is Boolean)
                {
                    return XmlConvert.ToString((bool)value);
                }
                return value.ToString();
            }
        }

        public override object ReadContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver)
        {
            throw new NotImplementedException();
        }

        int bufferPos;

        public override int ReadContentAsBase64(byte[] buffer, int index, int count)
        {
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.Base64 && (value is byte[]))
            {
                byte[] data = (byte[])value;
                int available = data.Length - bufferPos;
                int returned = Math.Min(available, count);
                if (returned != 0)
                {
                    Array.Copy(data, bufferPos, buffer, index, returned);
                    bufferPos += returned;
                }
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.Bool)
            {
                var rc = (bool)value;
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.DateTime)
            {
                var rc = (DateTime)value;
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.Decimal)
            {
                var rc = (decimal)value;
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.Double)
            {
                var rc = (double)value;
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.Float)
            {
                var rc = (float)value;
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.Int)
            {
                var rc = (int)value;
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.Long)
            {
                var rc = (long)value;
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Attribute)
            {
                ReadAttributeValue();
            }
            if (token == XmlNodeTypeToken.Text)
            {
                var rc = (string)value;
                if (currentAttribute == null)
                {
                    Read();
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
            if (token == XmlNodeTypeToken.Element && i < this.currentElement.AttributeCount)
            {
                SetCurrentAttribute(i);
            }
        }

        public override string Name
        {
            get
            {
                if (token == XmlNodeTypeToken.Attribute)
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
            currentElement = (this.elementDepth >= 0) ? this.elementStack[this.elementDepth] : null;
            value = null;
            isElementEmpty = false;
        }

        private void SetTextNode(object value)
        {
            nodeType = XmlNodeType.Text;
            currentElement = leafNode;
            currentElement.Name = null;
            this.value = value;
        }

        private void ReadElement()
        {
            BinaryXmlElement e = PushElement();
            this.nodeType = XmlNodeType.Element;
            e.Prefix = nameTable.Add(reader.ReadString());
            e.Name = nameTable.Add(reader.ReadString());
            e.NamespaceUri = nameTable.Add(reader.ReadString());
            ReadAttributes(e);
            currentElement = e;
        }

        #region Attributes

        private void ReadAttributes(BinaryXmlElement e)
        {
            value = null;
            this.attributePos = -1;
            this.currentAttribute = null;
            e.AttributeCount = 0;
            XmlNodeTypeToken next = (XmlNodeTypeToken)reader.ReadInt16();
            while (next != XmlNodeTypeToken.EndStartTag && next != XmlNodeTypeToken.EmptyEndStartTag)
            {
                Debug.Assert(next == XmlNodeTypeToken.StartAttribute);
                BinaryXmlAttribute a = e.PushAttribute();
                a.Prefix = nameTable.Add(reader.ReadString());
                a.Name = nameTable.Add(reader.ReadString());
                a.Namespace = nameTable.Add(reader.ReadString());

                next = (XmlNodeTypeToken)reader.ReadInt16();
                while (next != XmlNodeTypeToken.EndAttribute)
                {
                    a.Token = next;
                    switch (next)
                    {
                        case XmlNodeTypeToken.Text:
                            a.Value = reader.ReadString();
                            break;
                        case XmlNodeTypeToken.Base64:
                            a.Value = ReadBase64();
                            break;
                        case XmlNodeTypeToken.RawText:
                            a.Value = ReadString();
                            break;
                        case XmlNodeTypeToken.RawChars:
                        case XmlNodeTypeToken.Chars:
                            a.Value = ReadRaw();
                            break;
                        case XmlNodeTypeToken.Bool:
                            a.Value = reader.ReadBoolean();
                            break;
                        case XmlNodeTypeToken.DateTime:
                            a.Value = new DateTime(reader.ReadInt64());
                            break;
                        case XmlNodeTypeToken.Decimal:
                            a.Value = reader.ReadDecimal();
                            break;
                        case XmlNodeTypeToken.Double:
                            a.Value = reader.ReadDouble();
                            break;
                        case XmlNodeTypeToken.Float:
                            a.Value = reader.ReadSingle();
                            break;
                        case XmlNodeTypeToken.Int:
                            a.Value = reader.ReadInt32();
                            break;
                        case XmlNodeTypeToken.Long:
                            a.Value = reader.ReadInt64();
                            break;
                        default:
                            throw new Exception(string.Format("Unexpected token '{0}' reading attributes", (int)next));
                    }

                    next = (XmlNodeTypeToken)reader.ReadInt16();
                }
                if (a.Prefix == "xmlns")
                {
                    a.Namespace = "http://www.w3.org/2000/xmlns/";
                    mgr.AddNamespace(a.Name, (string)a.Value);
                }
                else if ((string.IsNullOrEmpty(a.Prefix) && a.Name == "xmlns"))
                {
                    a.Namespace = "http://www.w3.org/2000/xmlns/";
                    mgr.AddNamespace(string.Empty, (string)a.Value);
                }

                next = (XmlNodeTypeToken)reader.ReadInt16();// consume EndAttribute
            }

            this.isElementEmpty = (next == XmlNodeTypeToken.EmptyEndStartTag);
        }

        private byte[] ReadBase64()
        {
            bufferPos = 0;
            int count = reader.ReadInt32();
            char[] buffer = reader.ReadChars(count);
            return Convert.FromBase64CharArray(buffer, 0, count);
        }

        private char[] ReadRaw()
        {
            bufferPos = 0;
            int count = reader.ReadInt32();
            return reader.ReadChars(count);
        }

        // This stack operates as a high water mark kind of stack so we keep BinaryXmlElements allocated so we can reuse them efficiently.
        private BinaryXmlElement PushElement()
        {
            if (elementDepth < elementStack.Count)
            {
                return elementStack[elementDepth];
            }
            BinaryXmlElement e = new BinaryXmlElement();
            elementStack.Add(e);
            return e;
        }

        #endregion
    }
    #endregion 
}
