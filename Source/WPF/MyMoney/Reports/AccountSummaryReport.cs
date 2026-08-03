using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Security.Policy;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Xml.Linq;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.StockQuotes;
using Walkabout.Views;
using Walkabout.Views.Controls;

namespace Walkabout.Reports
{
    public class AccountSummaryReport : Report
    {
        private MyMoney myMoney;
        private FlowDocumentView view;
        private DateTime reportDate;
        private bool generating;
        private StockQuoteCache cache;
        private string normalizeCurrency;
        private ReportsControl panel;

        /// <summary>
        /// Create new AccountSummaryReport
        /// </summary>
        public AccountSummaryReport(FlowDocumentView view, ReportsControl panel)
        {
            this.view = view;
            this.reportDate = DateTime.Today;
            this.panel = panel;
            panel.ReportDate = this.reportDate;
            panel.ReportDateChanged += this.OnReportDateChanged;
            panel.NormalizedCurrencyChanged += this.OnNormalizedCurrencyChanged;
        }

        private void OnReportDateChanged(object sender, DateTime e)
        {
            this.reportDate = e;
            this.Regenerate();
        }

        private void OnNormalizedCurrencyChanged(object sender, string e)
        {
            this.normalizeCurrency = e;
            this.Regenerate();
        }

        public event EventHandler<Account> SelectAccount;

        ~AccountSummaryReport()
        {
            Debug.WriteLine("AccountSummaryReport disposed!");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                panel.ReportDateChanged -= this.OnReportDateChanged;
                panel.NormalizedCurrencyChanged -= this.OnNormalizedCurrencyChanged;
            }
            base.Dispose(disposing);
        }

        public DateTime ReportDate
        {
            get => reportDate;
            set => reportDate = value;
        }

        public override void OnSiteChanged()
        {
            this.cache = (StockQuoteCache)this.ServiceProvider.GetService(typeof(StockQuoteCache));
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
        }

        class AccountSummaryReportState : IReportState
        {
            public DateTime ReportDate { get; set; }
            public string NormalizeCurrency { get; set; }

            public AccountSummaryReportState()
            {
            }

            public Type GetReportType()
            {
                return typeof(AccountSummaryReport);
            }
        }

        public override IReportState GetState()
        {
            return new AccountSummaryReportState()
            {
                ReportDate = this.reportDate,
                NormalizeCurrency = this.normalizeCurrency,
            };
        }

        public override void ApplyState(IReportState state)
        {
            if (state is AccountSummaryReportState reportState)
            {
                this.reportDate = reportState.ReportDate;
                this.normalizeCurrency = reportState.NormalizeCurrency;
            }
        }

        public override Task Generate(IReportWriter writer)
        {
            this.generating = true;
            try
            {
                return this.InternalGenerate(writer);
            }
            finally
            {
                this.generating = false;
            }
        }

        private async Task InternalGenerate(IReportWriter writer)
        {
            await Task.CompletedTask;
            if (myMoney == null) return;

            if (this.reportDate == DateTime.MinValue)
            {
                this.reportDate = DateTime.Now;
            }

            this.commonSymbol = null;
            var calc = new CostBasisCalculator(this.myMoney, this.reportDate);
            writer.WriteHeading("Account Summary as of " + this.reportDate.ToString("D"));

            this.SetDefaultCurrency(writer, this.normalizeCurrency);
            Currency currency = this.DefaultCurrency;

            writer.StartTable();
            writer.StartColumnDefinitions();
            writer.WriteColumnDefinition("400", 400, 400);
            writer.WriteColumnDefinition("100", 100, 100);
            writer.WriteColumnDefinition("100", 100, 100);
            writer.EndColumnDefinitions();

            decimal total = 0;
            foreach (var type in Enum.GetValues(typeof(AccountType)))
            {
                if (type is AccountType atype && atype != AccountType.CategoryFund)
                {
                    total += await this.WriteAccountSummary(writer, calc, currency, atype);
                }
            }
            if (total != 0 && !string.IsNullOrEmpty(this.commonSymbol))
            {
                WriteHeader(writer, "Total");
                this.WriteRow(writer, "All Accounts", total, this.commonSymbol, null);
            }
            writer.EndTable();

            this.WriteTrailer(writer, this.reportDate);
        }

        private static void WriteHeader(IReportWriter writer, string caption)
        {
            writer.StartHeaderRow();
            writer.StartCell(1, 3);
            writer.WriteParagraph(caption);
            writer.EndCell();
            writer.EndHeaderRow();
        }

        string commonSymbol;

        private async Task<decimal> WriteAccountSummary(IReportWriter writer, CostBasisCalculator calc, Currency currency, AccountType type)
        {
            var accounts = myMoney.Accounts.GetAccountsByType(type, false);
            decimal normalizedTotal = 0;
            bool various = false;
            bool heading = true;
            if (accounts.Count > 0)
            {
                bool normalize = currency != null;

                foreach (Account account in accounts)
                {
                    var transactions = this.myMoney.Transactions;
                    var rows = transactions.GetTransactionsFrom(account);
                    var balance = await transactions.GetBalance(calc, this.cache, rows, account, normalize, false);                    
                    var amount = balance.Balance + balance.InvestmentValue;

                    string symbol;
                    if (currency != null && currency.Ratio != 0)
                    {
                        symbol = currency.Symbol;
                        amount = account.GetNormalizedAmount(amount) / currency.Ratio;
                    }
                    else
                    {
                        symbol = account.GetCurrency().Symbol;                               
                    }

                    normalizedTotal += amount;
                    if (this.commonSymbol == null)
                    {
                        this.commonSymbol = symbol;
                    }
                    else if (this.commonSymbol != symbol)
                    {
                        various = true;
                    }

                    if (amount != 0)
                    {
                        if (heading)
                        {
                            WriteHeader(writer, type.ToString() + " Accounts");
                            heading = false;
                        }
                        this.WriteRow(writer, account.Name, amount, symbol, () => this.OnSelectAccount(account));
                    }
                }
            }
            if (!heading)
            {
                // create a gap
                WriteHeader(writer, "");
            }
            if (various)
            {
                normalizedTotal = 0;
            }
            return normalizedTotal;
        }

        private void OnSelectAccount(Account account)
        {
            if (SelectAccount != null)
            {
                SelectAccount(this, account);
            }
        }

        private void WriteRow(IReportWriter writer, string name, decimal balance, string currency, Action hyperlink)
        {
            writer.StartRow();
            writer.StartCell();
            if (hyperlink != null)
            {
                writer.WriteHyperlink(name, FontStyles.Normal, FontWeights.Normal, (s, e) => hyperlink());
            }
            else
            {
                writer.WriteParagraph(name);
            }
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber(balance.ToString("C2"));
            writer.EndCell();
            writer.StartCell();
            if (writer is FlowDocumentReportWriter fwriter)
            {
                fwriter.WriteParagraph(currency, FontStyles.Normal, FontWeights.Normal, null, 12.0);
            }
            else
            {
                writer.WriteParagraph(currency);
            }
            writer.EndCell();
            writer.EndRow();
        }

        private async void Regenerate()
        {
            await this.view.Generate(this);
        }

    }
}