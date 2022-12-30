using SkiaSharp;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Forms;

namespace XMoney.ViewModels
{
    public class Categories
    {
        public static List<Categories> _cache = new();
        public static List<Categories> _cacheTop = new();
        public static Dictionary<int, Categories> _IdToObject = new();


        public static void Cache(SQLiteConnection sqliteConnection)
        {
            _cache = (from x in sqliteConnection.Table<Categories>() select x).ToList();
        }

        private static void CreateFlatTree()
        {
            _IdToObject.Clear();

            foreach (var c in _cache)
            {
                _IdToObject.Add(c.Id, c);
            }
        }


        public static Categories Get(int id)
        {
            if (_IdToObject.ContainsKey(id))
            {
                return _IdToObject[id];
            }
            return null;
        }

        public static Categories GetByName(string name)
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

        public List<int> GetDecendentIds()
        {
            var list = new List<int>();
            this.GetDecendentIds(list);
            return list;
        }

        public void GetDecendentIds(List<int> list)
        {
            list.Add(this.Id);

            foreach (Categories c in _cache)
            {
                if (c.ParentId == this.Id)
                {
                    c.GetDecendentIds(list);
                }
            }
        }

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int ParentId { get; set; }
        public Categories GetParent()
        {
            return Get(this.ParentId);
        }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Type { get; set; }
        public string TypeAsText
        {
            get
            {
                switch ((CategoryTypes)this.Type)
                {
                    case CategoryTypes.None:
                        return "<none>";

                    case CategoryTypes.Income:
                        return "Income";

                    case CategoryTypes.Expenses:
                        return "Expense";

                    case CategoryTypes.Saving:
                        return "Saving";

                    case CategoryTypes.Reserved:
                        return "Reserved";

                    case CategoryTypes.Transfer:
                        return "Transfer";

                    case CategoryTypes.Investments:
                        return "Investment";
                    default:
                        break;
                }

                return this.Type.ToString();
            }
        }

        public Color TypeAsColor
        {
            get
            {
                switch ((CategoryTypes)this.Type)
                {
                    case CategoryTypes.Income:
                        return Xamarin.Forms.Color.DarkGreen;

                    case CategoryTypes.Expenses:
                        return Xamarin.Forms.Color.DarkRed;
                    case CategoryTypes.None:
                        break;
                    case CategoryTypes.Saving:
                        break;
                    case CategoryTypes.Reserved:
                        break;
                    case CategoryTypes.Transfer:
                        break;
                    case CategoryTypes.Investments:
                        break;
                    default:
                        break;
                }

                return Xamarin.Forms.Color.Black;
            }
        }

        public Color AmountColor
        {
            get
            {
                if (this.Amount < 0)
                {
                    return Xamarin.Forms.Color.DarkRed;
                }
                return Xamarin.Forms.Color.DarkGreen;
            }
        }


        public string Color { get; set; }

        public Color RealColor
        {
            get
            {
                Color c = MyColors.GetColor(this.Id);
                return c;
            }
        }

        public SKColor GetSkColor()
        {
            return SKColor.Parse(HexConverter(RealColor));
        }


        // Runtime field
        public decimal Amount { get; set; }
        public string AmountAsText
        {
            get
            {
                return this.Amount.ToString("n2");
            }
        }

        public int Quantity { get; set; }

        public bool IsIncome
        {
            get
            {
                return this.Type == (int)CategoryTypes.Income;
            }
        }


        public bool IsExpense
        {
            get
            {
                return this.Type == (int)CategoryTypes.Expenses;
            }
        }


        public bool IsDescedantOrMatching(int categoryId)
        {
            if (this.Id == categoryId)
            {
                return true;
            }

            var parent = GetParent();
            if (parent != null && parent.IsDescedantOrMatching(categoryId))
            {
                return true;
            }
            return false;
        }

        public bool IsDescedantOrMatching(List<int> categoriesIdToMatch)
        {

            if (categoriesIdToMatch.Contains(this.Id))
            {
                return true;
            }

            var parent = GetParent();
            if (parent != null && parent.IsDescedantOrMatching(categoriesIdToMatch))
            {
                return true;
            }
            return false;
        }

        private static string HexConverter(System.Drawing.Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        private static int idOfSplitCategory = -1;

        public static int SplitCategoryId()
        {
            if (idOfSplitCategory == -1)
            {
                var cat = GetByName("Split");
                if (cat != null)
                {
                    idOfSplitCategory = cat.Id;
                }
            }
            return idOfSplitCategory;
        }

        public enum CategoryTypes
        {
            None = 0,
            Income = 1,
            Expenses = 2,
            Saving = 3,
            Reserved = 4,
            Transfer = 5,
            Investments = 6
        }

        public static string GetAsString(int id)
        {
            var category = Get(id);
            return category == null ? "<unknown>" : category.Name;
        }

        public static void OnDemoData()
        {
            var categories = new Categories() { Id = 1, Name = "Furnitures", Type = (int)CategoryTypes.Expenses };
            _cache.Add(categories);
        }


        public static void OnAllDataLoaded()
        {
            // Optimization - Top Category
            {
                _cacheTop = (from x in _cache where x.ParentId == -1 orderby x.Type select x).ToList();
            }

            // Optimization - Tree view
            {
                CreateFlatTree();
            }

            // Roll up amount for each categories
            {
                foreach (var item in _cache)
                {
                    item.Quantity = 0;
                    item.Amount = 0;
                }

                Transactions.WalkAllTransactionsAndSplits((Transactions t, Splits s) =>
                {
                    if (s == null)
                    {
                        var toUpdate = Get(t.Category);
                        if (toUpdate != null)
                        {
                            toUpdate.ApplyTransactionAmount(t.Amount);
                        }
                    }
                    else
                    {
                        var toUpdate = Get(s.Category);
                        if (toUpdate != null)
                        {
                            toUpdate.ApplyTransactionAmount(s.Amount);
                        }
                    }
                });
            }
        }

        public void ApplyTransactionAmount(decimal amount)
        {
            this.Amount += amount;
            this.Quantity++;

            var parent = this.GetParent();
            if (parent != null)
            {
                parent.ApplyTransactionAmount(amount);
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
