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
using Walkabout.Controls;
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
            this.InitializeComponent();

            this.dataGrid1.ItemsSource = this.queryRows;

            this.dataGrid1.CurrentCellChanged += new EventHandler<EventArgs>(this.OnDataGrid1_CurrentCellChanged);
            this.dataGrid1.BeginningEdit += new EventHandler<DataGridBeginningEditEventArgs>(this.OnDataGrid_BeginningEdit);
            this.dataGrid1.RowEditEnding += new EventHandler<DataGridRowEditEndingEventArgs>(this.OnDataGrid_RowEditEnding);
            this.dataGrid1.CanUserAddRows = true;
        }

        #region Properties

        private readonly ObservableCollection<QueryRow> queryRows = new ObservableCollection<QueryRow>();
        private List<Conjunction> listOfConjunctions;

        public List<Conjunction> ListOfConjunctions
        {
            get
            {
                if (this.listOfConjunctions == null)
                {
                    this.listOfConjunctions = new List<Conjunction>();
                    this.listOfConjunctions.Add(Conjunction.None);
                    this.listOfConjunctions.Add(Conjunction.And);
                    this.listOfConjunctions.Add(Conjunction.Or);
                }
                return this.listOfConjunctions;
            }
        }

        private List<Field> listOfFields;
        public List<Field> ListOfFields
        {
            get
            {
                if (this.listOfFields == null)
                {
                    this.listOfFields = new List<Field>();
                    this.listOfFields.Add(Field.None);
                    this.listOfFields.Add(Field.Accepted);
                    this.listOfFields.Add(Field.Account);
                    this.listOfFields.Add(Field.Budgeted);
                    this.listOfFields.Add(Field.Category);
                    this.listOfFields.Add(Field.Deposit);
                    this.listOfFields.Add(Field.Date);
                    this.listOfFields.Add(Field.Memo);
                    this.listOfFields.Add(Field.Number);
                    this.listOfFields.Add(Field.Payee);
                    this.listOfFields.Add(Field.Payment);
                    this.listOfFields.Add(Field.SalesTax);
                    this.listOfFields.Add(Field.Status);
                }
                return this.listOfFields;
            }
        }

        private List<string> listOfOperations;
        public List<string> ListOfOperations
        {
            get
            {
                if (this.listOfOperations == null)
                {
                    this.listOfOperations = new List<string>();
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.None));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.Contains));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.Equals));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.GreaterThan));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.GreaterThanEquals));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.LessThan));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.LessThanEquals));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.NotContains));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.NotEquals));
                    this.listOfOperations.Add(QueryRow.GetOperationDisplayString(Operation.Regex));
                }
                return this.listOfOperations;
            }
        }

        private bool isEditing;
        #endregion

        #region Data Grid Event

        private DataGridBeginningEditEventArgs editingArgs;

        private void OnDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            this.isEditing = true;
            this.editingArgs = e;
        }

        private void OnDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            this.isEditing = false;
            this.editingArgs = null;
        }

        private void OnDataGrid1_CurrentCellChanged(object sender, EventArgs e)
        {
            // Edit as soon as you click on a cell
            // if we don't do this the user has to click twice on the cell in order to edit (Very anoying)
            DataGrid grid = (DataGrid)sender;
            bool rc = grid.BeginEdit();
            if (!rc)
            {
                try
                {
                    if (!grid.CommitEdit())
                    {
                        if (!grid.CommitEdit(DataGridEditingUnit.Row, true))
                        {
                            System.Diagnostics.Debug.WriteLine("Why is CommitEdit failing?");
                        }
                    }
                    rc = grid.BeginEdit();
                    if (!rc)
                    {
                        System.Diagnostics.Debug.WriteLine("Why is BeginEdit failing?");
                    }
                }
                catch
                {

                }
            }
        }

        private void OnDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete && !(e.OriginalSource is TextBox))
            {
                this.Delete();
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
                    this.queryRows.Add(new QueryRow(r.Conjunction, r.Field, r.Operation, r.Value));
                }
            }
        }

        public void Clear()
        {
            this.queryRows.Clear();
        }

        internal void OnShow()
        {
            if (this.queryRows.Count == 0)
            {
                this.queryRows.Add(new QueryRow());
            }
        }

        public QueryRow[] GetQuery()
        {
            if (this.isEditing)
            {
                try
                {

                    this.dataGrid1.CommitEdit(DataGridEditingUnit.Row, false);
                }
                catch
                {
                    // I've seen case where the Data Grid inner Cells was throwing NULL exception
                    // still need to understand whwy and fix this
                    this.isEditing = false;
                }
            }
            List<QueryRow> complete = new List<QueryRow>(from q in this.queryRows where q.Operation != Operation.None && q.Operation != Operation.None select q);
            return complete.ToArray();
        }

        public static QueryRow GetQueryRow(DataRow row)
        {
            QueryRow q = new QueryRow();
            if (!(row["Field"] is DBNull))
            {
                q.Field = (Field)row["Field"];
            }

            if (!(row["Operation"] is DBNull))
            {
                q.Operation = (Operation)row["Operation"];
            }

            q.Value = row["Value"] as string;
            if (!(row["Conjunction"] is DBNull))
            {
                q.Conjunction = (Conjunction)row["Conjunction"];
            }

            return q;
        }
        #endregion

        #region Serialize

        private static string Serialize(QueryRow[] query)
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

            List<QueryRow> list = this.GetSelectedRows();
            foreach (QueryRow row in list)
            {
                this.queryRows.Remove(row);
            }
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

        private List<QueryRow> GetSelectedRows()
        {
            List<QueryRow> list = new List<QueryRow>();
            for (int i = 0, n = this.queryRows.Count; i < n; i++)
            {
                QueryRow qr = this.queryRows[i];
                if (this.dataGrid1.SelectedItems.Contains(qr))
                {
                    list.Add(qr);
                }
            }
            return list;
        }

        public void Copy()
        {
            List<QueryRow> list = this.GetSelectedRows();
            if (list.Count > 0)
            {
                string xml = Serialize(list.ToArray());
                Clipboard.SetDataObject(xml, true);
            }
        }

        public bool CanDelete
        {
            get { return true; } // todo: efficiently keep track of whether we have any selected rows.
        }

        public void Delete()
        {
            if (this.dataGrid1.SelectedItems.Count > 0 &&
                this.queryRows.Count > this.dataGrid1.SelectedIndex)
            {
                this.queryRows.RemoveAt(this.dataGrid1.SelectedIndex);
            }
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
                    this.AddQuery(rows);
                }
                catch
                {
                }
            }
        }

        internal bool ContainsKeyboardFocus()
        {
            DependencyObject e = Keyboard.FocusedElement as DependencyObject;
            if (e != null && WpfHelper.FindAncestor<QueryViewControl>(e) == this)
            {
                return true;
            }
            return false;
        }
        #endregion

        private void ComboBoxForConjunction_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) =>
            {
                return ((Conjunction)o).ToString().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        private void ComboBoxForField_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) =>
            {
                return ((Field)o).ToString().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        private void ComboBoxForOperation_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) =>
            {
                return ((string)o).IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }
    }

}
