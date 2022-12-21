using System.Collections.Generic;
using System.Windows.Controls;

namespace Walkabout.Charts
{
    /// <summary>
    /// Interaction logic for ProfitLossColumn.xaml
    /// </summary>
    public partial class RentalChartColumn : UserControl
    {
        public RentalData MyRentalData
        {
            get { return this.DataContext as RentalData; }
            set { this.DataContext = value; }
        }

        public RentalChartColumn()
        {
            this.InitializeComponent();
        }

        List<object> fields = null;
        List<string> fieldLabels = null;

        internal void SetExpensesDistribution(int whatExpenseIsAtTheBottom)
        {
            if (this.MyRentalData != null)
            {
                if (this.fields == null)
                {
                    this.fields = new List<object>();
                    this.fieldLabels = new List<string>();

                    this.fields.Add(this.MyRentalData.ExpenseTaxes);
                    this.fieldLabels.Add("Tax");

                    this.fields.Add(this.MyRentalData.ExpenseRepair);
                    this.fieldLabels.Add("Repair");

                    this.fields.Add(this.MyRentalData.ExpenseMaintenance);
                    this.fieldLabels.Add("Maintenance");

                    this.fields.Add(this.MyRentalData.ExpenseManagement);
                    this.fieldLabels.Add("Management");

                    this.fields.Add(this.MyRentalData.ExpenseInterest);
                    this.fieldLabels.Add("Interest");
                }

                this.UpdateSubExpense(
                    this.Row0,
                    this.Expense0,
                    (double)this.fields[whatExpenseIsAtTheBottom],
                    this.fieldLabels[whatExpenseIsAtTheBottom]
                    );

                whatExpenseIsAtTheBottom = Next(whatExpenseIsAtTheBottom);

                this.UpdateSubExpense(
                    this.Row1,
                    this.Expense1,
                    (double)this.fields[whatExpenseIsAtTheBottom],
                    this.fieldLabels[whatExpenseIsAtTheBottom]
                    );

                whatExpenseIsAtTheBottom = Next(whatExpenseIsAtTheBottom);

                this.UpdateSubExpense(
                    this.Row2,
                    this.Expense2,
                    (double)this.fields[whatExpenseIsAtTheBottom],
                    this.fieldLabels[whatExpenseIsAtTheBottom]
                    );

                whatExpenseIsAtTheBottom = Next(whatExpenseIsAtTheBottom);

                this.UpdateSubExpense(
                    this.Row3,
                    this.Expense3,
                    (double)this.fields[whatExpenseIsAtTheBottom],
                    this.fieldLabels[whatExpenseIsAtTheBottom]
                    );

                whatExpenseIsAtTheBottom = Next(whatExpenseIsAtTheBottom);

                this.UpdateSubExpense(
                    this.Row4,
                    this.Expense4,
                    (double)this.fields[whatExpenseIsAtTheBottom],
                    this.fieldLabels[whatExpenseIsAtTheBottom]
                    );
            }
        }

        private static int Next(int whatExpenseIsAtTheBottom)
        {
            whatExpenseIsAtTheBottom++;
            if (whatExpenseIsAtTheBottom > 4)
            {
                whatExpenseIsAtTheBottom = 0;
            }
            return whatExpenseIsAtTheBottom;
        }

        private void UpdateSubExpense(
            RowDefinition rd,
            Border b,
            double expenseValue,
            string label
            )
        {
            double defaultHeight = 0;
            if (this.MyRentalData.Expense != 0)
            {
                defaultHeight = expenseValue / this.MyRentalData.Expense * 100;
            }
            rd.Height = new System.Windows.GridLength(defaultHeight, System.Windows.GridUnitType.Star);
            b.DataContext = string.Format("{0}{1}{2:N}", label, System.Environment.NewLine, expenseValue);
        }
    }
}