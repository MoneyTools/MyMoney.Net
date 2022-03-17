using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Data;
using Walkabout.Interfaces.Views;
using Walkabout.StockQuotes;
using Walkabout.Utilities;
using Walkabout.Views;

namespace Walkabout.Configuration
{

    //==========================================================================================
    /// <summary>
    /// This class encapsulates the bag of settings used by various components in this application
    /// and knows how to serialize and deserialize them between application sessions.
    /// </summary>
    public class Settings : IXmlSerializable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        //
        // This is the global singleton Settings instance used by the everywhere in MyMoney
        // it is initialized by App.Xaml.cs
        //
        public static Settings TheSettings { get; set; }

        private bool persist;

        #region PROPERTIES

        public Point WindowLocation
        {
            get {
                object value = map["WindowLocation"];
                return value is Point ? (Point)value : new Point(0, 0);
            }
            set { 
                map["WindowLocation"] = value;
                OnPropertyChanged("WindowLocation");
            }
        }

        public Size WindowSize
        {
            get
            {
                object value = map["WindowSize"];
                return value is Size ? (Size)value : new Size(0, 0);
            }
            set { 
                map["WindowSize"] = value;
                OnPropertyChanged("WindowSize");
            }
        }

        public Size AttachmentDialogSize
        {
            get
            {
                object value = map["ReceiptDialogSize"];
                if (value != null)
                {
                    // convert to new name
                    map["AttachmentDialogSize"] = value;
                    map.Remove("ReceiptDialogSize");
                }
                value = map["AttachmentDialogSize"];
                return value is Size ? (Size)value : new Size(0, 0);
            }
            set { 
                map["AttachmentDialogSize"] = value;
                OnPropertyChanged("AttachmentDialogSize");
            }
        }

        public string AttachmentDirectory
        {
            get
            {
                object value = map["Receipts"]; 
                if (value != null)
                {
                    // convert to new name
                    map["AttachmentDirectory"] = value;
                    map.Remove("Receipts");
                }
                value = map["AttachmentDirectory"];
                return value is string ? (string)value : null;
            }
            set { 
                map["AttachmentDirectory"] = value;
                OnPropertyChanged("AttachmentDirectory");
            }
        }

        public string[] RecentFiles
        {
            get
            {
                object value = map["RecentFiles"];
                return value is string[] ? (string[])value : null;
            }
            set { 
                map["RecentFiles"] = value;
                OnPropertyChanged("RecentFiles");
            }
        }

        public int ToolBoxWidth
        {
            get
            {
                object value = map["ToolBoxWidth"];
                return value is int ? (int)value : 0;
            }
            set { 
                map["ToolBoxWidth"] = value;
                OnPropertyChanged("ToolBoxWidth");
            }
        }

        public int GraphHeight
        {
            get
            {
                object value = map["GraphHeight"];
                return value is int ? (int)value : 0;
            }
            set { 
                map["GraphHeight"] = value;
                OnPropertyChanged("GraphHeight");
            }
        }

        /// <summary>
        /// The current database (older ones are listed in RecentFiles)
        /// </summary>
        public string Database
        {
            get
            {
                object value = map["Database"];
                return value is string ? (string)value : null;
            }
            set { 
                map["Database"] = value;
            }
        }

        public int FiscalYearStart
        {
            get
            {
                object value = map["FiscalYearStart"];
                return value is int i ? i : 0;
            }
            set
            {
                map["FiscalYearStart"] = value;
                OnPropertyChanged("FiscalYearStart");
            }
        }

        public string Connection
        {
            set 
            { 
                // Migrate old setting to new separate fields.
                ParseConnectionString(value);
            }
        }

        private void ParseConnectionString(string constr)
        {
            if (string.IsNullOrWhiteSpace(constr))
            {
                return;
            }
            string[] parts = constr.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in parts)
            {
                int i = s.IndexOf('=');
                if (i > 0) 
                {
                    string name = s.Substring(0, i).Trim();
                    string value = s.Substring(i + 1);
                    if (string.Compare(name, "Data Source", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        Server = value;
                    }
                    else if (string.Compare(name, "Initial Catalog", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        Database = value;
                    }
                    else if (string.Compare(name, "DataSource", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // CE doesn't have separate Server string, the DataSource is the path to the database.
                        Database = value;
                    }
                    else if (string.Compare(name, "User Id", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        this.UserId = value;
                    }
                }
            }
        }

        public string Server
        {
            get
            {
                object value = map["Server"];
                return value is string ? (string)value : null;
            }
            set { 
                map["Server"] = value; 
            }
        }

        public string BackupPath
        {
            get
            {
                object value = map["BackupPath"];
                return value is string ? (string)value : null;
            }
            set { 
                map["BackupPath"] = value;
                OnPropertyChanged("BackupPath");
            }
        }

        public string UserId
        {
            get
            {
                object value = map["UserId"];
                return value is string ? (string)value : null;
            }
            set { 
                map["UserId"] = value;
                OnPropertyChanged("UserId");
            }
        }

        public bool DisplayClosedAccounts
        {
            get
            {
                object value = map["DisplayClosedAccounts"];
                return value is bool ? (bool)value : false;
            }
            set { 
                map["DisplayClosedAccounts"] = value;
                OnPropertyChanged("DisplayClosedAccounts");
            }
        }

        public bool PlaySounds
        {
            get
            {
                object value = map["PlaySounds"];
                return value is bool ? (bool)value : false;
            }
            set { 
                map["PlaySounds"] = value;
                OnPropertyChanged("PlaySounds");
            }
        }

        public bool RentalManagement
        {
            get
            {
                object value = map["RentalManagement"];
                return value is bool ? (bool)value : false;
            }
            set { 
                map["RentalManagement"] = value;
                OnPropertyChanged("RentalManagement");
            }
        }

        public List<StockServiceSettings> StockServiceSettings
        {
            get
            {
                object value = map["StockServiceSettings"];
                return value is List<StockServiceSettings> ? (List<StockServiceSettings>)value : null;
            }
            set
            {
                map["StockServiceSettings"] = value;
                OnPropertyChanged("StockServiceSettings");
            }
        }

        public QueryRow[] Query
        {
            get
            {
                object value = map["Query"];
                return value is QueryRow[] ? (QueryRow[])value : null;
            }
            set { 
                map["Query"] = value;
                OnPropertyChanged("Query");
            }
        }
        
        public GraphState GraphState
        {
            get
            {
                object value = map["GraphState"];
                return value is GraphState ? (GraphState)value : null;
            }
            set { 
                map["GraphState"] = value;
                OnPropertyChanged("GraphState");
            }
        }

        public DateTime StartDate
        {
            get
            {
                object value = map["StartDate"];
                return value is DateTime ? (DateTime)value : DateTime.MinValue;
            }
            set { map["StartDate"] = value; }
        }
        
        public DateTime EndDate
        {
            get
            {
                object value = map["EndDate"];
                return value is DateTime ? (DateTime)value : DateTime.MinValue;
            }
            set { map["EndDate"] = value; }
        }
        
        public DateTime LastStockRequest
        {
            get
            {
                object value = map["LastStockRequest"];
                return value is DateTime ? (DateTime)value : DateTime.MinValue;
            }
            set { 
                map["LastStockRequest"] = value;
                OnPropertyChanged("LastStockRequest");
            }
        }

        
        public string Theme
        {
            get
            {
                object value = map["Theme"];
                return value is string ? (string)value : null;
            }
            set {
                map["Theme"] = value;
                OnPropertyChanged("Theme");
            }
        }

        public string ExeVersion
        {
            get
            {
                object value = map["ExeVersion"];
                return value is string ? (string)value : null;
            }
            set
            {
                map["ExeVersion"] = value; 
            }
        }

        public DateTime LastExeTimestamp
        {
            get
            {
                object value = map["LastExeTimestamp"];
                return value is DateTime ? (DateTime)value : DateTime.MinValue;
            }
            set
            {
                map["LastExeTimestamp"] = value;
            }
        }

        /// <summary>
        /// The range of dates to go back and look for a duplicate
        /// </summary>
        public TimeSpan DuplicateRange
        {
            get
            {
                object value = map["DuplicateRange"];
                return value is TimeSpan ? (TimeSpan)value : TimeSpan.FromDays(60);
            }
            set { 
                map["DuplicateRange"] = value;
                OnPropertyChanged("DuplicateRange");
            }
        }
        
        #endregion

        public Settings(bool save)
        {
            StartDate = DateTime.Now;
            EndDate = DateTime.Now;
            this.persist = save;
        }


        string _fileName;

        public string ConfigFile
        {
            get { return _fileName; }
            set { _fileName = value; }
        }

        XmlDocument doc = new XmlDocument();

        Hashtable map = new Hashtable();

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public object this[string key]
        {
            get {  return map[key];  }
            set {  map[key] = value;  }
        }

        public void Read(string path)
        {
            using (XmlTextReader r = new XmlTextReader(path))
            {
                ReadXml(r);
            }
        }

        public bool Persist { get { return this.persist; } }

        public void Write(string path)
        {
            using (XmlTextWriter w = new XmlTextWriter(path, Encoding.UTF8))
            {
                w.Formatting = Formatting.Indented;
                WriteXml(w);
            }
        }

        public void ReadXml(XmlReader r)
        {
            r.MoveToContent();
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.Element && !r.IsEmptyElement)
                {
                    PropertyInfo pi = this.GetType().GetProperty(r.Name);
                    if (pi != null)
                    {
                        Type t = pi.PropertyType;
                        if (t == typeof(Point))
                        {
                            pi.SetValue(this, DeserializePoint(r), null);
                        }
                        else if (t == typeof(Size))
                        {
                            pi.SetValue(this, DeserializeSize(r), null);
                        }
                        else if (t == typeof(int))
                        {
                            pi.SetValue(this, Int32.Parse(r.ReadString()), null);
                        }
                        else if (t == typeof(string))
                        {
                            pi.SetValue(this, r.ReadString(), null);
                        }
                        else if (t == typeof(string[]))
                        {
                            List<string> items = new List<string>();
                            while (r.Read() && r.NodeType != XmlNodeType.EndElement)
                            {
                                if (r.NodeType == XmlNodeType.Element && r.LocalName == "item")
                                {
                                    if (!r.IsEmptyElement)
                                    {
                                        var value = r.ReadElementContentAsString();
                                        items.Add(value);
                                    }
                                }
                            }
                            pi.SetValue(this, items.ToArray(), null);
                        }
                        else if (t == typeof(XmlElement))
                        {
                            pi.SetValue(this, (XmlElement)doc.ReadNode(r), null);
                        }
                        else if (t == typeof(GraphState))
                        {
                            pi.SetValue(this, GraphState.ReadFrom(r), null);
                        }
                        else if (t == typeof(DateTime))
                        {
                            pi.SetValue(this, DeserializeDateTime(r), null);
                        }
                        else if (t == typeof(TimeSpan))
                        {
                            pi.SetValue(this, DeserializeTimeSpan(r), null);
                        }
                        else if (t == typeof(bool))
                        {
                            pi.SetValue(this, bool.Parse(r.ReadString()), null);
                        }
                        else if (t == typeof(QueryRow[]))
                        {
                            pi.SetValue(this, ReadQuery(r), null);
                        }
                        else if (t == typeof(List<StockServiceSettings>))
                        {
                            StockServiceSettings s = new StockServiceSettings();
                            s.Deserialize(r);
                            if (this.StockServiceSettings == null)
                            {
                                this.StockServiceSettings = new List<StockServiceSettings>();
                            }
                            this.StockServiceSettings.Add(s);
                        }
                        else
                        {
                            throw new Exception("Settings.ReadXml encountered unsupported property type '" + t.FullName + "'");
                        }
                    }
                    else if (r.Name == "ViewState")
                    {
                        // backwards compat for old TransactionViewState slot.
                        this.SetViewStateNode(typeof(TransactionsView), (XmlElement)doc.ReadNode(r));
                    }
                    else if (r.Name == "Password")
                    {
                        // removing passwords from the settings file!
                    }
                    else
                    {
                        string viewType = r.GetAttribute("ViewType");
                        if (!string.IsNullOrWhiteSpace(viewType))
                        {
                            Type t = typeof(Settings).Assembly.GetType(viewType);
                            if (t != null && typeof(IView).IsAssignableFrom(t))
                            {
                                // special case...we don't know how to deserialize these so we leave it for later.
                                this.SetViewStateNode(t, (XmlElement)doc.ReadNode(r));
                            }
                        }
                        else
                        {
                            map[r.Name] = r.ReadString();
                        }
                    }
                }
            }
        }

        public static QueryRow[] ReadQuery(XmlReader r)
        {
            ArrayList list = new ArrayList();
            if (r.IsEmptyElement) return null;
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "QueryRow")
                    {
                        list.Add(QueryRow.ReadQuery(r));
                    }
                }
            }
            return (QueryRow[])list.ToArray(typeof(QueryRow));
        }

        public static QueryRow[] Deserialize(string xml)
        {
            QueryRow[] rows = null;
            using (StringReader sr = new StringReader(xml))
            {
                using (XmlTextReader r = new XmlTextReader(sr))
                {
                    if (r.IsStartElement("Query"))
                    {
                        rows = ReadQuery(r);
                    }
                    r.Close();
                }
            }
            return rows;
        }

        public void WriteXml(XmlWriter w)
        {
            w.WriteStartDocument();
            w.WriteStartElement("Settings");
            ArrayList keys = new ArrayList(map.Keys);
            keys.Sort();
            foreach (string key in keys)
            {
                object value = map[key];
                if (value == null) continue;

                PropertyInfo pi = this.GetType().GetProperty(key);
                if (pi != null)
                {
                    Type t = pi.PropertyType;
                    if (t == typeof(Point))
                    {
                        w.WriteStartElement(key);
                        SerializePoint(w, (Point)value);
                        w.WriteEndElement();
                    }
                    else if (t == typeof(Size))
                    {
                        SerializeSize(w, key, (Size)value);
                    }
                    else if (t == typeof(int))
                    {
                        w.WriteElementString(key, ((int)value).ToString());
                    }
                    else if (t == typeof(string))
                    {
                        w.WriteElementString(key, ((string)value));
                    }
                    else if (t == typeof(string[]))
                    {
                        w.WriteStartElement(key);
                        string[] values = (string[])value;
                        foreach (var item in values)
                        {
                            w.WriteElementString("item", item);
                        }
                        w.WriteEndElement();
                    }
                    else if (t == typeof(GraphState))
                    {
                        w.WriteStartElement(key);
                        this.GraphState.WriteXml(w);
                        w.WriteEndElement();
                    }
                    else if (t == typeof(DateTime))
                    {
                        SerializeDateTime(w, key, (DateTime)value);
                    }
                    else if (t == typeof(TimeSpan))
                    {
                        SerializeTimeSpan(w, key, (TimeSpan)value);
                    }
                    else if (t == typeof(bool))
                    {
                        w.WriteElementString(key, ((bool)value).ToString());
                    }
                    else if (t == typeof(QueryRow[]))
                    {
                        w.WriteStartElement(key);
                        WriteQuery(w, (QueryRow[])value);
                        w.WriteEndElement();
                    }
                    else if (t == typeof(List<StockServiceSettings>))
                    {
                        List<StockServiceSettings> s = (List<StockServiceSettings>)value;
                        foreach (var item in s)
                        {
                            w.WriteStartElement("StockServiceSettings");
                            item.Serialize(w);
                            w.WriteEndElement();
                        }
                    }
                    else
                    {
                        throw new Exception("Settings.ReadXml encountered unsupported property type '" + t.FullName + "'");
                    }
                }
                else if (value is IXmlSerializable)
                {
                    IXmlSerializable s = (IXmlSerializable)value;
                    if (s != null)
                    {
                        w.WriteStartElement(key);
                        s.WriteXml(w);
                        w.WriteEndElement();
                    }
                } 
                else 
                {
                    w.WriteElementString(key, value.ToString());
                }
            }

            HashSet<Type> saved = new HashSet<Type>();
            foreach (KeyValuePair<Type, ViewState> pair in this.viewStates)
            {
                Type t = pair.Key;
                ViewState s = pair.Value;
                if (s != null)
                {
                    saved.Add(t);
                    string key = s.GetType().FullName;
                    w.WriteStartElement(key);
                    w.WriteAttributeString("ViewType", t.FullName);
                    s.WriteXml(w);
                    w.WriteEndElement();
                }
            }

            foreach (KeyValuePair<Type, XmlElement> pair in this.viewStateNodes)
            {
                Type t = pair.Key;
                if (!saved.Contains(t))
                {
                    // this view type was not used, but we need to round trip the previous settings so we don't lose them.
                    XmlElement e = pair.Value;
                    if (e != null)
                    {
                        e.WriteTo(w);
                    }
                }
            }

            w.WriteEndElement();
            w.WriteEndDocument();
        }



        public static void WriteQuery(XmlWriter w, QueryRow[] query)
        {
            if (query != null)
            {
                foreach (QueryRow r in query)
                {
                    w.WriteStartElement("QueryRow");
                    r.WriteXml(w);
                    w.WriteEndElement();
                }
            }
        }


        static private Point DeserializePoint(XmlReader r)
        {
            Point p = new Point();
            if (r.IsEmptyElement) return p;
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "X")
                    {
                        p.X = Int32.Parse(r.ReadString());
                    }
                    else if (r.Name == "Y")
                    {
                        p.Y = Int32.Parse(r.ReadString());
                    }
                }
            }
            return p;
        }

        static private Size DeserializeSize(XmlReader r)
        {
            Size s = new Size();
            if (r.IsEmptyElement) return s;
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "Width")
                    {
                        string temp = r.ReadString();
                        double w = 0;
                        if (double.TryParse(temp, out w)) 
                        {
                            s.Width = (int)w;
                        }
                    }
                    else if (r.Name == "Height")
                    {
                        string temp = r.ReadString();
                        double h = 0;
                        if (double.TryParse(temp, out h))
                        {
                            s.Height = (int)h;
                        }
                    }
                }
            }
            return s;
        }

        static private DateTime DeserializeDateTime(XmlReader r)
        {
            if (r.IsEmptyElement) return new DateTime();
            if (r.Read())
            {
                string s = r.ReadString();
                try
                {
                    DateTime dt = XmlConvert.ToDateTime(s, XmlDateTimeSerializationMode.Utc);
                    return dt.ToLocalTime();
                }
                catch
                {
                    try
                    {
                        // backwards compatibility.
                        return DateTime.Parse(s);
                    }
                    catch
                    {
                    }
                }
            }
            return new DateTime();
        }

        static private TimeSpan DeserializeTimeSpan(XmlReader r)
        {
            if (r.IsEmptyElement) return new TimeSpan();
            if (r.Read())
            {
                string s = r.ReadString();
                try
                {
                    return XmlConvert.ToTimeSpan(s);
                }
                catch
                {
                }
            }
            return new TimeSpan();
        }
        static private void SerializePoint(XmlWriter w, Point p)
        {
            w.WriteElementString("X", p.X.ToString());
            w.WriteElementString("Y", p.Y.ToString());
        }

        static private void SerializeSize(XmlWriter w, string name, Size s)
        {
            w.WriteStartElement(name);
            w.WriteElementString("Width", s.Width.ToString());
            w.WriteElementString("Height", s.Height.ToString());
            w.WriteEndElement();
        }

        static private void SerializeDateTime(XmlWriter w, string name, DateTime dt)
        {
            w.WriteStartElement(name);
            w.WriteString(XmlConvert.ToString(dt, XmlDateTimeSerializationMode.Utc));
            w.WriteEndElement();
        }

        static private void SerializeTimeSpan(XmlWriter w, string name, TimeSpan span)
        {
            w.WriteStartElement(name);
            w.WriteString(XmlConvert.ToString(span));
            w.WriteEndElement();
        }

        private Dictionary<Type, ViewState> viewStates = new Dictionary<Type, ViewState>();

        private Dictionary<Type, XmlElement> viewStateNodes = new Dictionary<Type, XmlElement>();


        internal ViewState GetViewState(Type viewType)
        {
            ViewState state = null;
            viewStates.TryGetValue(viewType, out state);
            return state;
        }

        internal XmlElement GetViewStateNode(Type viewType)
        {
            XmlElement stateNode = null;
            viewStateNodes.TryGetValue(viewType, out stateNode);
            return stateNode;
        }

        internal void SetViewState(Type viewType, ViewState newState)
        {
            viewStates[viewType] = newState;
        }

        private void SetViewStateNode(Type viewType, XmlElement xmlElement)
        {
            viewStateNodes[viewType] = xmlElement;
        }

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }

    public class GraphState : IXmlSerializable
    {
        public CalendarRange Range;
        public int Years;
        public DateTime Start;
        public DateTime End;
        public bool YearToDate;
        public int Series;
        public bool ShowBalance;
        public bool ShowBudget;

        public bool ShowAll;

        public GraphState()
        {
        }

        public void WriteXml(XmlWriter w)
        {
            w.WriteElementString("Range", Range.ToString());
            w.WriteElementString("Years", Years.ToString());
            w.WriteElementString("Start", Start.ToShortDateString());
            w.WriteElementString("End", End.ToShortDateString());
            w.WriteElementString("YearToDate", YearToDate.ToString());
            w.WriteElementString("ShowAll", ShowAll.ToString());
            w.WriteElementString("Series", Series.ToString());
            w.WriteElementString("ShowBalance", ShowBalance.ToString());
            w.WriteElementString("ShowBudget", ShowBudget.ToString());
        }

        public void ReadXml(XmlReader r)
        {
            while (r.Read() && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    switch (r.LocalName)
                    {
                        case "Range":
                            this.Range = (CalendarRange)StringHelpers.ParseEnum(typeof(CalendarRange), r.ReadString(), (int)CalendarRange.Annually);
                            break;
                        case "Years":
                            this.Years = ReadInt(r, 1);
                            break;
                        case "Start":
                            this.Start = ReadDateTime(r, DateTime.Today.AddYears(1));
                            break;
                        case "End":
                            this.End = ReadDateTime(r, DateTime.Today);
                            break;
                        case "YearToDate":
                            this.YearToDate = ReadBoolean(r, true);
                            break;
                        case "ShowAll":
                            this.ShowAll = ReadBoolean(r, true);
                            break;
                        case "Series":
                            this.Series = ReadInt(r, 1);
                            break;
                        case "ShowBalance":
                            this.ShowBalance = ReadBoolean(r, false);
                            break;
                        case "ShowBudget":
                            this.ShowBudget = ReadBoolean(r, true);
                            break;
                    }
                }
            }
        }

        public static GraphState ReadFrom(XmlReader r)
        {
            GraphState state = new GraphState();
            state.ReadXml(r);
            return state;
        }

        static int ReadInt(XmlReader r, int defValue)
        {
            try
            {
                string s = r.ReadString();
                return int.Parse(s);
            }
            catch (FormatException)
            {
                return defValue;
            }
        }

        static bool ReadBoolean(XmlReader r, bool defValue)
        {
            try
            {
                string s = r.ReadString();
                return bool.Parse(s);
            }
            catch (FormatException)
            {
                return defValue;
            }
        }


        static DateTime ReadDateTime(XmlReader r, DateTime defValue)
        {
            try
            {
                string s = r.ReadString();
                return DateTime.Parse(s);
            }
            catch (FormatException)
            {
                return defValue;
            }
        }

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }
    }


    public class FileAssociation
    {

        // Associate file extension with progID, description, icon and application
        public static void Associate(
            string extension,
            string fullPathToApplication
            )
        {
            string applicationFileName = Path.GetFileName(fullPathToApplication);


            // Register the Application
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + applicationFileName + "\\shell\\open\\command"))
            {
                key.SetValue(string.Empty, string.Format("\"{0}\" \"%1\"", fullPathToApplication));
            }

            // Register the extension with the Application
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + extension))
            {
                key.SetValue(string.Empty, applicationFileName);
            }

            string fullRegPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\" + extension;

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(fullRegPath, false);
            }
            catch { }

            try
            {
                // As a last resort attempt to educate the "Application picker" that we support the .QIF files
                Registry.CurrentUser.CreateSubKey(fullRegPath + "\\OpenWithList").SetValue("a", applicationFileName);
                Registry.CurrentUser.CreateSubKey(fullRegPath + "\\OpenWithList").SetValue("MRUList", "a");

                Registry.CurrentUser.CreateSubKey(fullRegPath + "\\OpenWithProgids").SetValue(applicationFileName, "\0", RegistryValueKind.Unknown);
                Registry.CurrentUser.CreateSubKey(fullRegPath + "\\UserChoice").SetValue("ProgId", applicationFileName);
            }
            catch
            {
            }



            // SYSTEM WIDE NEEDS ADMIN RIGHT 

            //Registry.ClassesRoot.CreateSubKey(".qif").SetValue("", "WalkAbout.exe", Microsoft.Win32.RegistryValueKind.String);

            //try
            //{
            //    Registry.ClassesRoot.DeleteSubKeyTree("WalkAbout.exe\\shell\\open\\command");
            //}
            //catch
            //{
            //}

            //try
            //{

            //    Registry.ClassesRoot.CreateSubKey("WalkAbout.exe\\shell\\open\\command").SetValue("", fullPathToApplication + "\"%1\"", Microsoft.Win32.RegistryValueKind.String);
            //}
            //catch (Exception e)
            //{
            //    MessageBoxEx.Show(e.Message);
            //}
        }


       

    }

  
}
