using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

        private readonly bool persist;

        #region PROPERTIES

        public Point WindowLocation
        {
            get
            {
                object value = this.map["WindowLocation"];
                return value is Point ? (Point)value : new Point(0, 0);
            }
            set
            {
                this.map["WindowLocation"] = value;
                this.OnPropertyChanged("WindowLocation");
            }
        }

        public Size WindowSize
        {
            get
            {
                object value = this.map["WindowSize"];
                return value is Size ? (Size)value : new Size(0, 0);
            }
            set
            {
                this.map["WindowSize"] = value;
                this.OnPropertyChanged("WindowSize");
            }
        }

        public Size AttachmentDialogSize
        {
            get
            {
                object value = this.map["ReceiptDialogSize"];
                if (value != null)
                {
                    // convert to new name
                    this.map["AttachmentDialogSize"] = value;
                    this.map.Remove("ReceiptDialogSize");
                }
                value = this.map["AttachmentDialogSize"];
                return value is Size ? (Size)value : new Size(0, 0);
            }
            set
            {
                this.map["AttachmentDialogSize"] = value;
                this.OnPropertyChanged("AttachmentDialogSize");
            }
        }

        public string AttachmentDirectory
        {
            get
            {
                object value = this.map["AttachmentDirectory"];
                return value is string ? (string)value : null;
            }
            set
            {
                this.map["AttachmentDirectory"] = value;
                this.OnPropertyChanged("AttachmentDirectory");
            }
        }

        public string StatementsDirectory
        {
            get
            {
                object value = this.map["StatementsDirectory"];
                return value is string ? (string)value : null;
            }
            set
            {
                this.map["StatementsDirectory"] = value;
                this.OnPropertyChanged("StatementsDirectory");
            }
        }

        public string[] RecentFiles
        {
            get
            {
                object value = this.map["RecentFiles"];
                return value is string[]? (string[])value : null;
            }
            set
            {
                this.map["RecentFiles"] = value;
                this.OnPropertyChanged("RecentFiles");
            }
        }

        public int ToolBoxWidth
        {
            get
            {
                object value = this.map["ToolBoxWidth"];
                return value is int ? (int)value : 0;
            }
            set
            {
                this.map["ToolBoxWidth"] = value;
                this.OnPropertyChanged("ToolBoxWidth");
            }
        }

        public int GraphHeight
        {
            get
            {
                object value = this.map["GraphHeight"];
                return value is int ? (int)value : 0;
            }
            set
            {
                this.map["GraphHeight"] = value;
                this.OnPropertyChanged("GraphHeight");
            }
        }

        /// <summary>
        /// The current database (older ones are listed in RecentFiles)
        /// </summary>
        public string Database
        {
            get
            {
                object value = this.map["Database"];
                return value is string ? (string)value : null;
            }
            set
            {
                this.map["Database"] = value;
            }
        }

        internal int MigrateFiscalYearStart()
        {
            object value = this.MigrateSetting("FiscalYearStart");
            return value is int i ? i : 0;
        }

        internal bool MigrateRentalManagement()
        {
            object value = this.MigrateSetting("RentalManagement");
            return value is bool i ? i : false;
        }

        private object MigrateSetting(string name)
        {
            if (this.map.ContainsKey(name))
            {
                object value = this.map[name];
                this.map.Remove(name);
                return value;
            }
            return null;
        }

        public string Connection
        {
            set
            {
                // Migrate old setting to new separate fields.
                this.ParseConnectionString(value);
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
                        this.Server = value;
                    }
                    else if (string.Compare(name, "Initial Catalog", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        this.Database = value;
                    }
                    else if (string.Compare(name, "DataSource", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // CE doesn't have separate Server string, the DataSource is the path to the database.
                        this.Database = value;
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
                object value = this.map["Server"];
                return value is string ? (string)value : null;
            }
            set
            {
                this.map["Server"] = value;
            }
        }

        public string BackupPath
        {
            get
            {
                object value = this.map["BackupPath"];
                return value is string ? (string)value : null;
            }
            set
            {
                this.map["BackupPath"] = value;
                this.OnPropertyChanged("BackupPath");
            }
        }

        public string UserId
        {
            get
            {
                object value = this.map["UserId"];
                return value is string ? (string)value : null;
            }
            set
            {
                this.map["UserId"] = value;
                this.OnPropertyChanged("UserId");
            }
        }

        public bool DisplayClosedAccounts
        {
            get
            {
                object value = this.map["DisplayClosedAccounts"];
                return value is bool ? (bool)value : false;
            }
            set
            {
                this.map["DisplayClosedAccounts"] = value;
                this.OnPropertyChanged("DisplayClosedAccounts");
            }
        }

        public bool AcceptReconciled
        {
            get
            {
                object value = this.map["AcceptReconciled"];
                return value is bool ? (bool)value : false;
            }
            set
            {
                this.map["AcceptReconciled"] = value;
                this.OnPropertyChanged("AcceptReconciled");
            }
        }

        public bool PlaySounds
        {
            get
            {
                object value = this.map["PlaySounds"];
                return value is bool ? (bool)value : false;
            }
            set
            {
                this.map["PlaySounds"] = value;
                this.OnPropertyChanged("PlaySounds");
            }
        }

        public int TransferSearchDays
        {
            get
            {
                object value = this.map["TransferSearchDays"];
                return value is int ? (int)value : 5;
            }
            set
            {
                this.map["TransferSearchDays"] = value;
                this.OnPropertyChanged("TransferSearchDays");
            }
        }

        public List<StockServiceSettings> StockServiceSettings
        {
            get
            {
                object value = this.map["StockServiceSettings"];
                return value is List<StockServiceSettings> ? (List<StockServiceSettings>)value : null;
            }
            set
            {
                this.map["StockServiceSettings"] = value;
                this.OnPropertyChanged("StockServiceSettings");
            }
        }

        public QueryRow[] Query
        {
            get
            {
                object value = this.map["Query"];
                return value is QueryRow[]? (QueryRow[])value : null;
            }
            set
            {
                this.map["Query"] = value;
                this.OnPropertyChanged("Query");
            }
        }

        public GraphState GraphState
        {
            get
            {
                object value = this.map["GraphState"];
                return value is GraphState ? (GraphState)value : null;
            }
            set
            {
                this.map["GraphState"] = value;
                this.OnPropertyChanged("GraphState");
            }
        }

        public DateTime StartDate
        {
            get
            {
                object value = this.map["StartDate"];
                return value is DateTime ? (DateTime)value : DateTime.MinValue;
            }
            set { this.map["StartDate"] = value; }
        }

        public DateTime EndDate
        {
            get
            {
                object value = this.map["EndDate"];
                return value is DateTime ? (DateTime)value : DateTime.MinValue;
            }
            set { this.map["EndDate"] = value; }
        }

        public DateTime LastStockRequest
        {
            get
            {
                object value = this.map["LastStockRequest"];
                return value is DateTime ? (DateTime)value : DateTime.MinValue;
            }
            set
            {
                this.map["LastStockRequest"] = value;
                this.OnPropertyChanged("LastStockRequest");
            }
        }


        public string Theme
        {
            get
            {
                object value = this.map["Theme"];
                return value is string ? (string)value : null;
            }
            set
            {
                if (this.Theme != value)
                {
                    this.map["Theme"] = value;
                    this.OnPropertyChanged("Theme");
                }
            }
        }

        public string ExeVersion
        {
            get
            {
                object value = this.map["ExeVersion"];
                return value is string ? (string)value : null;
            }
            set
            {
                this.map["ExeVersion"] = value;
            }
        }

        public DateTime LastExeTimestamp
        {
            get
            {
                object value = this.map["LastExeTimestamp"];
                return value is DateTime ? (DateTime)value : DateTime.MinValue;
            }
            set
            {
                this.map["LastExeTimestamp"] = value;
            }
        }

        /// <summary>
        /// The range of dates to go back and look for a duplicate
        /// </summary>
        public TimeSpan DuplicateRange
        {
            get
            {
                object value = this.map["DuplicateRange"];
                return value is TimeSpan ? (TimeSpan)value : TimeSpan.FromDays(60);
            }
            set
            {
                this.map["DuplicateRange"] = value;
                this.OnPropertyChanged("DuplicateRange");
            }
        }

        #endregion

        public Settings(bool save)
        {
            this.StartDate = DateTime.Now;
            this.EndDate = DateTime.Now;
            this.persist = save;
        }

        private string _fileName;

        public string ConfigFile
        {
            get { return this._fileName; }
            set { this._fileName = value; }
        }

        private readonly XmlDocument doc = new XmlDocument();
        private readonly Hashtable map = new Hashtable();

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public object this[string key]
        {
            get { return this.map[key]; }
            set { this.map[key] = value; }
        }

        public void Load(string path)
        {
            using (XmlTextReader r = new XmlTextReader(path))
            {
                this.ReadXml(r);
            }
        }

        public void Save()
        {
            if (!this.Persist)
            {
                return;
            }
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = Encoding.UTF8;
            using (XmlWriter w = XmlWriter.Create(this.ConfigFile, settings))
            {
                this.WriteXml(w);
                w.Close();
            }
        }

        public bool Persist { get { return this.persist; } }

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
                            pi.SetValue(this, int.Parse(r.ReadString()), null);
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
                            pi.SetValue(this, (XmlElement)this.doc.ReadNode(r), null);
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
                        this.SetViewStateNode(typeof(TransactionsView), (XmlElement)this.doc.ReadNode(r));
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
                                this.SetViewStateNode(t, (XmlElement)this.doc.ReadNode(r));
                            }
                        }
                        else
                        {
                            this.map[r.Name] = r.ReadString();
                        }
                    }
                }
            }
        }

        public static QueryRow[] ReadQuery(XmlReader r)
        {
            ArrayList list = new ArrayList();
            if (r.IsEmptyElement)
            {
                return null;
            }

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
            ArrayList keys = new ArrayList(this.map.Keys);
            keys.Sort();
            foreach (string key in keys)
            {
                object value = this.map[key];
                if (value == null)
                {
                    continue;
                }

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
                        w.WriteElementString(key, (string)value);
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


        private static Point DeserializePoint(XmlReader r)
        {
            Point p = new Point();
            if (r.IsEmptyElement)
            {
                return p;
            }

            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "X")
                    {
                        p.X = int.Parse(r.ReadString());
                    }
                    else if (r.Name == "Y")
                    {
                        p.Y = int.Parse(r.ReadString());
                    }
                }
            }
            return p;
        }

        private static Size DeserializeSize(XmlReader r)
        {
            Size s = new Size();
            if (r.IsEmptyElement)
            {
                return s;
            }

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

        private static DateTime DeserializeDateTime(XmlReader r)
        {
            if (r.IsEmptyElement)
            {
                return new DateTime();
            }

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

        private static TimeSpan DeserializeTimeSpan(XmlReader r)
        {
            if (r.IsEmptyElement)
            {
                return new TimeSpan();
            }

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
        private static void SerializePoint(XmlWriter w, Point p)
        {
            w.WriteElementString("X", p.X.ToString());
            w.WriteElementString("Y", p.Y.ToString());
        }

        private static void SerializeSize(XmlWriter w, string name, Size s)
        {
            w.WriteStartElement(name);
            w.WriteElementString("Width", s.Width.ToString());
            w.WriteElementString("Height", s.Height.ToString());
            w.WriteEndElement();
        }

        private static void SerializeDateTime(XmlWriter w, string name, DateTime dt)
        {
            w.WriteStartElement(name);
            w.WriteString(XmlConvert.ToString(dt, XmlDateTimeSerializationMode.Utc));
            w.WriteEndElement();
        }

        private static void SerializeTimeSpan(XmlWriter w, string name, TimeSpan span)
        {
            w.WriteStartElement(name);
            w.WriteString(XmlConvert.ToString(span));
            w.WriteEndElement();
        }

        private readonly Dictionary<Type, ViewState> viewStates = new Dictionary<Type, ViewState>();

        private readonly Dictionary<Type, XmlElement> viewStateNodes = new Dictionary<Type, XmlElement>();


        internal ViewState GetViewState(Type viewType)
        {
            ViewState state = null;
            this.viewStates.TryGetValue(viewType, out state);
            return state;
        }

        internal XmlElement GetViewStateNode(Type viewType)
        {
            XmlElement stateNode = null;
            this.viewStateNodes.TryGetValue(viewType, out stateNode);
            return stateNode;
        }

        internal void SetViewState(Type viewType, ViewState newState)
        {
            this.viewStates[viewType] = newState;
        }

        private void SetViewStateNode(Type viewType, XmlElement xmlElement)
        {
            this.viewStateNodes[viewType] = xmlElement;
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
            w.WriteElementString("Range", this.Range.ToString());
            w.WriteElementString("Years", this.Years.ToString());
            w.WriteElementString("Start", this.Start.ToShortDateString());
            w.WriteElementString("End", this.End.ToShortDateString());
            w.WriteElementString("YearToDate", this.YearToDate.ToString());
            w.WriteElementString("ShowAll", this.ShowAll.ToString());
            w.WriteElementString("Series", this.Series.ToString());
            w.WriteElementString("ShowBalance", this.ShowBalance.ToString());
            w.WriteElementString("ShowBudget", this.ShowBudget.ToString());
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

        private static int ReadInt(XmlReader r, int defValue)
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

        private static bool ReadBoolean(XmlReader r, bool defValue)
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

        private static DateTime ReadDateTime(XmlReader r, DateTime defValue)
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
            string fullPathToApplication)
        {
            Debug.Assert(OperatingSystem.IsWindows());

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
        }




    }


}
