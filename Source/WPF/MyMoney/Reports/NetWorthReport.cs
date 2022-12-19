using LovettSoftware.Charts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Walkabout.Charts;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.StockQuotes;
using Walkabout.Utilities;
using Walkabout.Views;

namespace Walkabout.Reports
{

    //=========================================================================================
    public class NetWorthReport : Report
    {
        FlowDocumentView view;
        MyMoney myMoney;
        Random rand = new Random(Environment.TickCount);
        byte minRandColor, maxRandColor;
        DateTime reportDate;
        StockQuoteCache cache;
        bool generating;
        bool filterOutClosedAccounts = false;

        public event EventHandler<SecurityGroup> SecurityDrillDown;
        public event EventHandler<AccountGroup> CashBalanceDrillDown;

        public NetWorthReport(FlowDocumentView view, MyMoney money, StockQuoteCache cache)
        {
            this.view = view;
            this.myMoney = money;
            this.cache = cache;
            this.reportDate = DateTime.Now;
            minRandColor = 20;
            maxRandColor = ("" + AppTheme.Instance.GetTheme()).Contains("Dark") ? (byte)128 : (byte)200;
        }

        public override async Task Generate(IReportWriter writer)
        {
            generating = true;
            // the lock locks out any change to the cache from background downloading of stock quotes
            // while we are generating this report.
            using (var cacheLock = this.cache.BeginLock())
            {
                try
                {
                    FlowDocumentReportWriter fwriter = (FlowDocumentReportWriter)writer;
                    writer.WriteHeading("Net Worth Statement");
                    Paragraph heading = fwriter.CurrentParagraph;

                    DatePicker picker = new DatePicker();
                    // byYearCombo.SelectionChanged += OnYearChanged;
                    picker.Margin = new Thickness(10, 0, 0, 0);
                    picker.SelectedDate = this.reportDate;
                    picker.DisplayDate = this.reportDate;
                    picker.SelectedDateChanged += Picker_SelectedDateChanged;
                    heading.Inlines.Add(new InlineUIContainer(picker));

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

                    foreach (Account a in this.myMoney.Accounts.GetAccounts(this.filterOutClosedAccounts))
                    {
                        if (a.IsTaxDeferred) hasTaxDeferred = true;
                        if (a.IsTaxFree) hasTaxFree = true;
                    }

                    Predicate<Account> bankAccountFilter = (a) => { return IsBankAccount(a); };
                    decimal balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, bankAccountFilter);
                    var cashGroup = new AccountGroup() { Filter = bankAccountFilter, Date = this.reportDate, Title = "Bank Account" };

                    // Non-investment Cash
                    var color = GetRandomColor();
                    data.Add(new ChartDataValue() { Label = "Cash", Value = (double)balance.RoundToNearestCent(), Color = color, UserData = cashGroup });
                    WriteRow(writer, color, "Cash", balance, () => OnSelectCashGroup(cashGroup));
                    totalBalance += balance;

                    // Investment Cash
                    Predicate<Account> investmentAccountFilter = (a) => { return IsInvestmentAccount(a) && !a.IsTaxDeferred && !a.IsTaxFree; };
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, investmentAccountFilter);
                    if (balance != 0)
                    {
                        var investmentCashGroup = new AccountGroup { Filter = investmentAccountFilter, Date = this.reportDate, Title = "Investment Account" };
                        color = GetRandomColor();
                        data.Add(new ChartDataValue() { Label = "Investment Cash", Value = (double)balance.RoundToNearestCent(), Color = color, UserData = investmentCashGroup });
                        WriteRow(writer, color, "Investment Cash", balance, () => OnSelectCashGroup(investmentCashGroup));
                        totalBalance += balance;
                    }

                    // Tax-Deferred Cash
                    Predicate<Account> taxDeferredAccountFilter = (a) => { return IsInvestmentAccount(a) && a.IsTaxDeferred; };
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, taxDeferredAccountFilter);
                    if (balance != 0)
                    {
                        var taxDeferredCashGroup = new AccountGroup { Filter = taxDeferredAccountFilter, Date = this.reportDate, Title = "Tax-Deferred Account" };
                        color = GetRandomColor();
                        data.Add(new ChartDataValue() { Label = "Tax-Deferred Cash", Value = (double)balance.RoundToNearestCent(), Color = color, UserData = taxDeferredCashGroup });
                        WriteRow(writer, color, "Tax-Deferred Cash", balance, () => OnSelectCashGroup(taxDeferredCashGroup));
                        totalBalance += balance;
                    }

                    // Tax-Free Cash
                    Predicate<Account> taxFreeAccountFilter = (a) => { return IsInvestmentAccount(a) && a.IsTaxFree; };
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, taxFreeAccountFilter);
                    if (balance != 0)
                    {
                        var taxFreeCashGroup = new AccountGroup { Filter = taxFreeAccountFilter, Date = this.reportDate, Title = "Tax-Free Account" };
                        color = GetRandomColor();
                        data.Add(new ChartDataValue() { Label = "Tax-Free Cash", Value = (double)balance.RoundToNearestCent(), Color = color, UserData = taxFreeCashGroup });
                        WriteRow(writer, color, "Tax-Free Cash", balance, () => OnSelectCashGroup(taxFreeCashGroup));
                        totalBalance += balance;
                    }

                    bool hasNoneTypeTaxDeferred = false;
                    Tuple<decimal, bool> r = null;
                    if (hasTaxDeferred)
                    {
                        WriteHeader(writer, "Tax Deferred Assets");
                        r = await WriteSecurities(writer, data, TaxStatus.TaxDeferred, new Predicate<Account>((a) => { return IsInvestmentAccount(a) && a.IsTaxDeferred; }));
                        totalBalance += r.Item1;
                        hasNoneTypeTaxDeferred = r.Item2;
                    }

                    bool hasNoneTypeTaxFree = false;
                    if (hasTaxFree)
                    {
                        WriteHeader(writer, "Tax Free Assets");
                        r = await WriteSecurities(writer, data, TaxStatus.TaxFree, new Predicate<Account>((a) => { return IsInvestmentAccount(a) && a.IsTaxFree; }));
                        totalBalance += r.Item1;
                        hasNoneTypeTaxFree = r.Item2;
                    }

                    balance = 0;

                    WriteHeader(writer, "Taxable Assets");

                    totalBalance += WriteLoanAccountRows(writer, data, color, false);
                    totalBalance += WriteAssetAccountRows(writer, data);

                    r = await WriteSecurities(writer, data, TaxStatus.Taxable, new Predicate<Account>((a) => { return IsInvestmentAccount(a) && !a.IsTaxDeferred && !a.IsTaxFree; }));
                    totalBalance += r.Item1;
                    bool hasNoneType = r.Item2;

                    // liabilities are not included in the pie chart because that would be confusing.
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (a) => a.Type == AccountType.Credit);
                    WriteHeader(writer, "Liabilities");

                    Predicate<Account> creditAccountFilter = (a) => a.Type == AccountType.Credit;
                    balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, creditAccountFilter);
                    totalBalance += balance;
                    var creditGroup = new AccountGroup() { Filter = creditAccountFilter, Date = this.reportDate, Title = "Credit Accounts" };
                    color = GetRandomColor();
                    WriteRow(writer, color, "Credit", balance, () => OnSelectCashGroup(creditGroup));
                    totalBalance += WriteLoanAccountRows(writer, data, color, true);

                    writer.StartFooterRow();

                    writer.StartCell(1, 2);
                    writer.WriteParagraph("Total");
                    writer.EndCell();

                    writer.StartCell();
                    writer.WriteNumber(totalBalance.ToString("C"));
                    writer.EndCell();

                    writer.EndRow();
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
                    chart.ToolTipGenerator = OnGenerateToolTip;
                    chart.PieSliceClicked += OnPieSliceClicked;

                    writer.WriteElement(chart);

                    writer.EndCell();
                    writer.EndRow();
                    writer.EndTable();

                    if (hasNoneTypeTaxDeferred || hasNoneTypeTaxFree || hasNoneType)
                    {
                        writer.WriteParagraph("(*) One ore more of your securities has no SecurityType, you can fix this using View/Securities",
                            System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Maroon);
                    }

                    writer.WriteParagraph("Generated for " + this.reportDate.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);
                }
                finally
                {
                    generating = false;
                }
            }
        }

        private decimal WriteAssetAccountRows(IReportWriter writer, IList<ChartDataValue> data)
        {
            decimal totalBalance = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(this.filterOutClosedAccounts))
            {
                if (a.Type == AccountType.Asset)
                {
                    var balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (x) => x == a);
                    if (balance != 0)
                    {
                        var color = GetRandomColor();
                        data.Add(new ChartDataValue() { Label = a.Name, Value = (double)balance.RoundToNearestCent(), Color = color });
                        WriteRow(writer, color, a.Name, balance, null);
                        totalBalance += balance;
                    }
                }
            }
            return totalBalance;
        }

        private decimal WriteLoanAccountRows(IReportWriter writer, IList<ChartDataValue> data, Color color, bool liabilities)
        {
            decimal totalBalance = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(this.filterOutClosedAccounts))
            {
                if (a.Type == AccountType.Loan)
                {
                    var loan = this.myMoney.GetOrCreateLoanAccount(a);
                    if (loan != null && loan.IsLiability == liabilities)
                    {
                        decimal balance = loan.ComputeLoanAccountBalance(this.reportDate);
                        if (balance != 0)
                        {
                            color = GetRandomColor();
                            data.Add(new ChartDataValue() { Label = a.Name, Value = (double)Math.Abs(balance).RoundToNearestCent(), Color = color });
                            WriteRow(writer, color, a.Name, balance, null);
                            totalBalance += balance;
                        }
                    }
                }
            }
            return totalBalance;
        }

        private void Picker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!generating)
            {
                DatePicker picker = (DatePicker)sender;
                if (picker.SelectedDate.HasValue)
                {
                    this.reportDate = picker.SelectedDate.Value;
                    this.filterOutClosedAccounts = (this.reportDate >= DateTime.Today);
                    _ = view.Generate(this);
                }
            }
        }

        private void OnPieSliceClicked(object sender, ChartDataValue e)
        {
            if (e.UserData is SecurityGroup g)
            {
                OnSecurityGroupSelected(g);
            }
            else if (e.UserData is AccountGroup a)
            {
                OnSelectCashGroup(a);
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
                    await this.cache.LoadHistory(sp.Security);
                    sb += sp.FuturesFactor * sp.UnitsRemaining * this.cache.GetSecurityMarketPrice(this.reportDate, sp.Security);
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
                        var color = GetRandomColor();
                        string caption = Security.GetSecurityTypeCaption(st);
                        string tooltip = caption;
                        if (!string.IsNullOrEmpty(prefix)) tooltip = prefix + " " + tooltip;
                        SecurityGroup group = groupsByType[st];
                        data.Add(new ChartDataValue() { Label = tooltip, Value = (double)Math.Abs(sb).RoundToNearestCent(), Color = color, UserData = group });
                        if (st == SecurityType.None)
                        {
                            hasNoneType = true;
                            caption += "*";
                        }
                        WriteRow(writer, color, caption, sb, () => OnSecurityGroupSelected(group));
                        balance += sb;
                    }
                }
            }
            else
            {
                WriteRow(writer, GetRandomColor(), "N/A", 0, null);
            }
            return new Tuple<decimal, bool>(balance, hasNoneType);
        }

        bool IsBankAccount(Account a)
        {
            if (a.Type == AccountType.Credit || // we'll show credit accounts as liabilities later.
                a.Type == AccountType.Asset ||
                a.Type == AccountType.Brokerage ||
                a.Type == AccountType.Retirement ||
                a.Type == AccountType.CategoryFund ||
                a.Type == AccountType.Loan)
            {
                return false;
            }
            return true;
        }

        bool IsInvestmentAccount(Account a)
        {
            return a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement;
        }

        private static void WriteHeader(IReportWriter writer, string caption)
        {
            writer.StartHeaderRow();
            writer.StartCell(1, 3);
            writer.WriteParagraph(caption);
            writer.EndCell();
            writer.EndRow();
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
            writer.WriteNumber(balance.ToString("C"));
            writer.EndCell();

            writer.EndRow();
        }

        private Color GetRandomColor()
        {
            return Color.FromRgb((byte)rand.Next(minRandColor, maxRandColor),
                (byte)rand.Next(minRandColor, maxRandColor),
                (byte)rand.Next(minRandColor, maxRandColor));
        }

    }


    public class AccountGroup
    {
        public DateTime Date { get; set; }
        public Predicate<Account> Filter { get; set; }
        public string Title { get; internal set; }
    }
}
