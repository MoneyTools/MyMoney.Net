using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class PageRentalsExpenses : Page
    {
        private readonly RentBuildings building;
        public string Name = "";
        public int Year = -1;

        public PageRentalsExpenses(RentBuildings building)
        {
            this.building = building;
            this.AddToolBarButtonSetting();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!seenOnce)
            {
                this.Title = "Expenses";
                this.ShowPageAsBusy();
                LoadList();
                seenOnce = true;
            }
        }

        private async void LoadList()
        {
            var stackList = new StackLayout()
            {
                HorizontalOptions = LayoutOptions.FillAndExpand
            };


            var map = new SortedList<int, List<RentExpense>>();

            await Task.Run(() =>
            {
                map = GetExpensesByYears();
            })
            .ContinueWith(t =>
            {
                decimal total = 0;
                int minYear = int.MaxValue;
                int maxYear = int.MinValue;
                {
                    foreach (var kvp in map)
                    {
                        foreach (var income in kvp.Value)
                        {
                            total += income.Amount;
                            minYear = Math.Min(income.Date.Year, minYear);
                            maxYear = Math.Max(income.Date.Year, maxYear);
                        }
                    }
                }

                // Roll up total
                var caption = "#\"" + building.Name + "\" " + (maxYear - minYear).ToString() + " years " + minYear.ToString() + "-" + maxYear.ToString();
                var viewHeader = this.CreateViewCaptionValue(caption, null, total);
                viewHeader.Margin = new Thickness(10, 0, 0, 0);
                stackList.Children.Add(viewHeader);


                foreach (var kvp in map.Reverse())
                {
                    var card = new Frame
                    {
                        HorizontalOptions = LayoutOptions.FillAndExpand,
                        BackgroundColor = Color.White,
                        HasShadow = true,
                        Margin = new Thickness(5, 5, 5, 0),
                        Padding = new Thickness(10),
                    };
                    stackList.Children.Add(card);

                    // each recorded payments
                    var rows = new StackLayout()
                    {
                        Orientation = StackOrientation.Vertical,
                        HorizontalOptions = LayoutOptions.FillAndExpand,
                    };
                    card.Content = rows;

                    // Title
                    {
                        decimal totalForThatYear = 0;
                        foreach (var income in kvp.Value)
                        {
                            totalForThatYear += income.Amount;
                        }

                        var rowCardTitle = CreateViewCaptionValue("#" + kvp.Key.ToString(), null, totalForThatYear);
                        rows.Children.Add(rowCardTitle);
                    }

                    // All transaction for that year
                    foreach (var income in kvp.Value)
                    {
                        var lastRow = CreateViewCaptionValue(income.DateAsText, income.Payee, income.Amount);
                        rows.Children.Add(lastRow);
                    }
                }

                this.Content = new ScrollView
                {
                    Content = stackList
                };

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private SortedList<int, List<RentExpense>> GetExpensesByYears()
        {
            var map = new SortedList<int, List<RentExpense>>();

            foreach (var t in Transactions._cache)
            {
                var cat = Categories.Get(t.Category);

                if (this.building.IsCategoriesMatched(cat))
                {
                    AddPayement(map, t.DateTime, t.Amount, t.PayeeAsText);
                }
                else
                {
                    var listOfIncomesForThisBuilding = Splits.GetSplitsForTransaction(t.Id);
                    foreach (var split in listOfIncomesForThisBuilding)
                    {
                        if (split.Category == this.building.CategoryForIncome)
                        {
                            AddPayement(map, t.DateTime, split.Amount, split.Memo);
                        }
                    }
                }
            }
            return map;
        }

        private static void AddPayement(SortedList<int, List<RentExpense>> map, DateTime dateTime, decimal amount, string memo)
        {
            if (!map.TryGetValue(dateTime.Year, out List<RentExpense> listForThisYear))
            {
                listForThisYear = new List<RentExpense>();
                map.Add(dateTime.Year, listForThisYear);
            }
            listForThisYear.Add(new RentExpense(dateTime, amount, memo));
        }
    }

    internal class RentExpense
    {
        public DateTime Date { get; set; }
        public string DateAsText { get { return this.Date.ToString("yyyy-MM-dd"); } }
        public decimal Amount { get; set; }
        public string Payee { get; set; }
        public bool PartOfSplit { get; set; }

        public RentExpense(DateTime date, decimal amount, string payee = "", bool partOfSplit = false)
        {
            this.Date = date;
            this.Amount = amount;
            this.Payee = payee;
            this.PartOfSplit = partOfSplit;
        }
    }
}