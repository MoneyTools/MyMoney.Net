using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml.Linq;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Utilities;
using System.Windows.Media;
using System.Windows.Threading;
using Walkabout.Help;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout.Views.Controls
{

    /// <summary>
    /// Interaction logic for CateogiresControl.xaml
    /// </summary>
    public partial class CategoriesControl : System.Windows.Controls.UserControl, IClipboardClient
    {
        #region Commands
        public static RoutedUICommand CommandCategoryProperties;
        public static RoutedUICommand CommandAddCategory;
        public static RoutedUICommand CommandRenameCategory;
        public static RoutedUICommand CommandDeleteCategory;
        public static RoutedUICommand CommandExpandAll;
        public static RoutedUICommand CommandCollapseAll;
        public static RoutedUICommand CommandResetBudget;

        static CategoriesControl()
        {
            CommandCategoryProperties = new RoutedUICommand("Properties", "CommandCategoryProperties", typeof(AccountsControl));
            CommandAddCategory = new RoutedUICommand("Add", "CommandAddCategory", typeof(AccountsControl));
            CommandRenameCategory = new RoutedUICommand("Rename", "CommandRenameCategory", typeof(AccountsControl));
            CommandDeleteCategory = new RoutedUICommand("Delete", "CommandDeleteCategory", typeof(AccountsControl));
            CommandExpandAll = new RoutedUICommand("Expand All", "CommandExpandAll", typeof(AccountsControl));
            CommandCollapseAll = new RoutedUICommand("Collapse All", "CommandCollapseAll", typeof(AccountsControl));
            CommandResetBudget = new RoutedUICommand("Reset Budget", "CommandResetBudget", typeof(AccountsControl));
        }

        #endregion 

        #region Constructors

        public CategoriesControl()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.CategoriesControlInitialize))
            {
#endif
                InitializeComponent();

                this.treeView.SelectedItemChanged += new RoutedPropertyChangedEventHandler<object>(OnSelectedItemChanged);
                this.treeView.Loaded += new RoutedEventHandler(OnTreeViewLoaded);

                this.IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnIsVisibleChanged);

                this.dragDropSupport = new DragAndDrop(
                    this.treeView,
                    this.dragDropformatNameForCategory,
                    this.OnDragDropObjectSource,
                    this.OnDragDropObjectTarget,
                    this.OnDragDropSourceOnTarget
                    );


#if PerformanceBlocks
            }
#endif
        }

        #endregion

        #region PROPERTIES

        MyMoney money;

        public MyMoney MyMoney
        {
            get { return this.money; }
            set
            {
                if (this.money != null)
                {
                    this.money.Changed -= new EventHandler<ChangeEventArgs>(OnMoneyChanged);
                }
                this.money = value;
                if (value != null)
                {
                    this.money.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);
                    OnMoneyChanged(this, new ChangeEventArgs(this.money.Categories, null, ChangeType.Reloaded));
                }
            }
        }

        public Category Selected
        {
            get
            {
                Category c = treeView.SelectedItem as Category;
                if (c != null)
                {
                    return c;
                }
                return null;
            }
            set
            {
                if (value != null && value != this.Selected)
                {
                    List<object> path = new List<object>();
                    if (FindTreeItemByCategory(this.treeView.Items, value, path))
                    {
                        SelectedAndExpandPath(treeView, path);
                    }
                }
            }
        }

        public CategoryGroup SelectedGroup
        {
            get
            {
                CategoryGroup g = treeView.SelectedItem as CategoryGroup;
                if (g != null)
                {
                    return g;
                }
                return null;
            }
        }

        public IServiceProvider Site { get; set; }

        #endregion

        #region DataContext

        bool insideUpdateRoots;
        CategoryBalance total;
        string filterCategory;

        void UpdateRoots()
        {
            if (insideUpdateRoots) return;
            insideUpdateRoots = true;

            try
            {
                bool hasRoots = this.treeView.ItemsSource != null;
                ObservableCollection<CategoryGroup> groups = new ObservableCollection<CategoryGroup>();

                List<Category> roots;

                if (string.IsNullOrWhiteSpace(filterCategory))
                {
                    // No filter return the top root items as is
                    roots = this.money.Categories.GetRootCategories();
                }
                else
                {
                    roots = GetDeepFilteredRootCategories(); 
                }

                //--------------------------------------------------------------
                // Create the 4 top holding Groups
                //
                //      0 - INCOME
                //      1 - EXPENSE
                //      2 - NONE
                //      3 - TOTAL
                //

                decimal balance = 0;

                foreach (CategoryType t in new CategoryType[] { CategoryType.Income, CategoryType.Expense, CategoryType.Investments, CategoryType.None })
                {
                    ObservableCollection<Category> list = new ObservableCollection<Category>();

                    foreach (Category c in roots)
                    {
                        if (c == this.MyMoney.Categories.Split)
                        {
                            continue;
                        }

                        if (c.Type == t || (c.Type == CategoryType.Savings && t == CategoryType.Income))
                        {
                            balance += c.Balance;
                            list.Add(c);
                        }
                    }
                    CategoryGroup g = new CategoryGroup()
                    {
                        Name = t.ToString(),
                        Subcategories = list
                    };

                    groups.Add(g);
                }

                total = new CategoryBalance() { Name = "Total", Balance = balance };
                groups.Add(total);

                if (!hasRoots)
                {
                    this.treeView.ItemsSource = groups;
                }
                else
                {
                    // merge!
                    ObservableCollection<CategoryGroup> oldGroups = this.treeView.ItemsSource as ObservableCollection<CategoryGroup>;

                    Debug.Assert(oldGroups.Count == groups.Count);

                    for (int i = 0; i < groups.Count; i++)
                    {
                        CategoryGroup a = groups[i];
                        CategoryGroup b = oldGroups[i];
                        if (a is CategoryBalance)
                        {
                            CategoryBalance ab = (CategoryBalance)a;
                            CategoryBalance bb = (CategoryBalance)b;
                            bb.Balance = ab.Balance;
                        }
                        else
                        {
                            SyncCollections(a.Subcategories, b.Subcategories);
                        }
                    }
                }
            }
            finally
            {
                insideUpdateRoots = false;
            }
        }

        private List<Category> GetDeepFilteredRootCategories()
        {

            List<Category> newFilteredList = new List<Category>();

            // Only keep the category matching the filter
            foreach (Category c in this.money.Categories.GetRootCategories())
            {
                if (StringHelpers.SafeLower(c.Name).Contains(this.filterCategory))
                {
                    // The root itself match the filter, no need to dig deeper
                    newFilteredList.Add(c);
                }
                else
                {
                    AddRootIfOneOrMoreChildMatchFilder(c, c, newFilteredList);
                }
            }
            return newFilteredList;
        }


        bool AddRootIfOneOrMoreChildMatchFilder(Category rootCategory, Category category, List<Category> newFilteredList)
        {

            if (category.HasSubcategories)
            {
                foreach (Category c in category.GetSubcategories())
                {
                    if (StringHelpers.SafeLower(c.Name).Contains(this.filterCategory))
                    {
                        newFilteredList.Add(rootCategory);
                        return true;
                    }
                    else
                    {
                        if (AddRootIfOneOrMoreChildMatchFilder(rootCategory, c, newFilteredList) == true)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        void UpdateBalance()
        {
            if (this.total != null)
            {
                List<Category> roots = this.money.Categories.GetRootCategories();
                decimal balance = 0;
                foreach (CategoryType t in new CategoryType[] { CategoryType.Income, CategoryType.Expense, CategoryType.Investments, CategoryType.None })
                {
                    foreach (Category c in roots)
                    {
                        if (c == this.MyMoney.Categories.Split)
                        {
                            continue;
                        }

                        if (c.Type == t || (c.Type == CategoryType.Savings && t == CategoryType.Income))
                        {
                            balance += c.Balance;
                        }
                    }
                }
                this.total.Balance = balance;
            }
        }

        void SyncCollections(ObservableCollection<Category> newList, ObservableCollection<Category> master)
        {
            HashSet<Category> masterLookup = new HashSet<Category>(master);
            HashSet<Category> newLookup = new HashSet<Category>(newList);

            // remove any items that should no longer be in the list.
            foreach (Category c in masterLookup)
            {
                if (!newLookup.Contains(c))
                {
                    master.Remove(c);
                }
            }
            masterLookup = new HashSet<Category>(master);

            // add any new items in the right order
            for (int i = 0, n = newList.Count; i < n; i++)
            {
                Category c = newList[i];
                if (i >= master.Count)
                {
                    master.Add(c);
                }
                else
                {
                    Category d = master[i];
                    if (c != d)
                    {
                        master.Insert(i, c);
                    }
                }
            }

            // truncate items that hang off the end of the list
            while (master.Count > newList.Count)
            {
                master.RemoveAt(master.Count - 1);
            }
        }

        void OnTreeViewLoaded(object sender, RoutedEventArgs e)
        {
            ExpandGroups();
        }

        void ExpandGroups()
        {
            foreach (CategoryGroup g in this.treeView.Items)
            {
                ExpandThisItem(this.treeView, g);
            }
        }

        #endregion

        #region Find And Select

        private bool SelectedAndExpandPath(ItemsControl parent, List<object> path)
        {
            if (parent == null || path == null || path.Count == 0)
            {
                return false;
            }

            object top = path[0];

            foreach (object childItem in parent.Items)
            {
                if (childItem == top)
                {
                    if (path.Count == 1)
                    {
                        // We found the item we were looking for, we can stop here
                        SelectThisItem(parent, top);
                        return true;
                    }
                    else
                    {
                        path.RemoveAt(0);
                        ExpandThisItem(parent, childItem);

                        ItemsControl childControl = parent.ItemContainerGenerator.ContainerFromItem(childItem) as ItemsControl;
                        if (childControl != null)
                        {
                            // recurs the remainder of the path.
                            if (SelectedAndExpandPath(childControl, path))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            // Could not build the child tree control containers that's not expected
                        }
                        // We could not find the child item, so at least select the currently matching item
                        SelectThisItem(parent, childItem);
                        return false;
                    }

                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if this item was collapsed and is now expanded.
        bool ExpandThisItem(ItemsControl parent, object item)
        {
            TreeViewItem childNode = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (childNode != null && !childNode.IsExpanded)
            {
                childNode.IsExpanded = true;                
                // Update layout is needed so that items can be selected in this newly visible list.
                // This is to work around a wierd bug in WPF where selecting items in a newly expanded list
                // doesn't work unless we call UpdateLayout here even though that can be wickedly slow.
                this.UpdateLayout();
                return true;
            }

            return false;
        }

        bool SelectThisItem(ItemsControl parent, object item)
        {
            TreeViewItem childItem = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (childItem != null)
            {
                childItem.Focus();
                childItem.BringIntoView();
                return childItem.IsSelected = true;
            }

            return false;
        }

        /// <summary>
        /// Find the given category in the tree and return the 'path' of parent objects that get us to that category.
        /// </summary>
        private bool FindTreeItemByCategory(ItemCollection itemCollection, Category value, List<object> path)
        {
            foreach (object item in itemCollection)
            {
                CategoryGroup g = item as CategoryGroup;
                if (g != null)
                {
                    path.Add(g);
                    if (FindTreeItemByCategory(g.Subcategories, value, path))
                    {
                        return true;
                    }
                    path.Remove(g);
                }
                else
                {
                    Category c = item as Category;
                    path.Add(c);
                    if (c == value)
                    {
                        return true;
                    }

                    if (c.Subcategories != null && c.Subcategories.Count > 0)
                    {
                        if (FindTreeItemByCategory(c.Subcategories, value, path))
                        {
                            return true;
                        }
                    }
                    path.Remove(c);
                }
            }
            return false;
        }

        private bool FindTreeItemByCategory(IList<Category> itemCollection, Category value, List<object> path)
        {
            if (itemCollection != null)
            {
                foreach (Category itv in itemCollection)
                {
                    path.Add(itv);
                    if (itv == value)
                    {
                        return true;
                    }

                    if (itv.Subcategories != null && itv.Subcategories.Count > 0)
                    {
                        if (FindTreeItemByCategory(itv.Subcategories, value, path))
                        {
                            return true;
                        }
                    }
                    path.Remove(itv);
                }
            }
            return false;
        }


        /// <summary>
        /// Apply filtering to the categories being displayed
        /// </summary>
        /// <param name="filterText"></param>
        public void Filter(string filterText)
        {
            filterCategory = StringHelpers.SafeLower(filterText);
            UpdateRoots();
        }
        #endregion

        #region Events

        public event EventHandler SelectionChanged;

        public event EventHandler GroupSelectionChanged;

        public event EventHandler SelectedTransactionChanged;

        void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Category dataBoundItemTreeview = (this.treeView.SelectedItem as Category);
            if (dataBoundItemTreeview != null)
            {
                if (SelectionChanged != null)
                {
                    SelectionChanged(this, EventArgs.Empty);
                }
            }
            CategoryGroup group = (this.treeView.SelectedItem as CategoryGroup);
            if (group != null)
            {
                if (GroupSelectionChanged != null)
                {
                    GroupSelectionChanged(this, EventArgs.Empty);
                }
            }
        }

        public IEnumerable<Transaction> SelectedTransactions { get; set; }

        public void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            bool updateBalance = false;
            bool updateRoots = false;

            if (this.treeView.ItemsSource != null)
            {
                if (sender is Categories || sender is CategoriesControl)
                {
                    updateRoots = true;
                }
                else
                {
                    if (this.IsVisible)
                    {
                        updateBalance = true;
                    }

                    while (args != null)
                    {
                        if (args.Item is Category)
                        {
                            updateRoots = true;
                        }
                        args = args.Next;
                    }

                }
            }
            if (updateRoots)
            {
                Dispatcher.BeginInvoke(new Action(() => { UpdateRoots(); }));
            }
            if (updateBalance)
            {
                Dispatcher.BeginInvoke(new Action(() => { UpdateBalance(); }));
            }
        }

        void OnSelectedTransactionChanged(IEnumerable<Transaction> selection)
        {
            this.SelectedTransactions = selection;
            if (SelectedTransactionChanged != null)
            {
                SelectedTransactionChanged(this, EventArgs.Empty);
            }
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.treeView.ItemsSource == null)
            {
                UpdateRoots();
            }
        }

        private void treeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            HitTestResult result = VisualTreeHelper.HitTest(treeView, e.GetPosition(treeView));
            FrameworkElement hit = result.VisualHit as FrameworkElement;
            if (hit != null)
            {
                Category c = hit.DataContext as Category;
                if (c != null)
                {
                    // select it before context menu appears.
                    this.Selected = c;
                }
            }
        }

        #endregion

        #region Clipboard Support

        static void CopyToClipboard(Category c)
        {
            if (c != null)
            {
                string xml = c.Serialize();
                System.Windows.Clipboard.SetDataObject(xml, true);
            }
        }

        public bool CanCut
        {
            get { return false; }
        }
        public bool CanCopy
        {
            get { return this.Selected != null; }
        }
        public bool CanPaste
        {
            get { return System.Windows.Clipboard.ContainsText(); }
        }
        public bool CanDelete
        {
            get { return this.Selected != null; }
        }

        public void Cut()
        {
        }

        public void Copy()
        {
            CopyToClipboard(this.Selected);
        }

        public void Paste()
        {
        }

        public void Delete()
        {
            if (this.IsEditing)
            {
                return; // ignore del key when editing.
            }

            Category c = this.Selected;
            if (c != null)
            {
                IList<Transaction> data = this.MyMoney.Transactions.GetTransactionsByCategory(c, null);
                Category nc = null;
                if (data.Count > 0)
                {
                    CategoryDialog dialog = CategoryDialog.ShowDialogCategory(this.MyMoney, c.Name);
                    dialog.Owner = App.Current.MainWindow;
                    dialog.Title = "Delete Category";
                    dialog.Message = "Please select a new category for the transactions that are still using the category '" + c.Name + "'";

                    if (dialog.ShowDialog() == false)
                    {
                        return;
                    }

                    // the onmoneychanged event should have fired, and the list is now updated.

                    nc = dialog.Category;
                    Account account = dialog.Transfer;
                    foreach (Transaction t in data)
                    {
                        if (t.Category == c)
                        {
                            if (account != null)
                            {
                                this.MyMoney.Transfer(t, account);
                                this.MyMoney.Rebalance(account);
                            }
                            else
                            {
                                t.Category = nc;
                            }
                        }
                        else if (t.IsSplit)
                        {
                            foreach (Split s in t.Splits.Items)
                            {
                                if (s.Category == c)
                                {
                                    if (account != null)
                                    {
                                        this.MyMoney.Transfer(s, account);
                                    }
                                    else
                                    {
                                        s.Category = nc;
                                    }
                                }
                            }
                        }
                    }
                }

                Category category = this.Selected;

                category.OnDelete();
            }
        }

        #endregion

        #region Context Menu Handlers

        private void menuItemRename_Click(object sender, System.EventArgs e)
        {
            SetCurrentItemInEditMode();
        }

        void ExpandAll(ItemsControl items, bool expand)
        {
            ItemContainerGenerator itemContainerGenerator = items.ItemContainerGenerator;

            for (int i = items.Items.Count - 1; i >= 0; --i)
            {
                ItemsControl childControl = itemContainerGenerator.ContainerFromIndex(i) as ItemsControl;

                if (childControl != null)
                {
                    ExpandAll(childControl, expand);
                }

            }

            TreeViewItem treeViewItem = items as TreeViewItem;

            if (treeViewItem != null)
            {
                treeViewItem.IsExpanded = expand;
            }
        }


        void ShowDetails()
        {
            ShowDetails(this.MyMoney, this.Selected);
        }

        static public void ShowDetails(MyMoney money, Category c)
        {
            if (c != null)
            {
                CategoryDialog dialog = CategoryDialog.ShowDialogCategory(money, c.Name);
                dialog.Owner = App.Current.MainWindow;
                if (dialog.ShowDialog() == false)
                {
                    // User clicked cancel
                    return;
                }
                // todo: a bit ambiguous here what to do if they renamed the category...
                // for example, do we move all subcategories with it?
            }
        }

        private void ReverseTransfer(Transaction t)
        {
            MyMoney.BeginUpdate(this);
            decimal amount = -t.Amount;
            t.IsBudgeted = false;
            t.Transfer.Transaction.IsBudgeted = false;
            this.MyMoney.RemoveTransaction(t);
            MyMoney.EndUpdate();
        }

        Account GetCategoryFund(Category c)
        {
            Account fund = this.MyMoney.Accounts.FindCategoryFund(c);
            if (fund == null)
            {
                fund = this.MyMoney.Accounts.AddCategoryFund(c);
            }
            return fund;
        }

        #endregion

        #region DragDrop

        DragAndDrop dragDropSupport;
        string dragDropformatNameForCategory = "MyMoneyCategory";


        Walkabout.Utilities.DragDropSource OnDragDropObjectSource(object source)
        {

            Walkabout.Utilities.DragDropSource returnSource = null;

            if (IsEditing)
            {
                // turn off drag when in editing mode
            }
            else
            {
                TreeViewItem treeViewItem = WpfHelper.FindAncestor<TreeViewItem>((DependencyObject)source);
                if (treeViewItem != null)
                {
                    // First child of a TreeViewItem seems to always be a Grid in all Themes
                    FrameworkElement grid = VisualTreeHelper.GetChild(treeViewItem, 0) as FrameworkElement;
                    if (grid != null)
                    {
                        //
                        // The grid has 2 child
                        // 0 = Expander
                        // 1 = Tree Node visual
                        //
                        //
                        // We want the TreeNode visual without the Expander
                        FrameworkElement representationOfTheTreeNodeToDrag = VisualTreeHelper.GetChild(grid, 1) as FrameworkElement;
                        if (representationOfTheTreeNodeToDrag != null)
                        {
                            Category category = treeViewItem.Header as Category;
                            if (category != null)
                            {
                                returnSource = new DragDropSource();
                                returnSource.DataSource = category;
                                returnSource.VisualForDraginSource = representationOfTheTreeNodeToDrag;
                            }
                        }
                    }
                }
            }

            return returnSource;
        }

        private static Border CreateFrameworkElementForBalanceDragDrop(Category category)
        {
            double margin = 5;
            Border categoryBalance = new Border();
            categoryBalance.Padding = new Thickness(margin);
            categoryBalance.Background = new SolidColorBrush(Color.FromArgb(0x80, 0xc1, 0xc1, 0x60));

            TextBlock amountValue = new TextBlock();
            amountValue.Text = string.Format("{0:C}", category.Balance);
            amountValue.Foreground = Brushes.Black;
            amountValue.FontSize = 12;
            amountValue.HorizontalAlignment = HorizontalAlignment.Center;
            amountValue.VerticalAlignment = VerticalAlignment.Center;
            amountValue.TextAlignment = TextAlignment.Center;

            FormattedText ft = new FormattedText(amountValue.Text, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface(amountValue.FontFamily, amountValue.FontStyle, amountValue.FontWeight,
                amountValue.FontStretch), amountValue.FontSize, amountValue.Foreground);

            Rect bounds = new Rect(0, 0, ft.Width, ft.Height);
            bounds.Inflate(margin, margin);

            categoryBalance.Child = amountValue;

            categoryBalance.Width = bounds.Width;
            categoryBalance.Height = bounds.Height;
            categoryBalance.Arrange(bounds);

            return categoryBalance;
        }



        Category OnDragDropObjectTarget(object source, object target, DragDropEffects dropEfffect)
        {
            if (IsEditing)
            {
                // turn off drag when in editing mode

            }
            else
            {
                ContentControl listBoxItemControl = WpfHelper.FindAncestor<ContentControl>((DependencyObject)target);
                if (listBoxItemControl != null)
                {
                    return listBoxItemControl.Content as Category;
                }
            }

            return null;
        }


        private void OnDragDropSourceOnTarget(object source, object target, DragDropEffects dropEffect)
        {         
            try
            {
                MoveOrMergeItem(source as Category, target as Category);                
            }
            catch (Exception)
            {
            }
        }

        private void MoveOrMergeItem(Category categorySource, Category categoryTarget)
        {
            if (categorySource == null || categoryTarget == null)
            {
                return;
            }

            // Asking the user if they want to merge or add


            MoveMergeCategoryDialog choiceDialog = new MoveMergeCategoryDialog();
            choiceDialog.Owner = Application.Current.MainWindow;
            
            // Move
            string newName = Category.Combine(categoryTarget.Name, categorySource.Label);
            choiceDialog.TextForMoveSource.Text = categorySource.ToString();
            choiceDialog.TextForMoveTarget.Text = newName;

            
            // Merge
            choiceDialog.TextForMergeSource.Text = categorySource.ToString();
            choiceDialog.TextForMergeTarget.Text = categoryTarget.ToString();


            if (choiceDialog.ShowDialog() == true)
            {
                try
                {
                    if (choiceDialog.Choice == MoveMergeCategoryDialog.DragDropChoice.Move)
                    {
                        // Create new category under target and move all transactions to that
                        categoryTarget = MyMoney.Categories.GetOrCreateCategory(newName, categorySource.Type);
                    }

                    var sourceTransactions = MyMoney.Transactions.GetTransactionsByCategory(categorySource, null);
                    foreach (Transaction t in sourceTransactions)
                    {
                        t.ReCategorize(categorySource, categoryTarget);
                    }

                    // source category should now be unused.
                    categorySource.OnDelete();

                    treeView.Items.Refresh();

                    // Change the selection to the drop target, since the source target is about to be deleted
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Now select the new subcategory and enter Rename mode
                        this.Selected = categoryTarget;

                    }), DispatcherPriority.ContextIdle);


                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show("Please send this string to JP or Chris\n" + ex.ToString(), "Internal error merging categories", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        #endregion

        #region EDIT RENAMING

        TextBox editorForRenaming;
        Category categoryBeingRenamed;

        public bool IsEditing
        {
            get { return editorForRenaming != null; }
        }

        protected override void OnPreviewLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus == editorForRenaming)
            {
                // The Rename Edit box has lost the focus, preserve the edited changes
                OnRenameNode_CommitAndStopEditing();
            }
            base.OnPreviewLostKeyboardFocus(e);
        }

        private void OnExecutedRename(object sender, ExecutedRoutedEventArgs e)
        {
            SetCurrentItemInEditMode();
        }

        private void IsCategorySelected(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Selected != null;
        }

        void OnRenameNode_StopEditing()
        {
            editorForRenaming = null;
            if (this.categoryBeingRenamed != null)
            {
                this.categoryBeingRenamed.IsEditing = false;
            }
        }

        void OnRenameNode_CommitAndStopEditing()
        {
            if (editorForRenaming != null)
            {
                string renameTo = editorForRenaming.Text.Trim();

                Category categoryToRename = this.categoryBeingRenamed;

                OnRenameNode_StopEditing();

                if ( string.Compare(renameTo, categoryToRename.Label, true) == 0)
                {
                    // The label has not changed, the user either typed the same name or did not really want to change the name
                    // so we have nothing to do
                }
                else
                {
                    //
                    // First lets make sure that we don't have a name collision
                    //
                    string newNameForCategory = categoryToRename.GetFullNameOfParent() + renameTo;


                    if (this.money.Categories.FindCategory(newNameForCategory) == null)
                    {
                        // The new name is available let rename now
                        categoryToRename.Label = renameTo;
                    }
                    else
                    {
                        // The name is already present
                        MessageBoxEx.Show("Category \"" + newNameForCategory + "\" already exist", "Category", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }

            }
        }

        private void SetCurrentItemInEditMode()
        {
            Category c = this.Selected;
            if (c != null)
            {
                this.categoryBeingRenamed = c;
                this.categoryBeingRenamed.IsEditing = true;  // This will trigger the TreeViewItem TextBlock to switch to a TextBox
            }
        }

        private void OnTextEditorForRenaming_Loaded(object sender, RoutedEventArgs e)
        {
            this.editorForRenaming = sender as TextBox;
            this.editorForRenaming.Focus();
            this.editorForRenaming.CaretIndex = this.editorForRenaming.Text.Length;
        }

        private void RenameEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnRenameNode_CommitAndStopEditing();
            }
            else if (e.Key == Key.Escape)
            {
                OnRenameNode_StopEditing();
            }
        }

        #endregion

        #region Command Handlers

        private void OnShowProperties(object sender, ExecutedRoutedEventArgs e)
        {
            ShowDetails();       
        }

        private void OnAddCategory(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.IsEditing)
            {
                return;
            }

            string name = this.Selected != null ? Category.Combine(this.Selected.Name, "New Category") : "New Category";

            CategoryDialog dialog = CategoryDialog.ShowDialogCategory(this.MyMoney, name);
            dialog.Owner = App.Current.MainWindow;
            dialog.Title = "Add Category";
            dialog.Message = this.Selected != null ? "Please edit your new sub-category name or edit the whole string to add a new top level category" 
                : "Please edit your new category name";

            dialog.Select("New Category");

            if (dialog.ShowDialog() == false)
            {
                return;
            }

            treeView.Items.Refresh();

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Now select the new subcategory and enter Rename mode
                this.Selected = dialog.Category;

            }), DispatcherPriority.ContextIdle);


        }

        private void OnDeleteCategory(object sender, ExecutedRoutedEventArgs e)
        {
            Delete();
        }

        private void OnExpandAll(object sender, ExecutedRoutedEventArgs e)
        {
            this.ExpandAll(treeView, true);
        }

        private void OnCollapseAll(object sender, ExecutedRoutedEventArgs e)
        {
            this.ExpandAll(treeView, false);
        }

        #endregion 
    }


    public class CategoryGroup : DependencyObject
    {
        public string Name
        {
            get { return (string)GetValue(NameProperty); }
            set { SetValue(NameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Name.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register("Name", typeof(string), typeof(CategoryGroup));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public ObservableCollection<Category> Subcategories { get; set; }

     
    }

    public class CategoryBalance : CategoryGroup
    {
        public decimal Balance
        {
            get { return (decimal)GetValue(BalanceProperty); }
            set { SetValue(BalanceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Balance.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BalanceProperty =
            DependencyProperty.Register("Balance", typeof(decimal), typeof(CategoryBalance));

        public ObservableCollection<Category> SubcategoriesFiltered
        {
            get
            {
                return Subcategories;
            }
        }
    }

}
