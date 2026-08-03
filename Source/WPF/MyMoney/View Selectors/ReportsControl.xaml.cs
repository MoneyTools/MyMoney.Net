using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Walkabout.StockQuotes;

namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for ReportsControl.xaml
    /// </summary>
    public partial class ReportsControl : UserControl
    {
        public ReportsControl()
        {
            InitializeComponent();

            ComboBox dropDown = this.CurrencyPicker;
            dropDown.Items.Add("");
            foreach (var value in Enum.GetValues(typeof(CurrencyCode)))
            {
                dropDown.Items.Add(value.ToString());
            }

            this.ConsolidationRow.Visibility = Visibility.Collapsed;
            this.ConsolidationPicker.Items.Add(ConsolidationDateAcquired);
            this.ConsolidationPicker.Items.Add(ConsolidationDateSold);
            this.ConsolidationPicker.SelectedIndex = 0;

            this.ReportTypeRow.Visibility = Visibility.Collapsed;
            this.FiscalYearRow.Visibility = Visibility.Collapsed;
            this.ReportEndDateRow.Visibility = Visibility.Collapsed;
            this.ReportIntervalRow.Visibility = Visibility.Collapsed;
        }

        public static string ConsolidationDateAcquired = "Date Acquired";
        public static string ConsolidationDateSold = "Date Sold";

        public void HideReportDateRow()
        {
            this.ReportDateRow.Visibility = Visibility.Collapsed;
        }

        public void HideNormalizeCurrencyRow()
        {
            this.NormalizeCurrencyRow.Visibility = Visibility.Collapsed;
        }

        public DateTime ReportDate
        {
            get { return this.DatePickerReportDate.SelectedDate ?? DateTime.Today; }
            set { DatePickerReportDate.SelectedDate = value; }
        }

        public event EventHandler<DateTime> ReportDateChanged;

        private void OnReportDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportDateChanged != null && e.AddedItems != null && e.AddedItems.Count > 0) 
            {
                DateTime newDate = (DateTime)e.AddedItems[0];
                ReportDateChanged(this, newDate);
            }
        }

        public string NormalizedCurrency
        {
            get { return CurrencyPicker.Text; }
            set { CurrencyPicker.SelectedItem = value; }
        }

        public event EventHandler<string> NormalizedCurrencyChanged;

        private void OnCurrencyChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NormalizedCurrencyChanged != null && e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string currency = (string)e.AddedItems[0];
                NormalizedCurrencyChanged(this, currency);
            }
        }

        public void ShowConsolidationRow()
        {
            this.ConsolidationRow.Visibility = Visibility.Visible;
        }

        public event EventHandler<string> ConsolidationChanged;

        private void OnConsolidationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConsolidationChanged != null && e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string newValue = (string)e.AddedItems[0];
                ConsolidationChanged(this, newValue);
            }
        }

        public ComboBox ShowReportTypeRow()
        {
            this.ReportTypeRow.Visibility = Visibility.Visible;
            return this.ReportTypePicker;
        }

        public event EventHandler<string> ReportTypeChanged;

        private void OnReportTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportTypeChanged != null && e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string newValue = (string)e.AddedItems[0];
                ReportTypeChanged(this, newValue);
            }
        }

        public string FiscalYear
        {
            get => this.FiscalYearPicker.SelectedItem as string;
            set => this.FiscalYearPicker.SelectedItem = value;
        }

        public ComboBox ShowFiscalYearRow()
        {
            this.FiscalYearRow.Visibility = Visibility.Visible;
            return this.FiscalYearPicker;
        }

        public event EventHandler<string> FiscalYearChanged;

        private void OnFiscalYearChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FiscalYearChanged != null && e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string newValue = (string)e.AddedItems[0];
                FiscalYearChanged(this, newValue);
            }
        }

        public DateTime ReportEndDate
        {
            get { return this.DatePickerReportEndDate.SelectedDate ?? DateTime.Today; }
            set { DatePickerReportEndDate.SelectedDate = value; }
        }

        public void ShowEndDateRow()
        {
            this.StartDatePrompt.Text = "Report start date:";
            this.ReportEndDateRow.Visibility = Visibility.Visible;
        }

        public event EventHandler<DateTime> ReportEndDateChanged;

        private void OnReportEndDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportEndDateChanged != null && e.AddedItems != null && e.AddedItems.Count > 0)
            {
                DateTime newDate = (DateTime)e.AddedItems[0];
                ReportEndDateChanged(this, newDate);
            }
        }

        public ComboBox ShowReportInterval()
        {
            this.ReportIntervalRow.Visibility = Visibility.Visible;
            return this.ReportIntervalPicker;
        }

        public string ReportInterval
        {
            get { return this.ReportIntervalPicker.SelectedItem as string; }
            set { ReportIntervalPicker.SelectedItem = value; }
        }

        public event EventHandler<string > ReportIntervalChanged;

        private void OnReportIntervalChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportIntervalChanged != null && e.AddedItems != null && e.AddedItems.Count > 0)
            {
                string newValue = (string)e.AddedItems[0];
                ReportIntervalChanged(this, newValue);
            }
        }
    }
}
