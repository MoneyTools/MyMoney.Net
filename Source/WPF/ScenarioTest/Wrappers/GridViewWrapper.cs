using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Input;
using Walkabout.Tests.Interop;

namespace Walkabout.Tests.Wrappers
{
    public class GridViewWrapper
    {
        private readonly MainWindowWrapper window;
        private AutomationElement control;
        private GridViewColumnWrappers columns;

        public GridViewWrapper(MainWindowWrapper window, AutomationElement control)
        {
            this.window = window;
            this.control = control;
        }

        public MainWindowWrapper Window => this.window;

        public AutomationElement Control { get => this.control; set => this.control = value; }

        public virtual GridViewColumnWrappers Columns { get => columns; set => columns = value; }

        internal GridViewColumnWrapper GetColumn(string name)
        {
            return this.Columns.GetColumn(name);
        }

        internal void Focus()
        {
            this.Control.SetFocus();
        }

        internal void ScrollToEnd()
        {
            this.ScrollVertical(100);
        }

        internal void CommitEdit()
        {
            var selection = this.Selection;
            selection?.CommitEdit();
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

        public bool HasSelection
        {
            get
            {
                return this.Selection != null;
            }
        }

        public GridViewRowWrapper Selection
        {
            get
            {
                SelectionPattern selection = (SelectionPattern)this.Control.GetCurrentPattern(SelectionPattern.Pattern);
                AutomationElement[] selected = selection.Current.GetSelection();
                return (selected == null || selected.Length == 0) ? null : this.WrapRow(selected[0], this.GetRowIndex(selected[0]));
            }
        }

        public int GetRowIndex(AutomationElement row)
        {
            ScrollItemPattern scroll = (ScrollItemPattern)row.GetCurrentPattern(ScrollItemPattern.Pattern);
            scroll.ScrollIntoView();

            int index = 0;
            foreach (AutomationElement e in this.Control.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
            {
                if (e.Current.Name.StartsWith("Walkabout.Data") || e.Current.Name == "{NewItemPlaceholder}")
                {
                    if (e == row)
                    {
                        return index;
                    }
                    index++;
                }
            }

            throw new Exception("Cannot find the specified row");
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

        public List<GridViewRowWrapper> GetItems(bool includePlaceHolder = true)
        {
            List<GridViewRowWrapper> list = new();
            int index = 0;
            foreach (AutomationElement e in this.Control.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
            {
                if (e.Current.Name == "{NewItemPlaceholder}")
                {
                    if (includePlaceHolder)
                    {
                        list.Add(this.WrapRow(e, index));
                    }
                    index++;
                }
                else if (e.Current.Name.StartsWith("Walkabout.Data"))
                {
                    list.Add(this.WrapRow(e, index++));
                }
            }
            return list;
        }

        public GridViewRowWrapper Select(int index)
        {
            List<GridViewRowWrapper> list = this.GetItems();
            if (index >= list.Count)
            {
                throw new ArgumentOutOfRangeException("Index " + index + " is out of range, list only has " + list.Count + " items");
            }

            GridViewRowWrapper item = list[index];
            item.Select();
            return item;
        }

        public GridViewRowWrapper WaitForSelection(int retries = 10, int delay = 100)
        {
            for (int i = 0; i < retries; i++)
            {
                var s = this.Selection;
                if (s != null)
                {
                    return s;
                }
                Thread.Sleep(delay);
            }
            return null;
        }

        internal GridViewRowWrapper AddNew()
        {
            this.ScrollToEnd();
            Thread.Sleep(100);

            AutomationElement placeholder = TreeWalker.RawViewWalker.GetLastChild(this.Control);
            if (placeholder.Current.Name != "{NewItemPlaceholder}")
            {
                throw new Exception("Expecting {NewItemPlaceholder} at the bottom of the DataGrid");
            }

            this.Focus();
            SelectionItemPattern select = (SelectionItemPattern)placeholder.GetCurrentPattern(SelectionItemPattern.Pattern);
            select.Select();

            // This ensures a transaction is created for this placeholder.
            var row = this.GetNewRow();
            row.BeginEdit(); // this can invalidate the row level automation element!            
            return this.GetNewRow();
        }

        internal void Delete(int index)
        {
            var item = this.Select(index);
            item.Delete();
        }

        internal void ScrollVertical(double verticalPercent)
        {
            ScrollPattern sp = (ScrollPattern)this.control.GetCurrentPattern(ScrollPattern.Pattern);
            if (sp.Current.VerticallyScrollable && sp.Current.VerticalScrollPercent != verticalPercent)
            {
                sp.SetScrollPercent(System.Windows.Automation.ScrollPattern.NoScroll, verticalPercent);
            }
        }

        internal void SortBy(GridViewColumnWrapper column)
        {
            AutomationElement header = this.control.FindFirstWithRetries(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ClassNameProperty, "DataGridColumnHeader"),
                new PropertyCondition(AutomationElement.NameProperty, column.Header)));
            if (header != null)
            {
                AutomationElement e = TreeWalker.RawViewWalker.GetFirstChild(header);
                if (e != null)
                {
                    var thumb = TreeWalker.RawViewWalker.GetNextSibling(e);
                    if (thumb != null && !thumb.Current.IsOffscreen)
                    {
                        var arrow = thumb.Current.Name;
                        if (!string.IsNullOrEmpty(arrow))
                        {
                            // 59211 is the up arrow.
                            if (arrow[0] == 59210)
                            {
                                // then we are already sorting by this collumn.
                                return;
                            }
                        }
                    }
                }
                InvokePattern p = (InvokePattern)header.GetCurrentPattern(InvokePattern.Pattern);
                p.Invoke();
            }
            else
            {
                Debug.WriteLine("Could not find header for column: " + column.Name);
            }
        }

        public GridViewRowWrapper GetNewRow()
        {
            GridViewRowWrapper lastrow = null;
            int index = 0;
            foreach (AutomationElement e in this.Control.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
            {
                if (e.Current.Name == "{NewItemPlaceholder}")
                {
                    lastrow = this.WrapRow(e, index++);
                    break;
                }
                else
                {
                    lastrow = this.WrapRow(e, index++);
                }
            }

            // no place holder means the placeholder is being edited.
            lastrow.IsNewRow = true;
            return lastrow;
        }

        public virtual GridViewRowWrapper WrapRow(AutomationElement e, int index)
        {
            return new GridViewRowWrapper(this, e, index);
        }

    }

    public class GridViewRowWrapper
    {
        private readonly GridViewWrapper view;
        private readonly AutomationElement item;
        private readonly int index;

        public GridViewRowWrapper(GridViewWrapper view, AutomationElement item, int index)
        {
            this.view = view;
            this.item = item;
            this.index = index;
        }

        public AutomationElement Element { get { return this.item; } }

        public string Id => this.item.Current.AutomationId;

        public int Index => this.index;

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

            this.view.Focus();

            Thread.Sleep(30);
            Input.TapKey(Key.Delete);
            Thread.Sleep(30);
        }

        protected decimal GetDecimalColumn(string name)
        {
            var cell = this.GetCell(name);
            string s = cell.GetValue();
            if (decimal.TryParse(s, out decimal d))
            {
                return d;
            }
            return 0;
        }

        protected void SetDecimalColumn(string name, decimal value)
        {
            var cell = this.GetCell(name);
            cell.SetValue(value.ToString());
        }

        internal bool IsPlaceholder
        {
            get
            {
                return this.item.Current.Name == "{NewItemPlaceholder}";
            }
        }

        public bool IsNewRow { get; internal set; }

        public System.Windows.Rect Bounds => this.item.Current.BoundingRectangle;

        internal void Focus()
        {
            this.view.Focus();
            var cell = this.GetCell("Payee");
            cell.SetFocus();
        }

        internal void CommitEdit()
        {
            // BUGBUG: the datagrid imnplementation of the Invoke pattern only does a Cell level commit
            // not a row level commit which is what we need here.  
            // InvokePattern p = (InvokePattern)this.item.GetCurrentPattern(InvokePattern.Pattern);
            // p.Invoke();

            // Seems the only way to get a row level commit is to send the ENTER key.
            var newRow = this.IsNewRow;
            this.view.Focus();
            Thread.Sleep(500);
            this.view.Focus();
            Input.TapKey(Key.Enter);
            Thread.Sleep(50);

            // The enter key of course moves the selection to the next row, so this moves it back.
            if (newRow)
            {
                this.Refresh(); // refreshes this row as the placeholder
                // but we don't want to select the placeholder, we want to select the edited row.
                this.view.Select(this.index);
            }
            else
            {
                this.Select();
            }
        }

        internal virtual void BeginEdit()
        {
            InvokePattern p = (InvokePattern)this.item.GetCurrentPattern(InvokePattern.Pattern);
            p.Invoke();
        }

        internal GridViewCellWrapper GetCell(string columnName)
        {
            GridViewColumnWrapper col = this.view.GetColumn(columnName);
            ScrollItemPattern scroll = (ScrollItemPattern)this.item.GetCurrentPattern(ScrollItemPattern.Pattern);
            scroll.ScrollIntoView();
            string name = col.Name;
            int index = col.GetEffectiveIndex();
            AutomationElement cell = null;
            GridViewRowWrapper row = this;

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

            return new GridViewCellWrapper(this, cell, col);
        }

        internal GridViewRowWrapper Refresh()
        {
            if (this.IsPlaceholder || this.IsNewRow)
            {
                return this.view.GetNewRow();
            }
            else if (this.IsSelected)
            {
                return this.view.Selection;
            }
            else if (this.item.Current.BoundingRectangle.IsEmpty)
            {
                return this.view.Select(this.index);
            }
            // shouldn't need refreshing then.
            return this;
        }

    }



    public class GridViewCellWrapper
    {
        private GridViewRowWrapper row;
        private AutomationElement cell;
        private readonly GridViewColumnWrapper column;

        public GridViewCellWrapper(GridViewRowWrapper row, AutomationElement cell, GridViewColumnWrapper column)
        {
            this.row = row;
            this.cell = cell;
            this.column = column;
        }

        internal AutomationElement GetContent(bool forEditing)
        {
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
                    InvokePattern p = (InvokePattern)this.cell.GetCurrentPattern(InvokePattern.Pattern);
                    p.Invoke();

                    // But this also invalidates the cell AutomationElement! So we have to refetch this cell.
                    this.Refresh();

                    Thread.Sleep(100); // let editing mode kick in.

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
                if (e != null && name == "TransactionAmountControl")
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
                    return this.column.DataType switch
                    {
                        "Button" or "Custom" => "",
                        "TextBox" or "DatePicker" or "ComboBox" => this.GetCellValue(),
                        _ => throw new Exception("Unrecognized datatype: " + this.column.DataType),
                    };
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

            if (e.TryGetCurrentPattern(ValuePattern.Pattern, out object obj))
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

                if (e.TryGetCurrentPattern(InvokePattern.Pattern, out object obj))
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
            try
            {
                this.cell.SetFocus();
            }
            catch { }
        }
    }

    public class GridViewColumnWrapper
    {
        private readonly string header;
        private readonly string name;
        private readonly string datatype;
        private int index;

        protected GridViewColumnWrapper(string header)
        {
            this.header = header;
        }

        public GridViewColumnWrapper(string header, string name, string datatype)
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

        public CompoundGridViewColumnWrapper Parent { get; set; }

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

    public class CompoundGridViewColumnWrapper : GridViewColumnWrapper
    {
        private readonly List<GridViewColumnWrapper> columns = new();

        public CompoundGridViewColumnWrapper(string header, params GridViewColumnWrapper[] cols)
            : base(header)
        {
            if (cols != null)
            {
                int i = 0;
                foreach (GridViewColumnWrapper tc in cols)
                {
                    this.columns.Add(tc);
                    tc.Parent = this;
                    tc.Index = i++;
                }
            }
        }

        internal GridViewColumnWrapper GetColumn(string name)
        {
            foreach (GridViewColumnWrapper tc in this.columns)
            {
                if (tc.Name == name)
                {
                    return tc;
                }
            }
            return null;
        }

    }

    public class GridViewColumnWrappers
    {
        private readonly List<GridViewColumnWrapper> columns = new();

        public GridViewColumnWrappers(params GridViewColumnWrapper[] cols)
        {
            int i = 0;
            foreach (GridViewColumnWrapper tc in cols)
            {
                this.columns.Add(tc);
                tc.Index = i++;
            }
        }

        public void AddColumn(string header, string name, string datatype)
        {
            this.columns.Add(new GridViewColumnWrapper(header, name, datatype));
        }

        public int Count
        {
            get { return this.columns.Count; }
        }

        public GridViewColumnWrapper GetColumn(string name)
        {
            foreach (GridViewColumnWrapper tc in this.columns)
            {
                if (tc is CompoundGridViewColumnWrapper cc)
                {
                    GridViewColumnWrapper inner = cc.GetColumn(name);
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

        internal GridViewColumnWrapper GetColumn(int index)
        {
            if (index < 0 || index >= this.columns.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return this.columns[index];
        }
    }

}
