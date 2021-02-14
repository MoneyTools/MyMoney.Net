using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XMoney.ViewModels;


namespace XMoney.Views
{
    public class LoandEntry
    {
        public string AccountAsText { get; set; }
        public DateTime DateTime { get; set; }
        public string DateAsText { get; set; }
        public decimal Principale { get; set; }
        public decimal Interest { get; set; }
        public decimal Payment => this.Principale + this.Interest;
        public decimal Rate { get; set; }
        public decimal Balance { get; set; }
        public Color BalanceColor => MyColors.GetCurrencyColor(Balance);

        public string BalanceAsText => Balance.ToString("n2");
    }

    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PageLoans : Page
    {
        private SortBy _sortedBy = SortBy.Date;
        private bool _sortDirectionAcending = true;
        private readonly Filter _filter = new();
        public List<LoandEntry> _filteredItems = new();
        private readonly HashSet<string> setAccountsOpened = new();
        private decimal _totalPayments = 0;
        private decimal _totalPrincipal = 0;
        private decimal _totalInterest = 0;
        private decimal _runningBalance = 0;

        private DateTime dateMin;
        private DateTime dateMax;


        public PageLoans(Filter filter)
        {
            _filter = filter;

            var toolbarItemButton = this.AddToolBarButtonInfo();
            toolbarItemButton.Clicked += async (object sender, EventArgs e) =>
            {
                await Navigation.PushAsync(new PageLoanDetails(Accounts.Get(_filter.AccountId)));
            };
            Init();
        }

        private void Init()
        {
            InitializeComponent();

            this.AddToolBarButtonSetting();
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

        public static bool IdMatch(int id, int idToFilter)
        {
            if (idToFilter == -1)
            {
                return true; // this means all Accounts
            }
            return id == idToFilter;
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

        private void LoadList()
        {
            FilterBar.Text = _filter.GetDescription();

            this.IsWorking(true);

            _filteredItems.Clear();

            Accounts account = Accounts.Get(_filter.AccountId);

            int idCategoryPrincipal = account.CategoryIdForPrincipal;
            int idCategoryInterest = account.CategoryIdForInterest;


            foreach (Transactions transaction in Transactions._cache)
            {
                if (transaction.Category == Categories.SplitCategoryId())
                {
                    List<Splits> splits = Splits.GetSplitsForTransaction(transaction.Id);

                    decimal principale = 0;
                    decimal interest = 0;

                    foreach (Splits split in splits)
                    {
                        if (split.Category == idCategoryPrincipal)
                        {
                            principale += split.Amount;
                        }

                        if (split.Category == idCategoryInterest)
                        {
                            interest += split.Amount;
                        }
                    }

                    if (principale != 0 || interest != 0)
                    {
                        AddLoandEntry(transaction.AccountAsText, transaction.DateAsText, principale, interest);
                    }
                }
                else
                {
                    if (transaction.Category == idCategoryPrincipal)
                    {

                        AddLoandEntry(transaction.AccountAsText, transaction.DateAsText, transaction.Amount, 0);
                    }

                    if (transaction.Category == idCategoryInterest)
                    {

                        AddLoandEntry(transaction.AccountAsText, transaction.DateAsText, 0, transaction.Amount);
                    }
                }

            }


            //-----------------------------------------------------------------
            // Additional manual entry made for this Loan
            //
            foreach (LoanPayments loanTransaction in LoanPayments._cache)
            {
                if (!loanTransaction.IsDeleted)
                {
                    if (loanTransaction.AccountId == _filter.AccountId)
                    {
                        AddLoandEntry(
                            Accounts.Get(loanTransaction.AccountId).Name,
                            loanTransaction.DateAsText,
                            loanTransaction.Principal,
                            loanTransaction.Interest);
                    }
                }
            }

            LoandEntry lastEntry = ApplySorting();

            // show a projection
            if (Settings.Get().ShowLoanProjection)
            {
                if (lastEntry != null)
                {
                    if (this._runningBalance > 0)
                    {
                        decimal paymentRate = lastEntry.Rate / (decimal)100.0 / 12;
                        int year = this.dateMax.Year;
                        int month = this.dateMax.Month;

                        while (this._runningBalance > 0)
                        {

                            decimal fakeInterest = this._runningBalance * paymentRate;
                            decimal fakePrincipale = lastEntry.Payment - fakeInterest;
                            if (this._runningBalance < lastEntry.Payment)
                            {
                                fakePrincipale = this._runningBalance;
                            }

                            this._runningBalance -= fakePrincipale;

                            this.IncreaseYearMonth(ref year, ref month);
                            string fakeDate = year + "-" + month.ToString("D2") + "-15";

                            AddLoandEntry("-------- Projection >", fakeDate, fakePrincipale, fakeInterest);
                        }

                        _ = ApplySorting();
                    }
                }
            }

            SetupHeader();
            SetupFooter();

            this.IsWorking(false);
        }

        private void IncreaseYearMonth(ref int year, ref int month)
        {
            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        private void AddLoandEntry(string account, string dateAsText, decimal principale, decimal interest)
        {
            var entry = new LoandEntry
            {
                AccountAsText = account,
                DateTime = DateTime.Parse(dateAsText),
                DateAsText = dateAsText,
                Principale = principale,
                Interest = interest
            };
            _filteredItems.Add(entry);
        }


        private LoandEntry ApplySorting()
        {
            this.IsWorking(true);

            setAccountsOpened.Clear();
            this.dateMin = new DateTime();
            this.dateMax = new DateTime();
            _totalPayments = 0;
            _totalPrincipal = 0;
            _totalInterest = 0;


            this.columBarTop.SelectedByAutomationId((int)_sortedBy);

            List<LoandEntry> list = _filteredItems.OrderBy(item => item.DateAsText).ToList();

            this._runningBalance = 0;

            foreach (LoandEntry entry in list)
            {
                if (this._runningBalance > 0)
                {
                    entry.Rate = entry.Interest / this._runningBalance * 12 * 100;
                }

                if (entry.Principale < 0)
                {
                    this._runningBalance += -entry.Principale;
                }
                else
                {
                    this._runningBalance -= entry.Principale;
                }

                entry.Balance = this._runningBalance;


                _ = this.setAccountsOpened.Add(entry.AccountAsText);
                if (entry.Payment > 0)
                {
                    this._totalPayments += entry.Payment;
                }
                if (entry.Principale > 0)
                {
                    this._totalPrincipal += entry.Principale;
                }

                this._totalInterest += entry.Interest;

                if (this.dateMin.Year == 1 || entry.DateTime.CompareTo(this.dateMin) == -1)
                {
                    this.dateMin = entry.DateTime;
                }

                if (this.dateMax.Year == 1 || entry.DateTime.CompareTo(this.dateMax) == +1)
                {
                    this.dateMax = entry.DateTime;
                }
            }

            LoandEntry lastEntry = null;
            if (list.Count > 0)
            {
                lastEntry = list[list.Count - 1];
            }

            MyListView.ItemsSource = list;


            this.IsWorking(false);

            return lastEntry;
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
            columBarTop.Clear();

            // Account
            {
                XButtonFlex button = this.columBarTop.AddButton("Account", 20, (int)SortBy.Account);

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
                    _ = ApplySorting();
                };
            }


            // Date
            {
                XButtonFlex button = this.columBarTop.AddButton("Date", 20, (int)SortBy.Date);
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
                    _ = ApplySorting();
                };
            }


            // Payements
            {
                XButtonFlex button = this.columBarTop.AddButton("Payment", 15, (int)SortBy.Category);
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
                    _ = ApplySorting();
                };
            }


            // Principales
            {
                _ = this.columBarTop.AddButton("Principale", 10, -1);
            }


            // Interests
            {
                _ = this.columBarTop.AddButton("Interests", 10, -1);
            }

            // Rate
            {
                _ = this.columBarTop.AddButton("Rate", 10, -1);
            }

            // Balance
            {
                _ = this.columBarTop.AddButton("Balance", 15, -1);
            }

        }

        private void SetupFooter()
        {
            columBarBottom.Clear();

            // Account
            {
                XButtonFlex button = columBarBottom.AddButton("#" + setAccountsOpened.Count().ToString(), 20);
                button.Clicked += (sender, e) =>
                {

                    List<string> list = setAccountsOpened.ToList();

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
                        if (days > 800)
                        {
                            text = (days / 365).ToString() + " years";
                        }
                        else
                        {
                            text = (days / 30).ToString() + " months";
                        }
                    }
                    else
                    {
                        text = days.ToString() + " days";
                    }
                }

                XButtonFlex button = columBarBottom.AddButton(text, 20);
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


            // Payements
            {
                _ = columBarBottom.AddButton("Payments\n" + _totalPayments.ToString("C"), 20);
            }

            // Principales
            {
                _ = columBarBottom.AddButton("Principal\n" + _totalPrincipal.ToString("C"), 20);
            }

            // Interests
            {
                _ = columBarBottom.AddButton("Interest\n" + _totalInterest.ToString("C"), 20);
            }
        }

        private void OnItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item == null)
            {
                return;
            }

            _ = (LoandEntry)e.Item;
        }
    }
}