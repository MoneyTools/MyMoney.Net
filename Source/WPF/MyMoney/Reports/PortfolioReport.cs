using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Walkabout.Interfaces.Reports;
using Walkabout.Data;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using Walkabout.Views;
using Walkabout.Taxes;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Interfaces.Views;

namespace Walkabout.Reports
{
    //=========================================================================================
    public class PortfolioReport : IReport
    {
        MyMoney myMoney;
        Account account;
        FlowDocumentReportWriter flowwriter;
        CostBasisCalculator calc;
        IServiceProvider serviceProvider;
        FrameworkElement view;
        Paragraph mouseDownPara;
        Point downPos;
        DateTime reportDate;

        /// <summary>
        /// Create new PortfolioReport.  
        /// </summary>
        /// <param name="money">The money data</param>
        /// <param name="account">The account, or null to get complete portfolio</param>
        public PortfolioReport(FrameworkElement view, MyMoney money, Account account, IServiceProvider serviceProvider, DateTime asOfDate)
        {
            this.myMoney = money;
            this.account = account;
            this.serviceProvider = serviceProvider;
            this.view = view;
            this.reportDate = asOfDate;
            view.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(view);

            if (mouseDownPara != null && Math.Abs(downPos.X - pos.X) < 5 && Math.Abs(downPos.Y - pos.Y) < 5)
            {
                string name = (string)mouseDownPara.Tag;
                // navigate to show the cell.Data rows.
                IViewNavigator nav = serviceProvider.GetService(typeof(IViewNavigator)) as IViewNavigator;
                nav.ViewTransactionsBySecurity(this.myMoney.Securities.FindSecurity(name, false));
            }
        }


        private void OnReportCellMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            mouseDownPara = (Paragraph)sender;
            downPos = e.GetPosition(this.view);
        }


        class SecurityPieData
        {
            public decimal Total { get; set; }
            public string Name { get; set; }
        }

        decimal totalMarketValue;
        decimal totalGainLoss;


        private void WriteSummaryRow(IReportWriter writer, String Col1, String Col2, String Col3)
        {
            writer.StartCell();
            writer.WriteParagraph(Col1);
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber(Col2);
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber(Col3);
            writer.EndCell();
            writer.EndRow();

        }

        public void Generate(IReportWriter writer)
        {
            flowwriter = writer as FlowDocumentReportWriter;

            calc = new CostBasisCalculator(this.myMoney, this.reportDate);

            string heading = "Investment Portfolio Summary";
            if (this.account != null)
            {
                heading += " for " + account.Name + " (" + account.AccountId + ")";
            }

            writer.WriteHeading(heading);

            if (reportDate.Date != DateTime.Today)
            {
                writer.WriteSubHeading("As of " + reportDate.Date.AddDays(-1).ToLongDateString());
            }

            totalMarketValue = 0;
            totalGainLoss = 0;

            // outer table contains 2 columns, left is the summary table, right is the pie chart.
            writer.StartTable();
            writer.StartColumnDefinitions();
            writer.WriteColumnDefinition("Auto", 100, double.MaxValue);
            writer.WriteColumnDefinition("Auto", 100, double.MaxValue);
            writer.EndColumnDefinitions();
            writer.StartRow();
            writer.StartCell();

            writer.StartTable();
            writer.StartColumnDefinitions();

            foreach (double minWidth in new double[] { 300, 100, 100 })
            {
                writer.WriteColumnDefinition("Auto", minWidth, double.MaxValue);
            }
            writer.EndColumnDefinitions();



            List<SecurityPieData> data = new List<SecurityPieData>();

            if (account == null)
            {
                WriteSummary(writer, data, TaxableIncomeType.None,  "Retirement Tax Free ", new Predicate<Account>((a) => { return !a.IsClosed && !a.IsTaxDeferred && a.Type == AccountType.Retirement; }));
                WriteSummary(writer, data, TaxableIncomeType.All,   "Retirement ",          new Predicate<Account>((a) => { return !a.IsClosed && a.IsTaxDeferred && a.Type == AccountType.Retirement; }));
                WriteSummary(writer, data, TaxableIncomeType.All,   "Tax Deferred ",        new Predicate<Account>((a) => { return !a.IsClosed && a.IsTaxDeferred && a.Type == AccountType.Brokerage; }));
                WriteSummary(writer, data, TaxableIncomeType.Gains, "",                     new Predicate<Account>((a) => { return !a.IsClosed && !a.IsTaxDeferred && a.Type == AccountType.Brokerage; }));
            }
            else
            {
                WriteSummary(writer, data, TaxableIncomeType.Gains, "", new Predicate<Account>((a) => { return a == account; }));
            }

            writer.StartHeaderRow();
            WriteSummaryRow(writer, "Total", totalMarketValue.ToString("C"), totalGainLoss.ToString("C"));
            writer.EndTable();

            writer.EndCell();
            // pie chart
            Chart chart = new Chart();
            chart.MinWidth = 400;
            chart.MinHeight = 300;
            chart.BorderThickness = new Thickness(0);
            chart.Padding = new Thickness(0);
            chart.Margin = new Thickness(0, 00, 0, 0);
            chart.VerticalAlignment = VerticalAlignment.Top;
            chart.HorizontalAlignment = HorizontalAlignment.Left;

            PieSeries series = new PieSeries();
            series.IndependentValueBinding = new Binding("Name");
            series.DependentValueBinding = new Binding("Total");
            chart.Series.Add(series);
            series.ItemsSource = data;

            writer.StartCell();
            writer.WriteElement(chart);
            writer.EndCell();

            // end the outer table.
            writer.EndTable();

            totalMarketValue = 0;
            totalGainLoss = 0;

            List<SecuritySale> errors = new List<SecuritySale>(calc.GetPendingSales(new Predicate<Account>((a) => { return a == account; })));
            if (errors.Count > 0)
            {
                writer.WriteSubHeading("Pending Sales");

                foreach (var sp in errors) 
                {
                    writer.WriteParagraph(string.Format("Pending sale of {1} units of '{2}' from account '{0}' recorded on {3}", sp.Account.Name, sp.UnitsSold, sp.Security.Name, sp.DateSold.ToShortDateString()));
                }
            }

            if (account == null)
            {
                WriteDetails(writer, "Retirement Tax Free ", new Predicate<Account>((a) => { return !a.IsClosed && !a.IsTaxDeferred && a.Type == AccountType.Retirement; }));
                WriteDetails(writer, "Retirement ",          new Predicate<Account>((a) => { return !a.IsClosed && a.IsTaxDeferred && a.Type == AccountType.Retirement; }));
                WriteDetails(writer, "Tax Deferred ",        new Predicate<Account>((a) => { return !a.IsClosed && a.IsTaxDeferred && a.Type == AccountType.Brokerage; }));
                WriteDetails(writer, "",                     new Predicate<Account>((a) => { return !a.IsClosed && !a.IsTaxDeferred && a.Type == AccountType.Brokerage; }));
            }
            else 
            {
                WriteDetails(writer, "", new Predicate<Account>((a) => { return a == account; }));
            }            
        }

        /// <summary>
        /// write out the securities in the given list starting with an expandable/collapsable group header.
        /// </summary>
        private void WriteSecurities(IReportWriter writer, List<SecurityPurchase> bySecurity, ref decimal totalMarketValue, ref decimal totalCostBasis, ref decimal totalGainLoss)
        {
            if (bySecurity.Count == 0)
            {
                return;
            }

            decimal marketValue = 0;
            decimal costBasis = 0;
            decimal gainLoss = 0;
            decimal currentQuantity = 0;
            decimal price = 0;
            Security current = null;

            // compute the totals for the group header row.
            foreach (SecurityPurchase i in bySecurity)
            {
                current = i.Security;

                price = current.Price;
                // for tax reporting we need to report the real GainLoss, but if CostBasis is zero then it doesn't make sense to report something
                // as a gain or loss, it will be accounted for under MarketValue, but it will be misleading as a "Gain".  So we tweak the value here.
                decimal gain = (i.CostBasisPerUnit == 0) ? 0 : i.GainLoss;

                marketValue += i.MarketValue;
                costBasis += i.TotalCostBasis;
                gainLoss += gain;
                currentQuantity += i.UnitsRemaining;
            }

            if (current == null)
            {
                return;
            }

            totalMarketValue += marketValue;
            totalCostBasis += costBasis;
            totalGainLoss += gainLoss;

            // this is the expander row.
            writer.StartExpandableRowGroup();

            decimal averageUnitPrice = currentQuantity == 0 ? 0 : costBasis / currentQuantity;
            WriteRow(writer, false, false, FontWeights.Bold, null, current.Name, current.Name, currentQuantity, price, marketValue, averageUnitPrice, costBasis, gainLoss);


            foreach (SecurityPurchase i in bySecurity)
            {                
                // for tax reporting we need to report the real GainLoss, but if CostBasis is zero then it doesn't make sense to report something
                // as a gain or loss, it will be accounted for under MarketValue, but it will be misleading as a "Gain".  So we tweak the value here.
                decimal gain = (i.CostBasisPerUnit == 0) ? 0 : i.GainLoss;
                WriteRow(writer, false, false, FontWeights.Normal, i.DatePurchased, i.Security.Name, i.Security.Name, i.UnitsRemaining, i.CostBasisPerUnit, i.MarketValue, i.Security.Price, i.TotalCostBasis, gain);
            }

            writer.EndExpandableRowGroup();
        }

        public void ExpandAll()
        {
            if (flowwriter != null)
            {
                flowwriter.ExpandAll();
            }
        }

        public void CollapseAll()
        {
            if (flowwriter != null)
            {
                flowwriter.CollapseAll();
            }
        }
        private void WriteDetails(IReportWriter writer, string prefix, Predicate<Account> filter)
        {
            // compute summary
            foreach (var securityTypeGroup in calc.GetHoldingsBySecurityType(filter))
            {
                decimal marketValue = 0;
                decimal costBasis = 0;
                decimal gainLoss = 0;

                SecurityType st = securityTypeGroup.Key;

                string caption = prefix + Security.GetSecurityTypeCaption(st);

                bool foundSecuritiesInGroup = false;
                Security current = null;
                List<SecurityPurchase> bySecurity = new List<SecurityPurchase>();

                foreach (SecurityPurchase i in securityTypeGroup.Value)
                {
                    if (current != null && current != i.Security)
                    {
                        WriteSecurities(writer, bySecurity, ref marketValue, ref costBasis, ref gainLoss);
                        bySecurity.Clear();
                    }
                    current = i.Security;

                    // only report the security group header if it has some units left in it.
                    if (i.UnitsRemaining > 0)
                    {
                        if (!foundSecuritiesInGroup)
                        {
                            foundSecuritiesInGroup = true;

                            // create the security type group and subtable for these securities.
                            writer.WriteSubHeading(caption);
                            writer.StartTable();
                            writer.StartColumnDefinitions();

                            foreach (var minwidth in new double[] { 20,  //Expander
                                80,        // Date Acquired
                                300,       // Description
                                100,       // Quantity
                                100,       // Price
                                100,       // Market Value
                                100,       // Unit Cost
                                100,       // Cost Basis
                                100,       // Gain/Loss
                                50,        // %
                                 })      
                            {
                                writer.WriteColumnDefinition("Auto", minwidth, double.MaxValue);
                            }
                            writer.EndColumnDefinitions();
                            WriteRowHeaders(writer);
                        }
                        bySecurity.Add(i);
                    }
                }

                if (foundSecuritiesInGroup)
                {
                    // write the final group of securities.
                    WriteSecurities(writer, bySecurity, ref marketValue, ref costBasis, ref gainLoss);

                    writer.StartFooterRow();
                    
                    WriteRow(writer, true, true, FontWeights.Bold, null, "Total", null, null, null, marketValue, null, costBasis, gainLoss);

                    // only close the table 
                    writer.EndTable();
                }
            }
        }

        private void WriteSummary(IReportWriter writer, List<SecurityPieData> data, TaxableIncomeType taxableIncomeType, string prefix, Predicate<Account> filter)
        {
            string caption = prefix + "Investments";
            decimal totalSectionMarketValue;
            decimal totalSectionGainValue = 0;

            writer.StartHeaderRow();
            WriteSummaryRow(writer, caption, "Market Value", "Taxable");

            decimal cash = RoundToNearestCent(this.myMoney.GetInvestmentCashBalance(filter));

            if (taxableIncomeType == TaxableIncomeType.None) totalSectionGainValue = 0;
            if (taxableIncomeType == TaxableIncomeType.All) totalSectionGainValue = cash;
            totalSectionMarketValue = cash;

            if (cash > 0)
            {
                WriteSummaryRow(writer, "    Cash", cash.ToString("C"), totalSectionGainValue.ToString("C"));
                caption = prefix + "Cash";

                data.Add(new SecurityPieData()
                {
                    Total = RoundToNearestCent(cash),
                    Name = caption
                });
            }


            // compute summary
            foreach (var securityGroup in calc.GetHoldingsBySecurityType(filter))
            {
                decimal marketValue = 0;
                decimal gainLoss = 0;

                SecurityType st = securityGroup.Key;
                int count = 0;
                foreach (SecurityPurchase i in securityGroup.Value)
                {
                    if (i.UnitsRemaining > 0)
                    {
                        marketValue += i.MarketValue;
                        gainLoss += i.GainLoss;
                        count++;
                    }
                }

                if (taxableIncomeType == TaxableIncomeType.None) gainLoss = 0;
                if (taxableIncomeType == TaxableIncomeType.All) gainLoss = marketValue;

                if (count > 0)
                {
                    caption = prefix + Security.GetSecurityTypeCaption(st);
                    data.Add(new SecurityPieData()
                    {
                        Total = RoundToNearestCent(marketValue),
                        Name = caption
                    });

                    caption = "    " + Security.GetSecurityTypeCaption(st);
                    WriteSummaryRow(writer, caption, marketValue.ToString("C"), gainLoss.ToString("C"));
                }

                totalSectionMarketValue += marketValue;
                totalSectionGainValue += gainLoss;
            }

            WriteSummaryRow(writer, "    SubTotal", totalSectionMarketValue.ToString("C"), totalSectionGainValue.ToString("C"));

            totalMarketValue += totalSectionMarketValue;
            totalGainLoss += totalSectionGainValue;
        }

        private decimal RoundToNearestCent(decimal x)
        {
            return Math.Round(x * 100) / 100;
        }

        private static void WriteRowHeaders(IReportWriter writer)
        {
            writer.StartHeaderRow();
            writer.StartCell();
            // optional expander button.
            writer.EndCell();

            writer.StartCell();
            writer.WriteParagraph("Date Acquired");
            writer.EndCell();

            writer.StartCell();
            writer.WriteParagraph("Description");
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber("Quantity");
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber("Price");
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber("Market Value");
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber("Unit Cost");
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber("Cost Basis");
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber("Gain/Loss");
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber("%");
            writer.EndCell();
            writer.EndRow();
        }

        private void WriteRow(IReportWriter writer, bool expandable, bool header, FontWeight weight, DateTime? aquired, string description, string descriptionUrl, decimal? quantity, decimal? price, decimal marketValue, decimal? unitCost, decimal costBasis, decimal gainLoss)
        {
            if (header)
            {
                writer.StartHeaderRow();
            }
            else
            {
                writer.StartRow();
            }

            if (expandable)
            {
                writer.StartCell();
                writer.EndCell();
            }

            writer.StartCell();
            if (aquired.HasValue)
            {
                writer.WriteParagraph(aquired.Value.ToShortDateString(), FontStyles.Normal, weight, null);
            }
            writer.EndCell();

            writer.StartCell();
            writer.WriteParagraph(description, FontStyles.Normal, weight, null);
            if (!string.IsNullOrEmpty(descriptionUrl))
            {
                FlowDocumentReportWriter fw = (FlowDocumentReportWriter)writer;
                Paragraph p = fw.CurrentParagraph;
                p.Tag = descriptionUrl;
                p.PreviewMouseLeftButtonDown += OnReportCellMouseDown;
                p.Cursor = Cursors.Arrow;
                //p.TextDecorations.Add(TextDecorations.Underline);
                p.SetResourceReference(Paragraph.ForegroundProperty, "HyperlinkForeground");
            }
            writer.EndCell();

            writer.StartCell();
            if (quantity.HasValue)
            {
                writer.WriteNumber(quantity.Value.ToString("N2"));
            }
            writer.EndCell();

            writer.StartCell();
            if (price.HasValue)
            {
                writer.WriteNumber(price.Value.ToString("N2"));
            }
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(marketValue.ToString("N2"));
            writer.EndCell();

            writer.StartCell();
            if (unitCost.HasValue)
            {
                writer.WriteNumber(unitCost.Value.ToString("N2"));
            }
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(costBasis.ToString("N2"));
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(gainLoss.ToString("N2"));
            writer.EndCell();

            writer.StartCell();
            decimal percent = costBasis == 0 ? 0 : (gainLoss / costBasis) * 100;
            writer.WriteNumber(percent.ToString("N0"));
            writer.EndCell();

            writer.EndRow();
         
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
