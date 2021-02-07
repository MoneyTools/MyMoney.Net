using SQLite;
using System.Collections.Generic;
using System.Linq;

namespace XMoney.ViewModels
{

    public class RentBuildings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; }

        public string Address { get; set; }
        public string Note { get; set; }

        public int CategoryForIncome { get; set; }
        public int CategoryForInterest { get; set; }
        public int CategoryForMaintenance { get; set; }
        public int CategoryForManagement { get; set; }
        public int CategoryForRepairs { get; set; }
        public int CategoryForTaxes { get; set; }
        public decimal EstimatedValue { get; set; }
        public decimal LandValue { get; set; }
        public decimal PurchasedPrice { get; set; }

        public string OwnershipName1 { get; set; }
        public decimal OwnershipPercentage1 { get; set; }

        public string OwnershipName2 { get; set; }
        public decimal OwnershipPercentage2 { get; set; }

        public static List<RentBuildings> _cache = new();

        public static void Cache(SQLiteConnection sqliteConnection)
        {
            IEnumerable<RentBuildings> rawList = from x in sqliteConnection.Table<RentBuildings>() select x;

            _cache = rawList.ToList();
        }

        public List<int> listOfCategoryIds = new();

        public static void OnAllDataLoaded()
        {
            AggregateBuildingInformation();
        }

        public decimal TotalExpenses
        {
            get
            {
                decimal total = 0;

                foreach (var yearRolllUp in this.Years)
                {
                    total += yearRolllUp.Value.TotalExpense;
                }

                return total;
            }
        }

        public decimal TotalIncomes
        {
            get
            {
                decimal total = 0;

                foreach (var yearRolllUp in this.Years)
                {
                    total += yearRolllUp.Value.TotalIncome;
                }

                return total;
            }
        }

        public decimal TotalProfits
        {
            get
            {
                decimal total = 0;

                foreach (var yearRolllUp in this.Years)
                {
                    total += yearRolllUp.Value.TotalProfit;
                }

                return total;
            }
        }

        public SortedDictionary<int, RentalBuildingSingleYear> Years = new();

        public static string GetYearRangeString(int yearStart, int yearEnd)
        {
            if (yearStart == int.MaxValue || yearEnd == int.MinValue)
            {
                // This Rental does not yet contain any transactions
                // so there's no date range yet
                return string.Empty;
            }

            if (yearStart != yearEnd)
            {
                return string.Format("{0} - {1}", yearStart, yearEnd);
            }

            return string.Format("{0}", yearStart);
        }

        private void UpdateListOfCategories()
        {
            this.listOfCategoryIds.Clear();

            this.listOfCategoryIds.Add(this.CategoryForIncome);
            this.listOfCategoryIds.Add(this.CategoryForTaxes);
            this.listOfCategoryIds.Add(this.CategoryForInterest);
            this.listOfCategoryIds.Add(this.CategoryForManagement);
            this.listOfCategoryIds.Add(this.CategoryForMaintenance);
            this.listOfCategoryIds.Add(this.CategoryForRepairs);
        }

        public static void AggregateBuildingInformation()
        {
            // Bucket all the rent Incomes & Expenses into each building
            foreach (RentBuildings building in _cache)
            {
                building.UpdateListOfCategories();

                //-------------------------------------------------------------
                // Find all Transactions for this building
                // We need to math the BUILDING and Any Categories (also look in the Splits)
                //
                var yearsOfTransactionForThisBuilding = from t in Transactions._cache
                                                        where MatchAnyCategories(building, t)
                                                        orderby t.DateTime.Year
                                                        group t by t.DateTime.Year into yearGroup
                                                        select (Year: yearGroup.Key, TransactionsForThatYear: yearGroup);

                foreach (var (g, year, transactionsForYear) in from g in yearsOfTransactionForThisBuilding
                                                               let year = g.Year
                                                               let transactionsForYear = new RentalBuildingSingleYear(building, year)
                                                               select (g, year, transactionsForYear))
                {
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Income", Categories.Get(building.CategoryForIncome));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Taxes", Categories.Get(building.CategoryForTaxes));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Repairs", Categories.Get(building.CategoryForRepairs));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Maintenance", Categories.Get(building.CategoryForMaintenance));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Management", Categories.Get(building.CategoryForManagement));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Interest", Categories.Get(building.CategoryForInterest));
                    transactionsForYear.RecalcYears();
                    building.Years[year] = transactionsForYear;
                }
            }
        }

        private static void TotalForCategory(
            RentalBuildingSingleYear transactionsForYear,
            IGrouping<int, Transactions> transactionForSingleBuildingForSingleYear,
            string label,
            Categories categoryToFind
            )
        {

            decimal total = 0;

            if (categoryToFind != null)
            {
                foreach (var t in transactionForSingleBuildingForSingleYear)
                {
                    total += GetTotalAmountMatchingThisCategoryId(t, categoryToFind.Id);
                }
            }

            transactionsForYear.Departments.Add(new RentalBuildingSingleYearSingleDepartment()
            {
                Building = transactionsForYear.Building,
                Year = transactionsForYear.Year,
                Name = label,
                DepartmentCategory = categoryToFind,
                Total = total
            });

        }

        private static decimal GetTotalAmountMatchingThisCategoryId(Transactions t, int c)
        {

            if (t.Category != -1)
            {
                if (t.IsSplit)
                {
                    decimal totalMatchingInThisSplit = 0;
                    foreach (var s in t.Splits)
                    {
                        var category = Categories.Get(s.Category);
                        if (category != null && category.IsDescedantOrMatching(c))
                        {
                            totalMatchingInThisSplit += s.Amount;
                        }
                    }
                    return totalMatchingInThisSplit;
                }
                else
                {
                    var category = Categories.Get(t.Category);
                    if (category.IsDescedantOrMatching(c))
                    {
                        return t.Amount;
                    }
                }
            }

            return 0;
        }

        private static bool MatchAnyCategories(RentBuildings b, Transactions t)
        {

            if (t.Category > 0 && t.Amount != 0)
            {

                if (t.IsSplit)
                {
                    foreach (Splits s in t.Splits)
                    {
                        if (s.Amount != 0)
                        {
                            if (b.IsCategoriesMatched(Categories.Get(s.Category)))
                            {
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    return b.IsCategoriesMatched(Categories.Get(t.Category));
                }
            }

            return false;
        }

        public bool IsCategoriesMatched(Categories c)
        {
            return c != null && c.IsDescedantOrMatching(listOfCategoryIds);
        }
    }

    public class RentExpenseTotal
    {
        public decimal TotalTaxes { get; set; }
        public decimal TotalMaintenance { get; set; }
        public decimal TotalRepairs { get; set; }
        public decimal TotalManagement { get; set; }
        public decimal TotalInterest { get; set; }

        public RentExpenseTotal()
        {
            TotalMaintenance = 0;
            TotalTaxes = 0;
            TotalRepairs = 0;
            TotalManagement = 0;
            TotalInterest = 0;
        }

        public decimal AllExpenses
        {
            get { return TotalTaxes + TotalRepairs + TotalMaintenance + TotalManagement + TotalInterest; }
        }
    }


    public class RentalBuildingSingleYear
    {
        public List<RentalBuildingSingleYearSingleDepartment> Departments { get; set; }

        public int YearStart { get; private set; }

        public int YearEnd { get; private set; }

        public string Period
        {
            get
            {
                string period = RentBuildings.GetYearRangeString(YearStart, YearEnd);
                if (string.IsNullOrEmpty(period))
                {
                    period = Year.ToString();
                }
                return period;
            }
        }

        public RentBuildings Building { get; set; }

        public int Year { get; set; }


        public void RecalcYears()
        {
            this.YearStart = int.MaxValue;
            this.YearEnd = int.MinValue;

            // Incomes
            TotalIncome = this.Departments[0].Total;

            // Expenses
            this.TotalExpensesGroup = new RentExpenseTotal();

            TotalExpensesGroup.TotalTaxes = this.Departments[1].Total;
            TotalExpensesGroup.TotalRepairs = this.Departments[2].Total;
            TotalExpensesGroup.TotalMaintenance = this.Departments[3].Total;
            TotalExpensesGroup.TotalManagement = this.Departments[4].Total;
            TotalExpensesGroup.TotalInterest = this.Departments[5].Total;
        }

        public decimal TotalIncome { get; set; }

        public RentExpenseTotal TotalExpensesGroup { get; set; }

        public decimal TotalExpense
        {
            get
            {
                return TotalExpensesGroup.AllExpenses;
            }
        }

        public decimal TotalProfit
        {
            get
            {
                return TotalIncome + TotalExpense; // Expense is expressed in -negative form 
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="money"></param>
        /// <param name="building"></param>
        /// <param name="year"></param>
        public RentalBuildingSingleYear(
            RentBuildings building,
            int year
            )
        {
            this.Building = building;
            this.Year = year;
            Departments = new List<RentalBuildingSingleYearSingleDepartment>();
        }
    }

    public class RentalBuildingSingleYearSingleDepartment
    {
        public Categories DepartmentCategory;
        public string Name { get; set; }
        public RentBuildings Building { get; set; }
        public int Year { get; set; }
        public decimal Total { get; set; }

        public RentalBuildingSingleYearSingleDepartment()
        {
            Total = 0;
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", Name, Total);
        }
    }
}