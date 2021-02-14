using SQLite;
using System.Collections.Generic;
using System.Linq;

namespace XMoney.ViewModels
{
    /*
     {
      "Amount": 73.27,
      "BudgetBalanceDate": null,
      "Category": 383,
      "Flags": 0,
      "Id": 0,
      "Memo": "Principal",
      "Payee": -1,
      "Transaction": 23159,
      "Transfer": -1
    },
     */
    public class Splits
    {
        public static List<Splits> _cache = new();
        private static readonly Dictionary<int, List<Splits>> _mapCategoryIdToSplit = new();
        private static readonly Dictionary<int, List<Splits>> _mapTransactionIdToSplit = new();

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int Category { get; set; }
        public string CategoryAsText
        {
            get
            {
                return Categories.GetAsString(this.Category);
            }
        }

        public int Transaction { get; set; }
        public decimal Amount { get; set; }
        public int Payee { get; set; }
        public string Memo { get; set; }

        public static void Cache(SQLiteConnection sqliteConnection)
        {
            _cache = (from x in sqliteConnection.Table<Splits>() select x).ToList();

            foreach (var split in _cache)
            {
                // Map to Category Id
                if (!_mapCategoryIdToSplit.ContainsKey(split.Category))
                {
                    _mapCategoryIdToSplit.Add(split.Category, new List<Splits>());
                }
                _mapCategoryIdToSplit[split.Category].Add(split);


                // Map to Transaction Id
                if (!_mapTransactionIdToSplit.ContainsKey(split.Transaction))
                {
                    _mapTransactionIdToSplit.Add(split.Transaction, new List<Splits>());
                }
                _mapTransactionIdToSplit[split.Transaction].Add(split);

            }
        }

        public static void OnAllDataLoaded()
        {
            // Nothing more to process
        }


        public static List<Splits> GetSplitsForTransaction(int transactionId)
        {
            if (_mapTransactionIdToSplit.ContainsKey(transactionId))
            {
                return _mapTransactionIdToSplit[transactionId];
            }
            return new List<Splits>();
        }

        public static List<Splits> GetSplitsForCategory(int categoryId)
        {
            if (_mapCategoryIdToSplit.ContainsKey(categoryId))
            {
                return _mapCategoryIdToSplit[categoryId];
            }
            return new List<Splits>();
        }

    }
}
