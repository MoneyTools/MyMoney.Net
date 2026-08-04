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
using Walkabout.Views;
using Walkabout.Views.Controls;

namespace Walkabout.Reports
{
    internal class RetirementPlanReport : Report
    {
        private MyMoney myMoney;
        private FlowDocumentView view;
        private RetirementControl panel;
        private decimal investmentRateOfReturn = 0.05M;
        private decimal inflationRate = 0.04M;
        private bool DoRothConverstion = false;
        private string normalizedCurrency;
        private DateTime reportDate;
        private decimal desiredAnnualIncome = 200000;
        private int currentAge = 60;
        private int graduationAge = 95;        
        private StockQuoteCache cache;

        public RetirementPlanReport(FlowDocumentView view)
        {
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
            }
        }

        private void OnDesiredIncomeChanged(object sender, decimal e)
        {
            this.desiredAnnualIncome = e;
            this.Regenerate();
        }

        private void OnInflationRateChanged(object sender, decimal e)
        {
            this.inflationRate = e;
            this.Regenerate();
        }

        private void OnRateOfReturnChanged(object sender, decimal e)
        {
            this.investmentRateOfReturn = e;
            this.Regenerate();
        }

        private void OnGraduationAgeChanged(object sender, int e)
        {
            this.graduationAge = e;
            this.Regenerate();
        }
        private void OnCurrentAgeChanged(object sender, int e)
        {
            this.currentAge = e;
            this.Regenerate();
        }


        public override void OnSiteChanged()
        {
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
            this.cache = (StockQuoteCache)this.ServiceProvider.GetService(typeof(StockQuoteCache));

            // this makes the retirement control visible in the report view.
            var panel = (RetirementControl)this.ServiceProvider.GetService(typeof(RetirementControl));
            this.UnRegister();
            this.panel = panel;

            this.panel = panel;
            this.reportDate = DateTime.Today;
            this.panel.RateOfReturn = this.investmentRateOfReturn;
            this.panel.InflationRate = this.inflationRate;
            this.panel.DesiredIncome = this.desiredAnnualIncome;
            this.panel.GraduationAge = this.graduationAge;
            this.panel.CurrentAge = this.currentAge;
            this.UnRegister();
            this.Register();
        }

        public override async Task Generate(IReportWriter writer)
        {
            await Task.CompletedTask;

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
            decimal portfolio = await this.CalculatePortfolioBalance(date);
            var networth = this.GetNormalizedAmount(cashBalance + loanBalance + portfolio);
            decimal futureIncome = this.desiredAnnualIncome;

            //writer.StartTable();
            //writer.StartColumnDefinitions();
            //writer.WriteColumnDefinition("auto", 50, double.MaxValue);
            //writer.WriteColumnDefinition("auto", 100, double.MaxValue);
            //writer.WriteColumnDefinition("auto", 100, double.MaxValue);
            //writer.EndColumnDefinitions();
            //writer.StartHeaderRow();
            //writer.StartCell();
            //writer.WriteParagraph("Age");
            //writer.EndCell();
            //writer.StartCell();
            //writer.WriteNumber("Networth");
            //writer.EndCell();
            //writer.StartCell();
            //writer.WriteNumber("Income");
            //writer.EndCell();
            //writer.EndHeaderRow();

            var color = Colors.Green;
            var incomeColor = Colors.Goldenrod;

            var networthSeries = new ChartDataSeries() { Name = "Networth" };
            var incomeSeries = new ChartDataSeries() { Name = "Income" };

            for (int age = this.currentAge; age <= this.graduationAge; age++)
            {
                //writer.StartRow();
                //writer.StartCell();
                //writer.WriteNumber(age.ToString());
                //writer.EndCell();
                //writer.StartCell();
                //writer.WriteNumber(networth.ToString("C"));
                //writer.EndCell();
                //writer.StartCell();
                //writer.WriteNumber(futureIncome.ToString("C"));
                //writer.EndCell();
                //writer.EndRow();

                networthSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)networth, Color = color });
                incomeSeries.Values.Add(new ChartDataValue() { Label = age.ToString(), Value = (double)futureIncome, Color = incomeColor });

                networth = networth * (1 + this.investmentRateOfReturn) - futureIncome;
                futureIncome = futureIncome * (1 + this.inflationRate);
            }

            //writer.EndTable();

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
            chartData.AddSeries(networthSeries);
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
            chartData.AddSeries(incomeSeries);
            incomeChart.Data = chartData;

            writer.WriteElement(incomeChart);
        }

        private UIElement OnGenerateToolTip(ChartDataValue value)
        {
            var tip = new StackPanel() { Orientation = Orientation.Vertical };
            var age = value.Label;
            tip.Children.Add(new TextBlock() { Text = "Age: " + age, FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = "Amount: " + value.Value.ToString("C0") });
            return tip;
        }

        internal async Task<decimal> CalculatePortfolioBalance(DateTime date)
        {
            CostBasisCalculator calc = new CostBasisCalculator(this.myMoney, date);
            decimal total = 0;
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
                    total += holding.FuturesFactor * holding.UnitsRemaining * price;
                }
            }
            return this.GetNormalizedAmount(total);
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
                if (this.panel != null)
                {
                    this.UnRegister();
                    this.panel.RateOfReturn = this.investmentRateOfReturn;
                    this.panel.InflationRate = this.inflationRate;
                    this.panel.DesiredIncome = this.desiredAnnualIncome;
                    this.panel.CurrentAge = this.currentAge;
                    this.panel.GraduationAge = this.graduationAge;
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
                DoRothConverstion = this.DoRothConverstion,
                DesiredAnnualIncome = this.desiredAnnualIncome,
                CurrentAge = this.currentAge,
                GraduationAge = this.graduationAge
            };
        }

        class RetirementPlanState : IReportState
        {
            public DateTime ReportDate { get; set; }
            public string NormalizedCurrency { get; set; }

            public decimal InvestmentRateOfReturn { get; set; }
            public decimal InflationRate { get; set; }
            public bool DoRothConverstion { get; set; }
            public decimal DesiredAnnualIncome { get; set; }
            public int CurrentAge { get; set; }
            public int GraduationAge { get; set; }


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
