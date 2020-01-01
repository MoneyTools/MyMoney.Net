using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;
using Walkabout.Utilities;
using System.Windows.Input;
using System.Windows.Threading;
using System.Collections;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout.Controls
{
    /// <summary>
    /// This class provides a bunch of helper functionality that makes the DataGrid more user friendly.
    /// </summary>
    public class MoneyDataGrid : DataGrid
    {
        DataGridColumn sorted;
        bool isEditing;

        public MoneyDataGrid()
        {
            this.Loaded += OnLoaded;
        }

        public ContextMenu ParentMenu { get; set; }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            OnContentMarginChanged();
        }

        public Thickness ContentMargin
        {
            get { return (Thickness)GetValue(ContentMarginProperty); }
            set { SetValue(ContentMarginProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ContentMargin.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ContentMarginProperty =
            DependencyProperty.Register("ContentMargin", typeof(Thickness), typeof(MoneyDataGrid), new PropertyMetadata(new Thickness(0), OnContentMarginChanged));

        private static void OnContentMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MoneyDataGrid md = d as MoneyDataGrid;
            if (md != null)
            {
                md.OnContentMarginChanged();
            }
        }

        private void OnContentMarginChanged()
        {
            var presenter = this.GetRowsPresenter();
            if (presenter != null)
            {
                presenter.Margin = ContentMargin;
            }
        }

        protected override void OnSorting(DataGridSortingEventArgs eventArgs)
        {
            sorted = eventArgs.Column;

            ListSortDirection direction = ListSortDirection.Ascending;
            if (sorted.SortDirection.HasValue)
            {
                direction = sorted.SortDirection.Value;
                if (direction == ListSortDirection.Ascending)
                {
                    direction = ListSortDirection.Descending;
                }
                else
                {
                    direction = ListSortDirection.Ascending;
                }
                sorted.SortDirection = direction;
            }

            // user is sorting the grid, but we will still add our secondary sort order
            // information in this case.
            base.OnSorting(eventArgs);

            // No idea why DataGrid doesn't do this for us, but this method ensures the 
            // selected row stays in view when you sort.
            AsyncScrollSelectedRowIntoView();
        }        

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            ScrollViewer viewer = GetScrollViewer();
            if (viewer != null)
            {
                viewer.RequestBringIntoView += OnRequestBringIntoView;
                viewer.ScrollChanged += OnScrollChanged;
            }
            
            OnContentMarginChanged();

            // Behavior change on windows 8, we have to do this after the template is loaded.
            AsyncScrollSelectedRowIntoView();
        }

        public event ScrollChangedEventHandler ScrollChanged;

        void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ScrollChanged != null)
            {
                ScrollChanged(sender, e);
            }
        }

        void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // this is handy for debugging and finding out why the selected row is not being scrolled into view
            // when we expect it to.
            return;
        }

        public void ClearItemsSource()
        {
            SetItemsSource(null);
        }

        // There is a weird bug in DataGrid that blows if there is a sort in place when items are changed.
        // 
        //System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System.InvalidOperationException: 'Sorting' is not allowed during an AddNew or EditItem transaction.
        //at System.Windows.Data.ListCollectionView.SortDescriptionsChanged(Object sender, NotifyCollectionChangedEventArgs e)
        //at System.Collections.Specialized.NotifyCollectionChangedEventHandler.Invoke(Object sender, NotifyCollectionChangedEventArgs e)
        //at System.Windows.Controls.ItemCollection.CloneList(IList clone, IList master)
        //at System.Windows.Controls.ItemCollection.SynchronizeSortDescriptions(NotifyCollectionChangedEventArgs e, SortDescriptionCollection origin, SortDescriptionCollection clone)
        //at System.Windows.Controls.ItemCollection.SortDescriptionsChanged(Object sender, NotifyCollectionChangedEventArgs e)
        //at System.Collections.Specialized.NotifyCollectionChangedEventHandler.Invoke(Object sender, NotifyCollectionChangedEventArgs e)
        //at System.Windows.Controls.DataGrid.ClearSortDescriptionsOnItemsSourceChange()
        //at System.Windows.Controls.DataGrid.OnCoerceItemsSourceProperty(DependencyObject d, Object baseValue)
        //at System.Windows.DependencyObject.ProcessCoerceValue(DependencyProperty dp, PropertyMetadata metadata, EntryIndex& entryIndex, Int32& targetIndex, EffectiveValueEntry& newEntry, EffectiveValueEntry& oldEntry, Object& oldValue, Object baseValue, Object controlValue, CoerceValueCallback coerceValueCallback, Boolean coerceWithDeferredReference, Boolean coerceWithCurrentValue, Boolean skipBaseValueChecks)
        //at System.Windows.DependencyObject.UpdateEffectiveValue(EntryIndex entryIndex, DependencyProperty dp, PropertyMetadata metadata, EffectiveValueEntry oldEntry, EffectiveValueEntry& newEntry, Boolean coerceWithDeferredReference, Boolean coerceWithCurrentValue, OperationType operationType)
        //at System.Windows.DependencyObject.SetValueCommon(DependencyProperty dp, Object value, PropertyMetadata metadata, Boolean coerceWithDeferredReference, Boolean coerceWithCurrentValue, OperationType operationType, Boolean isInternal)
        //at System.Windows.DependencyObject.SetValue(DependencyProperty dp, Object value)
        //at Walkabout.Views.Controls.TransactionsView.Display(IList data, TransactionViewName name, String caption, Int64 selectedRowId)
        public void SetItemsSource(IEnumerable items)
        {
            ListCollectionView view = CollectionViewSource.GetDefaultView(this.ItemsSource) as ListCollectionView;
            if (view != null)
            {
                if (view.IsAddingNew || view.IsEditingItem)
                {
                    if (view.IsAddingNew)
                    {
                        try
                        {
                            view.CancelNew();
                        }
                        catch
                        {
                        }
                    }
                    else if (view.IsEditingItem)
                    {
                        try
                        {
                            view.CancelEdit();
                        }
                        catch
                        {
                            // sometimes this throws but also removes IsEditing...
                        }
                    }
                }
            }

            var sorted = RemoveSort();
            if (sorted != null)
            {
                this.sorted = sorted;
            }
            try
            {
                // sometimes we get a weird exception saying 
                // 'DeferRefresh' is not allowed during an AddNew or EditItem transaction
                this.ItemsSource = items;
            }
            catch (Exception ex)
            {
                // I want to see these errors
                if (MessageBoxEx.Show(ex.ToString() + "\n\nDo you want to try again?", "Debug Error", MessageBoxButton.OKCancel, MessageBoxImage.Error) == MessageBoxResult.OK)
                {
                    // and try again later...at low priority so that the UI has a chance to settle down...
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SetItemsSource(items);
                    }), DispatcherPriority.Background);
                }
            }
        }

        protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);

            // maintain the current sort order selected by the user
            if (sorted != null && this.Columns.Contains(sorted) && !sorting && e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add
                && e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                SortByColumn(sorted.SortMemberPath, sorted.SortDirection.HasValue ? sorted.SortDirection.Value : ListSortDirection.Ascending);
            }
            AsyncScrollSelectedRowIntoView();
        }


        public void AsyncScrollSelectedRowIntoView()
        {
            object selected = this.SelectedItem;
            bool hadFocus = this.IsKeyboardFocusWithin;
            if (selected != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (selected == this.SelectedItem)
                    {
                        this.ScrollIntoView(selected);
                        if (hadFocus)
                        {
                            if (previousCurrentCell != null && previousCurrentCell.Header != null)
                            {
                                string name = previousCurrentCell.Header.ToString();
                                this.SetFocusOnSelectedRow(selected, name);
                            }
                        }
                    }
                }));
            }
        }
        

#if PerformanceBlocks
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.PrepareContainerForItemOverride))
            {
                base.PrepareContainerForItemOverride(element, item);
            }
        }
#endif

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            AsyncScrollSelectedRowIntoView();
        }


        internal int GetColumnIndexByTemplateHeader(string header)
        {
            int i = 0;
            foreach (DataGridColumn c in this.Columns)
            {
                if (c.Header != null && c.Header.ToString().Contains(header))
                {
                    return i;
                }
                i++;
            }
            return -1;
        }

        public DataGridCell GetCell(int row, int column)
        {
            DataGridRow rowContainer = GetRow(row);

            if (rowContainer != null)
            {
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(rowContainer);
                if (presenter != null)
                {
                    // try to get the cell but it may possibly be virtualized
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // now try to bring into view and retreive the cell
                        this.ScrollIntoView(rowContainer, this.Columns[column]);
                        cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    }
                    return cell;
                }

            }
            return null;
        }

        public DataGridColumn GetColumnOfType<T>()
        {
            foreach (DataGridColumn c in this.Columns)
            {
                if (c.GetType() == typeof(T))
                {
                    return c;
                }
            }
            return null;
        }


        public DataGridRow GetRow(int index)
        {
            DataGridRow row = (DataGridRow)this.ItemContainerGenerator.ContainerFromIndex(index);
            if (row == null)
            {
                // may be virtualized, bring into view and try again
                this.ScrollIntoView(this.Items[index]);
                row = (DataGridRow)this.ItemContainerGenerator.ContainerFromIndex(index);
            }
            return row;
        }

        public DataGridRow GetRowFromItem(object item)
        {
            return (DataGridRow)this.ItemContainerGenerator.ContainerFromItem(item);
        }

        public int GetRowIndex(DataGridCellInfo dgci)
        {
            DataGridRow dgrow = GetRowFromItem(dgci.Item);
            if (dgrow == null)
            {
                MessageBoxEx.Show("Please debug me", "Internal Error");
                return 0;
            }
            return dgrow.GetIndex();
        }

        public int GetColIndex(DataGridCellInfo dgci)
        {
            return dgci.Column.DisplayIndex;
        }

        public bool IsKeyboardFocusWithinSelectedRow
        {
            get
            {
                object selected = this.SelectedItem;
                if (selected != null)
                {
                    DataGridRow row = this.ItemContainerGenerator.ContainerFromItem(selected) as DataGridRow;
                    if (row != null)
                    {
                        return row.IsKeyboardFocusWithin;
                    }
                }
                return false;
            }
        }

        public bool SetFocusOnSelectedRow(object selected, string columnHeader)
        {
            if ((this.ParentMenu != null && this.ParentMenu.IsOpen) ||
                 (this.ContextMenu != null && this.ContextMenu.IsOpen))
            {
                // fix bug where context menu disappears when you right click on a new item.
                return false;
            }
            // the focus this row so that user can continue using keyboard navigation 
            DataGridRow row = this.ItemContainerGenerator.ContainerFromItem(selected) as DataGridRow;
            if (row != null)
            {
                int columnToFocus = GetColumnIndexByTemplateHeader(columnHeader);
                if (columnToFocus < 0)
                {
                    return false;
                }
                DataGridCellInfo dgci = this.SelectedCells[columnToFocus];
                if (dgci != null)
                {
                    int rowIndex = row.GetIndex();
                    int colIndex = GetColIndex(dgci);
                    row.ApplyTemplate();
                    DataGridCell dgc = GetCell(rowIndex, colIndex);
                    if (dgc != null)
                    {
                        dgc.Focus();
                        return true;
                    }
                }
            }
            return false;
        }

        private static T GetVisualChild<T>(Visual parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }

        bool sorting;

        private void SortByColumn(string column, ListSortDirection direction)
        {
            sorting = true;
            try
            {
                ICollectionView dataView = CollectionViewSource.GetDefaultView(this.ItemsSource);
                if (dataView != null)
                {
                    //clear the existing sort order
                    dataView.SortDescriptions.Clear();
                    //create a new sort order for the sorting that is done lastly
                    dataView.SortDescriptions.Add(new SortDescription(column, direction));

                    HashSet<string> sort = new HashSet<string>();
                    sort.Add(column);

                    if (!string.IsNullOrEmpty(SecondarySortOrder))
                    {
                        // Add additional sort descriptions, so long as they are not redundant.
                        foreach (string secondary in SecondarySortOrder.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!sort.Contains(secondary)) 
                            {
                                sort.Add(secondary);
                                dataView.SortDescriptions.Add(new SortDescription(secondary, direction));
                            }
                        }
                    }

                    //refresh the view which in turn refresh the grid
                    dataView.Refresh();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        foreach (DataGridColumn c in this.Columns)
                        {
                            if (c.SortMemberPath == column)
                            {
                                // make sure the UI shows our sort direction!
                                c.SortDirection = direction;
                                break;
                            }
                        }
                    }));
                }

            }
            finally
            {
                sorting = false;
            }            
        }

        /// <summary>
        /// Specifies a columns that you want to always use as a secondary sort order in case the user
        /// sorts by another column that has duplicate values.  For example, if they sort by Payee, you
        /// make want to then do a secondary sort by Date.  This property can have multiple column names
        /// separated by comma.
        /// </summary>
        public string SecondarySortOrder { get; set; }


        private DataGridColumn RemoveSort()
        {
            DataGridColumn result = null;
            foreach (DataGridColumn c in this.Columns)
            {
                if (c.SortDirection.HasValue)
                {
                    result = c;
                    c.ClearValue(DataGridColumn.SortDirectionProperty);
                }
            }
            return result;
        }

        private enum InputEventType
        {
            None,
            TabKey,
            LeftMouseButton,
            RightMouseButton
        }

        InputEventType cellChangeEvent;

        protected override void OnPreviewMouseRightButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonDown(e);
            cellChangeEvent = InputEventType.RightMouseButton;
            this.Focus();
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            this.cellChangeEvent = (e.Key == Key.Tab) ? InputEventType.TabKey : InputEventType.None;
            base.OnPreviewKeyDown(e);
        }

        DataGridColumn previousCurrentCell;

        protected override void OnCurrentCellChanged(EventArgs e)
        {
            base.OnCurrentCellChanged(e);

            if (this.CurrentCell != null && this.CurrentCell.Column != null && this.CurrentCell.Column.Header != null) 
            {
                previousCurrentCell = this.CurrentCell.Column;
            }

            // Automatically enter edit mode on the next cell that we enter.
            if (cellChangeEvent == InputEventType.TabKey || cellChangeEvent == InputEventType.LeftMouseButton)
            {
                object selected = this.SelectedItem;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (this.SelectedItem == selected && this.CurrentCell != null)
                    {
                        this.BeginEdit();
                    }
                }), DispatcherPriority.Background);
            }
            ClearAutoEdit();
        }

        /// <summary>
        /// This stops the data grid from going into auto-edit mode when the cell changed event is handled.
        /// </summary>
        public void ClearAutoEdit()
        {
            cellChangeEvent = InputEventType.None;
        }


        /// <summary>
        /// In order to support multiple text boxes in one column, this method implements what you might want
        /// the TAB key to do.
        /// </summary>
        public virtual bool MoveFocusToNextEditableField()
        {
            if (this.SelectedItem == null)
            {
                return false;
            }

            // First check if the current column has more than one editable control.
            FrameworkElement contentPresenter = this.CurrentColumn.GetCellContent(this.SelectedItem);
            List<Control> editors = new List<Control>();
            WpfHelper.FindEditableControls(contentPresenter, editors);

            // Move focus to the next editor in the cell if there is one.  
            // This can happen on the "Payee/Category/Memo" column which has 3 editors in one column.
            for (int i = 0; i < editors.Count; i++)
            {
                Control editor = editors[i];
                if (editor.IsKeyboardFocusWithin && i + 1 < editors.Count)
                {
                    editor = editors[i + 1];
                    OnStartEdit(editor);
                    return true;
                }
            }
            {
                // Begin edit on the next column, and set focus on the text edit field and skip non-editable columns.
                DataGridColumn c = this.CurrentColumn;
                int i = this.Columns.IndexOf(c) + 1;
                for (int n = this.Columns.Count; i < n; i++)
                {
                    DataGridColumn next = this.Columns[i];
                    if (next != null && !next.IsReadOnly)
                    {
                        this.CurrentColumn = next;
                        return true;
                    }
                }
            }
            return false;
        }


        public virtual bool MoveFocusToPreviousEditableField()
        {
            if (this.SelectedItem == null)
            {
                return false;
            }

            // First check if the current column has more than one editable control.
            FrameworkElement contentPresenter = this.CurrentColumn.GetCellContent(this.SelectedItem);
            List<Control> editors = new List<Control>();
            WpfHelper.FindEditableControls(contentPresenter, editors);

            // move focus to the previous editor.
            for (int i = editors.Count - 1; i >= 0; i--)
            {
                Control editor = editors[i];
                if (editor.IsKeyboardFocusWithin && i - 1 >= 0)
                {
                    editor = editors[i - 1];
                    OnStartEdit(editor);
                    return true;
                }
            }
            {
                // Shift tab goes backwards.
                DataGridColumn c = this.CurrentColumn as DataGridColumn;
                int i = this.Columns.IndexOf(c) - 1;
                for (int n = this.Columns.Count; i >= 0; i--)
                {
                    DataGridColumn next = this.Columns[i] as DataGridColumn;
                    if (next != null && !next.IsReadOnly)
                    {
                        this.CurrentColumn = next;
                        return true;
                    }
                }
            }
            return false;
        }


        public void OnStartEdit(Control e)
        {
            e.Focus();
            TextBox box = e as TextBox;
            if (box == null && e.Template != null)
            {
                box = e.Template.FindName("PART_TextBox", e) as TextBox;
                if (box == null)
                {
                    box = e.Template.FindName("PART_EditableTextBox", e) as TextBox;
                }
            }

            if (box != null)
            {
                box.SelectAll();
            }
        }

        protected override void OnBeginningEdit(DataGridBeginningEditEventArgs e)
        {
            base.OnBeginningEdit(e);
            isEditing = true;

            if (!e.Cancel)
            {
                DataGridColumn column = e.Column;
                DataGridRow row = e.Row;
                RoutedEventArgs args = e.EditingEventArgs;
                if (row.IsSelected)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OnStartEdit(column, row, args);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        protected override void OnRowEditEnding(DataGridRowEditEndingEventArgs e)
        {
            base.OnRowEditEnding(e);
            isEditing = false;
        }

        public bool IsEditing { get { return this.isEditing; } }

        private void OnStartEdit(DataGridColumn column, DataGridRow row, RoutedEventArgs args)
        {
            Control editor = this.GetCellEditor(column, row, args);
            if (editor != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.OnStartEdit(editor);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        public Control GetCellEditor(DataGridColumn column, DataGridRow row, RoutedEventArgs args)
        {
            FrameworkElement contentPresenter = column.GetCellContent(row);
            Control editor = null;
            MouseButtonEventArgs me = args as MouseButtonEventArgs;
            if (me != null)
            {
                HitTestResult result = VisualTreeHelper.HitTest(contentPresenter, me.GetPosition(contentPresenter));
                if (result != null)
                {
                    editor = WpfHelper.FindAncestor<TextBox>(result.VisualHit);
                    if (editor == null)
                    {
                        editor = WpfHelper.FindAncestor<DatePicker>(result.VisualHit);
                        if (editor == null)
                        {
                            editor = WpfHelper.FindAncestor<ComboBox>(result.VisualHit);
                        }
                    }
                }
            }
            else
            {
                // maybe it was the TAB key or something, so we have to find the first TextBox in the new column that we can focus on.
                List<Control> editors = new List<Control>();
                WpfHelper.FindEditableControls(contentPresenter, editors);
                if (editors.Count > 0)
                {
                    editor = editors[0];
                }
            }
            return editor;
        }


        /// <summary>
        /// Find a column by SortMemberPath (since the DataGridColumns don't have names).
        /// </summary>
        public DataGridColumn FindColumn(string sortMemberPath)
        {
            foreach (DataGridColumn column in this.Columns)
            {
                if (column.SortMemberPath == sortMemberPath)
                {
                    return column;
                }
            }
            return null;
        }

        /// <summary>
        /// Get the uncommitted value of the given column in the currently selected row.
        /// </summary>
        /// <param name="sortMemberPath">The way to find the column</param>
        /// <param name="columnEditorIndex">The index of the edit in the column (some columns can have more than one editor)</param>
        /// <returns>The uncommitted TextBlock value</returns>
        public string GetUncommittedColumnText(DataGridRow row, string sortMemberPath, int columnEditorIndex = 0)
        {
            DataGridColumn column = this.FindColumn(sortMemberPath);
            if (column != null)
            {
                FrameworkElement contentPresenter = column.GetCellContent(row.DataContext);
                if (row.IsEditing)
                {
                    List<Control> editors = new List<Control>();
                    WpfHelper.FindEditableControls(contentPresenter, editors);
                    if (editors.Count > columnEditorIndex)
                    {
                        Control editor = editors[columnEditorIndex];
                        TextBox box = editor as TextBox;
                        if (box != null)
                        {
                            return box.Text;
                        }
                        ComboBox combo = editor as ComboBox;
                        if (combo != null)
                        {
                            return combo.Text;
                        }
                        DatePicker picker = editor as DatePicker;
                        if (picker != null)
                        {
                            return picker.Text;
                        }
                    }
                    else
                    {
                        // Row might not yet be committed to Transaction and edited value
                        // is stored in a TextBlock.
                        List<TextBlock> blocks = new List<TextBlock>();
                        WpfHelper.FindTextBlocks(contentPresenter, blocks);
                        if (blocks.Count > columnEditorIndex)
                        {
                            TextBlock block = blocks[columnEditorIndex];
                            if (block != null)
                            {
                                return block.Text;
                            }
                        }

                    }
                }
            }
            return null;
        }


        public void SetUncommittedColumnText(DataGridRow row, string sortMemberPath, int columnEditorIndex, string value)
        {
            DataGridColumn column = this.FindColumn(sortMemberPath);
            if (column != null)
            {
                SetColumnValue(row, column, columnEditorIndex, value);
            }
        }

        public static void SetColumnValue(DataGridRow row, DataGridColumn column, int columnEditorIndex, string value)
        {
            FrameworkElement contentPresenter = column.GetCellContent(row.DataContext);
            if (row.IsEditing)
            {                
                List<Control> editors = new List<Control>();
                WpfHelper.FindEditableControls(contentPresenter, editors);
                if (editors.Count > columnEditorIndex)
                {
                    Control editor = editors[columnEditorIndex];
                    TextBox box = editor as TextBox;
                    if (box != null)
                    {
                        box.Text = value;
                    }
                    ComboBox combo = editor as ComboBox;
                    if (combo != null)
                    {
                        combo.Text = value;
                    }
                    DatePicker picker = editor as DatePicker;
                    if (picker != null)
                    {
                        picker.Text = value;
                    }
                }
            }
            else
            {
                // can't set the text value if itis not being edited
                throw new Exception("Row is not being edited");
            }
        }

        #region Drag/Drop Gesture

        public bool SupportDragDrop { get; set; }

        private bool mouseDown;
        private bool dragging;
        private Point downPosition;
        private const int Threshold = 10;
        private TextBox editBox;

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            cellChangeEvent = InputEventType.LeftMouseButton;
            DataGrid grid = this;
            this.downPosition = e.GetPosition(grid);
            ScrollBar sb = WpfHelper.FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject);
            if (sb == null)
            {
                Thumb thumb = WpfHelper.FindAncestor<Thumb>(e.OriginalSource as DependencyObject);
                if (thumb == null)
                {
                    editBox = WpfHelper.FindAncestor<TextBox>(e.OriginalSource as DependencyObject);
                    mouseDown = true;
                }
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            MoneyDataGrid grid = this;

            if (mouseDown && !dragging && grid.SelectedItem != null && !e.Handled && SupportDragDrop)
            {
                Point pos = e.GetPosition(grid);

                if ((pos - downPosition).Length > Threshold)
                {
                    TextBox hit = null;
                    if (IsEditing)
                    {
                        HitTestResult result = VisualTreeHelper.HitTest(grid, pos);
                        if (result != null && result.VisualHit != null)
                        {
                            hit = WpfHelper.FindAncestor<TextBox>(result.VisualHit);
                        }
                    }
                    if (hit != editBox || !IsEditing)
                    {
                        // we have moved outside the current row, so must be trying to do a drag/drop
                        try
                        {
                            e.Handled = true;
                            CommitEdit();
                            DragDrop.DoDragDrop(grid, grid.SelectedItem, System.Windows.DragDropEffects.All);
                            StopAutoScrolling();
                        }
                        catch (Exception ex)
                        {
                            MessageBoxEx.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    dragging = false;
                    mouseDown = false;
                    editBox = null;
                }
            }
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            dragging = false;
            mouseDown = false;
            editBox = null;
        }

        const double AutoScrollMargin = 50;

        protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
        {
            base.OnQueryContinueDrag(e);

            if (e.Action == DragAction.Cancel || e.Action == DragAction.Drop)
            {
                StopAutoScrolling();
            }
            else if (e.Action == DragAction.Continue)
            {
                Point pt = MouseUtilities.GetMousePosition(this);
                double height = this.RenderSize.Height;
                if (pt.Y < AutoScrollMargin)
                {
                    // start scrolling scroll up
                    StartAutoScrolling(pt.Y - AutoScrollMargin);
                }
                else if (pt.Y > height - AutoScrollMargin)
                {
                    // start scrolling scroll down.
                    StartAutoScrolling(pt.Y - (height - AutoScrollMargin));
                }
                else
                {
                    // stop autoscrolling.
                    StopAutoScrolling();
                }
            }
        }

        private void StopAutoScrolling()
        {
            if (scrollTimer != null)
            {
                scrollTimer.Tick -= OnScrollTimerTick;
                scrollTimer.Stop();
                scrollTimer = null;
            }
        }

        double scrollSpeed;
        double nextInterval;
        const double InitialDelay = 300;

        private void StartAutoScrolling(double distanceFromMargin)
        {
            scrollSpeed = distanceFromMargin;
            if (scrollTimer == null)
            {
                scrollTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(InitialDelay), DispatcherPriority.Normal, OnScrollTimerTick, this.Dispatcher);
                scrollTimer.Start();
            }
            else
            {
                // speed increases linearly as distance from AutoScrollMargin increases
                // Speed is inverse delay so the delay = InitialDelay - abs(distance).
                double slope = (InitialDelay / AutoScrollMargin);
                nextInterval = Math.Max(1, InitialDelay - (Math.Abs(distanceFromMargin) * slope));
            }
        }

        DispatcherTimer scrollTimer;
        ScrollViewer scrollViewer;

        private ScrollViewer GetScrollViewer()
        {
            if (scrollViewer == null && this.Template != null)
            {
                scrollViewer = this.Template.FindName("DG_ScrollViewer", this) as ScrollViewer;
                if (scrollViewer == null)
                {
                    scrollViewer = this.FindFirstDescendantOfType<ScrollViewer>();
                }
            }
            return this.scrollViewer;
        }

        public Tuple<int,int> GetVisibleRows()
        {
            ScrollViewer sv = GetScrollViewer();
            if (sv != null)
            {
                int firstRow = (int)sv.VerticalOffset;
                int lastRow = (int)sv.VerticalOffset + (int)sv.ViewportHeight + 1;
                return new Tuple<int, int>(firstRow, lastRow);
            }
            return null;
        }

        private DataGridRowsPresenter GetRowsPresenter()
        {
            ScrollViewer viewer = GetScrollViewer();
            if (viewer != null)
            {
                ScrollContentPresenter scp = viewer.FindName("PART_ScrollContentPresenter") as ScrollContentPresenter;
                return viewer.FindFirstDescendantOfType<DataGridRowsPresenter>();
            }
            return null;
        }

        private IEnumerable<DataGridColumnHeader> GetDataGridColumnHeaders()
        {
            ScrollViewer viewer = GetScrollViewer();
            if (viewer != null)
            {
                DataGridColumnHeadersPresenter result = viewer.FindName("PART_ColumnHeadersPresenter") as DataGridColumnHeadersPresenter;
                result = viewer.FindFirstDescendantOfType<DataGridColumnHeadersPresenter>();
                return result.FindDescendantsOfType<DataGridColumnHeader>();
            }
            return new DataGridColumnHeader[0];
        }

        private void OnScrollTimerTick(object sender, EventArgs e)
        {
            ScrollViewer scrollViewer = GetScrollViewer();
            if (scrollViewer != null)
            {
                if (scrollSpeed < 0)
                {
                    scrollViewer.LineUp();
                }
                else if (scrollSpeed > 0)
                {
                    // if the last line is already visible, then we don't need to scroll any further.
                    bool scroll = true;
                    if (scrollViewer.VerticalOffset + 10 > scrollViewer.ScrollableHeight)
                    {
                        // This checks to see if the last row is really visible, and avoid a weird
                        // bouncing jump that happens if we keep calling LineDown without checking.
                        double height = scrollViewer.ActualHeight;
                        DataGridRow lastRow = this.GetRow(this.Items.Count - 1);
                        Point pos = lastRow.TransformToAncestor(scrollViewer).Transform(new Point(0, 0));
                        scroll = (pos.Y + lastRow.DesiredSize.Height > height + 16);
                    }
                    if (scroll)
                    {
                        scrollViewer.LineDown();
                    }
                }
            }
            if (scrollTimer != null)
            {
                scrollTimer.Interval = TimeSpan.FromMilliseconds(nextInterval);
            }

        }

        #endregion

    }
}
