using LovettSoftware.Charts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Xps.Serialization;
using Walkabout.Charts;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.PerformanceProvider;
using Walkabout.StockQuotes;
using Walkabout.Utilities;
using Walkabout.Views;
using Walkabout.Views.Controls;

namespace Walkabout.Reports
{
    internal static class TableHelper
    {
        public static void WriteRow(this IReportWriter writer, string label, decimal amount)
        {
            writer.StartRow();
            writer.StartCell();
            writer.WriteParagraph(label);
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber(amount.ToString("C0"));
            writer.EndCell();
            writer.EndRow();
        }
    }

    public class RetirementPlanReport : Report
    {
        private MyMoney myMoney;
        private StockQuoteCache cache;
        private FlowDocumentView view;
        private RetirementControl panel;
        private RetirementPlanState state = new RetirementPlanState()
        {
            InvestmentRateOfReturn = 0.05M,
            InflationRate = 0.04M,
            DesiredAnnualIncome = 200000,
            CurrentAge = 60,
            SpouseAge = 60,
            MarriedFilingJointly = false,
            RetirementAge = 65,
            GraduationAge = 95,
            TaxDeferredStrategy = TaxDeferredStrategyNone,
            TaxDeferredStrategyYears = 5,
            TaxDeferredStrategyAge = 65,
            SocialSecurityAmount = 3000,
            SocialSecurityAge = 67,
            SocialSecuritySpouseAge = 67,
            SocialSecuritySpouseAmount = 0,
            SocialSecurityCola = 0.025M,  // 2.5 % for inflation
            Stacked = true
        };
        private DelayedActions actions = new DelayedActions();

        public static string TaxDeferredStrategyNone = "None";
        public static string TaxDeferredStrategyRoth = "Roth Conversion";

        public RetirementPlanReport(FlowDocumentView view)
        {
            this.ReportDate = DateTime.Today;
            this.view = view;
        }

        protected override void Dispose(bool disposing)
        {
            this.UnRegister();
            base.Dispose(disposing);
        }

        private void Register()
        {
            this.panel.RateOfReturnChanged += this.OnRateOfReturnChanged;
            this.panel.InflationRateChanged += this.OnInflationRateChanged;
            this.panel.DesiredIncomeChanged += this.OnDesiredIncomeChanged;
            this.panel.GraduationAgeChanged += this.OnGraduationAgeChanged;
            this.panel.CurrentAgeChanged += this.OnCurrentAgeChanged;
            this.panel.SpouseAgeChanged += this.OnSpouseAgeChanged;
            this.panel.MarriedFilingJointlyChanged += this.OnMarriedFilingJointlyChanged;
            this.panel.RetirementAgeChanged += this.OnRetirementAgeChanged;
            this.panel.TaxDeferredStrategyChanged += this.OnTaxDeferredStrategyChanged;
            this.panel.TaxDeferredStrategyYearsChanged += this.OnTaxDeferredStrategyYearsChanged;
            this.panel.TaxDeferredStrategyAgeChanged += this.OnTaxDeferredStrategyAgeChanged;
            this.panel.SocialSecurityAgeChanged += this.OnSocialSecurityAgeChanged;
            this.panel.SocialSecurityAmountChanged += this.OnSocialSecurityAmountChanged;
            this.panel.StackedBarsChanged += this.OnStackedBarsChanged;
            this.panel.SocialSecuritySpouseAmountChanged += this.OnSocialSecuritySpouseAmountChanged;
            this.panel.SocialSecuritySpouseAgeChanged += this.OnSocialSecuritySpouseAgeChanged;
            this.panel.SocialSecurityColaChanged += this.OnSocialSecurityColaChanged;
        }

        private void UnRegister()
        {
            if (this.panel != null)
            {
                this.panel.RateOfReturnChanged -= this.OnRateOfReturnChanged;
                this.panel.InflationRateChanged -= this.OnInflationRateChanged;
                this.panel.DesiredIncomeChanged -= this.OnDesiredIncomeChanged;
                this.panel.GraduationAgeChanged -= this.OnGraduationAgeChanged;
                this.panel.CurrentAgeChanged -= this.OnCurrentAgeChanged;
                this.panel.SpouseAgeChanged -= this.OnSpouseAgeChanged;
                this.panel.MarriedFilingJointlyChanged -= this.OnMarriedFilingJointlyChanged;
                this.panel.RetirementAgeChanged -= this.OnRetirementAgeChanged;
                this.panel.TaxDeferredStrategyChanged -= this.OnTaxDeferredStrategyChanged;
                this.panel.TaxDeferredStrategyYearsChanged -= this.OnTaxDeferredStrategyYearsChanged;
                this.panel.TaxDeferredStrategyAgeChanged -= this.OnTaxDeferredStrategyAgeChanged;
                this.panel.SocialSecurityAgeChanged -= this.OnSocialSecurityAgeChanged;
                this.panel.SocialSecurityAmountChanged -= this.OnSocialSecurityAmountChanged;
                this.panel.StackedBarsChanged -= this.OnStackedBarsChanged;
                this.panel.SocialSecuritySpouseAmountChanged -= this.OnSocialSecuritySpouseAmountChanged;
                this.panel.SocialSecuritySpouseAgeChanged -= this.OnSocialSecuritySpouseAgeChanged;
                this.panel.SocialSecurityColaChanged -= this.OnSocialSecurityColaChanged;
            }
        }

        private void OnDesiredIncomeChanged(object sender, decimal e)
        {
            this.state.DesiredAnnualIncome = e; this.Regenerate();
        }

        private void OnInflationRateChanged(object sender, decimal e)
        {
            this.state.InflationRate = e; this.Regenerate();
        }

        private void OnRateOfReturnChanged(object sender, decimal e)
        {
            this.state.InvestmentRateOfReturn = e; this.Regenerate();
        }

        private void OnGraduationAgeChanged(object sender, int e)
        {
            this.state.GraduationAge = e; this.Regenerate();
        }
        private void OnCurrentAgeChanged(object sender, int e)
        {
            this.state.CurrentAge = e; this.Regenerate();
        }
        private void OnSpouseAgeChanged(object sender, int e)
        {
            this.state.SpouseAge = e; this.Regenerate();
        }
        private void OnMarriedFilingJointlyChanged(object sender, bool e)
        {
            this.state.MarriedFilingJointly = e; this.Regenerate();
        }

        private void OnRetirementAgeChanged(object sender, int e)
        {
            this.state.RetirementAge = e; this.Regenerate();
        }
        private void OnTaxDeferredStrategyChanged(object sender, string e)
        {
            this.state.TaxDeferredStrategy = e; this.Regenerate();
        }
        private void OnTaxDeferredStrategyYearsChanged(object sender, int e)
        {
            this.state.TaxDeferredStrategyYears = e; this.Regenerate();
        }
        private void OnTaxDeferredStrategyAgeChanged(object sender, int e)
        {
            this.state.TaxDeferredStrategyAge = e; this.Regenerate();
        }

        private void OnSocialSecurityAmountChanged(object sender, decimal e)
        {
            this.state.SocialSecurityAmount = e; this.Regenerate();
        }

        private void OnSocialSecurityAgeChanged(object sender, int e)
        {
            this.state.SocialSecurityAge = e; this.Regenerate();
        }

        private void OnSocialSecuritySpouseAgeChanged(object sender, int e)
        {
            this.state.SocialSecuritySpouseAge = e; this.Regenerate();
        }

        private void OnSocialSecuritySpouseAmountChanged(object sender, decimal e)
        {
            this.state.SocialSecuritySpouseAmount = e; this.Regenerate();
        }

        private void OnSocialSecurityColaChanged(object sender, decimal e)
        {
            this.state.SocialSecurityCola = e; this.Regenerate();
        }

        private void OnStackedBarsChanged(object sender, bool e)
        {            
            this.state.Stacked = e;
            var charts = this.simulation?.charts;
            if (charts != null)
            {
                foreach (var chart in charts)
                {
                    chart.Stacked = e;
                }
            }
        }

        public override void OnSiteChanged()
        {
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
            this.cache = (StockQuoteCache)this.ServiceProvider.GetService(typeof(StockQuoteCache));

            // this makes the retirement control visible in the report view.
            var panel = (RetirementControl)this.ServiceProvider.GetService(typeof(RetirementControl));
            this.UnRegister();
            this.panel = panel;

            bool hasTaxDeferredAccounts = this.MyMoney.Accounts.GetAccounts(true).Any(t => t.IsTaxDeferred);
            if (hasTaxDeferredAccounts)
            {
                var box = panel.ShowTaxDeferredRow();
                box.Items.Clear();
                box.Items.Add(TaxDeferredStrategyNone);
                box.Items.Add(TaxDeferredStrategyRoth);
                box.SelectedItem = this.state.TaxDeferredStrategy;
            }

            this.state.ReportDate = DateTime.Today;
            this.ApplyState(this.state);
            this.UnRegister();
            this.Register();
        }

        private static string ChartValueTaxable = "Taxable";
        private static string ChartValueTaxDeferred = "Tax Deferred";
        private static string ChartValueTaxFree = "Tax Free";
        private static string ChartValueSocialSecurity = "Social Security";
        private static string ChartValueDividends = "Dividends";
        private static string ChartValueGross = "Gross up";

        private static string ChartValueIncomeTaxes = "Income Tax";
        private static string ChartValueCapitalGainsTaxes = "Capital Gains Tax";
        private static string ChartValueTaxes = "Taxes";
        private static string ChartValueAssets = "Networth";

        class Simulation
        {
            public ChartDataSeries taxDeferredSeries = new ChartDataSeries() { Name = "Tax Deferred", XAxisLabel = "Age"};
            public ChartDataSeries taxableSeries = new ChartDataSeries() { Name = "Taxable", XAxisLabel = "Age" };
            public ChartDataSeries taxFreeSeries = new ChartDataSeries() { Name = "Tax Free", XAxisLabel = "Age" };

            public ChartDataSeries dividendIncomeSeries = new ChartDataSeries() { Name = "Dividend Income", XAxisLabel = "Age" };
            public ChartDataSeries taxableIncomeSeries = new ChartDataSeries() { Name = "Taxable Income", XAxisLabel = "Age" };
            public ChartDataSeries taxDeferredIncomeSeries = new ChartDataSeries() { Name = "Tax Deferred Income", XAxisLabel = "Age" };
            public ChartDataSeries taxFreeIncomeSeries = new ChartDataSeries() { Name = "Tax Free Income", XAxisLabel = "Age" };
            public ChartDataSeries socialSecurityIncomeSeries = new ChartDataSeries() { Name = "Social Security Income", XAxisLabel = "Age" };
            public ChartDataSeries grossIncomeSeries = new ChartDataSeries() { Name = "Extra Income to pay taxes", XAxisLabel = "Age" };

            public ChartDataSeries incomeTaxSeries = new ChartDataSeries() { Name = "Income Tax", XAxisLabel = "Age" };
            public ChartDataSeries capitalGainsTaxSeries = new ChartDataSeries() { Name = "Capital Gains Taxes", XAxisLabel = "Age" };
            public ChartDataSeries rothAssetsSeries = new ChartDataSeries() { Name = "Networth", XAxisLabel = "Year" };
            public ChartDataSeries rothTaxSeries = new ChartDataSeries() { Name = "Taxes", XAxisLabel = "Year" };

            public List<AnimatingBarChart> charts = new List<AnimatingBarChart>();
            private MyMoney myMoney;
            private RetirementPlanState state;
            private Report report;
            private StockQuoteCache cache;
            private RetirementFunds funds;
            private RetirementFunds finalFunds;
            private decimal totalAssets;
            private decimal totalTaxes;
            private CostBasisCalculator calc;
            private decimal rmdAge;

            public Simulation(MyMoney money, RetirementPlanState state, Report report, StockQuoteCache cache)
            {
                this.myMoney = money;
                this.state = state;
                this.report = report;
                this.cache = cache;

                int born = DateTime.Now.Year - state.CurrentAge;
                this.rmdAge = born >= 1960 ? 75 : 73;
            }

            public async Task RunRothSimulation()
            {
                var rothAssetsColor = Colors.Green;
                var rothTaxColor = Color.FromRgb(0xf0, 0x66, 0x00);

                for (int i = 0; i <= 15; i++)
                {
                    var modifiedState = this.state.Copy();
                    if (i == 0)
                    {
                        modifiedState.TaxDeferredStrategy = TaxDeferredStrategyNone;
                    }
                    else 
                    {
                        modifiedState.TaxDeferredStrategy = TaxDeferredStrategyRoth;
                        modifiedState.TaxDeferredStrategyYears = i;
                    }
                    modifiedState.TaxDeferredStrategyAge = modifiedState.RetirementAge; // start right away.

                    var temp = new Simulation(this.myMoney, modifiedState, this.report, this.cache);
                    temp.calc = this.calc;
                    temp.funds = this.funds;
                    temp.rmdAge = this.rmdAge;
                    await temp.Run();
                    rothAssetsSeries.Add(new ChartDataValue() { Label = i.ToString(), Value = (double)temp.totalAssets, Color = rothAssetsColor, UserData = ChartValueAssets });
                    rothTaxSeries.Add(new ChartDataValue() { Label = i.ToString(), Value = (double)temp.totalTaxes, Color = rothTaxColor, UserData = ChartValueTaxes }); 
                }
            }

            public async Task Run()
            {
                Transaction first = this.myMoney.Transactions.GetAllTransactionsByDate().FirstOrDefault();
                if (first == null)
                {
                    return;
                }

                if (this.funds == null)
                {
                    // Cache this funds object as it is expensive to compute.
#if PerformanceBlocks
                    using (PerformanceBlock.Create(ComponentId.Money, CategoryId.Model, MeasurementId.ReportPrepare))
                    {
#endif
                        var date = state.ReportDate;
                        Predicate<Account> notLoans = (a) => a.Type != AccountType.Loan;
                        var cashBalance = this.myMoney.GetCashBalanceNormalized(date, notLoans);
                        decimal loanBalance = this.GetTotalLoansBalance(date);
                        this.funds = await this.CalculatePortfolioBalance(date);                        
#if PerformanceBlocks
                    }
#endif
                }


#if PerformanceBlocks
                using (PerformanceBlock.Create(ComponentId.Money, CategoryId.Model, MeasurementId.ReportGenerate)) ;
#endif
                decimal futureIncome = this.state.DesiredAnnualIncome;
                var funds = this.funds.Copy(); // make a modifiable copy.
                this.finalFunds = funds;

                var taxableColor = Colors.Green;
                var taxFreeColor = Colors.LightGreen;
                var taxDeferredColor = Colors.SeaGreen;

                var taxableIncomeColor = Color.FromRgb(0xf0, 0x66, 0x00);
                var taxFreeIncomeColor = Color.FromRgb(0xDA, 0x80, 0x21);
                var taxDeferredIncomeColor = Color.FromRgb(0xF4, 0xA9, 0x01);
                var dividendIncomeColor = Color.FromRgb(0xB5, 0x93, 0x2F);
                var socialSecurityIncomeColor = Colors.Yellow;
                var grossIncomeColor = Color.FromRgb(0xB6, 0x49, 0x00);

                var incomeTaxColor = Color.FromRgb(0xFE, 0x77, 0x6C);
                var capitalGainsTaxColor = Color.FromRgb(0xD3, 0x45, 0x5B);

                this.totalTaxes = 0;
                decimal conversionAmount = 0;
                decimal socialSecurity = this.state.SocialSecurityAmount;
                decimal spousalSocialSecurity = this.ComputeSpousalSocialSecurity();
                int spousalSocialSecurityAge = this.state.SocialSecuritySpouseAge;
                if (this.state.SocialSecuritySpouseAmount > 0)
                {
                    // then spouse has their own social security and can start whenever they want.
                    spousalSocialSecurity = this.state.SocialSecuritySpouseAmount;
                }
                funds.SimulatedDate = DateTime.Today;
                funds.CreateSortedHoldings(this.calc);

                for (int age = this.state.CurrentAge; age <= this.state.GraduationAge; age++)
                {
                    if (age == this.state.TaxDeferredStrategyAge)
                    {
                        if (this.state.TaxDeferredStrategy == TaxDeferredStrategyRoth)
                        {
                            if (this.state.TaxDeferredStrategyYears < 1)
                            {
                                this.state.TaxDeferredStrategyYears = 1;
                            }

                            conversionAmount = funds.TaxDeferred / this.state.TaxDeferredStrategyYears;
                        }
                    }

                    if (age >= this.state.RetirementAge)
                    {
                        decimal income = 0;
                        decimal incomeTaxes = 0;
                        decimal capitalGainsTaxes = 0;
                        decimal baseIncome = 0;
                        decimal taxableIncome = 0;
                        decimal taxDeferredIncome = 0;
                        decimal taxFreeIncome = 0;
                        decimal socialSecurityIncome = 0;

                        if (age >= this.state.TaxDeferredStrategyAge && this.state.TaxDeferredStrategy == TaxDeferredStrategyRoth)
                        {
                            var (tax, amount) = funds.ConvertToRoth(ref baseIncome, conversionAmount);
                            incomeTaxes += tax;
                            taxDeferredIncome += amount;
                        }
                        decimal dividends = funds.EstimateDividends(calc);
                        if (dividends > 0)
                        {
                            income += dividends;
                            // Now pay taxes on these dividends from the dividends.
                            decimal dividendTax = funds.GetIncomeTax(baseIncome, dividends);
                            incomeTaxes += dividendTax;
                            baseIncome += dividends;
                        }

                        taxableSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)funds.Taxable, Color = taxableColor, UserData = ChartValueTaxable });
                        taxFreeSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)funds.TaxFree, Color = taxFreeColor, UserData = ChartValueTaxFree });
                        taxDeferredSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)funds.TaxDeferred, Color = taxDeferredColor, UserData = ChartValueTaxDeferred });

                        // Now figure out where to draw the income from and how to pay taxes on it.
                        if (funds.TaxDeferred > 0 && age >= this.rmdAge)
                        {
                            // must take RMD distribution.
                            var amount = funds.GetMinimumDistribution(age);
                            funds.TaxDeferred -= amount;
                            taxDeferredIncome += amount;
                            income += amount;
                            // Now to pay to taxes on this we need to take out more.                        
                            incomeTaxes += funds.PayIncomeTaxRecursively(ref baseIncome, amount);
                        }

                        if (age >= this.state.SocialSecurityAge)
                        {
                            socialSecurityIncome = socialSecurity * 12;
                            if (this.state.MarriedFilingJointly)
                            {
                                int spouseAge = age + this.state.SpouseAge - this.state.CurrentAge;
                                if (spouseAge >= spousalSocialSecurityAge)
                                {
                                    // Ok, how that spouse has started we can also apply COLA to spousal amount.
                                    socialSecurityIncome += (spousalSocialSecurity * 12);
                                    spousalSocialSecurity *= (1 + this.state.SocialSecurityCola);
                                }
                            }
                            baseIncome += socialSecurityIncome;
                            income += socialSecurityIncome;
                            socialSecurity *= (1 + this.state.SocialSecurityCola);

                            // estimate taxes on social security income
                            var sst = funds.CalcSocialSecurityTax(baseIncome, socialSecurityIncome);
                            incomeTaxes += sst;
                            // pay these taxes from the social security income.
                            socialSecurityIncome -= sst;
                        }

                        if (income < futureIncome)
                        {
                            // we need more from taxable.
                            var amount = futureIncome - income;
                            var (amountSold, gains) = funds.SellTaxableAmount(amount);
                            taxableIncome += amountSold;
                            income += amountSold;
                            if (gains > 0)
                            {
                                capitalGainsTaxes += funds.PayCapitalGainsTaxRecursively(ref baseIncome, gains);
                            }
                        }

                        // if we still have amount needed then take early withdrawal from tax deferred
                        if (income < futureIncome && funds.TaxDeferred > 0 && age >= 60)
                        {
                            var amount = futureIncome - income;
                            if (amount > funds.TaxDeferred)
                            {
                                amount = funds.TaxDeferred;
                            }
                            funds.TaxDeferred -= amount;
                            income += amount;
                            taxDeferredIncome += amount;
                            // Now to pay to taxes on this we need to take out more.                        
                            incomeTaxes += funds.PayIncomeTaxRecursively(ref baseIncome, amount);
                            baseIncome += amount;
                        }

                        if (income < futureIncome && funds.TaxFree > 0)
                        {
                            // draw down taxfree last to maximize taxfree inheritance.
                            var amount = futureIncome - income;
                            if (amount > funds.TaxFree)
                            {
                                amount = funds.TaxFree;
                            }
                            taxFreeIncome = amount;
                            funds.TaxFree -= amount;
                        }

                        this.totalTaxes += incomeTaxes + capitalGainsTaxes;
                        taxableIncomeSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)taxableIncome, Color = taxableIncomeColor, UserData = ChartValueTaxable });
                        taxDeferredIncomeSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)taxDeferredIncome, Color = taxDeferredIncomeColor, UserData = ChartValueTaxDeferred });
                        taxFreeIncomeSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)taxFreeIncome, Color = taxFreeIncomeColor, UserData = ChartValueTaxFree });
                        socialSecurityIncomeSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)socialSecurityIncome, Color = socialSecurityIncomeColor, UserData = ChartValueSocialSecurity });

                        decimal grossUp = baseIncome - taxableIncome - taxDeferredIncome - socialSecurityIncome - dividends;
                        if (grossUp < 0)
                        {
                            grossUp = 0;
                        }
                        grossIncomeSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)grossUp, Color = grossIncomeColor, UserData = ChartValueGross });
                        dividendIncomeSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)dividends, Color = dividendIncomeColor, UserData = ChartValueDividends }); 
                        incomeTaxSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)incomeTaxes, Color = incomeTaxColor, UserData = ChartValueIncomeTaxes });
                        capitalGainsTaxSeries.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)capitalGainsTaxes, Color = capitalGainsTaxColor, UserData = ChartValueCapitalGainsTaxes });

                        futureIncome = futureIncome * (1 + this.state.InflationRate);
                    }

                    if (age < this.state.GraduationAge)
                    {
                        funds.Appreciate(this.state.InvestmentRateOfReturn);
                    }
                    funds.SimulatedDate = funds.SimulatedDate.AddYears(1);
                }

                this.totalAssets = funds.Taxable + funds.TaxDeferred + funds.TaxFree;
            }

            private decimal ComputeSpousalSocialSecurity()
            {
                // Spousal social security is based on primary Full Retirement Age (67) amount, but we 
                // may not have that number, so we must compute it.
                var primaryAge = this.state.SocialSecurityAge;
                var primaryAmount = this.state.SocialSecurityAmount;
                if (primaryAge >= 67)
                {
                    for (int age = Math.Min(70, primaryAge); age > 67; age--)
                    {
                        primaryAmount *= 0.93M; // reduce it by 7% per year.
                    }
                }
                else 
                {
                    // we are taking early amount but spouse cannot start till 67, so
                    // compute our FRA amount.
                    for (int age = Math.Max(62, primaryAge); age < 67; age++)
                    {
                        primaryAmount *= 1.075M; // increase it by 7.5% per year.
                    }
                }
                if (this.state.SocialSecuritySpouseAge <= 62)
                {
                    return primaryAmount * 0.325M; // spousal gets 32.5% of FRA
                }
                else
                {
                    return primaryAmount * 0.50M; // spousal gets 50% of FRA
                }
            }

            private decimal GetNormalizedAmount(decimal amount)
            {
                return this.report.GetNormalizedAmount(amount);
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
                            balance += a.GetNormalizedAmount(loan.ComputeLoanAccountBalance(date));
                        }
                    }
                }
                return this.GetNormalizedAmount(balance);
            }

            internal async Task<RetirementFunds> CalculatePortfolioBalance(DateTime date)
            {
                if (this.calc == null)
                {
                    this.calc = new CostBasisCalculator(this.myMoney, date);
                }
                var funds = new RetirementFunds();
                funds.MarriedFilingJointly = this.state.MarriedFilingJointly;
                foreach (var accountHolding in this.calc.GetAccountHoldings())
                {
                    foreach (var holding in accountHolding.GetHoldings())
                    {
                        var price = await this.cache.GetSecurityMarketPrice(date, holding.Security);
                        if (accountHolding.Account.IsTaxDeferred)
                        {
                            funds.TaxDeferred += this.GetNormalizedAmount(holding.FuturesFactor * holding.UnitsRemaining * price);
                        }
                        else if (accountHolding.Account.IsTaxFree)
                        {
                            funds.TaxFree += this.GetNormalizedAmount(holding.FuturesFactor * holding.UnitsRemaining * price);
                        }
                        else
                        {
                            // we will deal with these holdings separately.
                        }
                    }
                }

                return funds;
            }

            internal UIElement CreateLegend(AnimatingBarChart chart, double width)
            {
                Grid grid = new Grid();
                grid.Width = width;
                grid.HorizontalAlignment = HorizontalAlignment.Stretch;
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                StackPanel legend = new StackPanel()
                {
                    Orientation = Orientation.Horizontal
                };
                grid.Children.Add(legend);
                Grid.SetColumn(legend, 1);
                var margin = new Thickness(4);
                foreach (var series in chart.Data.Series)
                {
                    legend.Children.Add(new Rectangle() { Margin= margin, Fill = new SolidColorBrush(series.Values[0].Color.Value), Width = 20, Height = 16 });
                    legend.Children.Add(new TextBlock() { Margin = margin, VerticalAlignment = VerticalAlignment.Center, Text = series.Name });
                }
                return grid;
            }

            public void Render(IReportWriter writer)
            {
                charts.Clear();

                // write top level summary table then the charts
                writer.StartTable();
                writer.StartColumnDefinitions();
                writer.WriteColumnDefinition("450", 450, 450);
                writer.WriteColumnDefinition("100", 100, 620);
                writer.EndColumnDefinitions();

                writer.WriteRow($"Assets remaining at age {this.state.GraduationAge}", this.totalAssets);
                writer.WriteRow("    Taxable assets", finalFunds.Taxable);

                if (funds.TaxDeferred > 0)
                {
                    writer.WriteRow("    Tax deferred assets", finalFunds.TaxDeferred);
                }
                if (funds.TaxFree > 0)
                {
                    writer.WriteRow("    Tax free assets", finalFunds.TaxFree);
                }
                writer.WriteRow("Total taxes paid during retirement", this.totalTaxes);
                writer.EndTable();

                double chartWidth = 1280;
                double chartHeight = 400;

                //--------------------------------------------------------------------------------------
                writer.WriteHeading($"Net worth at age {this.state.GraduationAge}");
                AnimatingBarChart networthChart = new AnimatingBarChart() { Stacked = this.state.Stacked };
                networthChart.Width = chartWidth;
                networthChart.Height = chartHeight;
                networthChart.LineBrush = AppTheme.Instance.GetThemedBrush("GridLineBrush");
                networthChart.HorizontalContentAlignment = HorizontalAlignment.Left;
                networthChart.Padding = new Thickness(20, 0, 100, 0);
                networthChart.VerticalAlignment = VerticalAlignment.Top;
                networthChart.HorizontalAlignment = HorizontalAlignment.Left;
                networthChart.ToolTipGenerator = this.OnGenerateToolTip;
                this.charts.Add(networthChart);

                ChartData chartData = new ChartData();
                chartData.AddSeries(taxableSeries);
                chartData.AddSeries(taxFreeSeries);
                chartData.AddSeries(taxDeferredSeries);
                networthChart.Data = chartData;
                writer.WriteElement(this.CreateLegend(networthChart, chartWidth));
                writer.WriteElement(networthChart);

                //--------------------------------------------------------------------------------------
                writer.WriteHeading("Income - adjusted for inflation");
                AnimatingBarChart incomeChart = new AnimatingBarChart() { Stacked = this.state.Stacked };
                incomeChart.Width = chartWidth;
                incomeChart.Height = chartHeight;
                incomeChart.LineBrush = AppTheme.Instance.GetThemedBrush("GridLineBrush");
                incomeChart.HorizontalContentAlignment = HorizontalAlignment.Left;
                incomeChart.Padding = new Thickness(20, 0, 100, 0);
                incomeChart.VerticalAlignment = VerticalAlignment.Top;
                incomeChart.HorizontalAlignment = HorizontalAlignment.Left;
                incomeChart.HorizontalAlignment = HorizontalAlignment.Left;
                incomeChart.ToolTipGenerator = this.OnGenerateToolTip;
                this.charts.Add(incomeChart);

                chartData = new ChartData();
                chartData.AddSeries(grossIncomeSeries);
                chartData.AddSeries(dividendIncomeSeries);
                chartData.AddSeries(taxableIncomeSeries);
                chartData.AddSeries(taxFreeIncomeSeries);
                chartData.AddSeries(taxDeferredIncomeSeries);
                chartData.AddSeries(socialSecurityIncomeSeries);
                incomeChart.Data = chartData;

                writer.WriteElement(this.CreateLegend(incomeChart, chartWidth));
                writer.WriteElement(incomeChart);

                //--------------------------------------------------------------------------------------
                writer.WriteHeading("Taxes paid to get this income");
                AnimatingBarChart taxesChart = new AnimatingBarChart() { Stacked = this.state.Stacked };
                taxesChart.Width = chartWidth;
                taxesChart.Height = chartHeight;
                taxesChart.LineBrush = AppTheme.Instance.GetThemedBrush("GridLineBrush");
                taxesChart.HorizontalContentAlignment = HorizontalAlignment.Left;
                taxesChart.Padding = new Thickness(20, 0, 100, 0);
                taxesChart.VerticalAlignment = VerticalAlignment.Top;
                taxesChart.HorizontalAlignment = HorizontalAlignment.Left;
                taxesChart.HorizontalAlignment = HorizontalAlignment.Left;
                taxesChart.ToolTipGenerator = this.OnGenerateToolTip;
                this.charts.Add(taxesChart);

                chartData = new ChartData();
                chartData.AddSeries(incomeTaxSeries);
                chartData.AddSeries(capitalGainsTaxSeries);
                taxesChart.Data = chartData;

                writer.WriteElement(this.CreateLegend(taxesChart, chartWidth));
                writer.WriteElement(taxesChart);

                //--------------------------------------------------------------------------------------
                writer.WriteHeading("Roth Simulation with different years");
                AnimatingBarChart rothChart = new AnimatingBarChart() { Stacked = false }; //  no point stacking this one
                rothChart.Width = chartWidth;
                rothChart.Height = chartHeight;
                rothChart.LineBrush = AppTheme.Instance.GetThemedBrush("GridLineBrush");
                rothChart.HorizontalContentAlignment = HorizontalAlignment.Left;
                rothChart.Padding = new Thickness(20, 0, 100, 0);
                rothChart.VerticalAlignment = VerticalAlignment.Top;
                rothChart.HorizontalAlignment = HorizontalAlignment.Left;
                rothChart.HorizontalAlignment = HorizontalAlignment.Left;
                rothChart.ToolTipGenerator = this.OnGenerateToolTip;
                this.charts.Add(rothChart);

                chartData = new ChartData();
                chartData.AddSeries(rothAssetsSeries);
                chartData.AddSeries(rothTaxSeries);
                rothChart.Data = chartData;

                writer.WriteElement(this.CreateLegend(rothChart, chartWidth));
                writer.WriteElement(rothChart);
            }

            private UIElement OnGenerateToolTip(ChartDataValue value)
            {
                var tip = new StackPanel() { Orientation = Orientation.Vertical };
                var age = value.Label;
                var prefix = value.UserData as String;
                if (!string.IsNullOrEmpty(prefix))
                {
                    prefix += " ";
                }
                var title = value.Series?.XAxisLabel;
                if (string.IsNullOrEmpty(title))
                {
                    title = "Item";
                }
                tip.Children.Add(new TextBlock() { Text = title + ": " + age, FontWeight = FontWeights.Bold });
                tip.Children.Add(new TextBlock() { Text = prefix + "Amount: " + value.Value.ToString("C0") });
                return tip;
            }
        }
        Simulation simulation = null;


        public override async Task Generate(IReportWriter writer)
        {
            await Task.CompletedTask;

            this.DelaySaveState();

            this.SetDefaultCurrency(writer, this.state.NormalizedCurrency);

            writer.WriteHeading("Retirement Plan as of " + this.state.ReportDate.ToString("D"));
            writer.WriteSubHeading("Currency " + this.DefaultCurrency.Symbol);

            var sim = this.simulation;
            if (sim == null)
            {
                writer.WriteParagraph("Running simulation...");
                _ = Task.Run(this.Simulate); // in the background (do not wait!)
            }
            else
            {
                sim.Render(writer);
            }
        }

        private async Task Simulate()
        {
            var simulation = new Simulation(this.MyMoney, this.state, (Report)this, this.cache);
            this.simulation = simulation;
            await simulation.Run();
            await simulation.RunRothSimulation();
            this.actions.StartDelayedAction("ShowSimulation", this.RenderReport, TimeSpan.FromSeconds(0.01));
        }


        internal class RetirementFunds
        {
            public decimal Taxable;
            public decimal TaxDeferred;
            public decimal TaxFree;
            public bool MarriedFilingJointly;
            public int RmdAge = 75;
            public DateTime SimulatedDate;
            private List<SecurityPurchase> holdings = new List<SecurityPurchase>();

            internal RetirementFunds Copy()
            {
                return new RetirementFunds()
                {
                    TaxDeferred = this.TaxDeferred,
                    TaxFree = this.TaxFree,
                    RmdAge = this.RmdAge,                    
                    MarriedFilingJointly = this.MarriedFilingJointly
                };
            }

            internal void Appreciate(decimal investmentRateOfReturn)
            {
                this.TaxDeferred = this.TaxDeferred * (1 + investmentRateOfReturn);
                this.TaxFree = this.TaxFree * (1 + investmentRateOfReturn);

                // appreciate the taxable holdings.
                foreach (var holding in this.holdings)
                {
                    holding.FuturePrice *= (1 + investmentRateOfReturn);
                }
                this.Taxable = this.ComputeTaxableHoldings();
            }

            private static (decimal Rate, decimal Threshold)[] JointBracket = new (decimal, decimal)[]
                {
                    (0.37M, 768700M),
                    (0.35M, 512450M),
                    (0.32M, 403550M),
                    (0.24M, 211400M),
                    (0.22M, 100800M),
                    (0.12M, 24800M),
                    (0.10M, 0M), // floor
            };
            private static (decimal Rate, decimal Threshold)[] SingleBrackets = new (decimal, decimal)[]
                {
                    (0.37M, 640600M),
                    (0.35M, 256225M),
                    (0.32M, 201775M),
                    (0.24M, 105700M),
                    (0.22M, 50400M),
                    (0.12M, 12400M),
                    (0.10M, 0M),// floor
            };

            /// <summary>
            /// Compute additional income tax to be paid for paycheck over and above what we have 
            /// already paid for the accumulated baseIncome.
            /// </summary>
            /// <param name="baseIncome">The amount we've already paid tax on.</param>
            /// <param name="paycheck">The new amount we need to pay tax on.</param>
            internal decimal GetIncomeTax(decimal baseIncome, decimal paycheck)
            {
                const decimal standardDeduction = 32200M;

                // Brackets for Married Filing Jointly (thresholds are upper bounds for lower brackets)
                // Ordered from highest threshold to lowest.
                var brackets = this.MarriedFilingJointly ? JointBracket : SingleBrackets;

                decimal income = baseIncome + paycheck - standardDeduction;
                if (income <= 0M)
                {
                    return 0M;
                }

                decimal tax = 0M;
                foreach (var bracket in brackets)
                {
                    if (income > bracket.Threshold && paycheck > 0)
                    {
                        decimal taxableAtThisRate = income - bracket.Threshold;
                        if (taxableAtThisRate > paycheck)
                        {
                            taxableAtThisRate = paycheck;
                        }
                        if (taxableAtThisRate > 0)
                        {
                            tax += taxableAtThisRate * bracket.Rate;
                        }
                        paycheck -= taxableAtThisRate;
                        income = bracket.Threshold;
                    }
                }

                return tax;
            }

            // See https://www.finsyn.com/wp-content/uploads/2026/04/Required-Minimum-Distributions-Tables-Summary-Guide.pdf
            // Here we start at 75 since that is the age at which RMD must start (of born after 1960).
            static decimal[] RmdTable = new decimal[]
            {
                24.6M,
                23.7M,
                22.9M,
                22.0M,
                21.1M,
                20.2M,
                19.4M,
                18.5M,
                17.7M,
                16.8M,
                16.0M,
                15.2M,
                14.4M,
                13.7M,
                12.9M,
                12.2M,
                11.5M,
                10.8M,
                10.1M,
                9.5M,
                8.9M,
                8.4M,
                7.8M,
                7.3M,
                6.8M,
                6.4M,
                6.0M,
                5.6M,
                5.2M,
                4.9M,
            };

            internal decimal GetRMDFactor(int age)
            {
                if (age >= RmdAge)
                {
                    int index = age - RmdAge;
                    if (index < RmdTable.Length)
                    {
                        return RmdTable[index];
                    }
                    return 4.9M;
                }
                return 0;
            }

            internal decimal GetMinimumDistribution(int age)
            {
                var factor = this.GetRMDFactor(age);
                if (factor == 0)
                {
                    return 0;
                }
                return this.TaxDeferred / factor;
            }

            internal decimal PayIncomeTaxRecursively(ref decimal baseIncome, decimal income)
            {
                decimal capitalGains = 0;
                decimal incomeTax = this.GetIncomeTax(baseIncome, income);
                baseIncome += income;
                decimal totalTax = incomeTax;
                while (incomeTax > 0)
                {
                    if (incomeTax > 0)
                    {
                        // sell taxable assets first to maximize how much of deferred assets we can convert to Roth.
                        var (amountSold, gains) = this.SellTaxableAmount(incomeTax);
                        capitalGains += gains;
                        incomeTax -= amountSold;
                        baseIncome += amountSold;
                    }

                    if (this.TaxDeferred > 0 && incomeTax > 0)
                    {
                        // Sell this much to pay the incomeTax (which generates new income tax)
                        var amount = incomeTax;
                        if (incomeTax > this.TaxDeferred)
                        {
                            amount = this.TaxDeferred;
                        }
                        this.TaxDeferred -= amount;
                        incomeTax -= amount;
                        var ic = this.GetIncomeTax(baseIncome, amount);
                        baseIncome += amount;
                        totalTax += ic;
                        incomeTax += ic;
                    }

                    if (this.TaxFree > 0 && incomeTax > 0)
                    {
                        var amount = incomeTax;
                        if (incomeTax > this.TaxFree)
                        {
                            amount = this.TaxFree;
                        }
                        this.TaxFree -= amount;
                        incomeTax -= amount;
                        // no tax consequence.
                    }
                    else if (incomeTax > 0)
                    {
                        // crap we cannot pay out taxes!
                        break;
                    }
                }
                if (capitalGains > 0)
                {
                    totalTax += this.PayCapitalGainsTaxRecursively(ref baseIncome, capitalGains);
                }
                else 
                {
                    // TODO: model carry loss forward to next year...
                }
                return totalTax;
            }

            internal decimal PayCapitalGainsTaxRecursively(ref decimal income, decimal capitalGains)
            {
                decimal capitalGainsTax = this.GetCapitalGainsTax(income, capitalGains);
                income += capitalGains;
                decimal totalTax = capitalGainsTax;
                while (capitalGainsTax > 0)
                {
                    if (capitalGainsTax > 0)
                    {
                        // Sell this much to pay the incomeTax (which generates capital gains tax)
                        var (amountSold, gains) = this.SellTaxableAmount(capitalGainsTax);
                        capitalGainsTax -= amountSold;
                        var capTax = this.GetCapitalGainsTax(income, gains);
                        capitalGainsTax += capTax;
                        income += amountSold;
                        totalTax += capTax;
                    }
                    
                    if (this.TaxFree > 0 && capitalGainsTax > 0)
                    {
                        var amount = capitalGainsTax;
                        if (capitalGainsTax > this.TaxFree)
                        {
                            amount = this.TaxFree;
                        }
                        this.TaxFree -= amount;
                        capitalGainsTax -= amount;
                        // no tax consequence.
                    }
                    else
                    {
                        // crap we cannot pay our taxes!
                        break;
                    }
                }
                return totalTax;
            }

            private decimal GetCapitalGainsTax(decimal income, decimal gains)
            {
                if (gains < 0)
                {
                    return 0;
                }
                // Add WA state gains (TODO: make this configurable).
                decimal stateRate = (gains > 250000) ? 0.07M : 0;
                if (income + gains > 613700)
                {
                    return gains * (0.20M + stateRate);
                }
                else if (income + gains > 98900)
                {
                    return gains * (0.15M + stateRate);
                }
                return 0;
            }

            internal Tuple<decimal, decimal> ConvertToRoth(ref decimal baseIncome, decimal amountToConvert)
            {
                decimal tax = 0;
                if (this.TaxDeferred > 0)
                {
                    var amount = amountToConvert;
                    if (amount > this.TaxDeferred)
                    {
                        amount = this.TaxDeferred;
                    }
                    this.TaxDeferred -= amount;
                    this.TaxFree += amount;
                    tax = this.PayIncomeTaxRecursively(ref baseIncome, amount);
                    return Tuple.Create(tax, amount);
                }
                return Tuple.Create(0M, 0M);
            }

            private static (decimal Rate, decimal Threshold)[] JointSocialBracket = new (decimal, decimal)[]
            {
                    (0.85M, 44000M),
                    (0.50M, 32000M),
                    (0.0M, 0M), // floor
            };
            private static (decimal Rate, decimal Threshold)[] SingleSocialBrackets = new (decimal, decimal)[]
            {
                    (0.85M, 34000M),
                    (0.50M, 25000M),
                    (0.0M, 0M), // floor
            };

            internal decimal CalcSocialSecurityTax(decimal baseIncome, decimal socialSecurityIncome)
            {
                // See https://www.aarp.org/social-security/faq/how-are-benefits-taxed
                decimal percentage = 0;
                var brackets = this.MarriedFilingJointly ? JointSocialBracket : SingleSocialBrackets;
                foreach (var (rate, bracket) in brackets)
                {
                    if (baseIncome + socialSecurityIncome > bracket)
                    {
                        percentage = rate;
                        break;
                    }
                }

                var taxable = socialSecurityIncome * percentage;
                if (taxable > 0)
                {
                    return this.GetIncomeTax(baseIncome, taxable);
                }
                return 0;
            }

            internal void CreateSortedHoldings(CostBasisCalculator calc)
            { 
                // reset the list of holdings to the original, note we will be modifying copies
                // of the SecurityPurchase objects during the simulation.
                this.holdings = new List<SecurityPurchase>();
                foreach (var accountHolding in calc.GetAccountHoldings())
                {
                    if (accountHolding.Account.IsTaxDeferred || accountHolding.Account.IsTaxFree)
                    {
                        continue; // skip these ones.
                    }
                    foreach (var holding in accountHolding.GetHoldings())
                    {
                        // ignore private holdings that are not easy to sell.
                        if (holding.Security != null && holding.Security.HasSymbol)
                        {
                            this.holdings.Add(holding.Copy());
                        }
                    }
                }

                calc.ComputeEstimateDividendYield(this.holdings);

                // Now sort the list by cost basis so we sell the highest cost basis assets first.
                // TODO: make this a settable strategy.
                this.holdings.Sort(new Comparison<SecurityPurchase>((a, b) =>
                {
                    if (a.FutureCostBasisRatio == b.FutureCostBasisRatio) return 0;
                    return (a.FutureCostBasisRatio > b.FutureCostBasisRatio) ? -1 : 1;
                }));

                // Now what does this add up to...
                this.Taxable = this.ComputeTaxableHoldings();
            }

            /// <summary>
            /// Try and sell some taxable assets to cover the given amount and return the total capital gains.
            /// </summary>
            /// <param name="amount">Amount we need to raise by sales.</param>
            /// <returns>The amount sold and the total capital gains.</returns>
            internal Tuple<decimal, decimal> SellTaxableAmount(decimal amount)
            {
                decimal covered = 0;
                decimal totalGains = 0;
                while (amount > 0)
                {
                    bool found = false;
                    foreach (var holding in this.holdings)
                    {
                        decimal unitsToSell = 0;
                        if (holding.FutureMarketValue > 0)
                        {
                            found = true;
                            if (holding.FutureMarketValue < amount)
                            {
                                // then we must sell everything.
                                unitsToSell = holding.UnitsRemaining;
                            }
                            else
                            {
                                unitsToSell = amount / holding.FuturePrice;
                            }
                        }
                        if (unitsToSell > 0)
                        {
                            // we have been appreciating the sale prices each year as per investment ROI.
                            // Debug.WriteLine($"Selling {unitsToSell} units of {holding.Security.Name}");
                            var sale = holding.Sell(this.SimulatedDate, unitsToSell, holding.FuturePrice);
                            var gain = sale.TotalGain;
                            totalGains += gain;
                            covered += sale.SaleProceeds;
                            amount -= sale.SaleProceeds;
                        }
                        if ((int)amount <= 0)
                        {
                            break;
                        }
                    }
                    if (!found)
                    {
                        // we are out of holdings!
                        break;
                    }
                }
                return new Tuple<decimal, decimal>(covered, totalGains);
            }

            internal decimal ComputeTaxableHoldings()
            {
                decimal totalFutureValue = 0;
                foreach (var holding in this.holdings) {
                    totalFutureValue += holding.FutureMarketValue;
                }
                return totalFutureValue;
            }

            internal decimal EstimateDividends(CostBasisCalculator calc)
            {
                return calc.ComputeEstimatedAnnualDividends(this.holdings); 
            }
        }

        public override void ApplyState(IReportState state)
        {
            if (state is RetirementPlanState s)
            {
                this.state = s.Copy();

                if (this.panel != null)
                {
                    this.UnRegister();
                    this.panel.RateOfReturn = s.InvestmentRateOfReturn;
                    this.panel.InflationRate = s.InflationRate;
                    this.panel.DesiredIncome = s.DesiredAnnualIncome;
                    this.panel.CurrentAge = s.CurrentAge;
                    this.panel.SpouseAge = s.SpouseAge;
                    this.panel.MarriedFilingJointly = s.MarriedFilingJointly;
                    this.panel.GraduationAge = s.GraduationAge;
                    this.panel.RetirementAge = s.RetirementAge;
                    this.panel.TaxDeferredStrategy = s.TaxDeferredStrategy;
                    this.panel.TaxDeferredStrategyYears = s.TaxDeferredStrategyYears;
                    this.panel.TaxDeferredStrategyAge = s.TaxDeferredStrategyAge;
                    this.panel.SocialSecurityAmount = s.SocialSecurityAmount;
                    this.panel.SocialSecurityAge = s.SocialSecurityAge;
                    this.panel.StackedBars = s.Stacked;
                    this.panel.SocialSecuritySpouseAge = s.SocialSecuritySpouseAge;
                    this.panel.SocialSecuritySpouseAmount = s.SocialSecuritySpouseAmount;
                    this.panel.SocialSecurityCola = s.SocialSecurityCola;
                    this.Register();
                }
            }
        }

        public override IReportState GetState()
        {
            return this.state.Copy();
        }

        public class RetirementPlanState : IReportState
        {
            public StateSource Source { get; set; }
            public DateTime ReportDate { get; set; }
            public string NormalizedCurrency { get; set; }

            public decimal InvestmentRateOfReturn { get; set; }
            public decimal InflationRate { get; set; }
            public decimal DesiredAnnualIncome { get; set; }
            public int CurrentAge { get; set; }
            public int SpouseAge { get; set; }
            public bool MarriedFilingJointly { get; set; }
            public int RetirementAge { get; set; }
            public int GraduationAge { get; set; }
            public string TaxDeferredStrategy { get; set; }
            public int TaxDeferredStrategyYears { get; set; }
            public int TaxDeferredStrategyAge { get; set; }
            public decimal SocialSecurityAmount { get; set; }
            public int SocialSecurityAge { get; set; }
            public int SocialSecuritySpouseAge { get; set; }
            public decimal SocialSecuritySpouseAmount { get; set; }
            public decimal SocialSecurityCola { get; set; }

            public bool Stacked { get; set; }


            public string Name => "RetirementPlan";

            public RetirementPlanState()
            {
            }

            public Type GetReportType()
            {
                return typeof(RetirementPlanReport);
            }
            public RetirementPlanState Copy()
            {
                return new RetirementPlanState()
                {
                    ReportDate = this.ReportDate,
                    NormalizedCurrency = this.NormalizedCurrency,
                    InvestmentRateOfReturn = this.InvestmentRateOfReturn,
                    InflationRate = this.InflationRate,
                    DesiredAnnualIncome = this.DesiredAnnualIncome,
                    CurrentAge = this.CurrentAge,
                    SpouseAge = this.SpouseAge,
                    MarriedFilingJointly = this.MarriedFilingJointly,
                    GraduationAge = this.GraduationAge,
                    RetirementAge = this.RetirementAge,
                    TaxDeferredStrategy = this.TaxDeferredStrategy,
                    TaxDeferredStrategyYears = this.TaxDeferredStrategyYears,
                    TaxDeferredStrategyAge = this.TaxDeferredStrategyAge,
                    SocialSecurityAmount = this.SocialSecurityAmount,
                    SocialSecurityAge = this.SocialSecurityAge,
                    SocialSecuritySpouseAge = this.SocialSecuritySpouseAge,
                    SocialSecuritySpouseAmount = this.SocialSecuritySpouseAmount,
                    SocialSecurityCola = this.SocialSecurityCola,
                    Stacked = this.Stacked
                };
            }
        }

        private void RenderReport()
        {
            this.Regenerate(false);
        }

        private async void Regenerate(bool reset = true)
        {
            if (reset)
            {
                this.simulation = null;
            }
            await this.view.Generate(this);
        }

    }
}
