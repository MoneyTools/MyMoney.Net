namespace Walkabout.Charts
{
    public class RentalData
    {
        public double Ratio { get; set; }
        public string Label { get; set; }
        public double Income { get; set; }

        public double ExpenseTaxes { get; set; }
        public double ExpenseRepair { get; set; }
        public double ExpenseMaintenance { get; set; }
        public double ExpenseManagement { get; set; }
        public double ExpenseInterest { get; set; }
        public double Expense
        {
            get
            {
                return this.ExpenseTaxes + this.ExpenseRepair + this.ExpenseMaintenance + this.ExpenseManagement + this.ExpenseInterest;
            }
        }
        public double Profit { get { return this.Income - this.Expense; } }

        public RentalData()
        {
            // Insert code required on object creation below this point.
        }
    }
}
