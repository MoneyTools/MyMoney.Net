using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Walkabout.Interfaces.Reports;
using Walkabout.Data;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows;
using System.Windows.Data;
using Walkabout.Taxes;
using System.Windows.Controls;

namespace Walkabout.Reports
{

    //=========================================================================================
    public class NetWorthReport : IReport
    {
        MyMoney myMoney;

        public NetWorthReport(MyMoney money)
        {
            this.myMoney = money;
        }

        class PieData
        {
            public decimal Total { get; set; }
            public string Name { get; set; }
        }

        public void Generate(IReportWriter writer)
        {
            writer.WriteHeading("Net Worth Statement");

            List<PieData> data = new List<PieData>();

            // outer table contains 2 columns, left is the summary table, right is the pie chart.
            writer.StartTable();
            writer.StartColumnDefinitions();
            writer.WriteColumnDefinition("420", 420, 420);
            writer.WriteColumnDefinition("620", 620, 620);
            writer.EndColumnDefinitions();
            writer.StartRow();
            writer.StartCell();
    
            // inner table contains the "data"
            writer.StartTable();
            writer.StartColumnDefinitions();
            foreach (double width in new double[] { 300, 100 })
            {
                writer.WriteColumnDefinition(width.ToString(), width, width);
            }
            writer.EndColumnDefinitions();

            WriteHeader(writer, "Liquid Assets");

            decimal totalBalance = 0;
            decimal balance = 0;
            bool hasTaxDeferred = false;

            Transactions transactions = myMoney.Transactions;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if (a.IsTaxDeferred) hasTaxDeferred = true;
                if (a.Type == AccountType.Credit || a.Type == AccountType.Investment || a.Type == AccountType.CategoryFund || a.Type == AccountType.Loan)
                {
                    continue;
                }
                else if (a.Type != AccountType.Asset)
                {
                    balance += a.BalanceNormalized;
                }
            }

            if (balance > 0) data.Add(new PieData() { Name = "Cash", Total = balance });
            WriteRow(writer, "Cash", balance);

            totalBalance += balance;
            balance = this.myMoney.GetInvestmentCashBalance(null);
            
            if (balance > 0) data.Add(new PieData() { Name = "Investment Cash", Total = balance });
            WriteRow(writer, "Investment Cash", balance);
            totalBalance += balance;

            balance = 0;

            bool hasNoneTypeTaxDeferred = false;
            if (hasTaxDeferred)
            {
                WriteHeader(writer, "Tax Deferred Assets");
                totalBalance += WriteSecurities(writer, data, "Tax Deferred ", new Predicate<Account>((a) => { return a.Type == AccountType.Investment && a.IsTaxDeferred; }), out hasNoneTypeTaxDeferred);
            }

            WriteHeader(writer, "Long Term Assets");

            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if ((a.Type == AccountType.Loan || a.Type == AccountType.Asset) && a.Balance > 0) // then this is a loan out to someone else...
                {
                    if (a.BalanceNormalized > 0) data.Add(new PieData() { Name = a.Name, Total = a.BalanceNormalized });
                    WriteRow(writer, a.Name, a.BalanceNormalized);
                    totalBalance += a.BalanceNormalized;
                }
            }

            bool hasNoneType = false;
            totalBalance += WriteSecurities(writer, data, "", new Predicate<Account>((a) => { return a.Type == AccountType.Investment && !a.IsTaxDeferred; }), out hasNoneType);

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

            if (balance > 0) data.Add(new PieData() { Name = "Credit", Total = balance });
            WriteRow(writer, "Credit", balance);
            balance = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if (a.Type == AccountType.Loan && a.BalanceNormalized < 0)
                {
                    balance += a.BalanceNormalized;
                    if (a.BalanceNormalized > 0) data.Add(new PieData() { Name = a.Name, Total = a.BalanceNormalized });
                    WriteRow(writer, a.Name, a.BalanceNormalized);
                }
            }
            totalBalance += balance;

            writer.StartFooterRow();

            writer.StartCell();
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
            Chart chart = new Chart();
            chart.MinWidth = 600;
            chart.MinHeight = 400;
            chart.BorderThickness = new Thickness(0);
            chart.VerticalAlignment = VerticalAlignment.Top;

            PieSeries series = new PieSeries();
            series.IndependentValueBinding = new Binding("Name");
            series.DependentValueBinding = new Binding("Total");
            chart.Series.Add(series);
            series.ItemsSource = data;

            writer.WriteElement(chart);

            writer.EndCell();
            writer.EndRow();
            writer.EndTable();

            if (hasNoneTypeTaxDeferred || hasNoneType)
            {
                writer.WriteParagraph("(*) One ore more of your securities has no SecurityType, you can fix this using View/Securities", 
                    System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Maroon);
            }

            writer.WriteParagraph("Generated on " + DateTime.Today.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);
        }

        private decimal WriteSecurities(IReportWriter writer, List<PieData> data, string prefix, Predicate<Account> filter, out bool hasNoneType)
        {
            hasNoneType = false;
            decimal balance = 0;
            Dictionary<SecurityType, decimal> byType = new Dictionary<SecurityType, decimal>();

            CostBasisCalculator calc = new CostBasisCalculator(this.myMoney, DateTime.Now);

            // compute summary
            foreach (var securityTypeGroup in calc.GetHoldingsBySecurityType(filter))
            {
                SecurityType stype = securityTypeGroup.Key;
                decimal sb = 0;
                byType.TryGetValue(stype, out sb);

                foreach (SecurityPurchase sp in securityTypeGroup.Value)
                {                    
                    sb += sp.MarketValue;
                }
                byType[stype] = sb;
            }

            if (byType.Count > 0)
            {
                foreach (SecurityType st in new SecurityType[] { SecurityType.Bond,
                    SecurityType.MutualFund, SecurityType.Equity, SecurityType.MoneyMarket, SecurityType.ETF, SecurityType.Reit, SecurityType.Futures,
                    SecurityType.None })
                {
                    decimal sb = 0;
                    if (byType.TryGetValue(st, out sb))
                    {
                        string caption = prefix + Security.GetSecurityTypeCaption(st);
                        if (sb > 0)
                        {
                            data.Add(new PieData() { Name = caption, Total = sb });
                            if (st == SecurityType.None)
                            {
                                hasNoneType = true;
                                caption += "*";
                            }
                        }
                        WriteRow(writer, caption, sb);
                        balance += sb;
                    }
                }
            }
            else
            {
                WriteRow(writer, "N/A", 0);
            }
            return balance;
        }

        private static void WriteHeader(IReportWriter writer, string caption)
        {
            writer.StartHeaderRow();
            writer.StartCell(1, 2);
            writer.WriteParagraph(caption);
            writer.EndCell();
            writer.EndRow();
        }

        private static void WriteRow(IReportWriter writer, string name, decimal balance)
        {
            writer.StartRow();
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
    }

}
