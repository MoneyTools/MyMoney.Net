using NUnit.Framework.Constraints;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Automation;
using System.Windows.Input;
using System.Xml.Linq;
using Walkabout.Tests.Interop;

namespace Walkabout.Tests.Wrappers
{
    public class TransactionViewWrapper
    {
        private readonly MainWindowWrapper window;
        private AutomationElement control;
        private static readonly Dictionary<string, TransactionViewColumns> mapping;

        static TransactionViewWrapper()
        {
            mapping = new Dictionary<string, TransactionViewColumns>();
            mapping["TheGrid_BankTransactionDetails"] = new TransactionViewColumns(
                    new TransactionViewColumn("A", "Attachment", "Button"),
                    new TransactionViewColumn("Num", "Number", "TextBox"),
                    new TransactionViewColumn("Date", "Date", "DatePicker"),
                    new TransactionViewColumn("C", "Color", "Custom"),
                    new CompoundTransactionViewColumn("Payee/Category/Memo",
                        new TransactionViewColumn("", "Payee", "ComboBox"), // ExpandCollapse, Selection, Value
                        new TransactionViewColumn("", "Category", "ComboBox"), // ExpandCollapse, Selection, Value
                        new TransactionViewColumn("", "Memo", "TextBox") // ExpandCollapse, Selection, Value
                    ),
                    new TransactionViewColumn("S", "Status", "Button"),
                    new TransactionViewColumn("Sales Tax", "SalesTax", "TextBox"),
                    new CompoundTransactionViewColumn("Payment",
                        new TransactionViewColumn("", "Splits", "Button"),
                        new TransactionViewColumn("Payment", "Payment", "TextBox")
                    ),
                    new CompoundTransactionViewColumn("Deposit",
                        new TransactionViewColumn("", "Splits", "Button"),
                        new TransactionViewColumn("Deposit", "Deposit", "TextBox")
                    )
                );


            mapping["TheGrid_InvestmentActivity"] = new TransactionViewColumns(
                    new TransactionViewColumn("A", "Attachment", "Button"),
                    new TransactionViewColumn("Num", "Number", "TextBox"),
                    new TransactionViewColumn("Date", "Date", "DatePicker"),
                    new TransactionViewColumn("C", "Color", "Custom"),
                    new CompoundTransactionViewColumn("Payee/Category/Memo",
                        new TransactionViewColumn("", "Payee", "ComboBox"), // ExpandCollapse, Selection, Value
                        new TransactionViewColumn("", "Category", "ComboBox"), // ExpandCollapse, Selection, Value
                        new TransactionViewColumn("", "Memo", "TextBox") // ExpandCollapse, Selection, Value
                    ),

                    new TransactionViewColumn("Activity", "Activity", "ComboBox"),
                    new TransactionViewColumn("Security", "Security", "ComboBox"),
                    new TransactionViewColumn("Units", "Units", "TextBox"),
                    new TransactionViewColumn("UnitPrice", "UnitPrice", "TextBox"),

                    new TransactionViewColumn("S", "Status", "Button"),
                    new TransactionViewColumn("Sales Tax", "SalesTax", "TextBox"),
                    new CompoundTransactionViewColumn("Payment",
                        new TransactionViewColumn("", "Splits", "Button"),
                        new TransactionViewColumn("", "Payment", "TextBox")
                    ),
                    new CompoundTransactionViewColumn("Deposit",
                        new TransactionViewColumn("", "Splits", "Button"),
                        new TransactionViewColumn("", "Deposit", "TextBox")
                    )
                );

            mapping["TheGrid_TransactionFromDetails"] = new TransactionViewColumns(
                    new TransactionViewColumn("A", "Attachment", "Button"),
                    new TransactionViewColumn("Account", "Account", "TextBlock"),
                    new TransactionViewColumn("Num", "Number", "TextBox"),
                    new TransactionViewColumn("Date", "Date", "DatePicker"),
                    new TransactionViewColumn("C", "Color", "Custom"),
                    new CompoundTransactionViewColumn("Payee/Category/Memo",
                        new TransactionViewColumn("", "Payee", "ComboBox"), // ExpandCollapse, Selection, Value
                        new TransactionViewColumn("", "Category", "ComboBox"), // ExpandCollapse, Selection, Value
                        new TransactionViewColumn("", "Memo", "TextBox") // ExpandCollapse, Selection, Value
                    ),
                    new TransactionViewColumn("S", "Status", "Button"),
                    new CompoundTransactionViewColumn("Payment",
                        new TransactionViewColumn("", "Splits", "Button"),
                        new TransactionViewColumn("", "Payment", "TextBox")
                    ),
                    new CompoundTransactionViewColumn("Deposit",
                        new TransactionViewColumn("", "Splits", "Button"),
                        new TransactionViewColumn("", "Deposit", "TextBox")
                    )
            );

            mapping["TheGrid_BySecurity"] = new TransactionViewColumns(
                    new TransactionViewColumn("Account", "Account", "TextBlock"),
                    new TransactionViewColumn("Date", "Date", "DatePicker"),
                    new CompoundTransactionViewColumn("Payee/Category/Memo",
                        new TransactionViewColumn("", "Payee", "ComboBox"), // ExpandCollapse, Selection, Value
                        new TransactionViewColumn("", "Category", "ComboBox"), // ExpandCollapse, Selection, Value
                        new TransactionViewColumn("", "Memo", "TextBox") // ExpandCollapse, Selection, Value
                    ),
                    new TransactionViewColumn("Security", "Security", "ComboBox"),
                    new TransactionViewColumn("Activity", "Activity", "ComboBox"),
                    new TransactionViewColumn("FIFO", "FIFO", "Custom"),
                    new TransactionViewColumn("Units", "Units", "TextBox"),
                    new TransactionViewColumn("Units A.S.", "UnitsAS", "TextBlock"),
                    new TransactionViewColumn("Holding", "Holding", "TextBlock"),
                    new TransactionViewColumn("Unit Price", "UnitPrice", "TextBlock"),
                    new TransactionViewColumn("Price A.S.", "UnitPriceAS", "TextBlock"),
                    new TransactionViewColumn("S", "Status", "Button"),
                    new CompoundTransactionViewColumn("Payment",
                        new TransactionViewColumn("", "Splits", "Button"),
                        new TransactionViewColumn("", "Payment", "TextBox")
                    ),
                    new CompoundTransactionViewColumn("Deposit",
                        new TransactionViewColumn("", "Splits", "Button"),
                        new TransactionViewColumn("", "Deposit", "TextBox")
                    )
                );

        }

        public TransactionViewWrapper(MainWindowWrapper window, AutomationElement control)
        {
            this.window = window;
            this.control = control;
        }

        public MainWindowWrapper Window { get { return this.window; } }

        public bool IsEditable
        {
            get
            {
                return this.IsBankAccount || this.IsInvestmentAccount;
            }
        }

        public bool IsBankAccount
        {
            get
            {
                string name = this.control.Current.AutomationId;
                return name == "TheGrid_BankTransactionDetails";
            }
        }

        public bool IsInvestmentAccount
        {
            get
            {
                string name = this.control.Current.AutomationId;
                return name == "TheGrid_InvestmentActivity";
            }
        }

        public bool HasTransactions
        {
            get
            {
                foreach (AutomationElement e in this.control.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
                {
                    if (e.Current.Name == "Walkabout.Data.Transaction" || e.Current.Name == "{NewItemPlaceholder}")
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public int Count
        {
            get
            {
                return this.GetItems(true).Count;
            }
        }

        public int CountNoPlaceholder
        {
            get
            {
                return this.GetItems(false).Count;
            }
        }

        public List<TransactionViewRow> GetItems(bool includePlaceHolder = true)
        {
            List<TransactionViewRow> list = new List<TransactionViewRow>();
            foreach (AutomationElement e in this.control.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
            {
                if (e.Current.Name == "Walkabout.Data.Transaction")
                {
                    list.Add(new TransactionViewRow(this, e));
                }
                else if (e.Current.Name == "{NewItemPlaceholder}" && includePlaceHolder)
                {
                    list.Add(new TransactionViewRow(this, e));
                }
            }
            return list;
        }

        public TransactionViewRow GetNewRow()
        {
            TransactionViewRow lastrow = null;
            foreach (AutomationElement e in this.control.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
            {
                if (e.Current.Name == "Walkabout.Data.Transaction")
                {
                    lastrow = new TransactionViewRow(this, e);
                }
                else if (e.Current.Name == "{NewItemPlaceholder}")
                {
                    lastrow = new TransactionViewRow(this, e);
                    break;
                }
            }

            // no place holder means the placeholder is being edited.
            lastrow.IsNewRow = true;
            return lastrow;
        }

        public TransactionViewRow Select(int index)
        {
            List<TransactionViewRow> list = this.GetItems();
            if (index >= list.Count)
            {
                throw new ArgumentOutOfRangeException("Index " + index + " is out of range, list only has " + list.Count + " items");
            }

            TransactionViewRow item = list[index];
            item.Select();
            return item;
        }

        public bool HasSelection
        {
            get
            {
                return this.Selection != null;
            }
        }

        public TransactionViewRow Selection
        {
            get
            {
                SelectionPattern selection = (SelectionPattern)this.control.GetCurrentPattern(SelectionPattern.Pattern);
                AutomationElement[] selected = selection.Current.GetSelection();
                return (selected == null || selected.Length == 0) ? null : new TransactionViewRow(this, selected[0]);
            }
        }

        internal void Delete(int index)
        {
            TransactionViewRow item = this.Select(index);
            item.Delete();
        }

        internal TransactionViewColumn GetColumn(string name)
        {
            return this.Columns.GetColumn(name);
        }

        internal TransactionViewColumns Columns
        {
            get
            {
                string gridName = this.control.Current.AutomationId;
                TransactionViewColumns cols;
                if (!mapping.TryGetValue(gridName, out cols))
                {
                    throw new Exception("DataGrid named '" + gridName + "' has no mapping");
                }
                return cols;
            }
        }

        internal void AddNew()
        {
            this.ScrollToEnd();
            Thread.Sleep(100);

            AutomationElement placeholder = TreeWalker.RawViewWalker.GetLastChild(this.control);
            if (placeholder.Current.Name != "{NewItemPlaceholder}")
            {
                throw new Exception("Expecting {NewItemPlaceholder} at the bottom of the DataGrid");
            }

            this.Focus();
            SelectionItemPattern select = (SelectionItemPattern)placeholder.GetCurrentPattern(SelectionItemPattern.Pattern);
            select.Select();

            // This ensures the a transaction is created for this placeholder.
            TransactionViewRow row = GetNewRow();
            row.BeginEdit(); // this can invalidate the row level automation element!            
            row = GetNewRow();          
        }

        internal void ScrollVertical(double verticalPercent)
        {
            ScrollPattern sp = (ScrollPattern)this.control.GetCurrentPattern(ScrollPattern.Pattern);
            if (sp.Current.VerticallyScrollable && sp.Current.VerticalScrollPercent != verticalPercent)
            {
                sp.SetScrollPercent(System.Windows.Automation.ScrollPattern.NoScroll, verticalPercent);
            }
        }

        internal void SortBy(TransactionViewColumn column)
        {
            AutomationElement header = this.control.FindFirstWithRetries(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ClassNameProperty, "DataGridColumnHeader"),
                new PropertyCondition(AutomationElement.NameProperty, column.Header)));
            if (header != null)
            {
                InvokePattern p = (InvokePattern)header.GetCurrentPattern(InvokePattern.Pattern);
                p.Invoke();
            }
            else
            {
                Debug.WriteLine("Could not find header for column: " + column.Name);
            }
        }

        internal void ScrollToEnd()
        {
            this.ScrollVertical(100);
        }

        internal void CommitEdit()
        {
            var selection = this.Selection;
            if (selection != null)
            {
                selection.Focus();
                selection.CommitEdit();
                selection.Select();
            }
        }

        internal void BeginEdit()
        {
            var selection = this.Selection;
            if (selection != null)
            {
                selection.Focus();
                selection.BeginEdit();
            }
        }

        internal void NavigateTransfer()
        {
            var selection = this.Selection;

            string tofrom;
            string sourceAccount = selection.ParseTransferPayee(out tofrom);
            decimal amount = selection.GetAmount();

            if (sourceAccount == null)
            {
                throw new Exception("Source account not found");
            }

            for (int delay = 100; delay < 500; delay += 100)
            {
                try
                {
                    selection.Focus();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    selection = this.Selection;
                }

                Thread.Sleep(delay);
                Input.TapKey(System.Windows.Input.Key.F12);
                // key sending is completely async, so we have to give it time to arrive and be processed.
                Thread.Sleep(delay);

                for (int retries = 5; retries > 0; retries--)
                {
                    // update the new "transactions" grid in place.
                    this.control = this.window.FindTransactionGrid().control;

                    // now verify we got a new selection
                    var target = this.Selection;
                    if (target == null)
                    {
                        throw new Exception("Navigation lost the selection");
                    }

                    string fromto;
                    string targetAccount = target.ParseTransferPayee(out fromto);
                    decimal targetAmount = target.GetAmount();

                    if (sourceAccount == targetAccount && fromto == tofrom)
                    {
                        // hasn't got the F12 yet.
                    }
                    else if (Math.Round(targetAmount, 2) != -Math.Round(amount, 2))
                    {
                        // we jumped to the wrong place then!
                        throw new Exception("F12 jumped to the wrong transaction");
                    }

                    if (sourceAccount != null && targetAccount != null && ((tofrom == "to" && fromto == "from") || (tofrom == "from" && fromto == "to")))
                    {
                        return;
                    }

                    // not there yet...
                    Thread.Sleep(100);
                }
            }

            throw new Exception("Timeout waiting for F12 to work");
        }

        internal void Export(string name)
        {
            this.control = this.window.FindTransactionGrid().control;
            this.control.SetFocus();
            ContextMenu menu = new ContextMenu(this.control, true);
            menu.InvokeMenuItem("menuItemExport");
        }

        internal void Focus()
        {
            this.control.SetFocus();
        }
    }

    public class TransactionViewRow
    {
        private readonly TransactionViewWrapper view;
        private readonly AutomationElement item;

        public TransactionViewRow(TransactionViewWrapper view, AutomationElement item)
        {
            this.view = view;
            this.item = item;
        }

        public AutomationElement Element { get { return this.item; } }

        public void Select()
        {
            this.view.Focus();
            SelectionItemPattern select = (SelectionItemPattern)this.item.GetCurrentPattern(SelectionItemPattern.Pattern);
            select.Select();
            this.ScrollIntoView();
        }

        public bool IsSelected
        {
            get
            {
                SelectionItemPattern select = (SelectionItemPattern)this.item.GetCurrentPattern(SelectionItemPattern.Pattern);
                return select.Current.IsSelected;
            }
        }

        public void ScrollIntoView()
        {
            ScrollItemPattern scroll = (ScrollItemPattern)this.item.GetCurrentPattern(ScrollItemPattern.Pattern);
            scroll.ScrollIntoView();
        }

        public void Delete()
        {
            this.Select();

            view.Focus();

            Thread.Sleep(30);
            Input.TapKey(Key.Delete);
            Thread.Sleep(30);
        }

        internal AttachmentDialogWrapper ClickAttachmentsButton()
        {
            this.ScrollIntoView();

            // AutomationId:	"CommandScanAttachment"
            try
            {
                var cell = this.GetCell("Attachment");
                cell.Invoke();
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot find the Attachment column: " + ex.Message);
            }

            AutomationElement dialog = this.view.Window.Element.FindChildWindow("Attachments", 5);
            if (dialog == null)
            {
                throw new Exception("Cannot find the AttachmentDialog");
            }
            return new AttachmentDialogWrapper(dialog);
        }

        internal string GetCheckNumber()
        {
            TransactionViewCell cell = this.GetCell("Number");
            return cell.GetValue();
        }
        internal void SetCheckNumber(string num)
        {
            TransactionViewCell cell = this.GetCell("Number");
            cell.SetValue(num);
        }

        internal string GetDate()
        {
            TransactionViewCell cell = this.GetCell("Date");
            return cell.GetValue();
        }
        internal void SetDate(string dateTime)
        {
            TransactionViewCell cell = this.GetCell("Date");
            cell.SetValue(dateTime);
        }

        internal string GetPayee()
        {
            TransactionViewCell cell = this.GetCell("Payee");
            return cell.GetValue();
        }
        internal void SetPayee(string payee)
        {
            TransactionViewCell cell = this.GetCell("Payee");
            cell.SetValue(payee);
        }

        internal string GetCategory()
        {
            TransactionViewCell cell = this.GetCell("Category");
            return cell.GetValue();
        }
        internal void SetCategory(string category)
        {
            TransactionViewCell cell = this.GetCell("Category");
            cell.SetValue(category);
        }
        internal string GetMemo()
        {
            TransactionViewCell cell = this.GetCell("Memo");
            return cell.GetValue();
        }
        internal void SetMemo(string memo)
        {
            TransactionViewCell cell = this.GetCell("Memo");
            cell.SetValue(memo);
        }
        internal decimal GetSalesTax()
        {
            return this.GetDecimalColumn("SalesTax");
        }

        private decimal GetDecimalColumn(string name)
        {
            TransactionViewCell cell = this.GetCell(name);
            string s = cell.GetValue();
            decimal d;
            decimal.TryParse(s, out d);
            return d;
        }
        internal void SetSalesTax(decimal tax)
        {
            TransactionViewCell cell = this.GetCell("SalesTax");
            cell.SetValue(tax == 0 ? "" : tax.ToString());
        }
        internal decimal GetPayment()
        {
            return this.GetDecimalColumn("Payment");
        }
        internal decimal GetDeposit()
        {
            return this.GetDecimalColumn("Deposit");
        }
        internal decimal GetAmount()
        {
            int sign = 1;
            TransactionViewCell cell = this.GetCell("Deposit");
            string s = cell.GetValue();
            if (string.IsNullOrEmpty(s))
            {
                sign = -1;
                cell = this.GetCell("Payment");
                s = cell.GetValue();
            }
            decimal p = 0;
            decimal.TryParse(s, out p);
            return p * sign;
        }
        internal void SetAmount(decimal amount)
        {
            if (amount < 0)
            {
                TransactionViewCell cell = this.GetCell("Payment");
                cell.SetValue(amount == 0 ? "" : (-amount).ToString());
            }
            else
            {
                TransactionViewCell cell = this.GetCell("Deposit");
                cell.SetValue(amount == 0 ? "" : amount.ToString());
            }
        }

        // investment transactions
        internal string GetActivity()
        {
            TransactionViewCell cell = this.GetCell("Activity");
            return cell.GetValue();
        }

        internal void SetActivity(string activity)
        {
            TransactionViewCell cell = this.GetCell("Activity");
            cell.SetValue(activity);
        }

        internal string GetSecurity()
        {
            TransactionViewCell cell = this.GetCell("Security");
            return cell.GetValue();
        }
        internal void SetSecurity(string security)
        {
            TransactionViewCell cell = this.GetCell("Security");
            cell.SetValue(security);
        }

        internal decimal GetUnits()
        {
            return this.GetDecimalColumn("Units");
        }
        internal void SetUnits(decimal units)
        {
            TransactionViewCell cell = this.GetCell("Units");
            cell.SetValue(units == 0 ? "" : units.ToString());
        }

        internal decimal GetUnitPrice()
        {
            return this.GetDecimalColumn("UnitPrice");
        }
        internal void SetUnitPrice(decimal price)
        {
            TransactionViewCell cell = this.GetCell("UnitPrice");
            cell.SetValue(price == 0 ? "" : price.ToString());
        }

        internal bool IsPlaceholder
        {
            get
            {
                return this.item.Current.Name == "{NewItemPlaceholder}";
            }
        }

        // todo: toggle status button
        // todo: edit splits.

        public bool IsTransfer
        {
            get
            {
                try
                {
                    string payee = this.GetPayee();
                    return payee.StartsWith("Transfer");
                }
                catch
                {
                    // Sometimes get "Invoke operation failed. Cannot edit another cell or row while the current one has validation errors"
                    return false;
                }
            }
        }

        public bool IsNewRow { get; internal set; }

        /// <summary>
        /// Parse the transfer string 
        /// </summary>
        /// <param name="tofrom">The string 'to' or 'from' dependending on direction of transfer</param>
        /// <returns>The account that we are transfering to or from</returns>
        public string ParseTransferPayee(out string tofrom)
        {
            string payee = this.GetPayee();

            if (payee.StartsWith("Transfer"))
            {
                payee = payee.Substring(8).Trim();
                int i = payee.IndexOf(':');
                if (i > 0)
                {
                    tofrom = payee.Substring(0, i);
                    return payee.Substring(i + 1).Trim();
                }
            }

            throw new Exception("Expecting a Transfer transaction");
        }

        internal void Focus()
        {
            var cell = this.GetCell("Payee");
            cell.SetFocus();
        }

        internal void CommitEdit()
        {
            InvokePattern p = (InvokePattern)this.item.GetCurrentPattern(InvokePattern.Pattern);
            p.Invoke();
            // Now the DataGrid implementation of Invoke deliberately de-selects the row for some
            // strange reason, so we have to reselect it here.
            this.Select();
        }

        internal void BeginEdit()
        {
            var payee = this.GetPayee();
            this.SetPayee(payee);
        }

        internal TransactionViewCell GetCell(string columnName)
        {
            TransactionViewColumn col = this.view.GetColumn(columnName);
            ScrollItemPattern scroll = (ScrollItemPattern)this.item.GetCurrentPattern(ScrollItemPattern.Pattern);
            scroll.ScrollIntoView();
            string name = col.Name;
            int index = col.GetEffectiveIndex();
            AutomationElement cell = null;
            TransactionViewRow row = this;

            for (int retries = 5; retries > 0; retries--)
            {
                int i = 0;
                foreach (AutomationElement e in row.item.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "DataGridCell")))
                {
                    if (i == index)
                    {
                        cell = e;
                        break;
                    }
                    i++;
                }
                if (cell != null)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(50);
                    row = this.Refresh();
                }
            }

            if (cell == null)
            {
                throw new Exception("Expecting a DataGridCell to appear at index " + index + " in the " + name + ".");
            }

            return new TransactionViewCell(this, cell, col);
        }

        internal TransactionViewRow Refresh()
        {
            if (this.IsPlaceholder || this.IsNewRow)
            {
                return this.view.GetNewRow();
            }
            else if (this.IsSelected)
            {
                return this.view.Selection;
            }
            // shouldn't need refreshing then.
            return this;
        }
    }

    public class TransactionViewColumn
    {
        private readonly string header;
        private readonly string name;
        private readonly string datatype;
        private int index;

        protected TransactionViewColumn(string header)
        {
            this.header = header;
        }

        public TransactionViewColumn(string header, string name, string datatype)
        {
            this.header = header;
            this.name = name;
            this.datatype = datatype;
        }

        public int Index
        {
            get { return this.index; }
            set { this.index = value; }
        }

        public CompoundTransactionViewColumn Parent { get; set; }

        public string Header { get { return this.header; } }

        public string Name { get { return this.name; } }

        public string DataType { get { return this.datatype; } }

        internal int GetEffectiveIndex()
        {
            // find the DataGridCell to activate.
            int index = this.index;
            if (this.Parent != null)
            {
                index = this.Parent.index;
            }
            return index;
        }
    }

    public class CompoundTransactionViewColumn : TransactionViewColumn
    {
        private readonly List<TransactionViewColumn> columns = new List<TransactionViewColumn>();

        public CompoundTransactionViewColumn(string header, params TransactionViewColumn[] cols)
            : base(header)
        {
            if (cols != null)
            {
                int i = 0;
                foreach (TransactionViewColumn tc in cols)
                {
                    this.columns.Add(tc);
                    tc.Parent = this;
                    tc.Index = i++;
                }
            }
        }

        internal TransactionViewColumn GetColumn(string name)
        {
            foreach (TransactionViewColumn tc in this.columns)
            {
                if (tc.Name == name)
                {
                    return tc;
                }
            }
            return null;
        }

    }

    public class TransactionViewColumns
    {
        private readonly List<TransactionViewColumn> columns = new List<TransactionViewColumn>();

        public TransactionViewColumns(params TransactionViewColumn[] cols)
        {
            int i = 0;
            foreach (TransactionViewColumn tc in cols)
            {
                this.columns.Add(tc);
                tc.Index = i++;
            }
        }

        public void AddColumn(string header, string name, string datatype)
        {
            this.columns.Add(new TransactionViewColumn(header, name, datatype));
        }

        public int Count
        {
            get { return this.columns.Count; }
        }

        public TransactionViewColumn GetColumn(string name)
        {
            foreach (TransactionViewColumn tc in this.columns)
            {
                CompoundTransactionViewColumn cc = tc as CompoundTransactionViewColumn;
                if (cc != null)
                {
                    TransactionViewColumn inner = cc.GetColumn(name);
                    if (inner != null)
                    {
                        return inner;
                    }
                }
                else if (tc.Name == name)
                {
                    return tc;
                }
            }
            throw new Exception("Column of name '" + name + "' not found");
        }


        internal TransactionViewColumn GetColumn(int index)
        {
            if (index < 0 || index >= this.columns.Count)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            return this.columns[index];
        }
    }

    public class TransactionViewCell
    {
        TransactionViewRow row;
        AutomationElement cell;
        TransactionViewColumn column;

        public TransactionViewCell(TransactionViewRow row, AutomationElement cell, TransactionViewColumn column)
        {
            this.row = row;
            this.cell = cell;
            this.column = column;
        }

        internal AutomationElement GetContent(bool forEditing)
        {
            string className = this.cell.Current.ClassName;
            if (!forEditing)
            {
                this.Refresh();
                AutomationElement found = null;
                int i = 0;
                foreach (AutomationElement child in this.cell.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.IsEnabledProperty, true)))
                {
                    found = child;
                    if (i == this.column.Index)
                    {
                        break;
                    }
                    i++;
                }

                // Sometimes we have some optional children (like the split buttons on the payment/deposit, so return the previous cell.
                return found;
            }
            else
            {
                AutomationElement editor = null;
                for (int retries = 5; retries > 0 && editor == null; retries--)
                {
                    // invoking the cell puts the cell into edit mode, revealing the inner controls
                    InvokePattern p = (InvokePattern)cell.GetCurrentPattern(InvokePattern.Pattern);
                    p.Invoke();

                    // But this also invalidates the cell AutomationElement! So we have to refetch this cell.
                    Refresh();

                    Thread.Sleep(50); // let editing mode kick in.

                    // Now find the editable control within cell 
                    editor = this.GetEditor();
                    if (editor == null)
                    {
                        Thread.Sleep(500);
                    }
                }

                if (editor == null)
                {
                    throw new Exception("Editor not found in compound cell at index " + this.column.Index);
                }

                return editor;
            }
        }

        private void Refresh()
        {
            this.row = this.row.Refresh();
            var newCell = this.row.GetCell(this.column.Name);
            this.cell = newCell.cell;
        }

        internal AutomationElement GetEditor()
        {
            int editorIndex = 0;
            if (this.column.Parent != null)
            {
                editorIndex = this.column.Index;
            }
            AutomationElement e = TreeWalker.RawViewWalker.GetFirstChild(this.cell);
            if (e == null)
            {
                return null;
            }
            var name = e.Current.ClassName;
            if (name == "TransactionAmountControl")
            {
                e = TreeWalker.RawViewWalker.GetFirstChild(e);
            }
            int i = 0;
            while (i < editorIndex && e != null && e.Current.ClassName != "TextBox")
            {
                name = e.Current.ClassName;
                e = TreeWalker.RawViewWalker.GetNextSibling(e);
                if (e != null && e.Current.ClassName == "TransactionAmountControl")
                {
                    e = TreeWalker.RawViewWalker.GetFirstChild(e);
                }
                i++;
            }
            name = e.Current.ClassName;
            if (name == "TextBlock")
            {
                return null;
            }
            return e;
        }


        public string GetValue()
        {
            int retries = 5;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    switch (this.column.DataType)
                    {
                        case "Button":
                        case "Custom":
                            return "";
                        case "TextBox":
                        case "DatePicker":
                        case "ComboBox":
                            return GetCellValue();
                        default:
                            throw new Exception("Unrecognized datatype: " + this.column.DataType);
                    }
                }
                catch
                {
                    if (i == retries - 1)
                    {
                        throw;
                    }
                }
            }
            return null;
        }

        public void SetValue(string value)
        {
            switch (this.column.DataType)
            {
                case "Button":
                case "TextBlock":
                case "Custom":
                    throw new Exception("Cannot set the value of a " + this.column.DataType + " column");
                case "DatePicker":
                case "ComboBox":
                case "TextBox":
                    this.SetCellValue(value);
                    break;
                default:
                    throw new Exception("Unrecognized datatype: " + this.column.DataType);
            }
        }

        public string GetCellValue()
        {
            var e = this.GetContent(false);
            if (e == null)
            {
                // This can happen on Payment/Deposit fields when one or the other has no value.
                return "";
            }

            AutomationElement text = e.Current.ClassName == "TextBlock" ? e : null;

            string name = this.column.Name;

            if (e.Current.ClassName == "TransactionAmountControl")
            {
                text = TreeWalker.RawViewWalker.GetFirstChild(e);
            }
            else if (e.Current.ClassName == "DataGridCell")
            {
                text = e.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "TextBlock"));
            }

            object obj;
            if (text != null)
            {
                if (text.TryGetCurrentPattern(ValuePattern.Pattern, out obj))
                {
                    ValuePattern vp = (ValuePattern)obj;
                    return vp.Current.Value;
                }

                return text.Current.Name;
            }

            if (e.TryGetCurrentPattern(ValuePattern.Pattern, out obj))
            {
                ValuePattern vp = (ValuePattern)obj;
                return vp.Current.Value;
            }

            throw new Exception("DataCell for column " + name + " at index " + this.column.Index + " does not have a ValuePatten");
        }

        private void SetCellValue(string value)
        {
            var e = this.GetContent(true);

            object obj;
            if (e.TryGetCurrentPattern(ValuePattern.Pattern, out obj))
            {
                ValuePattern vp = (ValuePattern)obj;
                vp.SetValue(value);
                return;
            }

            throw new Exception("DataCell for column " + this.column.Name + " at index " + this.column.Index + " does not have a ValuePatten");
        }

        public void Invoke()
        {
            if (this.column.DataType == "Button")
            {
                AutomationElement e = this.GetContent(true);

                object obj;
                if (e.TryGetCurrentPattern(InvokePattern.Pattern, out obj))
                {
                    InvokePattern invoke = (InvokePattern)obj;
                    invoke.Invoke();
                    return;
                }

                throw new Exception("DataGridCell " + this.column.Name + " does not contain an InvokePattern");
            }
            else
            {
                throw new Exception("Cannot invoke column of this type, expecting a button column");
            }
        }

        internal void SetFocus()
        {
            this.cell.SetFocus();
        }
    }
}
