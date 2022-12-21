using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for TaxReportDialog.xaml
    /// </summary>
    public partial class TaxReportDialog : BaseDialog
    {
        private List<string> months = new List<string>();
        public TaxReportDialog()
        {
            this.InitializeComponent();

            this.Year = DateTime.Now.Year;

            Loaded += new RoutedEventHandler(this.OnLoaded);

            for (int i = 0; i < 12; i++)
            {
                var month = new DateTime(this.Year, i + 1, 1);
                var label = month.ToString("MMMM");
                this.months.Add(label);
                this.FiscalStartMonthCombo.Items.Add(label);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.YearText.Focus();
            this.YearText.SelectionStart = this.YearText.Text.Length;
        }

        public bool ConsolidateSecuritiesOnDateSold
        {
            get { return this.ConsolidateSecuritiesCombo.SelectedIndex > 0; }
            set { this.ConsolidateSecuritiesCombo.SelectedIndex = value ? 1 : 0; }
        }

        public bool CapitalGainsOnly
        {
            get { return this.CapitalGainsOnlyCheckBox.IsChecked == true; }
            set { this.CapitalGainsOnlyCheckBox.IsChecked = value; }
        }

        public int Year
        {
            get
            {
                int result = 0;
                if (int.TryParse(this.YearText.Text, out result) && result < 100)
                {
                    result += 2000;
                }
                return result;
            }
            set
            {
                this.YearText.Text = value.ToString();
            }
        }

        public int Month
        {
            get { return this.FiscalStartMonthCombo.SelectedIndex; }
            set { this.FiscalStartMonthCombo.SelectedIndex = Math.Min(11, Math.Max(0, value)); }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void YearText_TextChanged(object sender, TextChangedEventArgs e)
        {
            int result = 0;
            this.OK.IsEnabled = int.TryParse(this.YearText.Text, out result);
        }
    }
}
