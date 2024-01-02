using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Walkabout.Data;
using Walkabout.Utilities;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for SecuritiesControl.xaml
    /// </summary>
    public partial class SecuritiesControl : UserControl, IClipboardClient
    {
        #region COMMANDS
        public static readonly RoutedUICommand CommandDeletePayee = new RoutedUICommand("Delete", "CommandDeletePayee", typeof(SecuritiesControl));

        #endregion

        #region PROPERTIES

        private MyMoney myMoney;

        public MyMoney MyMoney
        {
            get { return this.myMoney; }
            set
            {
                if (this.myMoney != null)
                {
                    this.myMoney.Securities.Changed -= new EventHandler<ChangeEventArgs>(this.OnSecuritiesChanged);
                    this.myMoney.Rebalanced -= new EventHandler<ChangeEventArgs>(this.OnBalanceChanged);
                    this.myMoney.Transactions.Changed -= new EventHandler<ChangeEventArgs>(this.OnTransactionsChanged);
                }
                this.myMoney = value;
                if (value != null)
                {
                    this.myMoney.Securities.Changed += new EventHandler<ChangeEventArgs>(this.OnSecuritiesChanged);
                    this.myMoney.Rebalanced += new EventHandler<ChangeEventArgs>(this.OnBalanceChanged);
                    this.myMoney.Transactions.Changed += new EventHandler<ChangeEventArgs>(this.OnTransactionsChanged);

                    this.OnSecuritiesChanged(this, new ChangeEventArgs(this.myMoney.Securities, null, ChangeType.Reloaded));
                }
            }
        }

        private object selection;

        public event EventHandler SelectionChanged;

        private string lastActiveFilter = string.Empty;

        public Security Selected
        {
            get { return this.listbox1.SelectedItem as Security; }
            set
            {
                this.selection = value;
                this.listbox1.SelectedItem = value;
                this.listbox1.ScrollIntoView(value);
            }
        }

        private readonly DragAndDrop dragDropSupport;
        private readonly string dragDropformatNameForSecurity = "MyMoneySecurity";
        private bool loaded;
        #endregion


        public SecuritiesControl()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.SecuritiesControlInitialize))
            {
#endif
            this.InitializeComponent();
            MouseUp += new MouseButtonEventHandler(this.OnMouseUp);
            this.listbox1.SelectionChanged += new SelectionChangedEventHandler(this.OnSelectionChanged);
            this.dragDropSupport = new DragAndDrop(this.listbox1, this.dragDropformatNameForSecurity, this.OnDragSource, this.OnDropTarget, this.OnDropSourceOnTarget, false);
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
                this.GetAllSecurities(this.lastActiveFilter);
            }
        }

        public void Filter(string filterText)
        {
            this.lastActiveFilter = filterText;
            this.GetAllSecurities(filterText);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                Security x = e.AddedItems[0] as Security;
                if (x != this.selection)
                {
                    this.selection = x;
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
                Security x = listBoxItemControl.Content as Security;
                if (x != null)
                {
                    returnSource = new DragDropSource();
                    returnSource.DataSource = x;
                    returnSource.VisualForDraginSource = this.CreateDragVisual(x);
                }
            }

            return returnSource;
        }

        private FrameworkElement CreateDragVisual(Security s)
        {
            Border visual = new Border();
            visual.SetResourceReference(Window.BackgroundProperty, "SystemControlHighlightAccent3RevealBackgroundBrush");
            visual.SetResourceReference(Window.ForegroundProperty, "SystemControlPageTextBaseHighBrush");
            var label = new TextBlock() { Text = s.Name, Margin = new Thickness(5), FontSize = this.FontSize, FontFamily = this.FontFamily };
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
                    DataSource = listBoxItemControl.Content as Security,
                    TargetElement = listBoxItemControl
                };
            }

            return null;
        }


        /// <summary>
        /// Execute the Drop operation 
        /// </summary>
        /// <param name="source">Security that was dragged</param>
        /// <param name="target">Security that was dropped on</param>
        private void OnDropSourceOnTarget(object source, object target, DragDropEffects dropEffect)
        {
            this.Rename(source as Security, target as Security);
        }

        #endregion

        public void OnSecuritiesChanged(object sender, ChangeEventArgs args)
        {
            if (this.IsVisible)
            {
                this.GetAllSecurities(this.lastActiveFilter);
            }
        }

        private void OnTransactionsChanged(object sender, ChangeEventArgs args)
        {
            if (this.IsVisible)
            {
                this.GetAllSecurities(this.lastActiveFilter);
            }
        }


        private void GetAllSecurities()
        {
            this.GetAllSecurities(this.lastActiveFilter);
        }

        private void GetAllSecurities(string filter)
        {
            List<Security> list;

            if (string.IsNullOrWhiteSpace(filter))
            {
                list = this.myMoney.Securities.GetSecuritiesAsList();
            }
            else
            {
                list = this.myMoney.Securities.GetSecuritiesAsList(filter);
            }
            list.Sort(Security.Compare);
            this.loaded = list.Count > 0;
            this.listbox1.ItemsSource = list;
        }

        private void OnBalanceChanged(object sender, ChangeEventArgs args)
        {
            // TODO
        }

        private void OnMenuItem_Rename(object sender, RoutedEventArgs e)
        {
            Security x = this.listbox1.SelectedItem as Security;
            this.Rename(x);
        }

        private void OnCanDeleteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Selected != null;
        }

        private void OnExecuteDelete(object sender, ExecutedRoutedEventArgs e)
        {
            Security x = this.Selected;
            this.DeleteSecurity(x);
        }

        private Security Rename(Security p)
        {
            return this.Rename(p, p);
        }

        private Security Rename(Security fromSecurity, Security renameToThisSecurity)
        {
            if (MessageBox.Show(string.Format(Properties.Resources.RenameSecurity, fromSecurity.Name, renameToThisSecurity.Name),
                Properties.Resources.MergeSecurityCaption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    this.myMoney.SwitchSecurities(fromSecurity, renameToThisSecurity);

                    this.myMoney.Securities.RemoveSecurity(fromSecurity);

                    this.listbox1.SelectedItem = renameToThisSecurity;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Properties.Resources.MergeSecurityCaption, MessageBoxButton.OKCancel, MessageBoxImage.Error);
                }
            }
            return null;
        }

        public bool DeleteSecurity(Security x)
        {
            Securities collection = this.myMoney.Securities;
            IList<Transaction> trans = this.myMoney.Transactions.GetTransactionsBySecurity(x, null);
            if (trans.Count == 0)
            {
                collection.RemoveSecurity(x);
                return true;
            }
            else
            {
                MessageBoxEx.Show(string.Format(Properties.Resources.SecurityDeleteDisabled, x.Name, trans.Count),
                    Properties.Resources.SecurityDeleteDisabledCaption, MessageBoxButton.OKCancel, MessageBoxImage.Error);
            }

            return false;
        }


        #region CLIPBOARD SUPPORT

        public bool CanCut
        {
            get { return this.Selected != null; }
        }
        public bool CanCopy
        {
            get { return this.Selected != null; }
        }
        public bool CanPaste
        {
            get { return false; } // Clipboard.ContainsText(); }
        }
        public bool CanDelete
        {
            get { return this.Selected != null; }
        }

        public void Cut()
        {
            Security s = this.Selected;
            if (s != null && this.DeleteSecurity(this.Selected))
            {
                string xml = s.Serialize();
                Clipboard.SetDataObject(xml, true);
            }
        }

        private static void CopyToClipboard(Security s)
        {
            if (s != null)
            {
                string xml = s.Serialize();
                Clipboard.SetDataObject(xml, true);
            }
        }

        public void Copy()
        {
            Security s = this.Selected;
            CopyToClipboard(s);
        }

        public void Delete()
        {
            Security s = this.Selected;
            if (s != null)
            {
                this.DeleteSecurity(this.Selected);
            }
        }

        public void Paste()
        {
            //IDataObject data = Clipboard.GetDataObject();
            //if (data.GetDataPresent(typeof(string)))
            //{
            //    string xml = (string)data.GetData(typeof(string));
            //    Payee p = Payee.Deserialize(xml);
            //    Payee p2 = this.MyMoney.Payees.FindPayee(p.Name, false);
            //    if (p2 != null)
            //    {
            //        if (MessageBoxEx.Show("Payee with the name '" + p.Name + "' already exists\nDo you want to select that payee?", "Paste Error", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            //        {
            //            this.listbox1.SelectedItem = p2;
            //        }
            //    }
            //    else
            //    {
            //        p.Id = -1; // get new id for it.
            //        this.MyMoney.Payees.AddPayee(p);
            //    }
            //}
        }

        #endregion

    }


}
