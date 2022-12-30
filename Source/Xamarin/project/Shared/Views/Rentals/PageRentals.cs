using System.Collections.Generic;
using Xamarin.Essentials;
using Xamarin.Forms;
using xMoney.UIControls;
using XMoney.ViewModels;
using static XMoney.ViewModels.RentBuildings;

namespace XMoney.Views
{
    public class PageRentals : Page
    {
        private readonly List<RentBuildings> _list = new();
        private readonly StackLayout TheList = new()
        {
            Padding = new Thickness(0, 10, 0, 30)
        };

        public PageRentals()
        {
            this.Title = "Rentals";
            this.AddToolBarButtonSetting();
            this.BackgroundColor = Color.LightGray;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!seenOnce)
            {
                this.ShowPageAsBusy();
                LoadList();
                seenOnce = true;
            }
        }

        private void LoadList()
        {
            _list.Clear();
            this.TheList.Children.Clear();

            foreach (var building in _cache)
            {
                _list.Add(building);
            }

            ApplySorting();

            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            var isSmallDevice = IsNarrow();

            foreach (var building in _list)
            {
                var card = new Frame
                {
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.FillAndExpand,
                    BackgroundColor = Color.White,
                    Padding = 10,
                    HasShadow = true,
                };

                if (isSmallDevice)
                {
                    card.Margin = new Thickness(2, 5, 2, 5);
                }
                else
                {
                    card.Margin = new Thickness(15, 15, 25, 0);
                }

                // Populate de grid
                {
                    var stack = new StackLayout()
                    {
                        Orientation = StackOrientation.Vertical
                    };


                    card.Content = stack;

                    // Grid Content
                    {
                        // Top row
                        //
                        // ( Title )  ( Incomes )  ( Expenses ) ( Profit )
                        //
                        // (             Title                )
                        // ( Incomes )  ( Expenses ) ( Profit )
                        {
                            var flex = new FlexLayout
                            {
                                Direction = FlexDirection.Row,
                                Wrap = FlexWrap.Wrap,
                                HorizontalOptions = LayoutOptions.FillAndExpand,
                                JustifyContent = FlexJustify.SpaceBetween,
                            };
                            stack.Children.Add(flex);

                            // Title
                            {
                                var b = this.AddButton(flex, building.Name, async () => { await Navigation.PushAsync(new PageRentalDetails(building)); });
                                b.BackgroundColor = Color.Transparent;
                                b.HeightRequest = 30;
                                b.Margin = new Thickness(0, 0, 6, 6);
                                FlexLayout.SetGrow(b, 1);
                            }

                            var stackButtons = new StackLayout
                            {
                                Orientation = StackOrientation.Horizontal,
                                HorizontalOptions = LayoutOptions.FillAndExpand,
                                HeightRequest = 30
                            };

                            flex.Children.Add(stackButtons);

                            // Incomes
                            this.AddButtonCurrency(stackButtons, building.TotalIncomes, async () => { await Navigation.PushAsync(new PageRentalsIncomes(building)); });

                            // Expense
                            this.AddButtonCurrency(stackButtons, building.TotalExpenses, async () => { await Navigation.PushAsync(new PageRentalsExpenses(building)); });

                            // Profit
                            this.AddButtonCurrency(stackButtons, building.TotalProfits, async () => { await Navigation.PushAsync(new PageRentalsByYears(building)); });
                        }

                        // Second Row
                        {
                            // Address
                            var element = this.AddElementText(stack, building.Address);
                            element.TextColor = Color.DarkGray;
                            element.FontSize = 12f;
                        }

                        // Third Row - Chart
                        {
                            // We host the chart in a horizontal scroll view so that the user can still see the left & rigth most columns
                            var scroll = new ScrollView()
                            {
                                Orientation = ScrollOrientation.Horizontal,
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Start,
                                HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                                VerticalScrollBarVisibility = ScrollBarVisibility.Never,
                            };

                            // Work arround a MacOS bug that does not take into the fact that scrollbar renders over the content
                            if (Device.RuntimePlatform == Device.macOS)
                            {
                                scroll.Padding = new Thickness(0, 0, 0, 30);
                            }

                            var chartView = new ChartBars();
                            scroll.Content = chartView;

                            chartView.SetHeight(120);
                            chartView.HorizontalOptions = LayoutOptions.Center;
                            chartView.VerticalOptions = LayoutOptions.Start;
                            chartView.ActionWhenBarClicked = async (ChartEntry entry) =>
                            {
                                var filter = new Filter
                                {
                                    Year = int.Parse(entry.TextTop)
                                };

                                foreach (var id in building.listOfCategoryIds)
                                {
                                    var cat = Categories.Get(id);
                                    if (cat != null)
                                    {
                                        filter.CategoryIds.AddRange(cat.GetDecendentIds());
                                    }
                                }

                                await Navigation.PushAsync(new PageTransactions(filter));
                            };

                            chartView.Clear();

                            int lastYear = 0;

                            foreach (var yearRolllUp in building.Years)
                            {
                                // Detect year gaps
                                if (lastYear == 0)
                                {
                                    lastYear = yearRolllUp.Value.Year;
                                }

                                for (int yearGap = lastYear + 1; yearGap < yearRolllUp.Value.Year; yearGap++)
                                {
                                    chartView.AddGapColumn(yearGap.ToString());
                                }

                                chartView.Add(yearRolllUp.Value.TotalProfit, yearRolllUp.Value.Year.ToString(), Helpers.ToKMB(yearRolllUp.Value.TotalProfit));

                                lastYear = yearRolllUp.Value.Year;
                            }
                            chartView.Render();

                            stack.Children.Add(scroll);
                        }
                    }
                }

                TheList.Children.Add(card);
            }

            this.Content = new ScrollView
            {
                Content = TheList
            };

        }

        private void ApplySorting()
        {
            this._list.Sort(delegate (RentBuildings x, RentBuildings y)
            {
                return x.Name.CompareTo(y.Name);
            });
        }
    }
}