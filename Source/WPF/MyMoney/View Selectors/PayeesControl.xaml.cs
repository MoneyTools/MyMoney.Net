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
            get { return this.myMoney; }
            set
            {
                if (this.myMoney != null)
                {
                    this.myMoney.Payees.Changed -= new EventHandler<ChangeEventArgs>(this.OnPayeesChanged);
                    this.myMoney.Rebalanced -= new EventHandler<ChangeEventArgs>(this.OnBalanceChanged);
                    this.myMoney.Transactions.Changed -= new EventHandler<ChangeEventArgs>(this.OnTransactionsChanged);
                }
                this.myMoney = value;
                if (value != null)
                {
                    this.myMoney.Payees.Changed += new EventHandler<ChangeEventArgs>(this.OnPayeesChanged);
                    this.myMoney.Rebalanced += new EventHandler<ChangeEventArgs>(this.OnBalanceChanged);
                    this.myMoney.Transactions.Changed += new EventHandler<ChangeEventArgs>(this.OnTransactionsChanged);

                    this.OnPayeesChanged(this, new ChangeEventArgs(this.myMoney.Payees, null, ChangeType.Reloaded));
                }
            }
        }

        private object selection;

        public event EventHandler SelectionChanged;

        private string lastActiveFilter = string.Empty;

        public Payee Selected
        {
            get { return this.listbox1.SelectedItem as Payee; }
            set
            {
                this.selection = value;
                this.listbox1.SelectedItem = value;
                this.listbox1.ScrollIntoView(value);
            }
        }

        private readonly DragAndDrop dragDropSupport;
        private readonly string dragDropformatNameForPayee = "MyMoneyPayee";
        private bool loaded;

        #endregion


        public PayeesControl()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.PayeesControlInitialize))
            {
#endif
                this.InitializeComponent();
                MouseUp += new MouseButtonEventHandler(this.OnMouseUp);
                this.listbox1.SelectionChanged += new SelectionChangedEventHandler(this.OnSelectionChanged);
                this.dragDropSupport = new DragAndDrop(this.listbox1, this.dragDropformatNameForPayee, this.OnDragSource, this.OnDropTarget, this.OnDropSourceOnTarget, false);
                IsVisibleChanged += new DependencyPropertyChangedEventHandler(this.OnIsVisibleChanged);
                Unloaded += (s, e) =>
                {
                    this.dragDropSupport.Disconnect();
                    this.MyMoney = null;
                };
#if PerformanceBlocks
            }
#endif
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!this.loaded)
            {
                this.GetAllPayees(this.lastActiveFilter);
            }
        }

        public void Filter(string filterText)
        {
            this.lastActiveFilter = filterText;
            this.GetAllPayees(filterText);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                Payee p = e.AddedItems[0] as Payee;
                if (p != this.selection)
                {
                    this.selection = p;
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

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.XButton1 == MouseButtonState.Pressed)
            {
                if (MouseButtonBackwardChanged != null)
                {
                    MouseButtonBackwardChanged(this, new EventArgs());
                }
            }

            if (e.XButton2 == MouseButtonState.Pressed)
            {
                if (MouseButtonForwardChanged != null)
                {
                    MouseButtonForwardChanged(this, new EventArgs());
                }
            }
        }

        #endregion


        #region DRAG DROP SUPPORT

        private Walkabout.Utilities.DragDropSource OnDragSource(object source)
        {
            Walkabout.Utilities.DragDropSource returnSource = null;

            ListBoxItem listBoxItemControl = WpfHelper.FindAncestor<ListBoxItem>((DependencyObject)source);
            if (listBoxItemControl != null)
            {
                Payee payee = listBoxItemControl.Content as Payee;
                if (payee != null)
                {
                    returnSource = new DragDropSource();
                    returnSource.DataSource = payee;
                    returnSource.VisualForDraginSource = this.CreateDragVisual(payee);
                }
            }

            return returnSource;
        }

        private FrameworkElement CreateDragVisual(Payee p)
        {
            Border visual = new Border();
            visual.SetResourceReference(Window.BackgroundProperty, "SystemControlHighlightAccent3RevealBackgroundBrush");
            visual.SetResourceReference(Window.ForegroundProperty, "SystemControlPageTextBaseHighBrush");
            var label = new TextBlock() { Text = p.Name, Margin = new Thickness(5), FontSize = FontSize, FontFamily = FontFamily };
            visual.Child = label;
            return visual;
        }

        private DragDropTarget OnDropTarget(object source, object target, DragDropEffects dropEfffect)
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
            this.Rename(source as Payee, target as Payee);
        }

        #endregion

        public void OnPayeesChanged(object sender, ChangeEventArgs args)
        {
            if (this.IsVisible)
            {
                this.GetAllPayees(this.lastActiveFilter);
            }
        }

        private void OnTransactionsChanged(object sender, ChangeEventArgs args)
        {
            if (this.IsVisible)
            {
                this.GetAllPayees(this.lastActiveFilter);
            }
        }

        private void GetAllPayees(string filter)
        {
            List<Payee> list;

            if (string.IsNullOrWhiteSpace(filter))
            {
                list = this.myMoney.Payees.GetPayeesAsList();
            }
            else
            {
                list = this.myMoney.Payees.GetPayeesAsList(filter);
            }
            list.Sort(new PayeeComparer2());
            this.loaded = list.Count > 0;
            this.listbox1.ItemsSource = list;
        }

        private void OnBalanceChanged(object sender, ChangeEventArgs args)
        {
            // TODO
        }

        private void OnMenuItem_Rename(object sender, RoutedEventArgs e)
        {
            Payee p = this.listbox1.SelectedItem as Payee;
            this.Rename(p);
        }

        private void OnMenuItem_Delete(object sender, RoutedEventArgs e)
        {
            Payee p = this.listbox1.SelectedItem as Payee;
            this.DeletePayee(p);
        }

        private Payee Rename(Payee p)
        {
            return this.Rename(p, p);
        }

        private Payee Rename(Payee fromPayee, Payee renameToThisPayee)
        {
            RenamePayeeDialog dialog = RenamePayeeDialog.ShowDialogRenamePayee(this.Site, this.myMoney, fromPayee, renameToThisPayee);

            dialog.Owner = App.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                HashSet<Payee> used = this.myMoney.GetUsedPayees();
                if (!used.Contains(fromPayee))
                {
                    // remove it now so that our list UI is updated...
                    Payees payees = this.myMoney.Payees;
                    payees.RemovePayee(fromPayee);
                }
            }
            return dialog.Payee;

        }



        public Payee DeletePayee(Payee p)
        {
            Payees payees = this.myMoney.Payees;
            if (this.myMoney.GetUsedPayees().Contains(p))
            {
                p = this.Rename(p);
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

        private static void CopyToClipboard(Payee p)
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
                this.DeletePayee(p);
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
