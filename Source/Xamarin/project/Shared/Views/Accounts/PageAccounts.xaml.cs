using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XMoney.ViewModels;
using static XMoney.ViewModels.Accounts;

namespace XMoney.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PageAccounts : Page
    {
        private enum SortBy
        {
            Name,
            Type,
            Transactions,
            Balance
        }

        private SortBy _sortedBy = SortBy.Type;
        private bool _sortDirectionAcending = true;
        private readonly List<Accounts> _list = new();

        public PageAccounts()
        {
            InitializeComponent();

            this.Title = "Accounts";
            this.AddToolBarButtonSetting();
            this.AddSearchBar(this.TheGrid);
            SetupHeader();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!seenOnce)
            {
                LoadList();
                seenOnce = true;
            }
        }

        public override void OnSearchBarTextChanged()
        {
            this.LoadList();
        }

        private void LoadList()
        {
            _list.Clear();

            bool hideClosedAccounts = !Settings.Get().ShowClodedAccounts;
            foreach (var account in _cache)
            {
                if (hideClosedAccounts)
                {
                    if (account.IsClosed)
                    {
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(this.filterText))
                {
                    if (account.Name.IndexOf(this.filterText, 0, StringComparison.CurrentCultureIgnoreCase) == -1)
                    {
                        continue;
                    }
                }

                if (!account.CategoryTypeOfAccount)
                {
                    _list.Add(account);
                }
            }

            ApplySorting();
        }

        private async void OnItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item == null)
            {
                return;
            }

            //Deselect Item
            ((ListView)sender).SelectedItem = null;

            Accounts account = (Accounts)e.Item;
            if (account != null)
            {
                var filter = new Filter
                {
                    AccountId = account.Id
                };

                if (account.Type == (int)AccountType.Loan)
                {
                    await Navigation.PushAsync(new PageLoans(filter));
                }
                else
                {
                    await Navigation.PushAsync(new PageTransactions(filter));
                }
            }
        }



        private void ApplySorting()
        {
            this.columBarTop.SelectedByAutomationId((int)_sortedBy);

            switch (_sortedBy)
            {
                case SortBy.Name:
                    MyListView.ItemsSource = _sortDirectionAcending ? _list.OrderBy(item => item.Name).ToList() : _list.OrderByDescending(item => item.Name).ToList();
                    break;

                case SortBy.Type:
                    MyListView.ItemsSource = _sortDirectionAcending ? _list.OrderBy(item => item.TypeAsText).ToList() : _list.OrderByDescending(item => item.TypeAsText).ToList();
                    break;

                case SortBy.Transactions:
                    MyListView.ItemsSource = _sortDirectionAcending ? _list.OrderBy(item => item.Count).ToList() : _list.OrderByDescending(item => item.Count).ToList();
                    break;

                case SortBy.Balance:
                    MyListView.ItemsSource = _sortDirectionAcending ? _list.OrderBy(item => item.Balance).ToList() : _list.OrderByDescending(item => item.Balance).ToList();
                    break;
                default:
                    break;
            }
        }



        private void SetupHeader()
        {
            // Category Name
            {
                var button = this.columBarTop.AddButton("Name", 40, (int)SortBy.Name);

                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Name)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Name;
                    }
                    ApplySorting();
                };
            }

            // Type
            {
                var button = this.columBarTop.AddButton("Type", 20, (int)SortBy.Type);

                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Type)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Type;
                    }
                    ApplySorting();
                };
            }



            // Transaction Count
            {
                var button = this.columBarTop.AddButton("Transactions", "#", 20, (int)SortBy.Transactions);
                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Transactions)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Transactions;
                    }
                    ApplySorting();
                };
            }


            // Balance
            {
                var button = this.columBarTop.AddButton("Balance", 20, (int)SortBy.Balance);
                button.Clicked += (sender, e) =>
                {
                    if (this._sortedBy == SortBy.Balance)
                    {
                        // already sorting by this field, so change the ordering
                        this._sortDirectionAcending = !this._sortDirectionAcending;
                    }
                    else
                    {
                        this._sortedBy = SortBy.Balance;
                    }
                    ApplySorting();
                };
            }
        }
    }
}
