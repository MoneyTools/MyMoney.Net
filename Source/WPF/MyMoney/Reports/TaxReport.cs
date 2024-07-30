﻿using Microsoft.Win32;
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
using Walkabout.Migrate;
using Walkabout.Taxes;
using Walkabout.Utilities;
using Walkabout.Views;

namespace Walkabout.Reports
{

    //=========================================================================================
    public class TaxReport : Report
    {
        private MyMoney money;
        private DateTime startDate;
        private DateTime endDate;
        private bool consolidateOnDateSold;
        private bool capitalGainsOnly;
        private int fiscalYearStart;
        private const string FiscalPrefix = "FY ";

        public TaxReport()
        {
            this.SetStartDate(DateTime.Now.Year);
        }

        ~TaxReport()
        {
            Debug.WriteLine("TaxReport disposed!");
        }

        public int FiscalYearStart
        {
            get => this.fiscalYearStart;
            set
            {
                this.fiscalYearStart = value;
            }
        }

        public override void OnSiteChanged()
        {
            this.money = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
        }

        class TaxReportState : IReportState
        {
            public int FiscalYearStart { get; set; }
            public bool CapitalGainsOnly { get; set; }
            public bool ConsolidateOnDateSold { get; set; }
            public int TaxYear { get; set; }

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
                CapitalGainsOnly = this.capitalGainsOnly,
                ConsolidateOnDateSold = this.consolidateOnDateSold,
                TaxYear = this.startDate.Year
            };
        }

        public override void ApplyState(IReportState state)
        {
            if (state is TaxReportState taxReportState)
            {
                this.FiscalYearStart = taxReportState.FiscalYearStart;
                this.capitalGainsOnly = taxReportState.CapitalGainsOnly;
                this.consolidateOnDateSold = taxReportState.ConsolidateOnDateSold;
                this.SetStartDate(taxReportState.TaxYear);
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
            writer.WriteHeading("Tax Report For ");

            var (firstYear, lastYear) = this.money.Transactions.GetTaxYearRange(this.fiscalYearStart);

            if (writer is FlowDocumentReportWriter fwriter)
            {
                Paragraph heading = heading = fwriter.CurrentParagraph;

                ComboBox byYearCombo = new ComboBox();
                byYearCombo.Margin = new System.Windows.Thickness(5, 0, 0, 0);
                int selected = -1;
                int index = 0;
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
                byYearCombo.SelectionChanged += this.OnYearChanged;
                byYearCombo.Margin = new Thickness(10, 0, 0, 0);
                this.AddInline(heading, byYearCombo);

                /*
                <StackPanel Margin="10,5,10,5"  Grid.Row="2" Orientation="Horizontal">
                    <TextBlock Text="Consolidate securities by: " Background="Transparent"/>
                    <ComboBox x:Name="ConsolidateSecuritiesCombo" SelectedIndex="0">
                        <ComboBoxItem>Date Acquired</ComboBoxItem>
                        <ComboBoxItem>Date Sold</ComboBoxItem>
                    </ComboBox>
                </StackPanel>
                <CheckBox Margin="10,5,10,5" x:Name="CapitalGainsOnlyCheckBox" Grid.Row="3">Capital Gains Only</CheckBox>
                */

                ComboBox consolidateCombo = new ComboBox();
                consolidateCombo.Items.Add("Date Acquired");
                consolidateCombo.Items.Add("Date Sold");
                consolidateCombo.SelectedIndex = this.consolidateOnDateSold ? 1 : 0;
                consolidateCombo.SelectionChanged += this.OnConsolidateComboSelectionChanged;

                writer.WriteParagraph("Consolidate securities by: ");
                Paragraph prompt = fwriter.CurrentParagraph;
                prompt.Margin = new Thickness(0, 0, 0, 4);
                this.AddInline(prompt, consolidateCombo);

                CheckBox checkBox = new CheckBox();
                checkBox.Content = "Capital Gains Only";
                checkBox.IsChecked = this.capitalGainsOnly;
                checkBox.Checked += this.OnCapitalGainsOnlyChanged;
                checkBox.Unchecked += this.OnCapitalGainsOnlyChanged;
                writer.WriteParagraph("");
                Paragraph checkBoxParagraph = fwriter.CurrentParagraph;
                checkBoxParagraph.Inlines.Add(new InlineUIContainer(checkBox));
            }

            this.WriteCurrencyHeading(writer, this.DefaultCurrency);

            if (!this.capitalGainsOnly)
            {
                // find all tax related categories and summarize accordingly.
                this.GenerateCategories(writer);
            }
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


        private void Renerate()
        {
            var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
            _ = view.Generate(this);
        }

        private void OnCapitalGainsOnlyChanged(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            this.capitalGainsOnly = checkBox.IsChecked == true;
            this.Renerate();
        }

        private void OnConsolidateComboSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = (ComboBox)sender;
            int index = box.SelectedIndex;
            this.consolidateOnDateSold = index == 1;
            this.Renerate();
        }

        private void OnYearChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = (ComboBox)sender;
            string label = (string)box.SelectedItem;
            if (label.StartsWith(FiscalPrefix))
            {
                label = label.Substring(FiscalPrefix.Length);
            }
            if (int.TryParse(label, out int year))
            {
                this.SetStartDate(year);
                this.Renerate();
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
            List<TaxCategory> list = taxCategories.GenerateGroups(this.money, this.startDate, this.endDate);

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
            exporter.Export(filename, this.startDate, this.endDate, this.capitalGainsOnly, this.consolidateOnDateSold);
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
