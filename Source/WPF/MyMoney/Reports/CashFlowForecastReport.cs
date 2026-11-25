using LovettSoftware.Charts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    internal class CashFlowForecastReport : Report
    {
        private AreaChart chart;
        private bool byYear;
        private ReportsControl panel;
        private MyMoney myMoney;
        private StockQuoteCache cache;
        private string normalizedCurrency;


        public CashFlowForecastReport()
        {
            this.ReportDate = DateTime.Today;
            this.byYear = false;
        }

        private static string ByYears = "By Years";
        private static string ByMonth = "By Month";

        ~CashFlowForecastReport()
        {
            Debug.WriteLine("CashFlowReport disposed!");
        }

        protected override void Dispose(bool disposing)
        {
            this.Unregister();
            base.Dispose(disposing);
        }

        private void Unregister()
        {
            if (this.panel == null)
            {
                return;
            }
            this.panel.ReportDateChanged -= this.OnReportStartDateChanged;
            this.panel.ReportIntervalChanged -= this.OnReportIntervalChanged;
            this.panel.NormalizedCurrencyChanged -= this.OnNormalizedCurrencyChanged;
        }
        private void Register()
        {
            if (this.panel != null)
            {
                this.panel.ReportDateChanged += this.OnReportStartDateChanged;
                this.panel.ReportIntervalChanged += this.OnReportIntervalChanged;
                this.panel.NormalizedCurrencyChanged += this.OnNormalizedCurrencyChanged;
            }
        }

        private void OnNormalizedCurrencyChanged(object sender, string e)
        {
            this.normalizedCurrency = e;
            this.Regenerate();
        }

        private void OnReportIntervalChanged(object sender, string selected)
        {
            if (selected == ByYears)
            {
                this.byYear = true;
            }
            else
            {
                this.byYear = false;
            }

            this.Regenerate();
        }

        private void OnReportStartDateChanged(object sender, DateTime e)
        {
            this.ReportDate = e;
            this.Regenerate();
        }

        public void Regenerate()
        {
            var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
            _ = view.Generate(this);
        }


        public override void OnSiteChanged()
        {
            this.cache = (StockQuoteCache)this.ServiceProvider.GetService(typeof(StockQuoteCache));
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
            // this also makes the panel visible.
            this.Unregister();
            var panel = (ReportsControl)this.ServiceProvider.GetService(typeof(ReportsControl));
            this.panel = panel;

            var box = panel.ShowReportInterval();
            box.Items.Clear();
            box.Items.Add(ByYears);
            box.Items.Add(ByMonth);
            box.SelectedIndex = 0;

            this.Unregister();
            this.Register();
        }

        public override void ApplyState(IReportState state)
        {
            if (state is CashFlowForecastReportState cs)
            {
                this.ReportDate = cs.ReportDate;
                this.byYear = cs.ByYear;
                if (this.panel != null)
                {
                    this.Unregister();
                    this.panel.ReportDate = this.ReportDate;
                    var box = panel.ShowReportInterval();
                    box.SelectedIndex = this.byYear ? 0 : 1;
                    this.Register();
                }
            }
        }

        public IList<Transaction> GetRecent(int years = 5, bool income = false)
        {
            DateTime today = this.ReportDate;
            DateTime start = today.AddYears(-years); // trim data older
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in this.myMoney.Transactions.GetAllTransactions())
            {
                if (t.IsDeleted || t.Status == TransactionStatus.Void || t.Account == null ||
                    t.Date < start || t.Date > today || t.Payee == null || t.Transfer != null)
                {
                    continue;
                }
                view.Add(t);
            }

            view.Sort(new TransactionComparerByDateDescending());
            return view;
        }

        public class ChartUserData
        {
            public DateTime Date { get; set; }
            public decimal PaymentAmount { get; set; }
        }
        
        public override async Task Generate(IReportWriter writer)
        {
            this.chart = new AreaChart()
            {
                Width = 1024,
                Height = 600
            };

            this.SetDefaultCurrency(writer, this.normalizedCurrency);

            this.panel.ReportDate = this.ReportDate;
            var box = panel.ShowReportInterval();
            box.SelectedIndex = this.byYear ? 0 : 1;

            this.chart.ToolTipGenerator = this.OnGenerateTip;
            var series = new TrendGraphSeries("Cash Flow");
            series.Category = new ChartCategory("Forecast", 0) { WpfColor = Color.FromRgb(0x36, 0x79, 0x41) };
            // find the cash flow for the next month
            var recent = this.GetRecent();
            var reportDate = this.ReportDate;

            writer.WriteHeading($"Cash Flow Forecast Report for " + reportDate.ToString("D"));

            this.WriteCurrencyHeading(writer, this.DefaultCurrency);

            var calc = new CostBasisCalculator(this.myMoney, reportDate);
            bool normalize = false; // todo: currency != null;
            int accountsFound = 0;
            double startingBalance = 0;
            // Now change the values to be cumulative, this means we need the bank account balances of the start date.
            foreach (var account in this.myMoney.Accounts.AllAccounts)
            {
                if (account.IsDeleted || !(account.Type == AccountType.Checking || account.Type == AccountType.Savings))
                {
                    continue;
                }
                accountsFound++;
                var transactions = this.myMoney.Transactions;
                var rows = transactions.GetTransactionsFrom(account, (t) => t.Date < reportDate);
                var balance = await transactions.GetBalance(calc, this.cache, rows, account, normalize, true);
                var amount = balance.Balance + balance.InvestmentValue;
                startingBalance += (double)this.GetNormalizedAmount(amount);
            }

            if (accountsFound == 0)
            {
                writer.WriteParagraph("No bank accounts found, so no cash flow forecast can be generated.");
                writer.WriteParagraph("Please set the account type on your accounts.");
                this.WriteTrailer(writer, reportDate);
                return;
            }

            var outgoing = Payments.FindRecurringPayments(recent, reportDate);
            var incoming = Payments.FindRecurringIncomes(recent, reportDate);
            if (outgoing.Count == 0 && incoming.Count == 0)
            {
                writer.WriteParagraph("No recurring incomes found in the last 5 years.");
                writer.WriteParagraph("Please ensure you have added categories of type Income to some of your transactions.");
                this.WriteTrailer(writer, reportDate);
                return;
            }
            var start = this.ReportDate;
            var end = start;
            if (this.byYear)
            {
                end = start.AddYears(1);
            }
            else
            {
                end = start.AddMonths(1);
            }
            List<ChartDataValue> sorted = new List<ChartDataValue>();
            foreach (var p in outgoing.Values)
            {
                while (p.NextDate < end)
                {
                    var d = p.NextDate;
                    var a = this.GetNormalizedAmount((decimal)p.GetNextPrediction());
                    var u = new ChartUserData() { Date = d, PaymentAmount = a };
                    sorted.Add(new ChartDataValue(p.Category, (double)a, u));
                }
            }

            foreach (var p in incoming.Values)
            {
                while (p.NextDate < end)
                {
                    var d = p.NextDate;
                    var a = this.GetNormalizedAmount((decimal)p.GetNextPrediction());
                    var u = new ChartUserData() { Date = d, PaymentAmount = a };
                    sorted.Add(new ChartDataValue(p.Category, (double)a, u));
                }
            }

            sorted.Sort(new ChartDataValueComparerByDate());

            // compute cumulative values
            double cumulative = startingBalance;
            var c2 = this.GetFormattedNormalizedAmount((decimal)startingBalance);
            writer.WriteParagraph($"Starting balance {c2}");
            series.Add(new ChartDataValue("Starting Balance", cumulative,
                new ChartUserData() { Date = start, PaymentAmount = 0 }));
            foreach (var v in sorted)
            {
                cumulative += v.Value;
                v.Value = cumulative;
                series.Add(v);
            }
            c2 = this.GetFormattedNormalizedAmount((decimal)cumulative);
            writer.WriteParagraph($"Ending balance {c2}");
            var data = new ChartData()
            {
                Title = "Cash Flow Forecast",
            };
            data.AddSeries(series);
            this.chart.Data = data;
            writer.WriteElement(this.chart);

            this.WriteTrailer(writer, reportDate);
        }

        private UIElement OnGenerateTip(ChartDataValue value)
        {
            var u = (ChartUserData)value.UserData;
            var tip = new StackPanel() { Orientation = Orientation.Vertical };
            tip.Children.Add(new TextBlock() { Text = value.Label, FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = "Date " + u.Date.ToString("D") });
            tip.Children.Add(new TextBlock() { Text = "Balance " + this.GetFormattedNormalizedAmount((decimal)value.Value) });
            if (u.PaymentAmount != 0)
            {
                tip.Children.Add(new TextBlock() { Text = "Amount " + this.GetFormattedNormalizedAmount(u.PaymentAmount) });
            }
            return tip;
        }

        internal class ChartDataValueComparerByDate : IComparer<ChartDataValue>
        {
            public int Compare(ChartDataValue x, ChartDataValue y)
            {
                var d1 = (ChartUserData)x.UserData;
                var d2 = (ChartUserData)y.UserData;
                return d1.Date.CompareTo(d2.Date);
            }
        }

        public override IReportState GetState()
        {
            return new CashFlowForecastReportState()
            {
                ReportDate = this.ReportDate,
                ByYear = this.byYear,
            };
        }

        public class CashFlowForecastReportState : IReportState
        {
            public StateSource Source { get; set; }
            public DateTime ReportDate { get; set; }
            public bool ByYear { get; set; }

            public CashFlowForecastReportState()
            {
            }

            public Type GetReportType()
            {
                return typeof(CashFlowForecastReport);
            }
        }

    }
}
