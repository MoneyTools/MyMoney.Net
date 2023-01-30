using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Walkabout.Data;
using Walkabout.Utilities;


namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for RentsControl.xaml
    /// </summary>
    public partial class RentsControl : UserControl, IClipboardClient
    {
        #region CONSTRUCTOR

        public RentsControl()
        {
            this.InitializeComponent();
            MouseUp += new MouseButtonEventHandler(this.OnMouseUp);
            this.treeView1.SelectedItemChanged += new RoutedPropertyChangedEventHandler<object>(this.OnTreeView_SelectedItemChanged);
        }

        #endregion

        #region PROPERTIES

        private MyMoney myMoney;

        public MyMoney MyMoney
        {
            get { return this.myMoney; }
            set
            {
                if (this.myMoney != value)
                {
                    // First stop monitoring changes on the existing Money type
                    if (this.myMoney != null)
                    {
                        this.myMoney.Rebalanced -= new EventHandler<ChangeEventArgs>(this.OnBalanceChanged);
                    }

                    this.myMoney = value;

                    if (this.myMoney == null)
                    {
                        // TO DO - clear the list of existing building if any
                    }
                    else
                    {
                        this.myMoney.Rebalanced += new EventHandler<ChangeEventArgs>(this.OnBalanceChanged);

                        // Fire initial change to display the Buildings in they new Money db
                        this.OnBalanceChanged(value.Buildings, new ChangeEventArgs(value.Buildings, null, ChangeType.Reloaded));
                    }
                }
            }
        }

        private object selection;
        #endregion

        #region EVENTS

        private void OnBalanceChanged(object sender, ChangeEventArgs args)
        {
            this.ReloadTreeView();
        }


        public event EventHandler SelectionChanged;

        public object Selected
        {
            get { return this.treeView1.SelectedItem; }
            set
            {
                this.selection = value;
                //   this.treeView1.SelectedItem = value; 
            }
        }

        private void OnTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (SelectionChanged != null)
            {
                SelectionChanged(this, e);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
            }
        }

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




        private void OnMenuNewRental_Click(object sender, RoutedEventArgs e)
        {
            RentBuilding rb = new RentBuilding(this.MyMoney.Buildings);
            rb.Name = "New Rental";

            if (rb != null)
            {
                if (this.EditProperties(rb) == true)
                {
                    this.MyMoney.Buildings.Add(rb);

                    this.ReloadTreeView(true);
                }
            }
        }


        private void OnMenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            this.ReloadTreeView(true);
        }

        private void ReloadTreeView(bool forRebuild = false)
        {
            if (this.treeView1.ItemsSource == null || forRebuild)
            {
                this.treeView1.ItemsSource = this.myMoney.Buildings.GetList();
            }
            this.treeView1.Items.Refresh();
        }


        private void OnMenuItem_Edit(object sender, RoutedEventArgs e)
        {
            RentBuilding x = this.treeView1.SelectedItem as RentBuilding;

            if (x != null)
            {
                this.EditProperties(x);
            }
        }

        private bool EditProperties(RentBuilding a)
        {
            Walkabout.Dialogs.RentalDialog dialog = new Dialogs.RentalDialog(this.myMoney);
            dialog.TheBuilding = a;
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                return true;
            }

            return false;
        }

        private void OnMenuItem_Delete(object sender, RoutedEventArgs e)
        {

            if (this.treeView1.SelectedItem is RentalBuildingSingleYear)
            {
                RentalBuildingSingleYear toDelete = this.treeView1.SelectedItem as RentalBuildingSingleYear;
                if (toDelete != null)
                {
                    if (MessageBoxEx.Show("Delete " + toDelete.Period, "Rental", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        toDelete.Building.Years.Remove(toDelete.Year);
                        this.treeView1.Items.Refresh();
                    }
                }
            }
            else
            {
                if (this.treeView1.SelectedItem is RentBuilding)
                {
                    RentBuilding toDelete = this.treeView1.SelectedItem as RentBuilding;

                    if (MessageBoxEx.Show("Delete " + toDelete.Name, "Rental", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        this.myMoney.Buildings.RemoveBuilding(toDelete);
                    }
                }

            }
        }
        #endregion

        #region CLIPBOARD SUPPORT

        public bool CanCut
        {
            get { return this.treeView1.SelectedItem != null; }
        }
        public bool CanCopy
        {
            get { return this.treeView1.SelectedItem != null; }
        }
        public bool CanPaste
        {
            get { return Clipboard.ContainsText(); }
        }
        public bool CanDelete
        {
            get { return this.treeView1.SelectedItem != null; }
        }

        public void Cut()
        {
            // To Do
        }


        public void Copy()
        {
            // To Do
        }

        public void Delete()
        {
            // To Do
        }

        public void Paste()
        {

        }

        #endregion
    }


}
