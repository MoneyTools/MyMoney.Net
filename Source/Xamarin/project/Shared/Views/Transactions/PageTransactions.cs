using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XMoney.ViewModels;


namespace XMoney.Views
{
    internal enum SortBy
    {
        Account,
        Date,
        Category,
        Payee,
        Amount
    }


    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PageTransactions : Page
    {
        /////////////////////////////////////////////////////
        // Data
        private SortBy _sortedBy = SortBy.Date;
        private bool _sortDirectionAcending = true;
        private readonly Filter _filter = new();
        public List<Transactions> _filteredItems = new();
        private int tallyTransactions = 0;
        private decimal tallyAmount = 0;
        private readonly HashSet<int> setAccountsOpened = new();
        private readonly HashSet<int> setCategories = new();
        private readonly HashSet<int> setPayees = new();
        private DateTime dateMin;
        private DateTime dateMax;

        /////////////////////////////////////////////////////
        // View
        private readonly Grid pageContent = new();

        // Top
        private readonly ViewButtonList formColumBarTop = new()
        {
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.FillAndExpand,
            HeightRequest = 60,
        };

        // List
        private readonly ListView formList = new()
        {
            VerticalOptions = LayoutOptions.FillAndExpand,
            HorizontalOptions = LayoutOptions.FillAndExpand,
            BackgroundColor = Color.White,
            RowHeight = 60,
            SeparatorVisibility = SeparatorVisibility.None
        };

        // Bottom
        private readonly ViewButtonList formColumBarBottom = new()
        {
            HeightRequest = 60,
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.FillAndExpand,
        };


        /////////////////////////////////////////////////////
        // Methods

        public PageTransactions()
        {
            Init();
        }

        public PageTransactions(Filter filter)
        {
            _filter = filter;
            Init();
        }

        private void Init()
        {
            if (_filter.AccountId != -1)
            {
                var toolbarItemButton = this.AddToolBarButtonInfo();
                toolbarItemButton.Clicked += async (object sender, EventArgs e) =>
                {
                    await Navigation.PushAsync(new PageAccountDetails(Accounts.Get(_filter.AccountId)));
                };
            }
            else if (_filter.CategoryIds.Count > 0)
            {
                var toolbarItemButton = this.AddToolBarButtonInfo();
                toolbarItemButton.Clicked += async (object sender, EventArgs e) =>
                {
                    await Navigation.PushAsync(new PageCategoryDetails(Categories.Get(_filter.CategoryIds.First())));
                };
            }
            else if (_filter.PayeeId != -1)
            {
                var toolbarItemButton = this.AddToolBarButtonInfo();
                toolbarItemButton.Clicked += async (object sender, EventArgs e) =>
                {
                    await Navigation.PushAsync(new PagePayeeDetails(Payees.Get(_filter.PayeeId)));
                };
            }
            this.AddToolBarButtonSetting();


            this.pageContent.VerticalOptions = LayoutOptions.FillAndExpand;
            this.pageContent.HorizontalOptions = LayoutOptions.FillAndExpand;
            this.pageContent.RowSpacing = 0;

            this.pageContent.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            this.pageContent.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            this.pageContent.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40, GridUnitType.Absolute) });
            this.pageContent.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(100, GridUnitType.Star) });
            this.pageContent.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40, GridUnitType.Absolute) });

            // Search Bar
            this.AddSearchBar(this.pageContent, 0);

            // Active Filter
            this.AddFilterBar(this.pageContent, 1);

            // Buttons at the top
            this.AddElement(this.pageContent, formColumBarTop, 2);

            // list
            formList.ItemTemplate = new DataTemplate(typeof(ViewCellForTransaction));
            formList.ItemTapped += OnItemTapped;
            if (Device.RuntimePlatform == Device.macOS)
            {
                // Work around an Xamarin for MacOS bug, the ListView control is adding some extra odd Margin Left and Right
                // we fix this by adding negative margins
                formList.Margin = new Thickness(-20, 0, -25, 0);
            }
            this.AddElement(this.pageContent, formList, 3);

            // Buttons at the bottom
            this.AddElement(this.pageContent, formColumBarBottom, 4);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!seenOnce)
            {
                _ = this.LoadListAsync();
                this.FormFilterBar.IsVisible = this._filter.HasFilters;
                seenOnce = true;
            }
        }

        public static bool IdMatch(int id, int idToFilter)
        {
            if (idToFilter == -1)
            {
                return true; // this means all Accounts
            }
            return id == idToFilter;
        }

        private async Task LoadListAsync()
        {
            this.ShowPageAsBusy();

            FormFilterBar.Text = _filter.GetDescription() + " " + this.FormFilterBar.Text;

            _filteredItems.Clear();
            tallyAmount = 0;
            setAccountsOpened.Clear();
            setCategories.Clear();
            setPayees.Clear();
            this.dateMin = new DateTime();
            this.dateMax = new DateTime();

            TaskScheduler uiThread = TaskScheduler.FromCurrentSynchronizationContext();

            await Task.Run(() => FilterAndSortList()).ContinueWith(t =>
            {
                this.Content = this.pageContent;
                ApplySorting();

                SetupHeader();
                SetupFooter();

            }, uiThread);
        }

        private void FilterAndSortList()
        {
            foreach (Transactions transaction in Transactions._cache)
            {
                if (this._filter.IsValid(transaction))
                {
                    tallyAmount += transaction.Amount;

                    setAccountsOpened.Add(transaction.Account);
                    setCategories.Add(transaction.Category);
                    setPayees.Add(transaction.Payee);

                    if (this.dateMin.Year == 1 || transaction.DateTime.CompareTo(this.dateMin) == -1)
                    {
                        this.dateMin = transaction.DateTime;
                    }

                    if (this.dateMax.Year == 1 || transaction.DateTime.CompareTo(this.dateMax) == +1)
                    {
                        this.dateMax = transaction.DateTime;
                    }

                    _filteredItems.Add(transaction);
                }
            }

            tallyTransactions = _filteredItems.Count();
        }

        private void ApplySorting()
        {
            this.formColumBarTop.SelectedByAutomationId((int)_sortedBy);

            switch (_sortedBy)
            {
                case SortBy.Account:
                    formList.ItemsSource = _sortDirectionAcending
                        ? _filteredItems.OrderBy(item => item.Account).ToList()
                        : _filteredItems.OrderByDescending(item => item.Account).ToList();
                    break;

                case SortBy.Date:
                    formList.ItemsSource = _sortDirectionAcending
                        ? _filteredItems.OrderBy(item => item.Date).ToList()
                        : _filteredItems.OrderByDescending(item => item.Date).ToList();
                    break;

                case SortBy.Category:
                    formList.ItemsSource = _sortDirectionAcending
                        ? _filteredItems.OrderBy(item => item.CategoryAsText).ToList()
                        : _filteredItems.OrderByDescending(item => item.CategoryAsText).ToList();
                    break;

                case SortBy.Payee:
                    formList.ItemsSource = _sortDirectionAcending
                        ? _filteredItems.OrderBy(item => item.PayeeAsText).ToList()
                        : _filteredItems.OrderByDescending(item => item.PayeeAsText).ToList();
                    break;

                case SortBy.Amount:
                    formList.ItemsSource = _sortDirectionAcending
                        ? _filteredItems.OrderBy(item => item.Amount).ToList()
                        : _filteredItems.OrderByDescending(item => item.Amount).ToList();

                    break;
                default:
                    break;
            }
        }

        private string ListToStrings(List<string> list)
        {
            string s = "";
            foreach (string o in list)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    s += Environment.NewLine;
                }
                s += o.ToString();
            }
            return s;
        }

        private void SetupHeader()
        {
            formColumBarTop.Clear();

            // Account
            {
                XButtonFlex button = this.formColumBarTop.AddButton("Account", 20, (int)SortBy.Account);

                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Account)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Account;
                    }
                    ApplySorting();
                };
            }


            // Date
            {
                XButtonFlex button = this.formColumBarTop.AddButton("Date", 10, (int)SortBy.Date);
                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Date)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Date;
                    }
                    ApplySorting();
                };
            }

            // Payee
            {
                XButtonFlex button = this.formColumBarTop.AddButton("Payee", 30, (int)SortBy.Payee);
                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Payee)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Payee;
                    }
                    ApplySorting();
                };
            }

            // Category
            {
                XButtonFlex button = this.formColumBarTop.AddButton("Category", 20, (int)SortBy.Category);
                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Category)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Category;
                    }
                    ApplySorting();
                };
            }

            // Amount
            {
                XButtonFlex button = this.formColumBarTop.AddButton(tallyTransactions.ToString("n0"), 20, (int)SortBy.Amount);
                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Amount)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Amount;
                    }
                    ApplySorting();
                };
            }
        }

        private void SetupFooter()
        {

            formColumBarBottom.Clear();

            // Account
            {
                XButtonFlex button = formColumBarBottom.AddButton(setAccountsOpened.Count().ToString(), 20);
                button.Clicked += (sender, e) =>
                {

                    List<string> list = Accounts.ListOfIdsToListOfString(setAccountsOpened.ToList());

                    App.AlertConfirm(
                        "Accounts",
                        ListToStrings(list),
                        "Ok");
                };
            }

            // Date range
            {
                string text = "";

                if (this.dateMin != null || this.dateMax != null)
                {
                    int days = (this.dateMax.Date - this.dateMin.Date).Days;
                    if (days > 365)
                    {
                        switch (days)
                        {
                            case > 800:
                                text = (days / 365).ToString() + " years";
                                break;
                            default:
                                text = (days / 30).ToString() + " months";
                                break;
                        }
                    }
                    else
                    {
                        text = days.ToString() + " days";
                    }
                }

                XButtonFlex button = formColumBarBottom.AddButton(text, 10);
                button.Clicked += (sender, e) =>
                {
                    App.AlertConfirm(
                        "Date Range",
                        this.dateMin.ToShortDateString() +
                        Environment.NewLine +
                        this.dateMax.ToShortDateString(),
                        "Ok");
                };
            }

            // Payees
            {
                XButtonFlex button = formColumBarBottom.AddButton(setPayees.Count().ToString("n0"), 30);
                button.Clicked += (sender, e) =>
                {

                    List<string> list = Payees.ListOfIdsToListOfString(setPayees.ToList());

                    App.AlertConfirm(
                        "Payees",
                        ListToStrings(list),
                        "Ok");
                };
            }

            // Categories
            {
                XButtonFlex button = formColumBarBottom.AddButton(setCategories.Count().ToString("n0"), 20);
                button.Clicked += (sender, e) =>
                {

                    List<string> list = Categories.ListOfIdsToListOfString(setCategories.ToList());

                    App.AlertConfirm(
                        "Categories",
                        ListToStrings(list),
                        "Ok");
                };
            }

            // Total $ _.__
            {
                formColumBarBottom.AddButton(tallyAmount.ToString("n2"), 20);
            }
        }

        private async void OnItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item == null)
            {
                return;
            }

            Transactions t = (Transactions)e.Item;

            await Navigation.PushAsync(new PageTransacationDetails(t));
        }

        private System.Timers.Timer timerForLoadingList = null;

        public override void OnSearchBarTextChanged()
        {
            this._filter.SearchText = filterText;

            if (this.timerForLoadingList == null)
            {
                this.timerForLoadingList = new System.Timers.Timer
                {
                    Interval = 1500,
                    Enabled = true
                };
                TaskScheduler uiThread = TaskScheduler.FromCurrentSynchronizationContext();
                this.timerForLoadingList.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
                {
                    this.timerForLoadingList.Stop();
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await this.LoadListAsync();
                    });
                };
            }

            // reset the timer
            this.timerForLoadingList.Stop();
            this.timerForLoadingList.Start();
        }
    }


    public class ViewCellForTransaction : ViewCell
    {
        public ViewCellForTransaction()
        {
            var grid = new Grid()
            {
                Padding = new Thickness(1, 4, 1, 4),
                RowSpacing = 0,
                VerticalOptions = LayoutOptions.FillAndExpand,
            };

            if (App.IsSmallDevice())
            {
                grid.Margin = new Thickness(1, 0, 1, 0);
            }
            else
            {
                // Fix a bug in ListView for MacOS the right side is showing under the scrollbar
                grid.Margin = new Thickness(0, 0, 20, 0);
            }

            var separatorLine = new BoxView()
            {
                HeightRequest = 1,
                BackgroundColor = Color.DarkGray,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(4, 0, 4, -4)
            };

            if (App.IsSmallDevice())
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(40, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(40, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Star) });

                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(50, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(50, GridUnitType.Star) });

                // First row
                {
                    // Col 1
                    AddElement(grid, "DateAsText", 0, 0);

                    // Col 2
                    AddElement(grid, "PayeeAsText", 1, 0);

                    // Col 3
                    {
                        var element = AddElement(grid, "AmountAsText", 2, 0);
                        element.HorizontalOptions = LayoutOptions.End;
                        element.SetBinding(Label.TextColorProperty, "AmountColor");
                    }
                }

                // Row 2
                {
                    // Col 1
                    AddElement(grid, "AccountAsText", 0, 1, 1, 9);

                    // Col 2
                    AddElement(grid, "CategoryAsText", 1, 1, 2);
                }

                AddElement(grid, separatorLine, 0, 1, 3);
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(10, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(30, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Star) });

                AddElement(grid, "AccountAsText", 0);
                AddElement(grid, "DateAsText", 1);
                AddElement(grid, "PayeeAsText", 3);
                AddElement(grid, "CategoryAsText", 2);

                // Amount
                {
                    var element = AddElement(grid, "AmountAsText", 4);
                    element.HorizontalOptions = LayoutOptions.End;
                    element.SetBinding(Label.TextColorProperty, "AmountColor");
                }

                AddElement(grid, separatorLine, 0, 0, 5);
            }

            this.View = grid;


        }

        private static View AddElement(Grid grid, string bindingName, int column = 0, int row = 0, int spanCol = 1, int fontSize = 12)
        {
            var element = new Label
            {
                TextColor = Color.Black,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.CenterAndExpand,
                FontSize = fontSize,
                //BackgroundColor = Color.Pink
            };
            element.SetBinding(Label.TextProperty, bindingName);

            AddElement(grid, element, column, row, spanCol);

            return element;
        }

        private static void AddElement(Grid grid, View element, int column, int row, int spanCol = 1)
        {
            Grid.SetColumn(element, column);
            Grid.SetRow(element, row);
            Grid.SetColumnSpan(element, spanCol);
            grid.Children.Add(element);
        }
    }
}