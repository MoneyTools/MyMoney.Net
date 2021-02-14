using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XMoney.ViewModels
{
    public class LoanPayments
    {
        public static List<LoanPayments> _cache = new();

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
        public int AccountId { get; set; }
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

        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public string Memo { get; set; }

        public static void Cache(SQLiteConnection sqliteConnection)
        {
            _cache = (from x in sqliteConnection.Table<LoanPayments>() select x).ToList();
        }

        public static void OnAllDataLoaded()
        {
            // Nothing more to process
        }

    }
}
