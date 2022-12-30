using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Forms;

namespace XMoney.ViewModels
{
    public class Transactions
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int Account { get; set; }
        public string AccountAsText
        {
            get
            {
                var account = Accounts.Get(this.Account);
                return account == null ? "<" + this.Account.ToString() + ">" : account.Name;
            }
        }

        public int Payee { get; set; }
        public string PayeeAsText
        {
            get
            {
                var payee = Payees.Get(this.Payee);
                return payee == null ? "" : payee.Name;
            }
        }

        public string Date { get; set; }
        public string DateAsText
        {
            get
            {
                return DateTime.ToString("yyyy-MM-dd");
            }
        }

        public DateTime DateTime
        {
            get
            {
                return DateTime.Parse(this.Date);
            }
        }

        public int Category { get; set; }
        public string CategoryAsText
        {
            get
            {
                var category = Categories.Get(this.Category);
                return category == null ? "" : category.Name;
            }
        }

        public decimal Amount { get; set; }
        public string AmountAsText
        {
            get
            {
                return Amount.ToString("n2");
            }
        }

        public Color AmountColor
        {
            get
            {
                return MyColors.GetCurrencyColor(this.Amount);
            }
        }


        public string Memo { get; set; }

        public bool IsSplit { get { return Categories.SplitCategoryId() == this.Category; } }

        public List<Splits> Splits
        {
            get
            {
                return ViewModels.Splits.GetSplitsForTransaction(this.Id);
            }
        }


        public bool IsTextMatch(string textToFind)
        {
            if (textToFind == null || textToFind == string.Empty)
            {
                return false;
            }

            if (this.PayeeAsText.IndexOf(textToFind, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                return true;
            }

            if (this.DateAsText.IndexOf(textToFind, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                return true;
            }

            if (this.AmountAsText.IndexOf(textToFind, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                return true;
            }

            if (this.Memo.IndexOf(textToFind, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                return true;
            }

            if (this.CategoryAsText.IndexOf(textToFind, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                return true;
            }

            if (this.AccountAsText.IndexOf(textToFind, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                return true;
            }

            return false;
        }

        public static List<Transactions> _cache = new();

        public static void Cache(SQLiteConnection sqliteConnection)
        {
            _cache = (from x in sqliteConnection.Table<Transactions>() select x).ToList();
        }

        public static void OnDemoData()
        {
            var transation = new Transactions() { Id = 0, Account = 0, Date = "2022-02-25", Category = 1, Amount = (decimal)123.45 };
            _cache.Add(transation);
        }

        public static void OnAllDataLoaded()
        {
            // Nothing more to process
        }

        public static void WalkAllTransactionsAndSplits(Action<Transactions, Splits> callback)
        {
            int splitId = Categories.SplitCategoryId();
            foreach (var transaction in _cache)
            {
                if (transaction.Category == splitId)
                {
                    var splits = ViewModels.Splits.GetSplitsForTransaction(transaction.Id);
                    foreach (var split in splits)
                    {
                        callback(transaction, split);
                    }
                }
                else
                {
                    callback(transaction, null);
                }
            }
        }
    }
}