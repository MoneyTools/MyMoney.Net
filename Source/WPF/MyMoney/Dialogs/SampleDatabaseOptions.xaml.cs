using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for SampleDatabaseOptions.xaml
    /// </summary>
    public partial class SampleDatabaseOptions : BaseDialog
    {
        public SampleDatabaseOptions()
        {
            this.InitializeComponent();
            this.EnableButtons();
        }

        private void EnableButtons()
        {
            if (this.ButtonOk != null)
            {
                this.Message.Text = "";

                bool templateExists = false;
                if (!string.IsNullOrEmpty(this.TextBoxTemplate.Text))
                {
                    templateExists = File.Exists(this.TextBoxTemplate.Text);
                    if (!templateExists)
                    {
                        this.Message.Text = "Template file not found";
                    }
                }

                if (!int.TryParse(this.TextBoxYears.Text, out int years))
                {
                    this.Message.Text = "Years must be a valid integer";
                }
                else if (!decimal.TryParse(this.TextBoxPaycheck.Text, out decimal pay))
                {
                    this.Message.Text = "Paycheck must be a valid decimal";
                }
                else if (!double.TryParse(this.TextBoxInflation.Text.Replace("%", string.Empty), out double inflation))
                {
                    this.Message.Text = "Inflation must be a valid decimal";
                }

                this.ButtonOk.IsEnabled = templateExists && !string.IsNullOrEmpty(this.Employer) &&
                    string.IsNullOrEmpty(this.Message.Text);
            }
        }

        public string Employer
        {
            get { return this.TextBoxEmployer.Text; }
        }

        public decimal PayCheck
        {
            get
            {
                decimal pay = 0;
                decimal.TryParse(this.TextBoxPaycheck.Text, out pay);
                return pay;
            }
        }

        public double Inflation
        {
            get
            {
                double inflation = 0;
                double.TryParse(this.TextBoxInflation.Text.Replace("%", string.Empty), out inflation);
                return inflation;
            }
        }

        public int Years
        {
            get
            {
                int years = 0;
                int.TryParse(this.TextBoxYears.Text, out years);
                return years;
            }
        }

        private void TextBoxEmployer_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.EnableButtons();
        }

        private void TextBoxPaycheck_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.EnableButtons();
        }

        private void TextBoxYears_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.EnableButtons();
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.TextBoxEmployer.Focus();
        }

        private void TextBoxInflation_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.EnableButtons();
        }

        private void TextBoxTemplate_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.EnableButtons();
        }

        public string SampleData
        {
            get { return this.TextBoxTemplate.Text; }
            set { this.TextBoxTemplate.Text = value; }
        }

        private void OnBrowseTemplate(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.FileName = this.SampleData;
            fd.CheckFileExists = true;
            if (fd.ShowDialog(this) == true)
            {
                this.SampleData = fd.FileName;
            }
        }
    }
}
