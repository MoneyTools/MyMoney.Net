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
using Walkabout.Charts;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.StockQuotes;
using Walkabout.Utilities;
using Walkabout.Views;
using Walkabout.Views.Controls;

namespace Walkabout.Reports
{
    public class RetirementPlanReport : Report
    {
        private MyMoney myMoney;
        private FlowDocumentView view;
        private RetirementControl panel;
        private decimal investmentRateOfReturn = 0.05M;
        private decimal inflationRate = 0.04M;
        private string normalizedCurrency;
        private DateTime reportDate;
        private decimal desiredAnnualIncome = 200000;
        private int currentAge = 60;
        private int retirementAge = 65;
        private int graduationAge = 95;
        private int taxDeferredStrategyYears = 5;
        private decimal socialSecurityAmount = 0;
        private int socialSecurityAge = 67;
        private decimal socialSecurityAdjustment = 0.04M; // for inflation
        private string taxDeferredStrategy = TaxDeferredStrategyNone;
        private StockQuoteCache cache;

        public static string TaxDeferredStrategyNone = "None";
        public static string TaxDeferredStrategyRoth = "Roth Conversion";


        public RetirementPlanReport(FlowDocumentView view)
        {
            this.view = view;
        }

        protected override void Dispose(bool disposing)
        {
            this.UnRegister();
            base.Dispose(disposing);
        }

        private void Load()
        {

        }

        private void Register()
        {
            this.panel.RateOfReturnChanged += this.OnRateOfReturnChanged;
            this.panel.InflationRateChanged += this.OnInflationRateChanged;
            this.panel.DesiredIncomeChanged += this.OnDesiredIncomeChanged;
            this.panel.GraduationAgeChanged += this.OnGraduationAgeChanged;
            this.panel.CurrentAgeChanged += this.OnCurrentAgeChanged;
            this.panel.RetirementAgeChanged += this.OnRetirementAgeChanged;
            this.panel.TaxDeferredStrategyChanged += this.OnTaxDeferredStrategyChanged;
            this.panel.TaxDeferredStrategyYearsChanged += this.OnTaxDeferredStrategyYearsChanged;
            this.panel.SocialSecurityAgeChanged += this.OnSocialSecurityAgeChanged;
            this.panel.SocialSecurityAmountChanged += this.OnSocialSecurityAmountChanged;
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
                this.panel.RetirementAgeChanged -= this.OnRetirementAgeChanged;
                this.panel.TaxDeferredStrategyChanged -= this.OnTaxDeferredStrategyChanged;
                this.panel.TaxDeferredStrategyYearsChanged -= this.OnTaxDeferredStrategyYearsChanged;
                this.panel.SocialSecurityAgeChanged -= this.OnSocialSecurityAgeChanged;
                this.panel.SocialSecurityAmountChanged -= this.OnSocialSecurityAmountChanged;
            }
        }

        private void OnDesiredIncomeChanged(object sender, decimal e)
        {
            this.desiredAnnualIncome = e; this.Regenerate();
        }

        private void OnInflationRateChanged(object sender, decimal e)
        {
            this.inflationRate = e; this.Regenerate();
        }

        private void OnRateOfReturnChanged(object sender, decimal e)
        {
            this.investmentRateOfReturn = e; this.Regenerate();
        }

        private void OnGraduationAgeChanged(object sender, int e)
        {
            this.graduationAge = e; this.Regenerate();
        }
        private void OnCurrentAgeChanged(object sender, int e)
        {
            this.currentAge = e; this.Regenerate();
        }
        private void OnRetirementAgeChanged(object sender, int e)
        {
            this.retirementAge = e; this.Regenerate();
        }
        private void OnTaxDeferredStrategyChanged(object sender, string e)
        {
            this.taxDeferredStrategy = e; this.Regenerate();
        }
        private void OnTaxDeferredStrategyYearsChanged(object sender, int e)
        {
            this.taxDeferredStrategyYears = e; this.Regenerate();
        }
        private void OnSocialSecurityAmountChanged(object sender, decimal e)
        {
            this.socialSecurityAmount = e; this.Regenerate();
        }

        private void OnSocialSecurityAgeChanged(object sender, int e)
        {
            this.socialSecurityAge = e; this.Regenerate();
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
                box.SelectedItem = this.taxDeferredStrategy;
            }

            this.reportDate = DateTime.Today;
            this.panel.RateOfReturn = this.investmentRateOfReturn;
            this.panel.InflationRate = this.inflationRate;
            this.panel.DesiredIncome = this.desiredAnnualIncome;
            this.panel.GraduationAge = this.graduationAge;
            this.panel.CurrentAge = this.currentAge;
            this.panel.RetirementAge = this.retirementAge;
            this.panel.TaxDeferredStrategy = this.taxDeferredStrategy;
            this.panel.TaxDeferredStrategyYears = this.taxDeferredStrategyYears;
            this.panel.SocialSecurityAge = this.socialSecurityAge;
            this.panel.SocialSecurityAmount = this.socialSecurityAmount;
            this.UnRegister();
            this.Register();
        }

        private static string ChartValueTaxable = "Taxable";
        private static string ChartValueTaxDeferred = "Tax Deferred";
        private static string ChartValueTaxFree = "Tax Free";
        private static string ChartValueSocialSecurity = "Social Security";
        private static string ChartValueGross = "Gross";

        public override async Task Generate(IReportWriter writer)
        {
            await Task.CompletedTask;

            this.DelaySaveState();

            this.SetDefaultCurrency(writer, this.normalizedCurrency);

            writer.WriteHeading("Retirement Plan as of " + this.reportDate.ToString("D"));
            writer.WriteSubHeading("Currency " + this.DefaultCurrency.Symbol);

            Transaction first = this.myMoney.Transactions.GetAllTransactionsByDate().FirstOrDefault();
            if (first == null)
            {
                return;
            }
            var date = this.reportDate;
            Predicate<Account> notLoans = (a) => a.Type != AccountType.Loan;
            var cashBalance = this.myMoney.GetCashBalanceNormalized(date, notLoans);
            decimal loanBalance = this.GetTotalLoansBalance(date);
            var funds = await this.CalculatePortfolioBalance(date);
            
            decimal futureIncome = this.desiredAnnualIncome;

            var taxableColor = Colors.Green;
            var taxFreeColor = Colors.LightGreen;
            var taxDeferredColor = Colors.SeaGreen;
            var taxableIncomeColor = Color.FromRgb(0xf0, 0x66, 0x00);
            var taxFreeIncomeColor = Color.FromRgb(0xDA, 0x80, 0x21);
            var taxDeferredIncomeColor = Color.FromRgb(0xF4, 0xA9, 0x01);
            var socialSecurityIncomeColor = Colors.Yellow;
            var grossIncomeColor = Colors.LightYellow;

            var taxColor = Colors.Salmon;

            var taxDeferredSeries = new ChartDataSeries() { Name = "Tax Deferred" };
            var taxableSeries = new ChartDataSeries() { Name = "Taxable" };
            var taxFreeSeries = new ChartDataSeries() { Name = "Tax Free" };

            var taxableIncomeSeries = new ChartDataSeries() { Name = "Taxable Income" };
            var taxDeferredIncomeSeries = new ChartDataSeries() { Name = "Tax Deferred Income" };
            var taxFreeIncomeSeries = new ChartDataSeries() { Name = "Tax Free Income" };
            var socialSecurityIncomeSeries = new ChartDataSeries() { Name = "Social Security Income" };
            var grossIncomeSeries = new ChartDataSeries() { Name = "Gross Income" };
            var taxesSeries = new ChartDataSeries() { Name = "Taxes" };

            decimal totalTaxes = 0;
            decimal conversionAmount = 0;
            decimal socialSecurity = this.socialSecurityAmount;


            for (int age = this.currentAge; age <= this.graduationAge; age++)
            {
                if (age == this.retirementAge)
                {
                    if (this.taxDeferredStrategy == TaxDeferredStrategyRoth)
                    {
                        if (this.taxDeferredStrategyYears < 1)
                        {
                            this.taxDeferredStrategyYears = 1;
                        }
                   
                        conversionAmount = funds.TaxDeferred / this.taxDeferredStrategyYears;
                    }
                }

                if (age >= this.retirementAge)
                {
                    decimal income = 0;
                    decimal taxes = 0;
                    decimal baseIncome = 0;

                    if (this.taxDeferredStrategy == TaxDeferredStrategyRoth)
                    {
                        taxes += funds.ConvertToRoth(ref baseIncome, conversionAmount);
                    }

                    taxableSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)funds.Taxable, Color = taxableColor, UserData = ChartValueTaxable});
                    taxFreeSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)funds.TaxFree, Color = taxFreeColor, UserData = ChartValueTaxFree });
                    taxDeferredSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)funds.TaxDeferred, Color = taxDeferredColor, UserData = ChartValueTaxDeferred });

                    decimal taxableIncome = 0;
                    decimal taxDeferredIncome = 0;
                    decimal taxFreeIncome = 0;
                    decimal socialSecurityIncome = 0;

                    if (age >= this.socialSecurityAge)
                    {
                        socialSecurityIncome = socialSecurity;
                        baseIncome += socialSecurity;
                        socialSecurity *= (1 + this.socialSecurityAdjustment);
                        income = socialSecurityIncome;
                    }

                    // Now figure out where to draw the income from and how to pay taxes on it.
                    if (funds.TaxDeferred > 0 && age >= 75)
                    {
                        // must take RMD distribution.
                        var amount = funds.GetMinimumDistribution(age);
                        funds.TaxDeferred -= amount;
                        taxDeferredIncome += amount;
                        income += amount;
                        // Now to pay to taxes on this we need to take out more.                        
                        taxes += funds.PayIncomeTaxRecursively(ref baseIncome, amount);
                    }
                    if (income < futureIncome && funds.Taxable > 0)
                    {
                        // we need more from taxable or taxfree accounts.
                        var amount = futureIncome - income;
                        if (amount > funds.Taxable)
                        {
                            amount = funds.Taxable;
                        }
                        taxableIncome += amount;
                        income += amount;
                        funds.Taxable -= amount;
                        taxes += funds.PayCapitalGainsTaxRecursively(ref baseIncome, amount);
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
                        taxes += funds.PayIncomeTaxRecursively(ref baseIncome, amount);
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

                    totalTaxes += taxes; 
                    taxableIncomeSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)taxableIncome, Color = taxableIncomeColor, UserData = ChartValueTaxable });
                    taxDeferredIncomeSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)taxDeferredIncome, Color = taxDeferredIncomeColor, UserData = ChartValueTaxDeferred });
                    taxFreeIncomeSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)taxFreeIncome, Color = taxFreeIncomeColor, UserData = ChartValueTaxFree });
                    socialSecurityIncomeSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)socialSecurityIncome, Color = socialSecurityIncomeColor, UserData = ChartValueSocialSecurity });
                    grossIncomeSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)baseIncome, Color = grossIncomeColor, UserData = ChartValueGross });

                    taxesSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)taxes, Color = taxColor });
                    futureIncome = futureIncome * (1 + this.inflationRate);
                }

                funds.Appreciate(this.investmentRateOfReturn);
            }

            var totalAssets = funds.Taxable + funds.TaxDeferred + funds.TaxFree;
            writer.WriteParagraph($"Assets remaining at age : {this.graduationAge} = { totalAssets.ToString("C0")}");
            writer.WriteParagraph($"Taxable assets = {funds.Taxable.ToString("C0")}");

            if (funds.TaxDeferred > 0)
            {
                writer.WriteParagraph($"Tax deferred assets = {funds.TaxDeferred.ToString("C0")}");
            }
            if (funds.TaxFree > 0)
            {
                writer.WriteParagraph($"Tax free assets = {funds.TaxFree.ToString("C0")}");
            }
            writer.WriteParagraph($"Total taxes paid during retirement = {totalTaxes.ToString("C0")}");

            // insert networth graph
            writer.WriteHeading("Networth");

            AnimatingBarChart networthChart = new AnimatingBarChart();
            networthChart.Width = 1280;
            networthChart.Height = 400;
            networthChart.HorizontalContentAlignment = HorizontalAlignment.Left;
            networthChart.Padding = new Thickness(20, 0, 100, 0);
            networthChart.BorderThickness = new Thickness(0);
            networthChart.VerticalAlignment = VerticalAlignment.Top;
            networthChart.HorizontalAlignment = HorizontalAlignment.Left;
            networthChart.ToolTipGenerator = this.OnGenerateToolTip;

            ChartData chartData = new ChartData();
            chartData.AddSeries(taxableSeries);
            chartData.AddSeries(taxFreeSeries);
            chartData.AddSeries(taxDeferredSeries);
            networthChart.Data = chartData;
            writer.WriteElement(networthChart);

            writer.WriteHeading("Income - adjusted for inflation");
            AnimatingBarChart incomeChart = new AnimatingBarChart();
            incomeChart.Width = 1280;
            incomeChart.Height = 400;
            incomeChart.HorizontalContentAlignment = HorizontalAlignment.Left;
            incomeChart.Padding = new Thickness(20, 0, 100, 0);
            incomeChart.BorderThickness = new Thickness(0);
            incomeChart.VerticalAlignment = VerticalAlignment.Top;
            incomeChart.HorizontalAlignment = HorizontalAlignment.Left;
            incomeChart.HorizontalAlignment = HorizontalAlignment.Left;
            incomeChart.ToolTipGenerator = this.OnGenerateToolTip;

            chartData = new ChartData();
            chartData.AddSeries(grossIncomeSeries);
            chartData.AddSeries(taxableIncomeSeries);
            chartData.AddSeries(taxFreeIncomeSeries);
            chartData.AddSeries(taxDeferredIncomeSeries);
            chartData.AddSeries(socialSecurityIncomeSeries);
            incomeChart.Data = chartData;

            writer.WriteElement(incomeChart);

            writer.WriteHeading("Taxes paid to get this income");
            AnimatingBarChart taxesChart = new AnimatingBarChart();
            taxesChart.Width = 1280;
            taxesChart.Height = 400;
            taxesChart.HorizontalContentAlignment = HorizontalAlignment.Left;
            taxesChart.Padding = new Thickness(20, 0, 100, 0);
            taxesChart.BorderThickness = new Thickness(0);
            taxesChart.VerticalAlignment = VerticalAlignment.Top;
            taxesChart.HorizontalAlignment = HorizontalAlignment.Left;
            taxesChart.HorizontalAlignment = HorizontalAlignment.Left;
            taxesChart.ToolTipGenerator = this.OnGenerateToolTip;

            chartData = new ChartData();
            chartData.AddSeries(taxesSeries);
            taxesChart.Data = chartData;

            writer.WriteElement(taxesChart);
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
            tip.Children.Add(new TextBlock() { Text = "Age: " + age, FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = prefix + "Amount: " + value.Value.ToString("C0") });
            return tip;
        }

        internal class RetirementFunds
        {
            public decimal Taxable;
            public decimal TaxDeferred;
            public decimal TaxFree;
            public decimal CostBasisRatio;

            internal void Appreciate(decimal investmentRateOfReturn)
            {
                this.Taxable = this.Taxable * (1 + investmentRateOfReturn);
                this.TaxDeferred = this.TaxDeferred * (1 + investmentRateOfReturn);
                this.TaxFree = this.TaxFree * (1 + investmentRateOfReturn);
            }

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
                (decimal Rate, decimal Threshold)[] brackets = new (decimal, decimal)[]
                {
                (0.37M, 768700M),
                (0.35M, 512450M),
                (0.32M, 403550M),
                (0.24M, 211400M),
                (0.22M, 100800M),
                (0.12M, 24800M),
                (0.00M, 0M) // floor
                };

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
                if (age >= 75)
                {
                    int index = age - 75;
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
                    else if (this.Taxable > 0)
                    {
                        // Sell this much to pay the incomeTax (which generates capital gains tax)
                        var amount = incomeTax;
                        if (incomeTax > this.Taxable)
                        {
                            amount = this.Taxable;
                        }
                        this.Taxable -= amount;
                        incomeTax -= amount;
                        baseIncome += amount;
                        capitalGains += amount;
                    }
                    else if (this.TaxFree > 0)
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
                    else
                    {
                        // crap we cannot pay out taxes!
                        break;
                    }
                }
                if (capitalGains > 0)
                {
                    totalTax += this.PayCapitalGainsTaxRecursively(ref baseIncome, capitalGains);
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
                    if (this.Taxable > 0.01M)
                    {
                        // Sell this much to pay the incomeTax (which generates capital gains tax)
                        var amount = capitalGainsTax;
                        if (capitalGainsTax > this.Taxable)
                        {
                            amount = this.Taxable;
                        }
                        this.Taxable -= amount;
                        capitalGainsTax -= amount;
                        var capTax = this.GetCapitalGainsTax(income, amount);
                        capitalGainsTax += capTax;
                        income += capTax;
                        totalTax += capTax;
                    }
                    else if (this.TaxFree > 0)
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

            private decimal GetCapitalGainsTax(decimal income, decimal amount)
            {
                var capGains = amount * (1 - CostBasisRatio);
                decimal stateRate = (income > 250000) ? 0.07M : 0;
                if (income + amount  > 613700)
                {
                    return capGains * (0.20M + stateRate);
                }
                else if (income + amount > 98900)
                {
                    return capGains * (0.15M + stateRate);
                }
                return 0;
            }

            internal decimal ConvertToRoth(ref decimal baseIncome, decimal amountToConvert)
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
                }
                return tax;
            }
        }

        internal async Task<RetirementFunds> CalculatePortfolioBalance(DateTime date)
        {
            CostBasisCalculator calc = new CostBasisCalculator(this.myMoney, date);
            var funds = new RetirementFunds();
            decimal totalCostBasis = 0;
            foreach (var accountHolding in calc.GetAccountHoldings())
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
                        funds.Taxable += this.GetNormalizedAmount(holding.FuturesFactor * holding.UnitsRemaining * price);
                        totalCostBasis += this.GetNormalizedAmount(holding.TotalCostBasis);
                    }
                }
            }

            if (totalCostBasis > 0 && funds.Taxable > 0)
            {
                funds.CostBasisRatio = totalCostBasis / funds.Taxable;
            }

            return funds;
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

        public override void ApplyState(IReportState state)
        {
            if (state is RetirementPlanState s)
            {
                this.reportDate = s.ReportDate;
                this.normalizedCurrency = s.NormalizedCurrency;
                this.investmentRateOfReturn = s.InvestmentRateOfReturn;
                this.desiredAnnualIncome = s.DesiredAnnualIncome;
                this.currentAge = s.CurrentAge;
                this.graduationAge = s.GraduationAge;
                this.retirementAge = s.RetirementAge;
                this.taxDeferredStrategy = s.TaxDeferredStrategy;
                this.taxDeferredStrategyYears = s.TaxDeferredStrategyYears;
                this.socialSecurityAmount = s.SocialSecurityAmount;
                this.socialSecurityAge = s.SocialSecurityAge;

                if (this.panel != null)
                {
                    this.UnRegister();
                    this.panel.RateOfReturn = this.investmentRateOfReturn;
                    this.panel.InflationRate = this.inflationRate;
                    this.panel.DesiredIncome = this.desiredAnnualIncome;
                    this.panel.CurrentAge = this.currentAge;
                    this.panel.GraduationAge = this.graduationAge;
                    this.panel.RetirementAge = this.retirementAge;
                    this.panel.TaxDeferredStrategy = this.taxDeferredStrategy;
                    this.panel.TaxDeferredStrategyYears = this.taxDeferredStrategyYears;
                    this.panel.SocialSecurityAmount = this.socialSecurityAmount;
                    this.panel.SocialSecurityAge = this.socialSecurityAge;
                    this.Register();
                }
            }
        }

        public override IReportState GetState()
        {
            return new RetirementPlanState()
            {
                ReportDate = this.reportDate,
                NormalizedCurrency = this.normalizedCurrency,
                InvestmentRateOfReturn = this.investmentRateOfReturn,
                InflationRate = this.inflationRate,
                DesiredAnnualIncome = this.desiredAnnualIncome,
                CurrentAge = this.currentAge,
                GraduationAge = this.graduationAge,
                RetirementAge = this.retirementAge,
                TaxDeferredStrategy = this.taxDeferredStrategy,
                TaxDeferredStrategyYears = this.taxDeferredStrategyYears,
                SocialSecurityAmount = this.socialSecurityAmount,
                SocialSecurityAge = this.socialSecurityAge,
            };
        }

        public class RetirementPlanState : IReportState
        {
            public DateTime ReportDate { get; set; }
            public string NormalizedCurrency { get; set; }

            public decimal InvestmentRateOfReturn { get; set; }
            public decimal InflationRate { get; set; }
            public decimal DesiredAnnualIncome { get; set; }
            public int CurrentAge { get; set; }
            public int RetirementAge { get; set; }
            public int GraduationAge { get; set; }
            public string TaxDeferredStrategy { get; set; }
            public int TaxDeferredStrategyYears { get; set; }
            public decimal SocialSecurityAmount { get; set; }
            public int SocialSecurityAge { get; set; }


            public string Name => "RetirementPlan";

            public RetirementPlanState()
            {
            }

            public Type GetReportType()
            {
                return typeof(RetirementPlanReport);
            }
        }

        private async void Regenerate()
        {
            await this.view.Generate(this);
        }

    }
}
