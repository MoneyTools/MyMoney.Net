using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Walkabout.Interfaces.Reports;
using Walkabout.Data;
using System.Windows;
using System.Windows.Data;
using Walkabout.Taxes;
using System.Windows.Controls;
using LovettSoftware.Charts;
using System.Windows.Media;
using System.Windows.Shapes;
using Walkabout.Charts;

namespace Walkabout.Reports
{

    //=========================================================================================
    public class NetWorthReport : IReport
    {
        MyMoney myMoney;
        Random rand = new Random(Environment.TickCount);

        public event EventHandler<SecurityGroup> DrillDown;

        public NetWorthReport(MyMoney money)
        {
            this.myMoney = money;
        }

        public void Generate(IReportWriter writer)
        {
            writer.WriteHeading("Net Worth Statement");

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
            foreach (double width in new double[] { 300, 100 })
            {
                writer.WriteColumnDefinition(width.ToString(), width, width);
            }
            writer.EndColumnDefinitions();

            WriteHeader(writer, "Liquid Assets");

            decimal totalBalance = 0;
            decimal balance = 0;
            bool hasTaxDeferred = false;
            bool hasRetirement = false;

            Transactions transactions = myMoney.Transactions;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if (a.Type == AccountType.Retirement)
                {
                    hasRetirement = true;
                    continue;
                }
                if (a.IsTaxDeferred) hasTaxDeferred = true;
                if (a.Type == AccountType.Credit || 
                    a.Type == AccountType.Asset ||                     
                    a.Type == AccountType.Brokerage || 
                    a.Type == AccountType.CategoryFund || 
                    a.Type == AccountType.Loan)
                {
                    continue;
                }

                balance += a.BalanceNormalized;
            }

            var color = GetRandomColor();
            if (balance > 0) data.Add(new ChartDataValue() { Label = "Cash", Value = (double)balance, Color = color });
            WriteRow(writer, color, "Cash", balance);

            totalBalance += balance;
            balance = this.myMoney.GetInvestmentCashBalance(new Predicate<Account>((a) => { return !a.IsClosed && (a.Type == AccountType.Brokerage); }));

            color = GetRandomColor();
            if (balance > 0) data.Add(new ChartDataValue() { Label = "Investment Cash", Value = (double)balance, Color = color });
            WriteRow(writer, color, "Investment Cash", balance);
            totalBalance += balance;

            bool hasNoneRetirement = false;
            if (hasRetirement)
            {
                WriteHeader(writer, "Retirement Assets");
                balance = this.myMoney.GetInvestmentCashBalance(new Predicate<Account>((a) => { return !a.IsClosed && a.Type == AccountType.Retirement; }));
                color = GetRandomColor();
                if (balance > 0) data.Add(new ChartDataValue() { Label = "Retirement Cash", Value = (double)balance, Color = color });
                WriteRow(writer, color, "Retirement Cash", balance);
                totalBalance += balance;

                totalBalance += WriteSecurities(writer, data, "Retirement ", new Predicate<Account>((a) => { return a.Type == AccountType.Retirement; }), out hasNoneRetirement);
            }

            bool hasNoneTypeTaxDeferred = false;
            if (hasTaxDeferred)
            {
                WriteHeader(writer, "Tax Deferred Assets");
                totalBalance += WriteSecurities(writer, data, "Tax Deferred ", new Predicate<Account>((a) => { return a.Type == AccountType.Brokerage && a.IsTaxDeferred; }), out hasNoneTypeTaxDeferred);
            }

            balance = 0;

            WriteHeader(writer, "Long Term Assets");

            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if ((a.Type == AccountType.Loan || a.Type == AccountType.Asset) && a.Balance > 0) // then this is a loan out to someone else...
                {
                    color = GetRandomColor();
                    if (a.BalanceNormalized > 0) data.Add(new ChartDataValue() { Label = a.Name, Value = (double)a.BalanceNormalized, Color = color });
                    WriteRow(writer, color, a.Name, a.BalanceNormalized);
                    totalBalance += a.BalanceNormalized;
                }
            }

            bool hasNoneType = false;
            totalBalance += WriteSecurities(writer, data, "", new Predicate<Account>((a) => { return a.Type == AccountType.Brokerage && !a.IsTaxDeferred; }), out hasNoneType);

            balance = 0;
            WriteHeader(writer, "Liabilities");

            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if (a.Type != AccountType.Credit)
                {
                    continue;
                }
                balance += a.BalanceNormalized;
            }
            totalBalance += balance;

            color = GetRandomColor();
            if (balance > 0) data.Add(new ChartDataValue() { Label = "Credit", Value = (double)balance, Color = color });
            WriteRow(writer, color, "Credit", balance);
            balance = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if (a.Type == AccountType.Loan && a.BalanceNormalized < 0)
                {
                    balance += a.BalanceNormalized;
                    color = GetRandomColor();
                    if (a.BalanceNormalized > 0) data.Add(new ChartDataValue() { Label = a.Name, Value = (double)a.BalanceNormalized, Color = color });
                    WriteRow(writer, color, a.Name, a.BalanceNormalized);
                }
            }
            totalBalance += balance;

            writer.StartFooterRow();

            writer.StartCell(1,2);
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

            if (hasNoneRetirement || hasNoneTypeTaxDeferred || hasNoneType)
            {
                writer.WriteParagraph("(*) One ore more of your securities has no SecurityType, you can fix this using View/Securities", 
                    System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Maroon);
            }

            writer.WriteParagraph("Generated on " + DateTime.Today.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);
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

        private decimal WriteSecurities(IReportWriter writer, IList<ChartDataValue> data, string prefix, Predicate<Account> filter, out bool hasNoneType)
        {
            hasNoneType = false;
            decimal balance = 0;
            Dictionary<SecurityType, decimal> byType = new Dictionary<SecurityType, decimal>();
            Dictionary<SecurityType, SecurityGroup> groupsByType = new Dictionary<SecurityType, SecurityGroup>();

            CostBasisCalculator calc = new CostBasisCalculator(this.myMoney, DateTime.Now);

            // compute summary
            foreach (var securityTypeGroup in calc.GetHoldingsBySecurityType(filter))
            {
                SecurityType stype = securityTypeGroup.Type;
                decimal sb = 0;
                byType.TryGetValue(stype, out sb);

                foreach (SecurityPurchase sp in securityTypeGroup.Purchases)
                {                    
                    sb += sp.MarketValue;
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
                        string caption = prefix + Security.GetSecurityTypeCaption(st);
                        if (sb > 0)
                        {
                            data.Add(new ChartDataValue() { Label = caption, Value = (double)sb, Color = color, UserData = groupsByType[st] });
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
            return balance;
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

        public void Export(string filename)
        {
            throw new NotImplementedException();
        }

        private Color GetRandomColor()
        {
            return Color.FromRgb((byte)rand.Next(80, 200), (byte)rand.Next(80, 200), (byte)rand.Next(80, 200));
        }

    }

}
