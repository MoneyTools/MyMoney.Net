using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Data;
using Walkabout.Interfaces.Views;

namespace Walkabout.Views
{
    /// <summary>
    /// Interaction logic for RentSummary.xaml
    /// </summary>
    public partial class RentSummaryView : UserControl, IView, INotifyPropertyChanged
    {
        public RentBuilding Building { get; set; }
        public string BuildingName { get; set; }
        public string Period { get; set; }
        public string Owner1 { get { return string.Format("{0} {1}%", Building.OwnershipName1, Building.OwnershipPercentage1.ToString()); } }
        public string Owner2 { get { return string.Format("{0} {1}%", Building.OwnershipName2, Building.OwnershipPercentage2.ToString()); } }

        public decimal TotalIncomes { get; set; }
        public decimal TotalExpenses
        {
            get
            {
                return TotalExpensesTaxes + TotalExpensesRepairs + TotalExpensesMaintenance + TotalExpensesManagement + TotalExpensesInterest;
            }
        }

        public void FocusQuickFilter()
        {
        }

        public decimal TotalExpensesTaxes { get; set; }
        public decimal TotalExpensesRepairs { get; set; }
        public decimal TotalExpensesMaintenance { get; set; }
        public decimal TotalExpensesManagement { get; set; }
        public decimal TotalExpensesInterest { get; set; }

        public decimal TotalProfit
        {
            get
            {
                return TotalIncomes + TotalExpenses; // Expenses is tallied in negative value -100
            }
        }

        public decimal TotalProfitOwner1
        {
            get
            {
                return TotalProfit * Building.OwnershipPercentage1 / 100;
            }
        }

        public decimal TotalProfitOwner2
        {
            get
            {
                return TotalProfit * Building.OwnershipPercentage2 / 100;
            }
        }

        public RentSummaryView()
        {
            InitializeComponent();
            this.DataContext = this;
            this.IsVisibleChanged += new DependencyPropertyChangedEventHandler(RentSummary_IsVisibleChanged);
        }


        void RentSummary_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                OnBeforeViewStateChanged();
                OnAfterViewStateChanged();
            }
        }

        public void SetViewToRentBuilding(RentBuilding value)
        {
            this.Building = value;
            this.BuildingName = value.Name;
            this.Period = value.Period;

            this.TotalIncomes = value.TotalIncome;
            this.TotalExpensesTaxes = value.TotalExpense.TotalTaxes;
            this.TotalExpensesRepairs = value.TotalExpense.TotalRepairs;
            this.TotalExpensesManagement = value.TotalExpense.TotalManagement;
            this.TotalExpensesMaintenance = value.TotalExpense.TotalMaintenance;
            this.TotalExpensesInterest = value.TotalExpense.TotalInterest;

            UpdateAllDataBindings();
        }


        public void SetViewToRentalBuildingSingleYear(RentalBuildingSingleYear value)
        {
            this.Building = value.Building;
            this.BuildingName = value.Building.Name;
            this.Period = value.Year.ToString();
            this.TotalIncomes = value.TotalIncome;
            this.TotalExpensesTaxes = value.Departments[1].Total;
            this.TotalExpensesRepairs = value.Departments[2].Total;
            this.TotalExpensesMaintenance = value.Departments[3].Total;
            this.TotalExpensesManagement = value.Departments[4].Total;
            this.TotalExpensesInterest = value.Departments[5].Total;

            UpdateAllDataBindings();
        }

        void UpdateAllDataBindings()
        {
            OnPropertyChanged("Building");
            OnPropertyChanged("BuildingName");
            OnPropertyChanged("Period");

            OnPropertyChanged("TotalIncomes");

            OnPropertyChanged("TotalExpenses");
            OnPropertyChanged("TotalExpensesTaxes");
            OnPropertyChanged("TotalExpensesRepairs");
            OnPropertyChanged("TotalExpensesManagement");
            OnPropertyChanged("TotalExpensesMaintenance");
            OnPropertyChanged("TotalExpensesInterest");

            OnPropertyChanged("TotalProfit");
            OnPropertyChanged("TotalProfitOwner1");
            OnPropertyChanged("TotalProfitOwner2");

            OnPropertyChanged("Owner1");
            OnPropertyChanged("Owner2");

        }

        // INotifyPropertyChanged event
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        #region IView

        public MyMoney Money { get; set; }

        public void ActivateView()
        {
            this.Focus();
        }

        public event EventHandler BeforeViewStateChanged;

        void OnBeforeViewStateChanged()
        {
            if (BeforeViewStateChanged != null)
            {
                BeforeViewStateChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        void OnAfterViewStateChanged()
        {
            if (AfterViewStateChanged != null)
            {
                AfterViewStateChanged(this, new AfterViewStateChangedEventArgs(0));
            }
        }

        IServiceProvider sp;

        public IServiceProvider ServiceProvider
        {
            get { return sp; }
            set { sp = value; }
        }

        public void Commit()
        {
            //tdo
        }

        public string Caption
        {
            get { return "Rent Summary"; }
        }

        public object SelectedRow
        {
            get { return this.Building; }
            set { this.Building = (RentBuilding)value; }
        }


        public ViewState ViewState
        {
            get
            {
                // todo;
                return null;
            }
            set
            {
                // todo;
            }
        }

        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            // todo;
            return null;
        }

        string quickFilter;

        public string QuickFilter
        {
            get { return this.quickFilter; }
            set
            {
                if (this.quickFilter != value)
                {
                    this.quickFilter = value;
                    // todo
                }
            }
        }

        public bool IsQueryPanelDisplayed { get; set; }

        #endregion
    }
}
