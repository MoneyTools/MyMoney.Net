using System.Collections;
using System.Collections.Generic;
using System.Windows;
using Walkabout.Data;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for AccountDialog.xaml
    /// </summary>
    public partial class BuildingDialog : BaseDialog
    {
        private MyMoney money;
        private RentBuilding editingBuilding = new RentBuilding();
        private RentBuilding theBuilding = new RentBuilding();

        public RentBuilding TheBuilding
        {
            get { return this.theBuilding; }
            set
            {
                this.theBuilding = value;
                this.editingBuilding = value.ShallowCopy();
                this.DataContext = this.editingBuilding;
                this.Units.ItemsSource = this.editingBuilding.Units;
                this.UpdateUI();
            }
        }

        public BuildingDialog(MyMoney money)
        {
            this.money = money;

            this.InitializeComponent();
            this.UpdateUI();

        }

        private void UpdateUI()
        {
        }

        private List<Category> categories;

        public IList Categories
        {

            get
            {
                if (this.categories == null)
                {
                    this.categories = new List<Category>();
                    Category na = new Category() { Name = "--N/A--", Id = -1 };
                    this.categories.Add(na);

                    foreach (Category c in this.money.Categories.SortedCategories)
                    {
                        this.categories.Add(c);
                    }
                }
                return this.categories;
            }
        }


        private void ButtonOk(object sender, RoutedEventArgs e)
        {
            this.theBuilding.Name = this.editingBuilding.Name;

            this.theBuilding.Address = this.editingBuilding.Address;
            this.theBuilding.Note = this.editingBuilding.Note;
            this.theBuilding.CategoryForIncome = this.editingBuilding.CategoryForIncome;
            this.theBuilding.CategoryForInterest = this.editingBuilding.CategoryForInterest;
            this.theBuilding.CategoryForMaintenance = this.editingBuilding.CategoryForMaintenance;
            this.theBuilding.CategoryForManagement = this.editingBuilding.CategoryForManagement;
            this.theBuilding.CategoryForRepairs = this.editingBuilding.CategoryForRepairs;
            this.theBuilding.CategoryForTaxes = this.editingBuilding.CategoryForTaxes;

            this.theBuilding.OwnershipName1 = this.editingBuilding.OwnershipName1;
            this.theBuilding.OwnershipName2 = this.editingBuilding.OwnershipName2;

            this.theBuilding.OwnershipPercentage1 = this.editingBuilding.OwnershipPercentage1;
            this.theBuilding.OwnershipPercentage2 = this.editingBuilding.OwnershipPercentage2;


            this.DialogResult = true;
        }


    }
}
