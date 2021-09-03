using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class PageRentalsIncomes : Page
    {
        private readonly RentBuildings building;
        public string Name = "";
        public int Year = -1;

        public PageRentalsIncomes(RentBuildings building)
        {
            this.building = building;
            this.AddToolBarButtonSetting();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!seenOnce)
            {
                this.Title = "Incomes";
                this.ShowPageAsBusy();
                this.LoadList();
                seenOnce = true;
            }
        }

        private void SortByDateDescending(Dictionary<int, List<RentPayment>> map)
        {
            foreach (var kvp in map)
            {
                kvp.Value.Sort((RentPayment a, RentPayment b) => { return a.Date.CompareTo(b.Date); });
            }
        }

        private async void LoadList()
        {
            var isSmallDevice = App.IsSmallDevice();
            var isDeviceLarge = !isSmallDevice;

            var stackList = new StackLayout
            {
                HorizontalOptions = LayoutOptions.FillAndExpand
            };

            var map = new Dictionary<int, List<RentPayment>>();

            await Task.Run(() =>
            {
                map = GetIncomesByYears();
                this.SortByDateDescending(map);
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

                // Roll up total Header          
                var caption = "#\"" + building.Name + "\" " + (maxYear - minYear).ToString() + " years " + minYear.ToString() + "-" + maxYear.ToString();
                var viewHeader = this.CreateViewCaptionValue(caption, null, total);
                viewHeader.Margin = new Thickness(10, 0, 20, 0);
                stackList.Children.Add(viewHeader);


                // Details
                foreach (var kvp in map.Reverse())
                {
                    var card = new Frame
                    {
                        HorizontalOptions = LayoutOptions.FillAndExpand,
                        BackgroundColor = Color.White,
                        HasShadow = true,
                        Margin = new Thickness(5, 5, 5, 0),
                        Padding = new Thickness(20),
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
                        rowCardTitle.HeightRequest = 30;
                        rows.Children.Add(rowCardTitle);
                    }

                    // All incomes for that year
                    var currenMonth = "";

                    foreach (var income in kvp.Value)
                    {
                        if (currenMonth != income.Date.ToString("MMMM"))
                        {
                            currenMonth = income.Date.ToString("MMMM");
                            rows.Children.Add(new Label { Text = currenMonth, TextColor = Color.DarkBlue });
                        }

                        var lastRow = CreateViewCaptionValue("^" + income.Date.Day.ToString(), income.Payee + " \n" + income.CategoryAsText, income.Amount);
                        if (!isSmallDevice)
                        {
                            lastRow.HeightRequest = 50;
                        }
                        rows.Children.Add(lastRow);
                    }
                }

                this.Content = new ScrollView
                {
                    Content = stackList,
                    Padding = new Thickness(0, 0, isSmallDevice ? 0 : 15, 0)

                };
            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

        private Dictionary<int, List<RentPayment>> GetIncomesByYears()
        {
            var map = new Dictionary<int, List<RentPayment>>();

            foreach (var t in Transactions._cache)
            {
                var category = Categories.Get(t.Category);
                if (category != null && category.IsDescedantOrMatching(this.building.CategoryForIncome))
                {
                    AddPayement(map, t.DateTime, t.Amount, t.PayeeAsText, t.CategoryAsText);
                }
                else
                {
                    var listOfIncomesForThisBuilding = Splits.GetSplitsForTransaction(t.Id);
                    foreach (var split in listOfIncomesForThisBuilding)
                    {
                        var categorySplit = Categories.Get(split.Category);
                        if (categorySplit != null && categorySplit.IsDescedantOrMatching(this.building.CategoryForIncome))
                        {
                            AddPayement(map, t.DateTime, split.Amount, split.Memo, split.CategoryAsText == "" ? t.CategoryAsText : split.CategoryAsText);
                        }
                    }
                }
            }
            return map;
        }

        private static void AddPayement(Dictionary<int, List<RentPayment>> map, DateTime dateTime, decimal amount, string memo, string categoryAsText)
        {
            if (!map.TryGetValue(dateTime.Year, out List<RentPayment> listForThisYear))
            {
                listForThisYear = new List<RentPayment>();
                map.Add(dateTime.Year, listForThisYear);
            }
            listForThisYear.Add(new RentPayment(dateTime, amount, memo, false, categoryAsText));
        }
    }

    internal class RentPayment
    {
        public DateTime Date { get; set; }
        public string DateAsText { get { return this.Date.ToString("yyyy-MM-dd"); } }
        public decimal Amount { get; set; }
        public string Payee { get; set; }
        public bool PartOfSplit { get; set; }
        public string CategoryAsText { get; set; }

        public RentPayment(DateTime date, decimal amount, string payee = "", bool partOfSplit = false, string categoryAsText = "")
        {
            this.Date = date;
            this.Amount = amount;
            this.Payee = payee;
            this.PartOfSplit = partOfSplit;
            this.CategoryAsText = categoryAsText;
        }
    }
}