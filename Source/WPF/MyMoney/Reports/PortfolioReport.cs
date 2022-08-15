using LovettSoftware.Charts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Walkabout.Charts;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Interfaces.Views;
using Walkabout.StockQuotes;
using Walkabout.Utilities;

namespace Walkabout.Reports
{
    //=========================================================================================
    public class PortfolioReport : Report
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
        StockQuoteCache cache;
        SecurityGroup selectedGroup;
        Random rand = new Random(Environment.TickCount);

        public event EventHandler<SecurityGroup> DrillDown;

        /// <summary>
        /// Create new PortfolioReport.  
        /// </summary>
        /// <param name="money">The money data</param>
        /// <param name="account">The account, or null to get complete portfolio</param>
        public PortfolioReport(FrameworkElement view, MyMoney money, Account account, IServiceProvider serviceProvider, DateTime asOfDate, SecurityGroup g)
        {
            this.myMoney = money;
            this.account = account;
            this.serviceProvider = serviceProvider;
            this.view = view;
            this.reportDate = asOfDate;
            this.selectedGroup = g;
            this.cache = (StockQuoteCache)serviceProvider.GetService(typeof(StockQuoteCache));
            view.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            view.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            view.Unloaded += (s, e) =>
            {
                this.view.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            };
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


        decimal totalMarketValue;
        decimal totalGainLoss;


        private void WriteSummaryRow(IReportWriter writer, Color c, String col1, String col2, String col3)
        {
            writer.StartCell();
            writer.WriteElement(new Rectangle() { Width = 20, Height = 16, Fill = new SolidColorBrush(c) });
            writer.EndCell();
            writer.StartCell();
            writer.WriteParagraph(col1);
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber(col2);
            writer.EndCell();
            writer.StartCell();
            if (col3 != null)
            {
                writer.WriteNumber(col3);
                writer.EndCell();
            }
            writer.EndRow();
        }

        private void WriteHeaderRow(IReportWriter writer, String col1, String col2, String col3)
        {
            writer.StartHeaderRow();
            writer.StartCell(1,2);
            writer.WriteParagraph(col1);
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber(col2);
            writer.EndCell();
            writer.StartCell();
            if (col3 != null)
            {
                writer.WriteNumber(col3);
                writer.EndCell();
            }
            writer.EndRow();
        }

        public override Task Generate(IReportWriter writer)
        {
            flowwriter = writer as FlowDocumentReportWriter;

            calc = new CostBasisCalculator(this.myMoney, this.reportDate);

            string heading = "Investment Portfolio Summary";
            if (this.selectedGroup != null)
            {
                heading = "Investment Portfolio - " + GetTaxStatusPrefix(this.selectedGroup.TaxStatus) + GetSecurityTypeCaption(this.selectedGroup.Type);
            }
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

            writer.WriteColumnDefinition("30", 30, 30);
            foreach (double minWidth in new double[] { 300, 100, 100 })
            {
                writer.WriteColumnDefinition("Auto", minWidth, double.MaxValue);
            }
            writer.EndColumnDefinitions();

            var series = new ChartDataSeries() { Name = "Portfolio" };
            IList<ChartDataValue> data = series.Values;
            if (account == null)
            {
                if (this.selectedGroup != null)
                {
                    WriteSummary(writer, data, this.selectedGroup.TaxStatus, null, false);
                }
                else
                {
                    WriteSummary(writer, data, TaxStatus.TaxFree, new Predicate<Account>((a) => { return a.IsTaxFree && IsInvestmentAccount(a); }), true);
                    WriteSummary(writer, data, TaxStatus.TaxDeferred, new Predicate<Account>((a) => { return a.IsTaxDeferred && IsInvestmentAccount(a);  }), true);
                    WriteSummary(writer, data, TaxStatus.Taxable, new Predicate<Account>((a) => { return !a.IsTaxDeferred && !a.IsTaxFree && IsInvestmentAccount(a); }), true);
                }
            }
            else
            {
                WriteSummary(writer, data, TaxStatus.Any, new Predicate<Account>((a) => { return a == account; }), false);
            }

            WriteHeaderRow(writer, "Total", totalMarketValue.ToString("C"), totalGainLoss.ToString("C"));
            writer.EndTable();

            writer.EndCell();
            // pie chart
            AnimatingPieChart chart = new AnimatingPieChart();
            chart.Width = 400;
            chart.Height = 300;
            chart.BorderThickness = new Thickness(0);
            chart.Padding = new Thickness(20,0,100,0);
            chart.VerticalAlignment = VerticalAlignment.Top;
            chart.HorizontalContentAlignment = HorizontalAlignment.Left;
            chart.Series = series;
            chart.ToolTipGenerator = OnGenerateToolTip;
            chart.PieSliceClicked += OnPieSliceClicked;

            writer.StartCell();
            writer.WriteElement(chart);
            writer.EndCell();

            // end the outer table.
            writer.EndTable();

            totalMarketValue = 0;
            totalGainLoss = 0;

            if (this.selectedGroup != null)
            {
                WriteDetails(writer, this.selectedGroup.TaxStatus, this.selectedGroup);
            }
            else
            {
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
                    WriteDetails(writer, TaxStatus.TaxFree, new Predicate<Account>((a) => { return a.IsTaxFree && IsInvestmentAccount(a);}));
                    WriteDetails(writer, TaxStatus.TaxDeferred, new Predicate<Account>((a) => { return a.IsTaxDeferred && IsInvestmentAccount(a); }));
                    WriteDetails(writer, TaxStatus.Taxable, new Predicate<Account>((a) => { return !a.IsTaxFree && !a.IsTaxDeferred && IsInvestmentAccount(a); }));
                }
                else
                {
                    WriteDetails(writer, account.TaxStatus, new Predicate<Account>((a) => { return a == account; }));
                }
            }
            return Task.CompletedTask;
        }

        private string GetSecurityTypeCaption(SecurityType st)
        {
            string caption = "";
            switch (st)
            {
                case SecurityType.Bond:
                    caption = "Bonds";
                    break;
                case SecurityType.MutualFund:
                    caption = "Mutual Funds";
                    break;
                case SecurityType.Equity:
                    caption = "Equities";
                    break;
                case SecurityType.MoneyMarket:
                    caption = "Money Market";
                    break;
                case SecurityType.ETF:
                    caption = "Exchange Traded Funds";
                    break;
                case SecurityType.Reit:
                    caption = "Reits";
                    break;
                case SecurityType.Futures:
                    caption = "Futures";
                    break;
                case SecurityType.Private:
                    caption = "Private Investments";
                    break;
                default:
                    break;
            }
            return caption;
        }

        bool IsInvestmentAccount(Account a)
        {
            return a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement;
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

        /// <summary>
        /// write out the securities in the given list starting with an expandable/collapsable group header.
        /// </summary>
        private void WriteSecurities(IReportWriter writer, IList<SecurityPurchase> bySecurity, ref decimal totalMarketValue, ref decimal totalCostBasis, ref decimal totalGainLoss)
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
            bool priceFound = false;
            Security current = null;
            Security previous = null;

            // compute the totals for the group header row.
            foreach (SecurityPurchase i in bySecurity)
            {
                current = i.Security;
                if (!priceFound || (current != previous))
                {
                    priceFound = true;
                    price = this.cache.GetSecurityMarketPrice(this.reportDate, i.Security);
                    previous = current;
                }
                costBasis += i.TotalCostBasis;
                // for tax reporting we need to report the real GainLoss, but if CostBasis is zero then it doesn't make sense to report something
                // as a gain or loss, it will be accounted for under MarketValue, but it will be misleading as a "Gain".  So we tweak the value here.
                var value = i.FuturesFactor * i.UnitsRemaining * price;
                marketValue += value;
                decimal gain = (i.CostBasisPerUnit == 0) ? 0 : value - i.TotalCostBasis;
                gainLoss += gain;
                currentQuantity += i.UnitsRemaining.RoundToNearestCent();
            }

            if (current == null || currentQuantity == 0)
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
                marketValue = i.FuturesFactor * i.UnitsRemaining * price;
                gainLoss = (i.CostBasisPerUnit == 0) ? 0 : marketValue - i.TotalCostBasis;                
                WriteRow(writer, false, false, FontWeights.Normal, i.DatePurchased, i.Security.Name, i.Security.Name,
                    i.UnitsRemaining, price, marketValue, i.CostBasisPerUnit, i.TotalCostBasis, gainLoss);
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

        private void WriteDetails(IReportWriter writer, TaxStatus status, Predicate<Account> filter)
        {
            // compute summary
            foreach (var securityTypeGroup in calc.GetHoldingsBySecurityType(status, filter))
            {
                WriteDetails(writer, status, securityTypeGroup);
            }
        }

        private string GetTaxStatusPrefix(TaxStatus status)
        {
            string label = "";
            switch (status)
            {
                case TaxStatus.Taxable:
                    label = "Taxable ";
                    break;
                case TaxStatus.TaxDeferred:
                    label = "Tax Deferred ";
                    break;
                case TaxStatus.TaxFree:
                    label = "Tax Free ";
                    break;
                case TaxStatus.Any:
                    break;
                default:
                    break;
            }
            return label;
        }

        private void WriteDetails(IReportWriter writer, TaxStatus status, SecurityGroup securityTypeGroup)
        {
            decimal marketValue = 0;
            decimal costBasis = 0;
            decimal gainLoss = 0;
            bool foundSecuritiesInGroup = false;
            SecurityType st = securityTypeGroup.Type;

            string prefix = GetTaxStatusPrefix(status);
            string caption = prefix + Security.GetSecurityTypeCaption(st);

            IList<SecurityGroup> groups = calc.RegroupBySecurity(securityTypeGroup);
            foreach (SecurityGroup g in groups)
            {
                foreach (var i in g.Purchases)
                {
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
                            break;
                        }
                    }
                }
                if (foundSecuritiesInGroup)
                {
                    WriteSecurities(writer, g.Purchases, ref marketValue, ref costBasis, ref gainLoss);
                }
            }

            writer.StartFooterRow();
            WriteRow(writer, true, true, FontWeights.Bold, null, "Total", null, null, null, marketValue, null, costBasis, gainLoss);
            // only close the table 
            writer.EndTable();
        }


        private void WriteSummary(IReportWriter writer, IList<ChartDataValue> data, TaxStatus taxStatus, Predicate<Account> filter, bool subtotal)
        {
            bool wroteSectionHeader = false;
            string prefix = GetTaxStatusPrefix(taxStatus);
            string caption = prefix + "Investments";
            decimal totalSectionMarketValue = 0;
            decimal totalSectionGainValue = 0;
            int rowCount = 0;

            IList<SecurityGroup> groups = null;
            if (this.selectedGroup != null)
            {
                groups = calc.RegroupBySecurity(this.selectedGroup);
            }
            else
            {
                groups = calc.GetHoldingsBySecurityType(taxStatus, filter);
            }

            // compute summary
            foreach (var securityGroup in groups)
            {
                decimal marketValue = 0;
                decimal gainLoss = 0;

                SecurityType st = securityGroup.Type;
                int count = 0;
                decimal price = 0;
                Security previous = null;
                bool hasPrice = false;
                foreach (SecurityPurchase i in securityGroup.Purchases)
                {
                    if (i.UnitsRemaining.RoundToNearestCent() > 0)
                    {
                        if (!hasPrice || (i.Security != previous))
                        {
                            hasPrice = true;
                            price = this.cache.GetSecurityMarketPrice(this.reportDate, i.Security);
                            previous = i.Security;
                        }
                        var value = i.FuturesFactor * i.UnitsRemaining * price;
                        marketValue += value;
                        gainLoss += value - i.TotalCostBasis;
                        count++;
                    }
                }

                if (taxStatus == TaxStatus.TaxFree) gainLoss = 0;

                if (count > 0)
                {
                    if (!wroteSectionHeader)
                    {
                        WriteHeaderRow(writer, caption, "Market Value", "Taxable Gains");
                        wroteSectionHeader = true;
                    }

                    var color = GetRandomColor();
                    if (securityGroup.Security != null && this.selectedGroup != null)
                    {
                        caption = securityGroup.Security.Name;
                    }
                    else
                    {
                        caption = prefix + Security.GetSecurityTypeCaption(st);
                    }
                    
                    data.Add(new ChartDataValue()
                    {
                        Value = (double)Math.Abs(marketValue).RoundToNearestCent(),
                        Label = caption,
                        Color = color,
                        UserData = securityGroup
                    });

                    if (securityGroup.Security == null && this.selectedGroup != null)
                    {
                        caption = "    " + Security.GetSecurityTypeCaption(st);
                    }
                    WriteSummaryRow(writer, color, caption, marketValue.ToString("C"), gainLoss.ToString("C"));
                    rowCount++;
                }

                totalSectionMarketValue += marketValue;
                totalSectionGainValue += gainLoss;
            }

            if (wroteSectionHeader && subtotal && rowCount > 1)
            {
                WriteSummaryRow(writer, Colors.Transparent, "    SubTotal", totalSectionMarketValue.ToString("C"), totalSectionGainValue.ToString("C"));
            }

            totalMarketValue += totalSectionMarketValue;
            totalGainLoss += totalSectionGainValue;
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
            writer.WriteHyperlink(description, FontStyles.Normal, weight, OnReportCellMouseDown);
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

        private Color GetRandomColor()
        {
            return Color.FromRgb((byte)rand.Next(80, 200), (byte)rand.Next(80, 200), (byte)rand.Next(80, 200));
        }

    }
}
