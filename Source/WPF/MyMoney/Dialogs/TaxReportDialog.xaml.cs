using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for TaxReportDialog.xaml
    /// </summary>
    public partial class TaxReportDialog : Window
    {
        List<string> months = new List<string>();
        public TaxReportDialog()
        {
            InitializeComponent();

            this.Year = DateTime.Now.Year;

            this.Loaded += new RoutedEventHandler(OnLoaded);

            for (int i = 0; i < 12; i++)
            {
                var month = new DateTime(this.Year, i + 1, 1);
                var label = month.ToString("MMMM");
                this.months.Add(label);
                this.FiscalStartMonthCombo.Items.Add(label);
            }
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.YearText.Focus();
            this.YearText.SelectionStart = YearText.Text.Length;
        }

        public bool ConsolidateSecuritiesOnDateSold
        {
            get { return ConsolidateSecuritiesCombo.SelectedIndex > 0; }
            set { ConsolidateSecuritiesCombo.SelectedIndex = (value ? 1 : 0); }
        }

        public bool CapitalGainsOnly
        {
            get { return CapitalGainsOnlyCheckBox.IsChecked == true; }
            set { CapitalGainsOnlyCheckBox.IsChecked = value; }
        }

        public int Year
        {
            get
            {
                int result = 0;
                if (int.TryParse(YearText.Text, out result) && result < 100)
                {
                    result += 2000;
                }                
                return result;
            }
            set
            {
                YearText.Text = value.ToString();
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
            this.OK.IsEnabled = int.TryParse(YearText.Text, out result);
        }
    }
}
