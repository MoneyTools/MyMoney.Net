using SQLite;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Forms;

namespace XMoney.ViewModels
{

    public class Payees
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; }

        public int Quantity { get; set; }
        public decimal Amount { get; set; }
        public string AmountAsText
        {
            get
            {
                return Amount.ToString("n2");
            }
        }
        public Color AmountColor => MyColors.GetCurrencyColor(Amount);

        public static List<Payees> _cache = new();
        public static Dictionary<int, Payees> _cacheIdToObject = new();

        public static void Cache(SQLiteConnection sqliteConnection)
        {
            _cache = (from x in sqliteConnection.Table<Payees>() select x).ToList();
            _cacheIds();
        }
        private static void _cacheIds()
        {
            foreach (var payee in _cache)
            {
                _cacheIdToObject.Add(payee.Id, payee);
            }
        }

        public static void OnDemoData()
        {
            var payees = new Payees() { Id = 0, Name = "Home Depot" };
            _cache.Add(payees);
            _cacheIds();
        }

        public static void OnAllDataLoaded()
        {
            EvaluateAmounts();
        }

        public static Payees Get(int id)
        {
            if (_cacheIdToObject.ContainsKey(id))
            {
                return _cacheIdToObject[id];
            }
            return null;
        }

        public static Payees GetByName(string name)
        {
            foreach (var item in _cache)
            {
                if (item.Name == name)
                {
                    return item;
                }
            }
            return null;
        }

        public static string GetAsString(int id)
        {
            var payee = Get(id);
            return payee == null ? "<unknown " + id + ">" : payee.Name;
        }


        public static void EvaluateAmounts()
        {
            foreach (var t in Transactions._cache)
            {
                var payee = Get(t.Payee);
                if (payee == null)
                {
                    //Debug.WriteLine("Invalid Payee ID " + t.Account);
                }
                else
                {
                    payee.Amount += t.Amount;
                    payee.Quantity++;
                }
            }
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