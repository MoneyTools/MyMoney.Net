using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XMoney.ViewModels;

namespace XMoney.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PagePayees : Page
    {
        private enum SortBy
        {
            Name,
            Transactions,
            Balance
        }

        private SortBy _sortedBy = SortBy.Name;
        private bool _sortDirectionAcending = true;

        public PagePayees()
        {
            InitializeComponent();
            SetupHeader();
        }

        private void IsWorking(bool isWorking)
        {
            if (this.WorkingSpinner.IsRunning != isWorking)
            {
                this.WorkingSpinner.IsRunning = isWorking;
                this.WorkingSpinner.IsVisible = isWorking;
                this.MyListView.IsVisible = !isWorking;
            }
        }

        public override void OnSearchBarTextChanged()
        {
            this.LoadList();
        }

        private void LoadList()
        {
            this.IsWorking(true);
            ApplySorting();
            this.IsWorking(false);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!seenOnce)
            {
                this.AddSearchBar(TheGrid);
                DoAsync();
                seenOnce = true;
            }
        }

        private void DoAsync()
        {
            LoadList();
        }

        private async void OnItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item == null)
            {
                return;
            }


            //Deselect Item
            ((ListView)sender).SelectedItem = null;

            Payees payee = (Payees)e.Item;
            if (payee != null)
            {
                var filter = new Filter
                {
                    PayeeId = payee.Id
                };
                await Navigation.PushAsync(new PageTransactions(filter));
            }
        }

        private void ApplySorting()
        {
            this.columBarTop.SelectedByAutomationId((int)_sortedBy);

            List<Payees> list;


            if (this.filterText == string.Empty)
            {
                list = Payees._cache;
            }
            else
            {
                list = Payees._cache.Where(payee =>
                {
                    if (payee.Name == null)
                    {
                        return false;
                    }
                    return payee.Name.IndexOf(this.filterText, 0, StringComparison.CurrentCultureIgnoreCase) != -1;
                }
                ).ToList();
            }

            switch (_sortedBy)
            {
                case SortBy.Name:
                    MyListView.ItemsSource = _sortDirectionAcending
                        ? list.OrderBy(item => item.Name).ToList()
                        : list.OrderByDescending(item => item.Name).ToList();
                    break;

                case SortBy.Transactions:
                    MyListView.ItemsSource = _sortDirectionAcending
                        ? list.OrderBy(item => item.Quantity).ToList()
                        : list.OrderByDescending(item => item.Quantity).ToList();
                    break;

                case SortBy.Balance:
                    MyListView.ItemsSource = _sortDirectionAcending
                        ? list.OrderBy(item => item.Amount).ToList()
                        : list.OrderByDescending(item => item.Amount).ToList();
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


            // Transaction Count
            {
                var button = this.columBarTop.AddButton("Transactions", "#", 30, (int)SortBy.Transactions);
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
                var button = this.columBarTop.AddButton("Balance", 30, (int)SortBy.Balance);
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