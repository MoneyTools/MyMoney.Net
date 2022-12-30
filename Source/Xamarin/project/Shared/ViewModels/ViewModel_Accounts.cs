using SQLite;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Forms;

namespace XMoney.ViewModels
{
    public class Accounts
    {

        public enum AccountType
        {
            Savings = 0,
            Checking = 1,
            MoneyMarket = 2,
            Cash = 3,
            Credit = 4,
            Investment = 5,
            Retirement = 6,
            // There is a hole here from deleted type which we can fill when we invent new types, but the types 8-10 have to keep those numbers        
            // or else we mess up the existing databases.
            Asset = 8,              // Used for tracking Assets like "House, Car, Boat, Jewelry, this helps to make NetWorth more accurate
            CategoryFund = 9,       // a pseudo account for managing category budgets
            Loan = 10,
            CreditLine = 11
        }

        public enum AccountFlags
        {
            None = 0,
            Budgeted = 1,
            Closed = 2,
            TaxDeferred = 4
        }


        [PrimaryKey, AutoIncrement, Column("Id")]
        public int Id { get; set; }

        [Column("Flags")]
        public int Flags { get; set; }

        public bool IsClosed
        {
            get
            {
                return (this.Flags & (int)AccountFlags.Closed) != 0;
            }
        }

        public string AccountId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Type { get; set; }

        public int CategoryIdForPrincipal { get; set; }
        public int CategoryIdForInterest { get; set; }

        public string Currency
        {
            get;
            set;
        }

        public decimal OpeningBalance
        {
            get;
            set;
        }

        public bool CategoryTypeOfAccount
        {
            get
            {
                return Type == (int)AccountType.CategoryFund;
            }
        }

        public string TypeAsText
        {
            get
            {
                switch ((AccountType)Type)
                {
                    case AccountType.Savings:
                        return "Savings";
                    case AccountType.Checking:
                        return "Checking";
                    case AccountType.MoneyMarket:
                        return "MoneyMarket";
                    case AccountType.Cash:
                        return "Cash";
                    case AccountType.Credit:
                        return "Credit";
                    case AccountType.Investment:
                        return "Investment";
                    case AccountType.Retirement:
                        return "Retirement";
                    case AccountType.Asset:
                        return "Asset";
                    case AccountType.CategoryFund:
                        return "CategoryFund";
                    case AccountType.Loan:
                        return "Loan";
                    case AccountType.CreditLine:
                        return "CreditLine";
                    default:
                        break;
                }

                return "other " + Type.ToString();
            }
        }

        public int Count { get; set; }

        public decimal Balance { get; set; }

        public string BalanceAsText
        {
            get
            {
                return Balance.ToString("n2");
            }
        }

        public decimal BalanceNormalized
        {
            get
            {
                //decimal? c = money.Currencies.FindCurrency(this.currency);
                //if (c != null)
                //{
                //    //-----------------------------------------------------
                //    // Apply ratio of conversion
                //    // for example USA 2,000 * CAN .95 = 1,900 (in USA currency)
                //    return this.Balance * c.Ratio;
                //}
                return this.Balance;
            }
        }

        public Color BalanceColor
        {
            get
            {
                return MyColors.GetCurrencyColor(this.Balance);
            }
        }

        public static List<Accounts> _cache = new();
        public static HashSet<int> _cachedClosedAccountIds = new();

        public static void Cache(SQLiteConnection sqliteConnection)
        {
            IEnumerable<Accounts> rawList = from x in sqliteConnection.Table<Accounts>() select x;
            _cache = rawList.ToList();
            foreach (Accounts a in _cache)
            {
                if (a.IsClosed)
                {
                    _ = _cachedClosedAccountIds.Add(a.Id);
                }
            }
        }

        public static void OnDemoData()
        {
            var account = new Accounts() { Id = 0, Name = "Bank of America", Type = (int)AccountType.Checking };
            _cache.Add(account);
        }

        public static void OnAllDataLoaded()
        {
            foreach (var item in _cache)
            {
                item.Count = 0;
                item.Balance = 0;
            }

            foreach (var t in Transactions._cache)
            {
                var account = Get(t.Account);
                if (account != null)
                {
                    account.Count++;
                    account.Balance += t.Amount;
                }
            };
        }


        public static Accounts Get(int id)
        {
            foreach (Accounts item in _cache)
            {
                if (item.Id == id)
                {
                    return item;
                }
            }
            return null;
        }

        public static string GetAsString(int id)
        {
            Accounts account = Get(id);
            return account == null ? "<unknonw " + id + ">" : account.Name;
        }

        public static Accounts GetByName(string accountName)
        {
            foreach (Accounts item in _cache)
            {
                if (item.Name == accountName)
                {
                    return item;
                }
            }
            return null;
        }


        public static List<string> ListOfIdsToListOfString(List<int> listIds)
        {
            var list = new List<string>();
            foreach (int id in listIds)
            {
                list.Add(GetAsString(id));
            }
            list.Sort();
            return list;
        }
    }
}