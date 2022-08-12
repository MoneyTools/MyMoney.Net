using LovettSoftware.Charts;
using System;
using System.Collections.Generic;
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
        DownloadLog log;
        Dictionary<Security, decimal> cachedPrices = new Dictionary<Security, decimal>();
        IDictionary<Security, List<Investment>> transactionsBySecurity;
        bool generating;

        public event EventHandler<SecurityGroup> DrillDown;

        public NetWorthReport(FlowDocumentView view, MyMoney money, DownloadLog log)
        {
            this.view = view;
            this.myMoney = money;
            this.log = log;
            this.reportDate = DateTime.Today;
            minRandColor = 20;
            maxRandColor = (""+AppTheme.Instance.GetTheme()).Contains("Dark") ? (byte)128: (byte)200;
        }

        public override async Task Generate(IReportWriter writer)
        {
            generating = true;
            try
            {
                FlowDocumentReportWriter fwriter = (FlowDocumentReportWriter)writer;
                writer.WriteHeading("Net Worth Statement");
                Paragraph heading = fwriter.CurrentParagraph;

                this.transactionsBySecurity = this.myMoney.GetTransactionsGroupedBySecurity((a) => true, this.reportDate.AddDays(1));

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

                foreach (Account a in this.myMoney.Accounts.GetAccounts(false))
                {
                    if (a.IsTaxDeferred) hasTaxDeferred = true;
                    if (a.IsTaxFree) hasTaxFree = true;
                }

                decimal balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (a) => { return IsBankAccount(a); });

                // Non-investment Cash
                var color = GetRandomColor();
                if (balance > 0) data.Add(new ChartDataValue() { Label = "Cash", Value = (double)balance, Color = color });
                WriteRow(writer, color, "Cash", balance);
                totalBalance += balance;

                // Investment Cash
                balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (a) => { return IsInvestmentAccount(a); });
                color = GetRandomColor();
                data.Add(new ChartDataValue() { Label = "Investment Cash", Value = (double)balance, Color = color });
                WriteRow(writer, color, "Investment Cash", balance);
                totalBalance += balance;

                bool hasNoneTypeTaxDeferred = false;
                Tuple<decimal, bool> r = null;
                if (hasTaxDeferred)
                {
                    WriteHeader(writer, "Tax Deferred Assets");
                    r = await WriteSecurities(writer, data, "Tax Deferred ", new Predicate<Account>((a) => { return a.IsTaxDeferred; }));
                    totalBalance += r.Item1;
                    hasNoneTypeTaxDeferred = r.Item2;
                }

                bool hasNoneTypeTaxFree = false;
                if (hasTaxFree)
                {
                    WriteHeader(writer, "Tax Free Assets");
                    r = await WriteSecurities(writer, data, "Tax Free ", new Predicate<Account>((a) => { return a.IsTaxFree; }));
                    totalBalance += r.Item1;
                    hasNoneTypeTaxFree = r.Item2;
                }

                balance = 0;

                WriteHeader(writer, "Other Assets");

                foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
                {
                    if ((a.Type == AccountType.Loan || a.Type == AccountType.Asset) && a.Balance >= 0) // then this is a loan out to someone else...(so an asset)
                    {
                        color = GetRandomColor();
                        balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (x) => x == a);
                        if (balance > 0) data.Add(new ChartDataValue() { Label = a.Name, Value = (double)balance, Color = color });
                        WriteRow(writer, color, a.Name, balance);
                        totalBalance += balance;
                    }
                }

                r = await WriteSecurities(writer, data, "", new Predicate<Account>((a) => { return IsInvestmentAccount(a) && !a.IsTaxDeferred && !a.IsTaxFree; }));
                totalBalance += r.Item1;
                bool hasNoneType = r.Item2;

                // liabilities are not included in the pie chart because that would be confusing.
                balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (a) => a.Type == AccountType.Credit);
                WriteHeader(writer, "Liabilities");
                totalBalance += balance;

                color = GetRandomColor();
                WriteRow(writer, color, "Credit", balance);
                balance = 0;
                foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
                {
                    if (a.Type == AccountType.Loan && a.BalanceNormalized < 0) // loan we owe, so a liability!
                    {
                        balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (x) => x == a);
                        color = GetRandomColor();
                        WriteRow(writer, color, a.Name, balance);
                        totalBalance += balance;
                    }
                }

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
                chart.BorderThickness = new Thickness(0);
                chart.VerticalAlignment = VerticalAlignment.Top;
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

        private void Picker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!generating)
            {
                DatePicker picker = (DatePicker)sender;
                if (picker.SelectedDate.HasValue)
                {
                    this.cachedPrices = new Dictionary<Security, decimal>();
                    this.reportDate = picker.SelectedDate.Value;
                    _ = view.Generate(this);
                }
            }
        }

        private void OnPieSliceClicked(object sender, ChartDataValue e)
        {
            if (e.UserData is SecurityGroup g && DrillDown != null)
            {
                // now we can drill down and show a report on just this group of investments.
                DrillDown(this, g);
            }
        }

        private UIElement OnGenerateToolTip(ChartDataValue value)
        {
            var tip = new StackPanel() { Orientation = Orientation.Vertical };
            tip.Children.Add(new TextBlock() { Text = value.Label, FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = value.Value.ToString("C0") });
            return tip;
        }

        private async Task<decimal> GetSecurityPrice(DateTime date, Security s)
        {
            // return the closing price of the given security for this date.
            if (date.Date == DateTime.Today.Date)
            {
                return s.Price;
            }

            if (this.cachedPrices.TryGetValue(s, out decimal price))
            {
                return price;
            }

            // find the price in the download log if we have one.
            var symbol = s.Symbol;
            if (!string.IsNullOrEmpty(symbol))
            {
                var history = await this.log.GetHistory(symbol);
                if (history != null && history.History != null && history.History.Count > 0)
                {
                    foreach (var item in history.History)
                    {
                        if (item.Date > date)
                        {
                            break;
                        }
                        price = item.Close;
                    }
                }
            }

            // hmmm, then we have to search our own transactions for a recorded UnitPrice.
            if (price == 0 && transactionsBySecurity.TryGetValue(s, out List<Investment> trades) && trades != null)
            {
                price = 0;
                foreach (var t in trades)
                {
                    if (t.Date > date)
                    {
                        break;
                    }
                    if (t.UnitPrice != 0)
                    {
                        price = t.UnitPrice;
                    }
                }
            }

            if (price != 0)
            {
                this.cachedPrices[s] = price;
            }
            return price;
        }


        private async Task<Tuple<decimal, bool>> WriteSecurities(IReportWriter writer, IList<ChartDataValue> data, string prefix, Predicate<Account> filter)
        {
            bool hasNoneType = false;
            decimal balance = 0;
            Dictionary<SecurityType, decimal> byType = new Dictionary<SecurityType, decimal>();
            Dictionary<SecurityType, SecurityGroup> groupsByType = new Dictionary<SecurityType, SecurityGroup>();

            CostBasisCalculator calc = new CostBasisCalculator(this.myMoney, this.reportDate);

            // compute summary
            foreach (var securityTypeGroup in calc.GetHoldingsBySecurityType(filter))
            {
                SecurityType stype = securityTypeGroup.Type;
                decimal sb = 0;
                byType.TryGetValue(stype, out sb);

                foreach (SecurityPurchase sp in securityTypeGroup.Purchases)
                {                    
                    sb += sp.UnitsRemaining * await GetSecurityPrice(this.reportDate, sp.Security);
                }
                byType[stype] = sb;
                groupsByType[stype] = securityTypeGroup;
            }

            if (byType.Count > 0)
            {
                foreach (SecurityType st in new SecurityType[] { SecurityType.Bond,
                    SecurityType.MutualFund, SecurityType.Equity, SecurityType.MoneyMarket, SecurityType.ETF, SecurityType.Reit, SecurityType.Futures,
                    SecurityType.Private, SecurityType.None })
                {
                    decimal sb = 0;
                    if (byType.TryGetValue(st, out sb))
                    {
                        var color = GetRandomColor();
                        string caption = Security.GetSecurityTypeCaption(st);
                        if (sb > 0)
                        {
                            string tooltip = caption;
                            if (!string.IsNullOrEmpty(prefix)) tooltip = prefix + " " + tooltip;
                            data.Add(new ChartDataValue() { Label = tooltip, Value = (double)sb, Color = color, UserData = groupsByType[st] });
                            if (st == SecurityType.None)
                            {
                                hasNoneType = true;
                                caption += "*";
                            }
                        }
                        WriteRow(writer, color, caption, sb);
                        balance += sb;
                    }
                }
            }
            else
            {
                WriteRow(writer, GetRandomColor(), "N/A", 0);
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

        private static void WriteRow(IReportWriter writer, Color color, string name, decimal balance)
        {
            writer.StartRow();

            writer.StartCell();
            writer.WriteElement(new Rectangle() { Width = 20, Height = 16, Fill = new SolidColorBrush(color) });
            writer.EndCell();
            writer.StartCell();
            writer.WriteParagraph(name);
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

}
