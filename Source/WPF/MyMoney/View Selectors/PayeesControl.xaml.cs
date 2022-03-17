using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Utilities;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for PayeeControl.xaml
    /// </summary>
    public partial class PayeesControl : UserControl, IClipboardClient
    {
        #region PROPERTIES

        public IServiceProvider Site { get; set; }

        private MyMoney myMoney;

        public MyMoney MyMoney
        {
            get { return myMoney; }
            set
            {
                if (this.myMoney != null)
                {
                    myMoney.Payees.Changed -= new EventHandler<ChangeEventArgs>(OnPayeesChanged);
                    myMoney.Rebalanced -= new EventHandler<ChangeEventArgs>(OnBalanceChanged);
                    myMoney.Transactions.Changed -= new EventHandler<ChangeEventArgs>(OnTransactionsChanged);
                }
                myMoney = value;
                if (value != null)
                {
                    myMoney.Payees.Changed += new EventHandler<ChangeEventArgs>(OnPayeesChanged);
                    myMoney.Rebalanced += new EventHandler<ChangeEventArgs>(OnBalanceChanged);
                    myMoney.Transactions.Changed += new EventHandler<ChangeEventArgs>(OnTransactionsChanged);

                    OnPayeesChanged(this, new ChangeEventArgs(myMoney.Payees, null, ChangeType.Reloaded));
                }
            }
        }


        object selection;

        public event EventHandler SelectionChanged;

        string lastActiveFilter = string.Empty;

        public Payee Selected
        {
            get { return this.listbox1.SelectedItem as Payee; }
            set
            {
                selection = value;
                this.listbox1.SelectedItem = value;
                listbox1.ScrollIntoView(value);
            }
        }

        DragAndDrop dragDropSupport;
        string dragDropformatNameForPayee = "MyMoneyPayee";
        bool loaded;

        #endregion


        public PayeesControl()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.PayeesControlInitialize))
            {
#endif
                InitializeComponent();
                this.MouseUp += new MouseButtonEventHandler(OnMouseUp);
                this.listbox1.SelectionChanged += new SelectionChangedEventHandler(OnSelectionChanged);
                this.dragDropSupport = new DragAndDrop(listbox1, this.dragDropformatNameForPayee, OnDragSource, OnDropTarget, OnDropSourceOnTarget, false);
                this.IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnIsVisibleChanged);

#if PerformanceBlocks
            }
#endif
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!loaded)
            {
                GetAllPayees(lastActiveFilter);
            }
        }

        public void Filter(string filterText)
        {
            lastActiveFilter = filterText;
            GetAllPayees(filterText);
        }

        void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                Payee p = e.AddedItems[0] as Payee;
                if (p != selection)
                {
                    selection = p;
                    if (SelectionChanged != null)
                    {
                        SelectionChanged(this, EventArgs.Empty);
                    }
                }
            }
        }

        #region TRACK SELECTION CHANGE

        public event EventHandler MouseButtonBackwardChanged;
        public event EventHandler MouseButtonForwardChanged;


        void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.XButton1 == MouseButtonState.Pressed)
            {
                if (this.MouseButtonBackwardChanged != null)
                {
                    this.MouseButtonBackwardChanged(this, new EventArgs());
                }
            }

            if (e.XButton2 == MouseButtonState.Pressed)
            {
                if (this.MouseButtonForwardChanged != null)
                {
                    this.MouseButtonForwardChanged(this, new EventArgs());
                }
            }
        }

        #endregion


        #region DRAG DROP SUPPORT

        Walkabout.Utilities.DragDropSource OnDragSource(object source)
        {
            Walkabout.Utilities.DragDropSource returnSource = null;

            ListBoxItem listBoxItemControl = WpfHelper.FindAncestor<ListBoxItem>((DependencyObject)source);
            if (listBoxItemControl != null)
            {
                Payee payee = listBoxItemControl.Content as Payee;
                if ( payee != null )
                {
                    returnSource = new DragDropSource();
                    returnSource.DataSource = payee;
                    returnSource.VisualForDraginSource = CreateDragVisual(payee);
                }
            }

            return returnSource;
        }

        private FrameworkElement CreateDragVisual(Payee p)
        {
            Border visual = new Border();
            visual.SetResourceReference(Window.BackgroundProperty, "SystemControlHighlightAccent3RevealBackgroundBrush");
            visual.SetResourceReference(Window.ForegroundProperty, "SystemControlPageTextBaseHighBrush");
            var label = new TextBlock() { Text = p.Name, Margin = new Thickness(5), FontSize = this.FontSize, FontFamily = this.FontFamily };
            visual.Child = label;
            return visual;
        }

        DragDropTarget OnDropTarget(object source, object target, DragDropEffects dropEfffect)
        {
            ListBoxItem listBoxItemControl = WpfHelper.FindAncestor<ListBoxItem>((DependencyObject)target);
            if (listBoxItemControl != null)
            {
                return new DragDropTarget()
                {
                    DataSource = listBoxItemControl.Content as Payee,
                    TargetElement = listBoxItemControl
                };
            }

            return null;
        }


        /// <summary>
        /// Execute the Drop operation 
        /// </summary>
        /// <param name="source">Payee that was dragged</param>
        /// <param name="target">Payee that was dropped on</param>
        private void OnDropSourceOnTarget(object source, object target, DragDropEffects dropEffect)
        {
            Rename(source as Payee, target as Payee);
        }

        #endregion

        public void OnPayeesChanged(object sender, ChangeEventArgs args)
        {
            if (this.IsVisible)
            {
                GetAllPayees(this.lastActiveFilter);
            }
        }

        void OnTransactionsChanged(object sender, ChangeEventArgs args)
        {
            if (this.IsVisible)
            {
                GetAllPayees(this.lastActiveFilter);
            }
        }

        private void GetAllPayees(string filter)
        {
            List<Payee> list;
            
            if (string.IsNullOrWhiteSpace(filter))
            {
                list = myMoney.Payees.GetPayeesAsList();
            }
            else
            {
                list = myMoney.Payees.GetPayeesAsList(filter);
            }
            list.Sort(new PayeeComparer2());
            loaded = list.Count > 0;
            this.listbox1.ItemsSource = list;
        }

        void OnBalanceChanged(object sender, ChangeEventArgs args)
        {
            // TODO
        }

        private void OnMenuItem_Rename(object sender, RoutedEventArgs e)
        {
            Payee p = this.listbox1.SelectedItem as Payee;
            Rename(p);
        }

        private void OnMenuItem_Delete(object sender, RoutedEventArgs e)
        {
            Payee p = this.listbox1.SelectedItem as Payee;
            DeletePayee(p);
        }

        Payee Rename(Payee p)
        {
            return Rename(p, p);
        }

        Payee Rename(Payee fromPayee, Payee renameToThisPayee)
        {
            RenamePayeeDialog dialog = RenamePayeeDialog.ShowDialogRenamePayee(this.Site, this.myMoney, fromPayee, renameToThisPayee);

            dialog.Owner = App.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                HashSet<Payee> used = myMoney.GetUsedPayees();
                if (!used.Contains(fromPayee))
                {
                    // remove it now so that our list UI is updated...
                    Payees payees = myMoney.Payees;
                    payees.RemovePayee(fromPayee);
                }
            }
            return dialog.Payee;

        }



        public Payee DeletePayee(Payee p)
        {
            Payees payees = myMoney.Payees;
            if (myMoney.GetUsedPayees().Contains(p))
            {
                p = Rename(p);
            }
            else
            {
                payees.RemovePayee(p);
            }
            return p;
        }


        #region CLIPBOARD SUPPORT

        public bool CanCut
        {
            get { return this.listbox1.SelectedItem != null; }
        }
        public bool CanCopy
        {
            get { return this.listbox1.SelectedItem != null; }
        }
        public bool CanPaste
        {
            get { return Clipboard.ContainsText(); }
        }
        public bool CanDelete
        {
            get { return this.listbox1.SelectedItem != null; }
        }

        public void Cut()
        {
            Payee p = this.listbox1.SelectedItem as Payee;
            if (p != null)
            {
                p = this.DeletePayee(p);
                if (p != null)
                {
                    string xml = p.Serialize();
                    Clipboard.SetDataObject(xml, true);
                }
            }
        }

        static void CopyToClipboard(Payee p)
        {
            if (p != null)
            {
                string xml = p.Serialize();
                Clipboard.SetDataObject(xml, true);
            }
        }

        public void Copy()
        {
            Payee p = this.listbox1.SelectedItem as Payee;
            CopyToClipboard(p);
        }

        public void Delete()
        {
            Payee p = this.listbox1.SelectedItem as Payee;
            if (p != null)
            {
                DeletePayee(p);
            }
        }

        public void Paste()
        {
            IDataObject data = Clipboard.GetDataObject();
            if (data.GetDataPresent(typeof(string)))
            {
                string xml = (string)data.GetData(typeof(string));
                Payee p = Payee.Deserialize(xml);
                Payee p2 = this.MyMoney.Payees.FindPayee(p.Name, false);
                if (p2 != null)
                {
                    if (MessageBoxEx.Show("Payee with the name '" + p.Name + "' already exists\nDo you want to select that payee?", "Paste Error", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        this.listbox1.SelectedItem = p2;
                    }
                }
                else
                {
                    p.Id = -1; // get new id for it.
                    this.MyMoney.Payees.AddPayee(p);
                }
            }
        }

        #endregion

    }

   
}
