using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Media;

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
        List<Brush> fieldBrushes = null;
        List<string> fieldLabels = null;

        internal void SetExpensesDistribution(int whatExpenseIsAtTheBottom)
        {
            if (MyRentalData != null)
            {
                if (fields == null)
                {
                    fields = new List<object>();
                    fieldBrushes = new List<Brush>();
                    fieldLabels = new List<string>();

                    fields.Add(MyRentalData.ExpenseTaxes);
                    fieldBrushes.Add(Brushes.Blue);
                    fieldLabels.Add("Tax");

                    fields.Add(MyRentalData.ExpenseRepair);
                    fieldBrushes.Add(Brushes.White);
                    fieldLabels.Add("Repair");

                    fields.Add(MyRentalData.ExpenseMaintenance);
                    fieldBrushes.Add(Brushes.Pink);
                    fieldLabels.Add("Maintenance");

                    fields.Add(MyRentalData.ExpenseManagement);
                    fieldBrushes.Add(Brushes.Brown);
                    fieldLabels.Add("Management");

                    fields.Add(MyRentalData.ExpenseInterest);
                    fieldBrushes.Add(Brushes.Black);
                    fieldLabels.Add("Interest");
                }

                UpdateSubExpense(
                    this.Row0,
                    this.Expense0,
                    (double)fields[whatExpenseIsAtTheBottom],
                    fieldBrushes[whatExpenseIsAtTheBottom],
                    fieldLabels[whatExpenseIsAtTheBottom]
                    );

                whatExpenseIsAtTheBottom = Next(whatExpenseIsAtTheBottom);

                UpdateSubExpense(
                    this.Row1,
                    this.Expense1,
                    (double)fields[whatExpenseIsAtTheBottom],
                    fieldBrushes[whatExpenseIsAtTheBottom],
                    fieldLabels[whatExpenseIsAtTheBottom]
                    );

                whatExpenseIsAtTheBottom = Next(whatExpenseIsAtTheBottom);

                UpdateSubExpense(
                    this.Row2,
                    this.Expense2,
                    (double)fields[whatExpenseIsAtTheBottom],
                    null,
                    fieldLabels[whatExpenseIsAtTheBottom]
                    );

                whatExpenseIsAtTheBottom = Next(whatExpenseIsAtTheBottom);

                UpdateSubExpense(
                    this.Row3,
                    this.Expense3,
                    (double)fields[whatExpenseIsAtTheBottom],
                    null,
                    fieldLabels[whatExpenseIsAtTheBottom]
                    );

                whatExpenseIsAtTheBottom = Next(whatExpenseIsAtTheBottom);

                UpdateSubExpense(
                    this.Row4,
                    this.Expense4,
                    (double)fields[whatExpenseIsAtTheBottom],
                    Brushes.DarkRed,
                    fieldLabels[whatExpenseIsAtTheBottom]
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
            Brush background,
            string label
            )
        {
            double defaultHeight = 0;
            if (MyRentalData.Expense != 0)
            {
                defaultHeight = expenseValue / MyRentalData.Expense * 100;
            }
            rd.Height = new System.Windows.GridLength(defaultHeight, System.Windows.GridUnitType.Star);
            b.DataContext = string.Format("{0}{1}{2:N}", label, System.Environment.NewLine, expenseValue);
        }
    }
}