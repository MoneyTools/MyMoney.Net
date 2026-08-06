using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Importers;
using Walkabout.Taxes;
using Walkabout.Utilities;
using Walkabout.Views;
using Walkabout.Views.Controls;

namespace Walkabout.Reports
{

    //=========================================================================================
    public class TaxReport : Report
    {
        private MyMoney money;
        private DatabaseSettings databaseSettings;
        private DateTime startDate;
        private DateTime endDate;
        private bool consolidateOnDateSold;
        private bool investmentsOnly;
        private int fiscalYearStart;
        private const string FiscalPrefix = "FY ";
        private ReportsControl panel;
        public static string ReportTypeInvestmentsOnly = "Investments Only";
        public static string ReportTypeAllAccounts = "All Accounts";


        public TaxReport()
        {
        }

        ~TaxReport()
        {
            Debug.WriteLine("TaxReport disposed!");
        }

        protected override void Dispose(bool disposing)
        {
            this.Unregister();
            base.Dispose(disposing);
        }

        private void Register()
        {
            panel.ConsolidationChanged += this.OnConsolidationChanged;
            panel.ReportTypeChanged += this.OnReportTypeChanged;
            panel.FiscalYearChanged += this.OnFiscalYearChanged;
        }

        private void Unregister()
        {
            if (this.panel != null)
            {
                panel.ConsolidationChanged -= this.OnConsolidationChanged;
                panel.ReportTypeChanged -= this.OnReportTypeChanged;
                panel.FiscalYearChanged -= this.OnFiscalYearChanged;
            }
        }

        public override void OnSiteChanged()
        {
            this.money = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
            this.databaseSettings = (DatabaseSettings)this.ServiceProvider.GetService(typeof(DatabaseSettings));
            this.fiscalYearStart = this.databaseSettings.FiscalYearStart;
            this.SetStartDate(DateTime.Now.Year);
            // this also makes the panel visible!
            this.Unregister();
            var panel = (ReportsControl)this.ServiceProvider.GetService(typeof(ReportsControl));
            this.panel = panel;
            panel.HideReportDateRow();
            panel.HideNormalizeCurrencyRow();
            panel.ShowConsolidationRow();
            ComboBox box = panel.ShowReportTypeRow();
            box.Items.Clear();
            box.Items.Add(ReportTypeAllAccounts);
            box.Items.Add(ReportTypeInvestmentsOnly);
            box.SelectedIndex = 0;
            this.Unregister();
            this.Register();
        }

        public class TaxReportState : IReportState
        {
            public int FiscalYearStart { get; set; }
            public bool InvestmentsOnly { get; set; }
            public bool ConsolidateOnDateSold { get; set; }
            public int ReportYear { get; set; }

            public TaxReportState()
            {
            }

            public Type GetReportType()
            {
                return typeof(TaxReport);
            }
        }

        public override IReportState GetState()
        {
            return new TaxReportState()
            {
                FiscalYearStart = this.fiscalYearStart,
                InvestmentsOnly = this.investmentsOnly,
                ConsolidateOnDateSold = this.consolidateOnDateSold,
                ReportYear = this.startDate.Year
            };
        }

        public override void ApplyState(IReportState state)
        {
            if (state is TaxReportState taxReportState)
            {
                this.fiscalYearStart = taxReportState.FiscalYearStart;
                this.investmentsOnly = taxReportState.InvestmentsOnly;
                this.consolidateOnDateSold = taxReportState.ConsolidateOnDateSold;
                this.SetStartDate(taxReportState.ReportYear);
                if (this.panel != null)
                {
                    this.Unregister();
                    ComboBox box = panel.ShowReportTypeRow();
                    box.Items.Clear();
                    box.Items.Add(ReportTypeAllAccounts);
                    box.Items.Add(ReportTypeInvestmentsOnly);
                    box.SelectedIndex = this.consolidateOnDateSold ? 0 : 1;
                    this.Register();
                }
            }
        }

        private void SetStartDate(int year)
        {
            this.startDate = new DateTime(year, this.fiscalYearStart + 1, 1);
            if (this.fiscalYearStart > 0)
            {
                // Note: "FY2020" means July 2019 to July 2020, in other words
                // it is the end date that represents the year.
                this.startDate = this.startDate.AddYears(-1);
            }
            if (this.startDate > DateTime.Today)
            {
                this.startDate = this.startDate.AddYears(-1);
            }
            this.endDate = this.startDate.AddYears(1);
        }

        public override Task Generate(IReportWriter writer)
        {
            this.DelaySaveState();
            this.fiscalYearStart = this.databaseSettings.FiscalYearStart;
            writer.WriteHeading("Tax Report For Financial Year " + this.startDate.Year);

            var (firstYear, lastYear) = this.money.Transactions.GetTaxYearRange(this.fiscalYearStart);

            var box = panel.ShowFiscalYearRow();
            this.AddFiscalYearItems(box, firstYear, lastYear);

            this.WriteCurrencyHeading(writer, this.DefaultCurrency);

            // find all tax related categories and summarize accordingly.
            this.GenerateCategories(writer);
            this.GenerateCapitalGains(writer);

            if (writer is FlowDocumentReportWriter)
            {
                var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
                FlowDocument document = view.DocumentViewer.Document;
                document.Blocks.InsertAfter(document.Blocks.FirstBlock, new BlockUIContainer(this.CreateExportTxfButton()));
            }

            return Task.CompletedTask;
        }

        private void WriteHeaders(IReportWriter writer)
        {
            writer.StartTable();
            writer.StartColumnDefinitions();
            for (int i = 0; i < 9; i++)
            {
                writer.WriteColumnDefinition("Auto", 100, double.MaxValue);
            }
            writer.EndColumnDefinitions();

            writer.StartHeaderRow();
            writer.StartCell();
            writer.WriteParagraph("Security");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Quantity");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Date Acquired");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Acquisition Price");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Cost Basis");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Date Sold");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Sale Price");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Proceeds");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Gain or Loss");
            writer.EndCell();
            writer.EndHeaderRow();
        }


        private void Regenerate()
        {
            var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
            _ = view.Generate(this);
        }

        private void OnReportTypeChanged(object sender, string e)
        {
            if (e == ReportTypeInvestmentsOnly)
            {
                this.investmentsOnly = true;
            }
            else
            {
                this.investmentsOnly = false;
            }
            this.Regenerate();
        }

        private void OnConsolidationChanged(object sender, string consolidation)
        {
            this.consolidateOnDateSold = (consolidation == ReportsControl.ConsolidationDateSold);
            this.Regenerate();
        }

        private void AddFiscalYearItems(ComboBox byYearCombo, int firstYear, int lastYear)
        {
            int selected = -1;
            int index = 0;
            byYearCombo.Items.Clear();
            for (int i = lastYear; i >= firstYear; i--)
            {
                if (this.fiscalYearStart > 0 && i == this.endDate.Year)
                {
                    selected = index;
                }
                else if (this.fiscalYearStart == 0 && i == this.startDate.Year)
                {
                    selected = index;
                }
                if (this.fiscalYearStart > 0)
                {
                    byYearCombo.Items.Add("FY " + i);
                }
                else
                {
                    byYearCombo.Items.Add(i.ToString());
                }
                index++;
            }

            if (selected != -1)
            {
                byYearCombo.SelectedIndex = selected;
            }
        }

        private void OnFiscalYearChanged(object sender, string label)
        {
            string year = label;
            if (label.StartsWith(FiscalPrefix))
            {
                year = label.Substring(FiscalPrefix.Length);
            }
            if (int.TryParse(year, out int x))
            {
                this.SetStartDate(x);
                this.Regenerate();
            }
        }

        private bool InRange(DateTime date)
        {
            return date >= this.startDate && date < this.endDate;
        }

        private decimal GetSalesTax()
        {
            decimal total = 0;

            foreach (Transaction t in this.money.Transactions)
            {
                if (this.InRange(t.Date) && !t.IsDeleted && t.Status != TransactionStatus.Void)
                {
                    total += t.NetSalesTax;
                }
            }

            return total;
        }

        private void GenerateCapitalGains(IReportWriter writer)
        {
            var calculator = new CapitalGainsTaxCalculator(this.money, this.endDate, this.consolidateOnDateSold, true);

            List<SecuritySale> errors = new List<SecuritySale>(from s in calculator.GetSales() where s.Error != null select s);

            if (errors.Count > 0)
            {
                writer.WriteHeading("Errors Found");
                foreach (SecuritySale error in errors)
                {
                    writer.WriteParagraph(error.Error.Message);
                }
            }

            if ((from u in calculator.Unknown where this.InRange(u.DateSold) select u).Any())
            {
                writer.WriteHeading("Capital Gains with Unknown Cost Basis");

                writer.StartTable();
                writer.StartColumnDefinitions();
                for (int i = 0; i < 4; i++)
                {
                    writer.WriteColumnDefinition("Auto", 100, double.MaxValue);
                }
                writer.EndColumnDefinitions();

                writer.StartHeaderRow();
                writer.StartCell();
                writer.WriteParagraph("Security");
                writer.EndCell();
                writer.StartCell();
                writer.WriteNumber("Quantity");
                writer.EndCell();
                writer.StartCell();
                writer.WriteNumber("Date Sold");
                writer.EndCell();
                writer.StartCell();
                writer.WriteNumber("Sale Price");
                writer.EndCell();
                writer.StartCell();
                writer.WriteNumber("Proceeds");
                writer.EndCell();
                writer.EndHeaderRow();

                foreach (var data in calculator.Unknown)
                {
                    if (!this.InRange(data.DateSold))
                    {
                        continue;
                    }

                    writer.StartRow();
                    writer.StartCell();
                    writer.WriteParagraph(data.Security.Name);
                    writer.EndCell();

                    writer.StartCell();
                    writer.WriteNumber(this.Rounded(data.UnitsSold, 3));
                    writer.EndCell();

                    writer.StartCell();
                    writer.WriteNumber(data.DateSold.ToShortDateString());
                    writer.EndCell();

                    writer.StartCell();
                    writer.WriteNumber(this.GetFormattedNormalizedAmount(data.SalePricePerUnit));
                    writer.EndCell();

                    writer.StartCell();
                    writer.WriteNumber(this.GetFormattedNormalizedAmount(data.SaleProceeds));
                    writer.EndCell();
                    writer.EndRow();
                }

                writer.EndTable();
            }

            if (calculator.ShortTerm.Count > 0)
            {
                decimal total = 0;
                writer.WriteHeading("Short Term Capital Gains and Losses");
                this.WriteHeaders(writer);
                foreach (var data in calculator.ShortTerm)
                {
                    if (!this.InRange(data.DateSold))
                    {
                        continue;
                    }
                    if (data.Account.IsTaxDeferred || data.Account.IsTaxFree)
                    {
                        continue;
                    }

                    this.WriteCapitalGains(writer, data);
                    total += data.TotalGain;
                }
                this.WriteCapitalGainsTotal(writer, total);
                writer.EndTable();
            }

            if (calculator.LongTerm.Count > 0)
            {
                decimal total = 0;
                writer.WriteHeading("Long Term Capital Gains and Losses");
                this.WriteHeaders(writer);
                foreach (var data in calculator.LongTerm)
                {
                    if (!this.InRange(data.DateSold))
                    {
                        continue;
                    }
                    if (data.Account.IsTaxDeferred || data.Account.IsTaxFree)
                    {
                        continue;
                    }

                    this.WriteCapitalGains(writer, data);
                    total += data.TotalGain;
                }
                this.WriteCapitalGainsTotal(writer, total);
            }
            writer.EndTable();
        }

        private void WriteCapitalGainsTotal(IReportWriter writer, decimal total)
        {
            writer.StartHeaderRow();
            writer.StartCell();
            writer.WriteParagraph("Total");
            writer.EndCell();

            writer.StartCell();
            writer.EndCell();

            writer.StartCell();
            writer.EndCell();

            writer.StartCell();
            writer.EndCell();

            writer.StartCell();
            writer.EndCell();

            writer.StartCell();
            writer.EndCell();

            writer.StartCell();
            writer.EndCell();

            writer.StartCell();
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(this.GetFormattedNormalizedAmount(GiveUpTheFractionalPennies(total)));
            writer.EndCell();

            writer.EndHeaderRow();
        }

        private void WriteCapitalGains(IReportWriter writer, SecuritySale data)
        {
            writer.StartRow();
            writer.StartCell();
            writer.WriteParagraph(data.Security.Name);
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(this.Rounded(data.UnitsSold, 3));
            writer.EndCell();

            writer.StartCell();
            if (data.DateAcquired == null)
            {
                writer.WriteNumber("VARIOUS");
            }
            else
            {
                writer.WriteNumber(data.DateAcquired.Value.ToShortDateString());
            }
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(this.GetFormattedNormalizedAmount(data.CostBasisPerUnit));
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(this.GetFormattedNormalizedAmount(data.TotalCostBasis));
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(data.DateSold.ToShortDateString());
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(this.GetFormattedNormalizedAmount(data.SalePricePerUnit));
            writer.EndCell();

            writer.StartCell();
            if (data.SaleProceeds == 0)
            {
                Debug.WriteLine("???");
            }
            writer.WriteNumber(this.GetFormattedNormalizedAmount(data.SaleProceeds));
            writer.EndCell();

            writer.StartCell();
            writer.WriteNumber(this.GetFormattedNormalizedAmount(GiveUpTheFractionalPennies(data.TotalGain)));
            writer.EndCell();

            writer.EndRow();

        }

        private void GenerateCategories(IReportWriter writer)
        {
            TaxCategoryCollection taxCategories = new TaxCategoryCollection();
            List<TaxCategory> list = taxCategories.GenerateGroups(this.money, this.investmentsOnly, this.startDate, this.endDate);

            if (list == null)
            {
                writer.WriteParagraph("You have not associated any categories with tax categories.");
                writer.WriteParagraph("Please use the Category Properties dialog to associate tax categories then try again.");
                return;
            }

            writer.WriteHeading("Tax Categories");
            writer.StartTable();

            writer.StartColumnDefinitions();
            writer.WriteColumnDefinition("auto", 100, double.MaxValue);
            writer.WriteColumnDefinition("auto", 100, double.MaxValue);
            writer.WriteColumnDefinition("auto", 100, double.MaxValue);
            writer.EndColumnDefinitions();
            writer.StartHeaderRow();
            writer.StartCell();
            writer.WriteParagraph("Category");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Amount");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber("Tax Excempt");
            writer.EndCell();
            writer.EndHeaderRow();

            decimal tax = this.GetSalesTax();

            writer.StartRow();
            writer.StartCell();
            writer.WriteParagraph("Sales Tax");
            writer.EndCell();
            writer.StartCell();
            writer.WriteNumber(this.GetFormattedNormalizedAmount(tax), FontStyles.Normal, FontWeights.Bold, null);
            writer.EndCell();
            writer.EndRow();

            foreach (TaxCategory tc in list)
            {
                writer.StartHeaderRow();
                writer.StartCell();
                writer.WriteParagraph(tc.Name);
                writer.EndCell();
                writer.StartCell();
                writer.EndCell();
                writer.EndHeaderRow();

                decimal sum = 0;
                IDictionary<string, List<Transaction>> groups = tc.Groups;
                foreach (KeyValuePair<string, List<Transaction>> subtotal in groups)
                {
                    writer.StartRow();
                    writer.StartCell();
                    writer.WriteParagraph(subtotal.Key);
                    writer.EndCell();

                    decimal value = 0;
                    decimal taxExempt = 0;
                    foreach (Transaction t in subtotal.Value)
                    {
                        var amount = t.Amount;
                        if (t.Investment != null && t.Investment.Security != null && t.Investment.Security.Taxable == YesNo.No)
                        {
                            taxExempt += amount;
                        }
                        else
                        {
                            value += amount;
                        }
                    }

                    if (tc.DefaultSign < 0)
                    {
                        value *= -1;
                    }

                    writer.StartCell();
                    writer.WriteNumber(this.GetFormattedNormalizedAmount(value));
                    writer.EndCell();

                    writer.StartCell();
                    if (taxExempt > 0)
                    {
                        writer.WriteNumber(this.GetFormattedNormalizedAmount(taxExempt));
                    }
                    writer.EndCell();
                    writer.EndRow();
                    sum += value;
                }

                writer.StartRow();
                writer.StartCell();
                writer.EndCell();
                writer.StartCell();
                writer.WriteNumber(this.GetFormattedNormalizedAmount(sum), FontStyles.Normal, FontWeights.Bold, null);
                writer.EndCell();
                writer.EndRow();

            }

            writer.EndTable();
        }

        private string Rounded(decimal value, int decimals)
        {
            decimal rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            // for some odd reason decimal.ToString() always adds 3 decimal places so you get "23.000" instead of "23".
            double d = (double)rounded;
            return d.ToString();
        }

        /// <summary>
        /// In order to not owe the IRS anything, we want to round up the numbers and not mess with the half pennies.
        /// Technically we could file a rounding adjustment, but for a few pennies it's not worth the effort.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        private static decimal GiveUpTheFractionalPennies(decimal x)
        {
            return Math.Ceiling(x * 100) / 100;
        }


        public override void Export(string filename)
        {
            TxfExporter exporter = new TxfExporter(this.money);
            exporter.Export(filename, this.startDate, this.endDate, this.investmentsOnly, this.consolidateOnDateSold);
        }


        private Button CreateExportTxfButton()
        {
            Button button = this.CreateReportButton("Icons/TurboTax.png", "Export", "Export .txf file format for TuboTax");

            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(10);

            button.Click += new RoutedEventHandler((s, args) =>
            {
                this.OnExportTaxInfoAsTxf();
            });
            return button;
        }

        private void OnExportTaxInfoAsTxf()
        {
            SaveFileDialog fd = new SaveFileDialog();
            fd.CheckPathExists = true;
            fd.AddExtension = true;
            fd.Filter = "TXF File (.txf)|*.txf";
            if (this.fiscalYearStart > 0)
            {
                fd.FileName = "TaxFY" + this.startDate.Year;
            }
            else
            {
                fd.FileName = "Tax" + this.startDate.Year;
            }

            if (fd.ShowDialog(App.Current.MainWindow) == true)
            {
                try
                {
                    string filename = fd.FileName;
                    this.Export(filename);
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Error Exporting .txf", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
