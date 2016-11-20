using System.Collections;
using System.Collections.Generic;
using System.Windows;
using Walkabout.Data;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for AccountDialog.xaml
    /// </summary>
    public partial class BuildingDialog : Window
    {
        MyMoney money;
        RentBuilding editingBuilding = new RentBuilding();
        RentBuilding theBuilding = new RentBuilding();

        public RentBuilding TheBuilding
        {
            get { return theBuilding; }
            set
            {
                theBuilding = value;
                editingBuilding = value.ShallowCopy();
                this.DataContext = editingBuilding;
                this.Units.ItemsSource = editingBuilding.Units;
                UpdateUI();
            }
        }

        public BuildingDialog(MyMoney money)
        {
            this.money = money;

            InitializeComponent();
            UpdateUI();

        }

        void UpdateUI()
        {
        }

        List<Category> categories;

        public IList Categories
        {

            get
            {
                if (categories == null)
                {
                    categories = new List<Category>();
                    Category na = new Category() { Name = "--N/A--" , Id=-1};
                    categories.Add(na);

                    foreach (Category c in this.money.Categories.AllCategories)
                    {
                        categories.Add(c);
                    }
                }
                return categories;
            }
        }


        private void ButtonOk(object sender, RoutedEventArgs e)
        {
            theBuilding.Name = editingBuilding.Name;

            theBuilding.Address = editingBuilding.Address;
            theBuilding.Note = editingBuilding.Note;
            theBuilding.CategoryForIncome = editingBuilding.CategoryForIncome;
            theBuilding.CategoryForInterest = editingBuilding.CategoryForInterest;
            theBuilding.CategoryForMaintenance = editingBuilding.CategoryForMaintenance;
            theBuilding.CategoryForManagement = editingBuilding.CategoryForManagement;
            theBuilding.CategoryForRepairs = editingBuilding.CategoryForRepairs;
            theBuilding.CategoryForTaxes = editingBuilding.CategoryForTaxes;

            theBuilding.OwnershipName1 = editingBuilding.OwnershipName1;
            theBuilding.OwnershipName2 = editingBuilding.OwnershipName2;

            theBuilding.OwnershipPercentage1 = editingBuilding.OwnershipPercentage1;
            theBuilding.OwnershipPercentage2 = editingBuilding.OwnershipPercentage2;


            this.DialogResult = true;
        }


    }
}
