using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Configuration;
using Walkabout.Data;
using Walkabout.Utilities;


namespace Walkabout.Views.Controls
{
 
    /// <summary>
    /// Interaction logic for QueryViewControl.xaml
    /// </summary>
    public partial class QueryViewControl : UserControl, IClipboardClient, IXmlSerializable
    {

        public QueryViewControl()
        {
            InitializeComponent();

            dataGrid1.ItemsSource = queryRows;

            dataGrid1.CurrentCellChanged += new EventHandler<EventArgs>(OnDataGrid1_CurrentCellChanged);
            dataGrid1.BeginningEdit += new EventHandler<DataGridBeginningEditEventArgs>(OnDataGrid_BeginningEdit);
            dataGrid1.RowEditEnding += new EventHandler<DataGridRowEditEndingEventArgs>(OnDataGrid_RowEditEnding);
        }

        #region Properties

        ObservableCollection<QueryRow> queryRows = new ObservableCollection<QueryRow>();


        List<Conjunction> listOfConjunctions;

        public List<Conjunction> ListOfConjunctions
        {
            get
            {
                if (listOfConjunctions == null)
                {
                    listOfConjunctions = new List<Conjunction>();
                    listOfConjunctions.Add(Conjunction.None);
                    listOfConjunctions.Add(Conjunction.And);
                    listOfConjunctions.Add(Conjunction.Or);
                }
                return listOfConjunctions;
            }
        }

        List<Field> listOfFields;
        public List<Field> ListOfFields
        {
            get
            {
                if (listOfFields == null)
                {
                    listOfFields = new List<Field>();
                    listOfFields.Add(Field.Accepted);
                    listOfFields.Add(Field.Account);
                    listOfFields.Add(Field.Budgeted);
                    listOfFields.Add(Field.Category);
                    listOfFields.Add(Field.Deposit);
                    listOfFields.Add(Field.Date);
                    listOfFields.Add(Field.Memo);
                    listOfFields.Add(Field.Number);
                    listOfFields.Add(Field.Payee);
                    listOfFields.Add(Field.Payment);
                    listOfFields.Add(Field.SalesTax);
                    listOfFields.Add(Field.Status);
                }
                return listOfFields;
            }
        }


        List<Operation> listOfOperations;
        public List<Operation> ListOfOperations
        {
            get
            {
                if (listOfOperations == null)
                {
                    listOfOperations = new List<Operation>();
                    listOfOperations.Add(Operation.Contains);
                    listOfOperations.Add(Operation.Equals);
                    listOfOperations.Add(Operation.GreaterThan);
                    listOfOperations.Add(Operation.GreaterThanEquals);
                    listOfOperations.Add(Operation.LessThan);
                    listOfOperations.Add(Operation.LessThanEquals);
                    listOfOperations.Add(Operation.NotContains);
                    listOfOperations.Add(Operation.NotEquals);
                    listOfOperations.Add(Operation.Regex);
                }
                return listOfOperations;
            }
        }




        bool isEditing; 
        #endregion

        #region Data Grid Event

        void OnDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            isEditing = true;
        }

        void OnDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            isEditing = false;
        }

        void OnDataGrid1_CurrentCellChanged(object sender, EventArgs e)
        {
            // Edit as soon as you click on a cell
            // if we don't do this the user has to click twice on the cell in order to edit (Very anoying)
            DataGrid grid = (DataGrid)sender;
            grid.BeginEdit();
        }

        void OnDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (this.dataGrid1.SelectedItems.Count > 0)
                {
                    this.queryRows.RemoveAt(this.dataGrid1.SelectedIndex);
                }
                e.Handled = true;
            }
        } 
        #endregion

        #region Public methods

        public void AddQuery(QueryRow[] q)
        {
            if (q != null)
            {
                foreach (QueryRow r in q)
                {
                    queryRows.Add(new QueryRow(r.Conjunction, r.Field, r.Operation, r.Value));
                }
            }
        }

        public void Clear()
        {
            queryRows.Clear();
        }

        internal void OnShow()
        {
            if (queryRows.Count == 0)
            {
                queryRows.Add(new QueryRow());
            }
        }

        public QueryRow[] GetQuery()
        {
            if (this.isEditing)
            {
                try
                {

                    dataGrid1.CommitEdit(DataGridEditingUnit.Row, false);
                }
                catch
                {
                    // I've seen case where the Data Grid inner Cells was throwing NULL exception
                    // still need to understand whwy and fix this
                }
            }
            return queryRows.ToArray();
        }

        static public QueryRow GetQueryRow(DataRow row)
        {
            QueryRow q = new QueryRow();
            if (!(row["Field"] is DBNull))
                q.Field = (Field)row["Field"];
            if (!(row["Operation"] is DBNull))
                q.Operation = (Operation)row["Operation"];
            q.Value = row["Value"] as string;
            if (!(row["Conjunction"] is DBNull))
                q.Conjunction = (Conjunction)row["Conjunction"];
            return q;
        } 
        #endregion

        #region Serialize

        static string Serialize(QueryRow[] query)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter w = new XmlTextWriter(sw);
            w.Formatting = Formatting.Indented;
            w.WriteStartElement("Query");
            Settings.WriteQuery(w, query);
            w.Close();
            return sw.ToString();
        }

        #endregion

        #region IXmlSerializable Members

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {

        }

        public void WriteXml(XmlWriter writer)
        {

        }
        #endregion

        #region IClipboardClient


        public bool CanCut
        {
            get { return true; } // todo: efficiently keep track of whether we have any selected rows.
        }

        public void Cut()
        {
            ArrayList qrows = new ArrayList();
            ArrayList list = new ArrayList();
            //for (int i = 0, n = dataTable1.Rows.Count; i < n; i++)
            //{
            //    // JPD
            //    //if (dataGrid1.IsSelected(i))
            //    //{
            //    //    DataRow row = dataTable1.Rows[i];
            //    //    list.Add(row);
            //    //    QueryRow qr = GetQueryRow(row);
            //    //    qrows.Add(qr);
            //    //}
            //}
            //foreach (DataRow row in list)
            //{
            //    dataTable1.Rows.Remove(row);
            //}
            if (qrows.Count >= 0)
            {
                string xml = Serialize((QueryRow[])qrows.ToArray(typeof(QueryRow)));
                Clipboard.SetDataObject(xml, true);
            }
        }

        public bool CanCopy
        {
            get { return true; } // todo: efficiently keep track of whether we have any selected rows.
        }
        public void Copy()
        {
            ArrayList list = new ArrayList();
            //for (int i = 0, n = dataTable1.Rows.Count; i < n; i++)
            //{
            //    // JPD
            //    //if (dataGrid1.IsSelected(i))
            //    //{
            //    //    DataRow row = dataTable1.Rows[i];
            //    //    QueryRow qr = GetQueryRow(row);
            //    //    list.Add(qr);
            //    //}
            //}
            if (list.Count > 0)
            {
                string xml = Serialize((QueryRow[])list.ToArray(typeof(QueryRow)));
                Clipboard.SetDataObject(xml, true);
            }
        }

        public bool CanDelete
        {
            get { return true; } // todo: efficiently keep track of whether we have any selected rows.
        }
        public void Delete()
        {
            ArrayList list = new ArrayList();
            //for (int i = 0, n = dataTable1.Rows.Count; i < n; i++)
            //{
            //    // JPD
            //    //if (dataGrid1.IsSelected(i))
            //    //{
            //    //    DataRow row = dataTable1.Rows[i];
            //    //    list.Add(row);
            //    //}
            //}
            //foreach (DataRow row in list)
            //{
            //    dataTable1.Rows.Remove(row);
            //}
        }

        public bool CanPaste
        {
            get { return Clipboard.ContainsText(); }
        }
        public void Paste()
        {
            IDataObject data = Clipboard.GetDataObject();
            if (data.GetDataPresent(typeof(string)))
            {
                string xml = (string)data.GetData(typeof(string));
                try
                {
                    QueryRow[] rows = Settings.Deserialize(xml);
                    AddQuery(rows);
                }
                catch
                {
                }
            }
        }
        #endregion

    }


    //
    // THE COMMENTED CODE BELOW WILL EVENTUAL BE PUT INTO PRDODUCTION
    // IT IS USED TO RE MAKE THE OPERATION MORE HUMAN FRIENDLY INSTEAD OF "Grather Then" THE USER WOULD SEE ">"
    //

    //public class ReadableToEnumConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        if (value is Conjunction)
    //        {
    //            return QueryMapToHuman.GetHumanFormat(QueryMapToHuman.ListOfConjunctions, value);
    //        }
    //        return value.ToString();
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        //if (string.IsNullOrEmpty(value.ToString()))
    //        //    return null;
    //        return value;
    //    }

    //}

    /*
        public class QueryMapToHuman
    {
        public class MapHumanReadableToEnum
        {
            public string Readable { get; set; }
            public object EnumValue { get; set; }

            public override string ToString()
            {
                return Readable;
            }
        }

        static List<MapHumanReadableToEnum> listConjunctionMap;

        public static List<MapHumanReadableToEnum> ListOfConjunctions
        {
            get
            {
                if (listConjunctionMap == null)
                {
                    listConjunctionMap = new List<MapHumanReadableToEnum>();
                    listConjunctionMap.Add(new MapHumanReadableToEnum() { Readable = "", EnumValue = Conjunction.None });
                    listConjunctionMap.Add(new MapHumanReadableToEnum() { Readable = "And", EnumValue = Conjunction.And });
                    listConjunctionMap.Add(new MapHumanReadableToEnum() { Readable = "Or", EnumValue = Conjunction.Or });
                }
                return listConjunctionMap;
            }
        }

        public static string GetHumanFormat(List<MapHumanReadableToEnum> map, object valueToFind)
        {
            foreach(MapHumanReadableToEnum m in map)
            {
                if (m.EnumValue.ToString() == valueToFind.ToString())
                {
                    return m.Readable;
                }

            }
            return string.Empty;
        }
    }
*/
}
