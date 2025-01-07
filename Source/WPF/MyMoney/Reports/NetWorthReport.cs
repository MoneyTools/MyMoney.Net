using LovettSoftware.Charts;
using Microsoft.Win32;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;
using Walkabout.Charts;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.StockQuotes;
using Walkabout.Utilities;
using Walkabout.Views;
using Walkabout.Views.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Walkabout.Reports
{

    //=========================================================================================
    public class NetWorthReport : Report
    {
        private MyMoney myMoney;
        private readonly Random rand = new Random(Environment.TickCount);
        private readonly byte minRandColor, maxRandColor;
        private DateTime reportDate;
        private StockQuoteCache cache;
        private bool generating;
        private FlowDocumentView view;
        private AnimatingBarChart historicalChart;

        public event EventHandler<SecurityGroup> SecurityDrillDown;
        public event EventHandler<AccountGroup> CashBalanceDrillDown;

        public NetWorthReport(FlowDocumentView view)
        {
            this.view = view;
            this.reportDate = DateTime.Today;
            this.minRandColor = 20;
            this.maxRandColor = ("" + AppTheme.Instance.GetTheme()).Contains("Dark") ? (byte)128 : (byte)200;
        }

        ~NetWorthReport()
        {
            Debug.WriteLine("NetWorthReport disposed!");
        }

        protected override void Dispose(bool disposing)
        {
            this.historicalChart = null;
            base.Dispose(disposing);
        }

        public override void OnSiteChanged()
        {
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
            this.cache = (StockQuoteCache)this.ServiceProvider.GetService(typeof(StockQuoteCache));
        }

        class NetworthReportState : IReportState
        {
            public DateTime ReportDate { get; set; }

            public NetworthReportState(DateTime reportDate)
            {
                this.ReportDate = reportDate;
            }

            public Type GetReportType()
            {
                return typeof(NetWorthReport);
            }
        }

        public override IReportState GetState()
        {
            return new NetworthReportState(reportDate);
        }

        public override void ApplyState(IReportState state)
        {
            if (state is NetworthReportState networthReportState)
            {
                this.reportDate = networthReportState.ReportDate;
            }
        }

        public override async Task Generate(IReportWriter writer)
        {
            this.generating = true;
            // the lock locks out any change to the cache from background downloading of stock quotes
            // while we are generating this report.
            using (var cacheLock = this.cache.BeginLock())
            {
                try
                {
                    writer.WriteHeading("Net Worth Statement");

                    if (writer is FlowDocumentReportWriter fwriter)
                    {
                        Paragraph heading = fwriter.CurrentParagraph;
                        DatePicker picker = new DatePicker();
                        // byYearCombo.SelectionChanged += OnYearChanged;
                        System.Windows.Automation.AutomationProperties.SetName(picker, "ReportDate");
                        picker.Margin = new Thickness(10, 0, 0, 0);
                        picker.SelectedDate = this.reportDate;
                        picker.DisplayDate = this.reportDate;
                        picker.SelectedDateChanged += this.Picker_SelectedDateChanged;
                        this.AddInline(heading, picker);
                    }


                    this.WriteCurrencyHeading(writer, this.DefaultCurrency);
                    var series = new ChartDataSeries() { Name = "Net Worth" };
                    IList<ChartDataValue> data = series.Values;

                    // outer table contains 2 columns, left is the summary table, right is the pie chart.
                    writer.StartTable();
                    writer.StartColumnDefinitions();
                    writer.WriteColumnDefinition("450", 450, 450);
                    writer.WriteColumnDefinition("620", 620, 620);
                    writer.EndColumnDefinitions();
                    writer.StartRow();
                    writer.StartCell();

                    // inner table contains the "data"
                    writer.StartTable();
                    writer.StartColumnDefinitions();
                    writer.WriteColumnDefinition("30", 30, 30);
                    writer.WriteColumnDefinition("300", 300, 300);
                    writer.WriteColumnDefinition("Auto", 100, double.MaxValue);

                    writer.EndColumnDefinitions();

                    WriteHeader(writer, "Cash");

                    decimal totalBalance = 0;
                    bool hasTaxDeferred = false;
                    bool hasTaxFree = false;

                    foreach (Account a in this.myMoney.Accounts.GetAccounts(false))
                    {
                        if (a.IsTaxDeferred)
                        {
                            hasTaxDeferred = true;
                        }

                        if (a.IsTaxFree)
                        {
                            hasTaxFree = true;
                        }
                    }

                    Predicate<Account> bankAccountFilter = (a) => { return this.IsBankAccount(a); };
                    decimal balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, bankAccountFilter);
                    var cashGroup = new AccountGroup() { Filter = bankAccountFilter, Date = this.reportDate, Title = "Bank Account" };

                    // Non-investment Cash
                    var color = this.GetRandomColor();
                    data.Add(new ChartDataValue() { Label = "Cash", Value = (double)balance.RoundToNearestCent(), Color = color, UserData = cashGroup });
                    this.WriteRow(writer, color, "Cash", balance, () => this.OnSelectCashGroup(cashGroup));
                    totalBalance += balance;

                    // Investment Cash
                    Predicate<Account> investmentAccountFilter = (a) => { return this.IsInvestmentAccount(a) && !a.IsTaxDeferred && !a.IsTaxFree; };
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, investmentAccountFilter);
                    if (balance != 0)
                    {
                        var investmentCashGroup = new AccountGroup { Filter = investmentAccountFilter, Date = this.reportDate, Title = "Investment Account" };
                        color = this.GetRandomColor();
                        data.Add(new ChartDataValue() { Label = "Investment Cash", Value = (double)balance.RoundToNearestCent(), Color = color, UserData = investmentCashGroup });
                        this.WriteRow(writer, color, "Investment Cash", balance, () => this.OnSelectCashGroup(investmentCashGroup));
                        totalBalance += balance;
                    }

                    // Tax-Deferred Cash
                    Predicate<Account> taxDeferredAccountFilter = (a) => { return this.IsInvestmentAccount(a) && a.IsTaxDeferred; };
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, taxDeferredAccountFilter);
                    if (balance != 0)
                    {
                        var taxDeferredCashGroup = new AccountGroup { Filter = taxDeferredAccountFilter, Date = this.reportDate, Title = "Tax-Deferred Account" };
                        color = this.GetRandomColor();
                        data.Add(new ChartDataValue() { Label = "Tax-Deferred Cash", Value = (double)balance.RoundToNearestCent(), Color = color, UserData = taxDeferredCashGroup });
                        this.WriteRow(writer, color, "Tax-Deferred Cash", balance, () => this.OnSelectCashGroup(taxDeferredCashGroup));
                        totalBalance += balance;
                    }

                    // Tax-Free Cash
                    Predicate<Account> taxFreeAccountFilter = (a) => { return this.IsInvestmentAccount(a) && a.IsTaxFree; };
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, taxFreeAccountFilter);
                    if (balance != 0)
                    {
                        var taxFreeCashGroup = new AccountGroup { Filter = taxFreeAccountFilter, Date = this.reportDate, Title = "Tax-Free Account" };
                        color = this.GetRandomColor();
                        data.Add(new ChartDataValue() { Label = "Tax-Free Cash", Value = (double)balance.RoundToNearestCent(), Color = color, UserData = taxFreeCashGroup });
                        this.WriteRow(writer, color, "Tax-Free Cash", balance, () => this.OnSelectCashGroup(taxFreeCashGroup));
                        totalBalance += balance;
                    }

                    bool hasNoneTypeTaxDeferred = false;
                    Tuple<decimal, bool> r = null;
                    if (hasTaxDeferred)
                    {
                        WriteHeader(writer, "Tax Deferred Assets");
                        r = await this.WriteSecurities(writer, data, TaxStatus.TaxDeferred, new Predicate<Account>((a) => { return this.IsInvestmentAccount(a) && a.IsTaxDeferred; }));
                        totalBalance += r.Item1;
                        hasNoneTypeTaxDeferred = r.Item2;
                    }

                    bool hasNoneTypeTaxFree = false;
                    if (hasTaxFree)
                    {
                        WriteHeader(writer, "Tax Free Assets");
                        r = await this.WriteSecurities(writer, data, TaxStatus.TaxFree, new Predicate<Account>((a) => { return this.IsInvestmentAccount(a) && a.IsTaxFree; }));
                        totalBalance += r.Item1;
                        hasNoneTypeTaxFree = r.Item2;
                    }

                    balance = 0;

                    WriteHeader(writer, "Taxable Assets");

                    totalBalance += this.WriteLoanAccountRows(writer, data, color, false);
                    totalBalance += this.WriteAssetAccountRows(writer, data);

                    r = await this.WriteSecurities(writer, data, TaxStatus.Taxable, new Predicate<Account>((a) => { return this.IsInvestmentAccount(a) && !a.IsTaxDeferred && !a.IsTaxFree; }));
                    totalBalance += r.Item1;
                    bool hasNoneType = r.Item2;

                    // liabilities are not included in the pie chart because that would be confusing.
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (a) => a.Type == AccountType.Credit);
                    WriteHeader(writer, "Liabilities");

                    Predicate<Account> creditAccountFilter = (a) => a.Type == AccountType.Credit;
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, creditAccountFilter);
                    totalBalance += balance;
                    var creditGroup = new AccountGroup() { Filter = creditAccountFilter, Date = this.reportDate, Title = "Credit Accounts" };
                    color = this.GetRandomColor();
                    this.WriteRow(writer, color, "Credit", balance, () => this.OnSelectCashGroup(creditGroup));
                    totalBalance += this.WriteLoanAccountRows(writer, data, color, true);


                    writer.StartFooterRow();

                    writer.StartCell(1, 2);
                    writer.WriteParagraph("Total");
                    writer.EndCell();

                    writer.StartCell();
                    writer.WriteNumber(this.GetFormattedNormalizedAmount(totalBalance));
                    writer.EndCell();

                    writer.EndFooterRow();
                    writer.EndTable();

                    writer.EndCell();
                    writer.StartCell();

                    // pie chart
                    AnimatingPieChart chart = new AnimatingPieChart();
                    chart.Width = 600;
                    chart.Height = 400;
                    chart.HorizontalContentAlignment = HorizontalAlignment.Left;
                    chart.Padding = new Thickness(20, 0, 100, 0);
                    chart.BorderThickness = new Thickness(0);
                    chart.VerticalAlignment = VerticalAlignment.Top;
                    chart.HorizontalAlignment = HorizontalAlignment.Left;
                    chart.Series = series;
                    chart.ToolTipGenerator = this.OnGenerateToolTip;
                    chart.PieSliceClicked += this.OnPieSliceClicked;

                    writer.WriteElement(chart);

                    writer.EndCell();
                    writer.EndRow();
                    writer.EndTable();

                    // insert graph of historical networth.
                    AnimatingBarChart historicalChart = new AnimatingBarChart();
                    historicalChart.Width = 1280;
                    historicalChart.Height = 400;
                    historicalChart.HorizontalContentAlignment = HorizontalAlignment.Left;
                    historicalChart.Padding = new Thickness(20, 0, 100, 0);
                    historicalChart.BorderThickness = new Thickness(0);
                    historicalChart.VerticalAlignment = VerticalAlignment.Top;
                    historicalChart.HorizontalAlignment = HorizontalAlignment.Left;
                    historicalChart.ContextMenu = new ContextMenu();
                    historicalChart.ColumnClicked += this.OnBarChartColumnClicked;
                    var menuItem = new MenuItem() { Header = "Export..." };
                    menuItem.Click += this.ExportHistoryClick;
                    historicalChart.ContextMenu.Items.Add(menuItem);

                    color = this.GetRandomColor();
                    ChartData chartData = new ChartData();
                    var chartSeries = new ChartDataSeries() { Name = "Networth History" };
                    chartSeries.Values.Add(new ChartDataValue() { Label = this.reportDate.Year.ToString(), Value = (double)totalBalance, Color = color, UserData = this.reportDate });
                    chartData.AddSeries(chartSeries);
                    historicalChart.Data = chartData;
                    historicalChart.ToolTipGenerator = this.GenerateBarChartTips;

                    this.historicalChart = historicalChart;
                    writer.WriteElement(historicalChart);
                    _ = Task.Run(() => this.PopulateHistoricalNetWorth(this.historicalChart, chartSeries));

                    if (hasNoneTypeTaxDeferred || hasNoneTypeTaxFree || hasNoneType)
                    {
                        writer.WriteParagraph("(*) One ore more of your securities has no SecurityType, you can fix this using View/Securities",
                            System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Maroon);
                    }

                    this.WriteTrailer(writer, this.reportDate);
                }
                finally
                {
                    this.generating = false;
                }
            }
        }

        private void OnBarChartColumnClicked(object sender, ChartDataValue e)
        {
            this.reportDate = (DateTime)e.UserData;
            this.Regenerate();
        }

        private UIElement GenerateBarChartTips(ChartDataValue value)
        {
            var tip = new StackPanel() { Orientation = Orientation.Vertical };
            var date = (DateTime)value.UserData;
            tip.Children.Add(new TextBlock() { Text = date.ToShortDateString(), FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = value.Value.ToString("C0") });
            return tip;
        }

        private void ExportHistoryClick(object sender, RoutedEventArgs e)
        {
            if (this.historicalChart != null)
            {
                var series = this.historicalChart.Data.Series.FirstOrDefault();
                SaveFileDialog sd = new SaveFileDialog();
                string filter = Properties.Resources.CsvFileFilter;
                sd.Filter = filter;

                if (sd.ShowDialog(App.Current.MainWindow) == true)
                {
                    using (var file = File.CreateText(sd.FileName))
                    {
                        file.WriteLine("Year,Value");
                        foreach (var value in series.Values)
                        {
                            var v = value.Value.ToString("0");
                            file.WriteLine($"{value.Label},{v}");
                        }
                    }
                }
            }
        }

        private async void PopulateHistoricalNetWorth(AnimatingBarChart chart, ChartDataSeries series)
        {
            var color = series.Values[0].Color;
            Transaction first = this.myMoney.Transactions.GetAllTransactionsByDate().FirstOrDefault();
            if (first == null)
            {
                return;
            }

            SortedDictionary<DateTime, decimal> cashBalances = new SortedDictionary<DateTime, decimal>();
            for (DateTime date = this.reportDate.AddYears(-1); date > first.Date; date = date.AddYears(-1))
            {
                cashBalances.Add(date, 0);
            }

            Predicate<Account> notLoans = (a) => a.Type != AccountType.Loan;
            this.myMoney.GetCashBalanceNormalizedBatch(cashBalances, notLoans);

            for (DateTime date = this.reportDate.AddYears(-1); date > first.Date; date = date.AddYears(-1))
            {
                if (this.historicalChart != chart)
                {
                    break;
                }
                decimal cashBalance = cashBalances[date];
                decimal loanBalance = this.GetTotalLoansBalance(date);
                var portfolioBalance = await this.CalculatePortfolioBalance(date);
                decimal portfolio = 0;
                if (portfolioBalance.HasValue)
                {
                    portfolio = (decimal)portfolioBalance.Value;
                }
                var networth = cashBalance + loanBalance + portfolio;
                Debug.WriteLine($"Networth on {date.ToShortDateString()} is {networth:C0}");
                lock (series.Values)
                {
                    series.Values.Add(new ChartDataValue() { Label = date.Year.ToString(), Value = (double)networth, Color = color, UserData=date });
                }
                historicalChart.OnDelayedUpdate();
            }
        }

        internal async Task<decimal?> CalculatePortfolioBalance(DateTime date)
        {
            CostBasisCalculator calc = new CostBasisCalculator(this.myMoney, date);
            decimal? total = null;
            foreach (var accountHolding in calc.GetAccountHoldings())
            {
                var pending = accountHolding.GetPendingSales().Count();
                if (pending > 0)
                {
                    // todo: how to handle pending sales that have not cleared by the given date?
                    Debug.WriteLine($"Found {pending} pending sales for {accountHolding.Account.Name} on {date.ToShortDateString()}");
                }
                foreach (var holding in accountHolding.GetHoldings())
                {
                    var price = await this.cache.GetSecurityMarketPrice(date, holding.Security);
                    if (total == null)
                    {
                        total = 0;
                    }
                    total += holding.FuturesFactor * holding.UnitsRemaining * price;
                }
            }
            return total;
        }

        private decimal WriteAssetAccountRows(IReportWriter writer, IList<ChartDataValue> data)
        {
            decimal totalBalance = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(false))
            {
                if (a.Type == AccountType.Asset)
                {
                    var balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (x) => x == a);
                    if (balance != 0)
                    {
                        var color = this.GetRandomColor();
                        data.Add(new ChartDataValue() { Label = a.Name, Value = (double)balance.RoundToNearestCent(), Color = color });
                        this.WriteRow(writer, color, a.Name, balance, null);
                        totalBalance += balance;
                    }
                }
            }
            return totalBalance;
        }

        private decimal WriteLoanAccountRows(IReportWriter writer, IList<ChartDataValue> data, Color color, bool liabilities)
        {
            decimal totalBalance = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(false))
            {
                if (a.Type == AccountType.Loan)
                {
                    var loan = this.myMoney.GetOrCreateLoanAccount(a);
                    if (loan != null && loan.IsLiability == liabilities)
                    {
                        decimal balance = loan.ComputeLoanAccountBalance(this.reportDate);
                        if (balance != 0)
                        {
                            color = this.GetRandomColor();
                            data.Add(new ChartDataValue() { Label = a.Name, Value = (double)Math.Abs(balance).RoundToNearestCent(), Color = color });
                            this.WriteRow(writer, color, a.Name, balance, null);
                            totalBalance += balance;
                        }
                    }
                }
            }
            return totalBalance;
        }

        private decimal GetTotalLoansBalance(DateTime date)
        {
            decimal balance = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(false))
            {
                if (a.Type == AccountType.Loan)
                {
                    var loan = this.myMoney.GetOrCreateLoanAccount(a);
                    if (loan != null)
                    {
                        balance += loan.ComputeLoanAccountBalance(date);
                    }
                }
            }
            return balance;
        }

        private void Picker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.generating)
            {
                DatePicker picker = (DatePicker)sender;
                if (picker.SelectedDate.HasValue)
                {
                    this.reportDate = picker.SelectedDate.Value.Date;
                    this.Regenerate();
                }
            }
        }

        private async void Regenerate()
        {
            await this.view.Generate(this);
        }

        private void OnPieSliceClicked(object sender, ChartDataValue e)
        {
            if (e.UserData is SecurityGroup g)
            {
                this.OnSecurityGroupSelected(g);
            }
            else if (e.UserData is AccountGroup a)
            {
                this.OnSelectCashGroup(a);
            }
        }

        private void OnSecurityGroupSelected(SecurityGroup g)
        {
            if (SecurityDrillDown != null)
            {
                // now we can drill down and show a report on just this group of investments.
                SecurityDrillDown(this, g);
            }
        }

        private void OnSelectCashGroup(AccountGroup g)
        {
            if (CashBalanceDrillDown != null)
            {
                // now we can drill down and show a account cash balances.
                CashBalanceDrillDown(this, g);
            }
        }


        private UIElement OnGenerateToolTip(ChartDataValue value)
        {
            var tip = new StackPanel() { Orientation = Orientation.Vertical };
            tip.Children.Add(new TextBlock() { Text = value.Label, FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = value.Value.ToString("C0") });
            return tip;
        }


        private async Task<Tuple<decimal, bool>> WriteSecurities(IReportWriter writer, IList<ChartDataValue> data, TaxStatus status, Predicate<Account> filter)
        {
            bool hasNoneType = false;
            decimal balance = 0;
            Dictionary<SecurityType, decimal> byType = new Dictionary<SecurityType, decimal>();
            Dictionary<SecurityType, SecurityGroup> groupsByType = new Dictionary<SecurityType, SecurityGroup>();

            string prefix = "";
            switch (status)
            {
                case TaxStatus.TaxDeferred:
                    prefix = "Tax Deferred";
                    break;
                case TaxStatus.TaxFree:
                    prefix = "Tax Free";
                    break;
                default:
                    break;
            }

            CostBasisCalculator calc = new CostBasisCalculator(this.myMoney, this.reportDate);

            // compute summary
            foreach (var securityTypeGroup in calc.GetHoldingsBySecurityType(filter))
            {
                securityTypeGroup.TaxStatus = status; // inherited from the account.
                securityTypeGroup.Filter = filter;
                SecurityType stype = securityTypeGroup.Type;
                decimal sb = 0;
                byType.TryGetValue(stype, out sb);

                foreach (SecurityPurchase sp in securityTypeGroup.Purchases)
                {
                    // load the Stock Quote history from the download log. 
                    var price = await this.cache.GetSecurityMarketPrice(this.reportDate, sp.Security);
                    sb += sp.FuturesFactor * sp.UnitsRemaining * price;
                }
                byType[stype] = sb;
                groupsByType[stype] = securityTypeGroup;
            }

            if (byType.Count > 0)
            {
                foreach (SecurityType st in new SecurityType[] {
                    SecurityType.Bond,
                    SecurityType.MutualFund,
                    SecurityType.Equity,
                    SecurityType.MoneyMarket,
                    SecurityType.ETF,
                    SecurityType.Reit,
                    SecurityType.Futures,
                    SecurityType.Private,
                    SecurityType.None })
                {
                    decimal sb = 0;
                    if (byType.TryGetValue(st, out sb))
                    {
                        var color = this.GetRandomColor();
                        string caption = Security.GetSecurityTypeCaption(st);
                        string tooltip = caption;
                        if (!string.IsNullOrEmpty(prefix))
                        {
                            tooltip = prefix + " " + tooltip;
                        }

                        SecurityGroup group = groupsByType[st];
                        data.Add(new ChartDataValue() { Label = tooltip, Value = (double)Math.Abs(sb).RoundToNearestCent(), Color = color, UserData = group });
                        if (st == SecurityType.None)
                        {
                            hasNoneType = true;
                            caption += "*";
                        }
                        this.WriteRow(writer, color, caption, sb, () => this.OnSecurityGroupSelected(group));
                        balance += sb;
                    }
                }
            }
            else
            {
                this.WriteRow(writer, this.GetRandomColor(), "N/A", 0, null);
            }
            return new Tuple<decimal, bool>(balance, hasNoneType);
        }

        private bool IsBankAccount(Account a)
        {
            if (a.Type == AccountType.Credit || // we'll show credit accounts as liabilities later.
                a.Type == AccountType.Asset ||
                a.Type == AccountType.Brokerage ||
                a.Type == AccountType.Retirement ||
                a.Type == AccountType.Loan)
            {
                return false;
            }
            return true;
        }

        private bool IsInvestmentAccount(Account a)
        {
            return a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement;
        }

        private static void WriteHeader(IReportWriter writer, string caption)
        {
            writer.StartHeaderRow();
            writer.StartCell(1, 3);
            writer.WriteParagraph(caption);
            writer.EndCell();
            writer.EndHeaderRow();
        }

        private void WriteRow(IReportWriter writer, Color color, string name, decimal balance, Action hyperlink)
        {
            writer.StartRow();

            writer.StartCell();
            writer.WriteElement(new Rectangle() { Width = 20, Height = 16, Fill = new SolidColorBrush(color) });
            writer.EndCell();
            writer.StartCell();
            if (hyperlink != null)
            {
                writer.WriteHyperlink(name, FontStyles.Normal, FontWeights.Normal, (s, e) => hyperlink());
            }
            else
            {
                writer.WriteParagraph(name);
            }
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(this.GetFormattedNormalizedAmount(balance));
            writer.EndCell();

            writer.EndRow();
        }

        private Color GetRandomColor()
        {
            return Color.FromRgb((byte)this.rand.Next(this.minRandColor, this.maxRandColor),
                (byte)this.rand.Next(this.minRandColor, this.maxRandColor),
                (byte)this.rand.Next(this.minRandColor, this.maxRandColor));
        }

    }

    public class AccountGroup
    {
        public DateTime Date { get; set; }
        public Predicate<Account> Filter { get; set; }
        public string Title { get; internal set; }
    }
}
