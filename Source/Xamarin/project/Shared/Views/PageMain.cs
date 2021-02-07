using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using XMoney.ViewModels;
using XMoney.Views;

namespace XMoney
{

    public class PageMain : Views.Page
    {
        private bool dataLoaded = false;
        public object LastTappedItem;

        private decimal Total { get; set; }

        private enum ChartType
        {
            Incomes,
            Expenses,
            NetWorth
        }

        private readonly Grid grid = new() { HorizontalOptions = LayoutOptions.FillAndExpand, VerticalOptions = LayoutOptions.FillAndExpand };
        private readonly ViewButtonList columBarTop = new();
        private readonly ChartPie Chart = new() { HorizontalOptions = LayoutOptions.FillAndExpand, VerticalOptions = LayoutOptions.FillAndExpand };
        private readonly Button viewTotal = new() { HeightRequest = 160, WidthRequest = 160, BackgroundColor = Color.FromHex("#bbFFFFFF"), CornerRadius = 80, FontAttributes = FontAttributes.Bold, FontSize = 16, TextColor = Color.FromHex("#3874D6"), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center, Padding = new Thickness(8, 0, 8, 0) };
        private readonly ListView listView = new() { HorizontalOptions = LayoutOptions.FillAndExpand, VerticalOptions = LayoutOptions.FillAndExpand, BackgroundColor = Color.White };
        private readonly ViewBarBottom actionBar = new();

        public PageMain()
        {
            this.Title = "xMoney";
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (this.dataLoaded == false)
            {
                LoadAndShowPage();
            }
            else
            {
                if (!seenOnce)
                {
                    setupPage();

                    this.Content = this.grid;
                    seenOnce = true;
                }
            }
        }

        private void setupPage()
        {
            this.AddToolBarButtonSetting();

            grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(50, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(50, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(60, GridUnitType.Absolute) });

            this.AddElement(grid, columBarTop, 0);
            this.AddElement(grid, Chart, 1);
            this.AddElement(grid, viewTotal, 1);
            this.AddElement(grid, listView, 2);
            this.AddElement(grid, actionBar, 3);


            listView.ItemTemplate = new DataTemplate(typeof(ViewCellForCategoryRollUp));
            listView.ItemTapped += OnItemTapped;

            // Top Button Row
            AddTopButtonRow();

            // Bottom
            AddBottoRow();
        }

        private async void LoadAndShowPage()
        {
            this.ShowPageAsBusy("Loading "+ Settings.SourceDatabase);

            await Task.Run(() =>
            {
                this.dataLoaded = Data.Get.LoadDatabase();
            })
            .ContinueWith(async t =>
            {
                if (this.dataLoaded)
                {
                    this.OnAppearing();
                    this.columBarTop.SelectedByAutomationId((int)ChartType.NetWorth);
                    UpdateChartForIncomes(ChartType.NetWorth);
                }
                else
                {
                    await Navigation.PushAsync(new PageSettings());
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }


        private void AddTopButtonRow()
        {

            // Income
            {
                var button = this.columBarTop.AddButton("Income", 50, (int)ChartType.Incomes);

                button.Clicked += (sender, e) =>
                {
                    this.columBarTop.Selected(sender);
                    UpdateChartForIncomes(ChartType.Incomes);
                };
            }

            XButtonFlex buttonExpense;
            // Expense
            {
                buttonExpense = this.columBarTop.AddButton("Spending", 50, (int)ChartType.Expenses);

                buttonExpense.Clicked += (sender, e) =>
                {
                    this.columBarTop.Selected(sender);
                    UpdateChartForIncomes(ChartType.Expenses);
                };
            }

            // Net Worth
            {
                var button = this.columBarTop.AddButton("Net Worth", 50, (int)ChartType.NetWorth);

                button.Clicked += (sender, e) =>
                {
                    this.columBarTop.Selected(sender);
                    UpdateChartForIncomes(ChartType.NetWorth);
                };
            }
        }

        private void AddBottoRow()
        {
            {
                XButtonFlex b = actionBar.AddButton("🏛\nAccounts", " 🏛 ");
                b.Clicked += ButtonAccounts_Clicked;
            }

            {
                XButtonFlex b = actionBar.AddButton("🏷\nCategories", " 🏷 ");
                b.Clicked += ButtonCategories_Clicked;
            }

            {
                XButtonFlex b = actionBar.AddButton("👥\nPayees", " 👥 ");
                b.Clicked += ButtonPayees_Clicked;
            }

            if (Settings.Get().ManageRentalProperties)
            {
                XButtonFlex b = actionBar.AddButton("🏘\nRentals", " 🏘 ");
                b.Clicked += ButtonRentals_Clicked;
            }

            {
                XButtonFlex b = actionBar.AddButton("🗂\nTransactions", " 🗂 ");
                b.Clicked += ButtonTransactions_Clicked;
            }
        }


        private async void UpdateChartForIncomes(ChartType chartType)
        {
            this.ShowPageAsBusy();

            this.Chart.Clear();
            this.listView.ItemsSource = null;
            this.Total = 0;

            var uiThread = TaskScheduler.FromCurrentSynchronizationContext();
            await Task.Run(() => FilterData(chartType)).ContinueWith(task =>
            {
                this.Content = this.grid;
                this.listView.ItemsSource = task.Result;
                this.RenderChart(task.Result);
                this.UpdateViewTotal();
            }, uiThread);
        }

        private void RenderChart(ObservableCollection<Categories> list)
        {
            foreach (var entry in list)
            {
                try
                {
                    Chart.AddData((int)Math.Abs(entry.Amount), entry.GetSkColor());
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }

        private void UpdateViewTotal()
        {
            this.viewTotal.Text = this.Total.ToString("C");
        }

        private ObservableCollection<Categories> FilterData(ChartType chartType)
        {
            // Show Categories            
            ObservableCollection<Categories> list = new();

            var sorted = Categories._cache.OrderByDescending(c => Math.Abs(c.Amount));

            foreach (var category in sorted)
            {
                if (category.ParentId == -1)
                {
                    switch (chartType)
                    {
                        case ChartType.Incomes:

                            if (category.Type == (int)Categories.CategoryTypes.Income)
                            {
                                if (category.Amount != 0)
                                {
                                    list.Add(category);
                                    this.Total += category.Amount;
                                }
                            }
                            break;

                        case ChartType.Expenses:

                            if (category.Type == (int)Categories.CategoryTypes.Expenses)
                            {
                                if (category.Amount != 0)
                                {
                                    list.Add(category);
                                    this.Total += category.Amount;
                                }

                            }
                            break;

                        case ChartType.NetWorth:
                            {
                                if (category.Amount != 0)
                                {
                                    list.Add(category);
                                    this.Total += category.Amount;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            return list;
        }

        private async void OnItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item == null)
            {
                return;
            }

            //Deselect Item
            ((ListView)sender).SelectedItem = null;


            Categories catagory = (Categories)e.Item;
            if (catagory != null)
            {
                var filter = new Filter();

                filter.CategoryIds.Clear();
                catagory.GetDecendentIds(filter.CategoryIds);

                await Navigation.PushAsync(new PageTransactions(filter));
            }
        }

        // Accounts
        private PageAccounts pageAccounts;

        private async void ButtonAccounts_Clicked(object sender, EventArgs e)
        {
            if (pageAccounts == null)
            {
                this.pageAccounts = new PageAccounts();
            }
            await Navigation.PushAsync(this.pageAccounts);
        }

        // Categories
        private PageCategories pageCategories;
        private async void ButtonCategories_Clicked(object sender, EventArgs e)
        {
            if (pageCategories == null)
            {
                pageCategories = new PageCategories();
            }
            await Navigation.PushAsync(this.pageCategories);
        }

        // Payees
        private PagePayees pagePayees;

        private async void ButtonPayees_Clicked(object sender, EventArgs e)
        {
            if (pagePayees == null)
            {
                pagePayees = new PagePayees();
            }
            await Navigation.PushAsync(this.pagePayees);
        }

        // Transactions
        private PageRentals pageRentals;
        private async void ButtonRentals_Clicked(object sender, EventArgs e)
        {
            if (pageRentals == null)
            {
                pageRentals = new PageRentals();
            }
            await Navigation.PushAsync(this.pageRentals);
        }

        // Transactions
        private PageTransactions pageTransaction;
        private async void ButtonTransactions_Clicked(object sender, EventArgs e)
        {
            if (pageTransaction == null)
            {
                pageTransaction = new PageTransactions();
            }
            await Navigation.PushAsync(this.pageTransaction);
        }
    }


    public class CategoryRollUp
    {
        public string Category;
        public Color Color;
        public int transacationCount = 0;
        public decimal total;
        public string AmountAsText;
        public Color AmountColor => MyColors.GetCurrencyColor(total);
    }


    public class ViewCellForCategoryRollUp : ViewCell
    {
        public ViewCellForCategoryRollUp()
        {
            var grid = new Grid();

            if (App.IsSmallDevice())
            {
                grid.Margin = new Thickness(1, 0, 1, 0);
            }
            else
            {
                // Fix a bug in ListView for MacOS the right side is showing under the scrollbar
                grid.Margin = new Thickness(0, 0, 20, 0);
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(40, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(30, GridUnitType.Star) });

            {
                var element = new Button
                {
                    CornerRadius = 10,
                    WidthRequest = 20,
                    HeightRequest = 20,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                element.SetBinding(VisualElement.BackgroundColorProperty, "RealColor");
                Grid.SetColumn(element, 0);
                grid.Children.Add(element);
            }

            AddElement(grid, "Name", 1);

            AddElement(grid, "Quantity", 2).HorizontalOptions = LayoutOptions.Center;

            {
                var element = AddElement(grid, "AmountAsText", 3);
                element.HorizontalOptions = LayoutOptions.End;
                element.SetBinding(Label.TextColorProperty, "AmountColor");
            }

            this.View = grid;

            static View AddElement(Grid grid, string bindingName, int column)
            {
                var element = new Label
                {
                    TextColor = Color.Black,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center
                };
                element.SetBinding(Label.TextProperty, bindingName);
                Grid.SetColumn(element, column);
                grid.Children.Add(element);
                return element;
            }
        }
    }
}