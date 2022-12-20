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
            decimal pay;
            double inflation;
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

                this.ButtonOk.IsEnabled = templateExists && (!string.IsNullOrEmpty(this.Employer) && decimal.TryParse(this.TextBoxPaycheck.Text, out pay) &&
                    double.TryParse(this.TextBoxInflation.Text.Replace("%", string.Empty), out inflation));
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

        private void TextBoxEmployer_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.EnableButtons();
        }

        private void TextBoxPaycheck_TextChanged(object sender, TextChangedEventArgs e)
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
