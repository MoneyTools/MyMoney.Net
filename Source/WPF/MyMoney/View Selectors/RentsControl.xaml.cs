using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Utilities;
using Walkabout.Help;


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
            InitializeComponent();
            this.MouseUp += new MouseButtonEventHandler(OnMouseUp);
            this.treeView1.SelectedItemChanged += new RoutedPropertyChangedEventHandler<object>(OnTreeView_SelectedItemChanged);
        }

        #endregion

        #region PROPERTIES

        private MyMoney myMoney;

        public MyMoney MyMoney
        {
            get { return myMoney; }
            set
            {
                if (this.myMoney != value)
                {
                    // First stop monitoring changes on the existing Money type
                    if (this.myMoney != null)
                    {
                        myMoney.Rebalanced -= new EventHandler<ChangeEventArgs>(OnBalanceChanged);
                    }

                    this.myMoney = value;

                    if (this.myMoney == null)
                    {
                        // TO DO - clear the list of existing building if any
                    }
                    else
                    {
                        this.myMoney.Rebalanced += new EventHandler<ChangeEventArgs>(OnBalanceChanged);

                        // Fire initial change to display the Buildings in they new Money db
                        OnBalanceChanged(value.Buildings, new ChangeEventArgs(value.Buildings, null, ChangeType.Reloaded));
                    }
                }
            }
        }



        object selection;
        #endregion

        #region EVENTS

        void OnBalanceChanged(object sender, ChangeEventArgs args)
        {
            ReloadTreeView();
        }


        public event EventHandler SelectionChanged;

        public object Selected
        {
            get { return this.treeView1.SelectedItem; }
            set
            {
                selection = value;
                //   this.treeView1.SelectedItem = value; 
            }
        }



        void OnTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (SelectionChanged != null)
            {
                SelectionChanged(this, e);
            }
        }

        void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
            }
        }

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




        private void OnMenuNewRental_Click(object sender, RoutedEventArgs e)
        {
            RentBuilding rb = new RentBuilding(this.MyMoney.Buildings);
            rb.Name = "New Rental";

            if (rb != null)
            {
                if (EditProperties(rb) == true)
                {
                    this.MyMoney.Buildings.Add(rb);

                    ReloadTreeView();
                }
            }
        }


        private void OnMenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            ReloadTreeView();
        }

        private void ReloadTreeView()
        {
            this.treeView1.ItemsSource = myMoney.Buildings.GetList();
            this.treeView1.Items.Refresh();
        }


        private void OnMenuItem_Edit(object sender, RoutedEventArgs e)
        {
            RentBuilding x = this.treeView1.SelectedItem as RentBuilding;

            if (x != null)
            {
                EditProperties(x);
            }
        }

        bool EditProperties(RentBuilding a)
        {
            Walkabout.Dialogs.BuildingDialog dialog = new Dialogs.BuildingDialog(myMoney);
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
                        myMoney.Buildings.RemoveBuilding(toDelete);
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
