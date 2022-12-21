using LovettSoftware.Charts;
using System;
using System.Collections.Generic;
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
using Walkabout.Views;

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
        FlowDocumentView view;
        Paragraph mouseDownPara;
        Point downPos;
        DateTime reportDate;
        StockQuoteCache cache;
        SecurityGroup selectedGroup;
        AccountGroup accountGroup;
        Random rand = new Random(Environment.TickCount);
        bool generating;

        public event EventHandler<SecurityGroup> DrillDown;

        /// <summary>
        /// Create new PortfolioReport
        /// </summary>
        /// <param name="view">The FlowDocumentView we are generating the report in</param>
        /// <param name="money">The money database</param>
        /// <param name="account">Optional account for single account portfolio</param>
        /// <param name="serviceProvider">Required to access additional services</param>
        /// <param name="asOfDate">The date to compute the portfolio balances to</param>
        public PortfolioReport(FlowDocumentView view, MyMoney money, Account account, IServiceProvider serviceProvider, DateTime asOfDate)
        {
            this.myMoney = money;
            this.account = account;
            this.serviceProvider = serviceProvider;
            this.view = view;
            this.reportDate = asOfDate;
            this.cache = (StockQuoteCache)serviceProvider.GetService(typeof(StockQuoteCache));
            view.PreviewMouseLeftButtonUp -= this.OnPreviewMouseLeftButtonUp;
            view.PreviewMouseLeftButtonUp += this.OnPreviewMouseLeftButtonUp;
            view.Unloaded += (s, e) =>
            {
                this.view.PreviewMouseLeftButtonUp -= this.OnPreviewMouseLeftButtonUp;
            };
        }

        /// <summary>
        /// Create new PortfolioReport for a given SecurityGroup we are drilling into from the Networth Report, like "Taxable Mutual Funds".
        /// </summary>
        public PortfolioReport(FlowDocumentView view, MyMoney money, IServiceProvider serviceProvider, DateTime asOfDate, SecurityGroup a) :
            this(view, money, null, serviceProvider, asOfDate)
        {
            this.selectedGroup = a;
        }

        /// <summary>
        /// Create new PortfolioReport for a given AccountGroup, this will show just the cash balances of these accounts.
        /// </summary>
        public PortfolioReport(FlowDocumentView view, MyMoney money, IServiceProvider serviceProvider, DateTime asOfDate, AccountGroup a) :
            this(view, money, null, serviceProvider, asOfDate)
        {
            this.accountGroup = a;
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this.view);

            if (this.mouseDownPara != null && Math.Abs(this.downPos.X - pos.X) < 5 && Math.Abs(this.downPos.Y - pos.Y) < 5)
            {
                string name = (string)this.mouseDownPara.Tag;
                // navigate to show the cell.Data rows.
                IViewNavigator nav = this.serviceProvider.GetService(typeof(IViewNavigator)) as IViewNavigator;
                nav.ViewTransactionsBySecurity(this.myMoney.Securities.FindSecurity(name, false));
            }
        }

        private void OnReportCellMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.mouseDownPara = (Paragraph)sender;
            this.downPos = e.GetPosition(this.view);
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
            writer.StartCell(1, 2);
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
            this.generating = true;
            try
            {
                this.InternalGenerate(writer);
            }
            finally
            {
                this.generating = false;
            }
            return Task.CompletedTask;
        }

        private void InternalGenerate(IReportWriter writer)
        {
            this.flowwriter = writer as FlowDocumentReportWriter;

            this.calc = new CostBasisCalculator(this.myMoney, this.reportDate);
            if (this.selectedGroup != null)
            {
                bool found = false;
                // user may have changed the reportDate, so we may need to recompute this.
                foreach (var securityTypeGroup in this.calc.GetHoldingsBySecurityType(this.selectedGroup.Filter))
                {
                    if (securityTypeGroup.Type == this.selectedGroup.Type)
                    {
                        securityTypeGroup.TaxStatus = this.selectedGroup.TaxStatus;
                        securityTypeGroup.Filter = this.selectedGroup.Filter;
                        this.selectedGroup = securityTypeGroup;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    this.selectedGroup.Purchases.Clear();
                    this.selectedGroup.Date = this.reportDate;
                }
            }

            string heading = null;
            if (this.selectedGroup != null)
            {
                heading = "Investment Portfolio - " + this.GetTaxStatusPrefix(this.selectedGroup.TaxStatus) + this.GetSecurityTypeCaption(this.selectedGroup.Type);
            }
            else if (this.accountGroup != null)
            {
                heading = this.accountGroup.Title + " Cash Balances";
            }
            else if (this.account != null)
            {
                heading = "Investment Portfolio Summary for " + this.account.Name;
                if (!string.IsNullOrEmpty(this.account.AccountId))
                {
                    heading += " (" + this.account.AccountId + ")";
                }
            }
            else
            {
                heading = "Investment Portfolio Summary";
            }

            writer.WriteHeading(heading);

            Paragraph pheading = this.flowwriter.CurrentParagraph;

            DatePicker picker = new DatePicker();
            // byYearCombo.SelectionChanged += OnYearChanged;
            picker.Margin = new Thickness(10, 0, 0, 0);
            picker.SelectedDate = this.reportDate;
            picker.DisplayDate = this.reportDate;
            picker.SelectedDateChanged += this.Picker_SelectedDateChanged;
            pheading.Inlines.Add(new InlineUIContainer(picker));

            if (this.reportDate.Date != DateTime.Today)
            {
                writer.WriteSubHeading("As of " + this.reportDate.Date.AddDays(-1).ToLongDateString());
            }

            this.totalMarketValue = 0;
            this.totalGainLoss = 0;

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
            var columns = new double[] { 300, 100, 100 };
            if (this.accountGroup != null)
            {
                columns = new double[] { 300, 100, 10 };
            }
            foreach (double minWidth in columns)
            {
                writer.WriteColumnDefinition("Auto", minWidth, double.MaxValue);
            }
            writer.EndColumnDefinitions();

            var series = new ChartDataSeries() { Name = "Portfolio" };

            IList<ChartDataValue> data = series.Values;
            if (this.account == null)
            {
                if (this.selectedGroup != null)
                {
                    this.WriteSummary(writer, data, this.selectedGroup.TaxStatus, null, false, false);
                }
                else if (this.accountGroup != null)
                {
                    this.WriteCashBalanceSummary(writer, data, this.accountGroup);
                }
                else
                {
                    this.WriteSummary(writer, data, TaxStatus.TaxFree, new Predicate<Account>((a) => { return a.IsTaxFree && this.IsInvestmentAccount(a); }), true, false);
                    this.WriteSummary(writer, data, TaxStatus.TaxDeferred, new Predicate<Account>((a) => { return a.IsTaxDeferred && this.IsInvestmentAccount(a); }), true, false);
                    this.WriteSummary(writer, data, TaxStatus.Taxable, new Predicate<Account>((a) => { return !a.IsTaxDeferred && !a.IsTaxFree && this.IsInvestmentAccount(a); }), true, false);
                }
            }
            else
            {
                this.WriteSummary(writer, data, TaxStatus.Any, new Predicate<Account>((a) => { return a == this.account; }), false, true);
            }

            this.WriteHeaderRow(writer, "Total", this.totalMarketValue.ToString("C"), this.accountGroup != null ? "" : this.totalGainLoss.ToString("C"));
            writer.EndTable();

            writer.EndCell();
            // pie chart
            AnimatingPieChart chart = new AnimatingPieChart();
            chart.Width = 400;
            chart.Height = 300;
            chart.BorderThickness = new Thickness(0);
            chart.Padding = new Thickness(20, 0, 100, 0);
            chart.VerticalAlignment = VerticalAlignment.Top;
            chart.HorizontalContentAlignment = HorizontalAlignment.Left;
            chart.Series = series;
            chart.ToolTipGenerator = this.OnGenerateToolTip;
            chart.PieSliceClicked += this.OnPieSliceClicked;

            writer.StartCell();
            writer.WriteElement(chart);
            writer.EndCell();

            // end the outer table.
            writer.EndTable();

            if (this.accountGroup == null)
            {
                this.totalMarketValue = 0;
                this.totalGainLoss = 0;

                if (this.selectedGroup != null)
                {
                    this.WriteDetails(writer, this.selectedGroup.TaxStatus, this.selectedGroup);
                }
                else
                {
                    List<SecuritySale> errors = new List<SecuritySale>(this.calc.GetPendingSales(new Predicate<Account>((a) => { return a == this.account; })));
                    if (errors.Count > 0)
                    {
                        writer.WriteSubHeading("Pending Sales");

                        foreach (var sp in errors)
                        {
                            writer.WriteParagraph(string.Format("Pending sale of {1} units of '{2}' from account '{0}' recorded on {3}", sp.Account.Name, sp.UnitsSold, sp.Security.Name, sp.DateSold.ToShortDateString()));
                        }
                    }

                    if (this.account == null)
                    {
                        this.WriteDetails(writer, TaxStatus.TaxFree, new Predicate<Account>((a) => { return a.IsTaxFree && this.IsInvestmentAccount(a); }));
                        this.WriteDetails(writer, TaxStatus.TaxDeferred, new Predicate<Account>((a) => { return a.IsTaxDeferred && this.IsInvestmentAccount(a); }));
                        this.WriteDetails(writer, TaxStatus.Taxable, new Predicate<Account>((a) => { return !a.IsTaxFree && !a.IsTaxDeferred && this.IsInvestmentAccount(a); }));
                    }
                    else
                    {
                        this.WriteDetails(writer, this.account.TaxStatus, new Predicate<Account>((a) => { return a == this.account; }));
                    }
                }
            }

            writer.WriteParagraph("Generated for " + this.reportDate.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);
        }

        private void Picker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.generating)
            {
                DatePicker picker = (DatePicker)sender;
                if (picker.SelectedDate.HasValue)
                {
                    var newDate = picker.SelectedDate.Value;
                    if (newDate != this.reportDate)
                    {
                        this.reportDate = newDate;
                        if (this.selectedGroup != null)
                        {
                            this.selectedGroup.Date = newDate;
                        }
                        if (this.accountGroup != null)
                        {
                            this.accountGroup.Date = newDate;
                        }
                        _ = this.view.Generate(this);
                    }
                }
            }
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
            this.WriteRow(writer, false, false, FontWeights.Bold, null, current.Name, current.Name, currentQuantity, price, marketValue, averageUnitPrice, costBasis, gainLoss);

            foreach (SecurityPurchase i in bySecurity)
            {
                current = i.Security;
                if (!priceFound || (current != previous))
                {
                    priceFound = true;
                    price = this.cache.GetSecurityMarketPrice(this.reportDate, i.Security);
                    previous = current;
                }
                // for tax reporting we need to report the real GainLoss, but if CostBasis is zero then it doesn't make sense to report something
                // as a gain or loss, it will be accounted for under MarketValue, but it will be misleading as a "Gain".  So we tweak the value here.
                marketValue = i.FuturesFactor * i.UnitsRemaining * price;
                gainLoss = (i.CostBasisPerUnit == 0) ? 0 : marketValue - i.TotalCostBasis;
                this.WriteRow(writer, false, false, FontWeights.Normal, i.DatePurchased, i.Security.Name, i.Security.Name,
                    i.UnitsRemaining, price, marketValue, i.CostBasisPerUnit, i.TotalCostBasis, gainLoss);
            }

            writer.EndExpandableRowGroup();
        }

        public void ExpandAll()
        {
            if (this.flowwriter != null)
            {
                this.flowwriter.ExpandAll();
            }
        }

        public void CollapseAll()
        {
            if (this.flowwriter != null)
            {
                this.flowwriter.CollapseAll();
            }
        }

        private void WriteDetails(IReportWriter writer, TaxStatus status, Predicate<Account> filter)
        {
            // compute summary
            foreach (var securityTypeGroup in this.calc.GetHoldingsBySecurityType(filter))
            {
                securityTypeGroup.TaxStatus = status; // inherited from the account types we are filtering here.
                this.WriteDetails(writer, status, securityTypeGroup);
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

            string prefix = this.GetTaxStatusPrefix(status);
            string caption = prefix + Security.GetSecurityTypeCaption(st);

            IList<SecurityGroup> groups = this.calc.RegroupBySecurity(securityTypeGroup);
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
                    this.WriteSecurities(writer, g.Purchases, ref marketValue, ref costBasis, ref gainLoss);
                }
            }

            writer.StartFooterRow();
            this.WriteRow(writer, true, true, FontWeights.Bold, null, "Total", null, null, null, marketValue, null, costBasis, gainLoss);
            // only close the table 
            writer.EndTable();
        }

        private void WriteCashBalanceSummary(IReportWriter writer, IList<ChartDataValue> data, AccountGroup group)
        {
            decimal total = 0;
            this.WriteHeaderRow(writer, "Account", "Cash Balance", null);
            foreach (var account in this.myMoney.Accounts.GetAccounts())
            {
                if (this.accountGroup.Filter(account))
                {
                    decimal balance = this.myMoney.GetCashBalanceNormalized(this.reportDate, (a) => a == account);
                    if (balance != 0)
                    {
                        var caption = account.Name;
                        var color = this.GetRandomColor();
                        data.Add(new ChartDataValue()
                        {
                            Value = (double)balance.RoundToNearestCent(),
                            Label = caption,
                            Color = color,
                            UserData = account
                        });

                        this.WriteSummaryRow(writer, color, caption, balance.ToString("C"), null);
                        total += balance;
                    }
                }
            }
            this.totalMarketValue = total;
        }

        private void WriteSummary(IReportWriter writer, IList<ChartDataValue> data, TaxStatus taxStatus, Predicate<Account> filter, bool subtotal, bool includeCashBalance)
        {
            bool wroteSectionHeader = false;
            string prefix = this.GetTaxStatusPrefix(taxStatus);
            string caption = prefix + "Investments";
            decimal totalSectionMarketValue = 0;
            decimal totalSectionGainValue = 0;
            int rowCount = 0;

            IList<SecurityGroup> groups = null;
            if (this.selectedGroup != null)
            {
                groups = this.calc.RegroupBySecurity(this.selectedGroup);
            }
            else
            {
                groups = this.calc.GetHoldingsBySecurityType(filter);
            }

            // compute summary
            foreach (var securityGroup in groups)
            {
                decimal marketValue = 0;
                decimal gainLoss = 0;

                securityGroup.TaxStatus = taxStatus; // inherited from the accounts we are filtering here.
                securityGroup.Filter = filter;
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
                        this.WriteHeaderRow(writer, caption, "Market Value", "Taxable Gains");
                        wroteSectionHeader = true;
                    }

                    var color = this.GetRandomColor();
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
                        Value = (double)marketValue.RoundToNearestCent(),
                        Label = caption,
                        Color = color,
                        UserData = securityGroup
                    });

                    if (securityGroup.Security == null && this.selectedGroup != null)
                    {
                        caption = "    " + Security.GetSecurityTypeCaption(st);
                    }
                    this.WriteSummaryRow(writer, color, caption, marketValue.ToString("C"), gainLoss.ToString("C"));
                    rowCount++;
                }

                totalSectionMarketValue += marketValue;
                totalSectionGainValue += gainLoss;
            }

            if (includeCashBalance)
            {
                decimal cashBalance = this.myMoney.GetCashBalanceNormalized(this.reportDate, filter);
                var color = this.GetRandomColor();
                data.Add(new ChartDataValue()
                {
                    Value = (double)cashBalance.RoundToNearestCent(),
                    Label = "Cash",
                    Color = color
                });

                this.WriteSummaryRow(writer, color, "Cash", cashBalance.ToString("C"), "");

                rowCount++;
                totalSectionMarketValue += cashBalance;
            }

            if (wroteSectionHeader && subtotal && rowCount > 1)
            {
                this.WriteSummaryRow(writer, Colors.Transparent, "    SubTotal", totalSectionMarketValue.ToString("C"), totalSectionGainValue.ToString("C"));
            }

            this.totalMarketValue += totalSectionMarketValue;
            this.totalGainLoss += totalSectionGainValue;
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
            writer.WriteHyperlink(description, FontStyles.Normal, weight, this.OnReportCellMouseDown);
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
            return Color.FromRgb((byte)this.rand.Next(80, 200), (byte)this.rand.Next(80, 200), (byte)this.rand.Next(80, 200));
        }

    }
}
