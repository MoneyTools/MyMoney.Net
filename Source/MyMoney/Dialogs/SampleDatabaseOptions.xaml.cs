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
using Microsoft.Win32;
using System.IO;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for SampleDatabaseOptions.xaml
    /// </summary>
    public partial class SampleDatabaseOptions : BaseDialog
    {
        public SampleDatabaseOptions()
        {
            InitializeComponent();
            EnableButtons();
        }

        private void EnableButtons()
        {
            decimal pay;
            double inflation;            
            if (ButtonOk != null)
            {
                Message.Text = "";

                bool templateExists = false;
                if (!string.IsNullOrEmpty(TextBoxTemplate.Text))
                {
                    templateExists = File.Exists(TextBoxTemplate.Text);
                    if (!templateExists)
                    {
                        Message.Text = "Template file not found";
                    }
                }

                ButtonOk.IsEnabled = templateExists && (!string.IsNullOrEmpty(Employer) && decimal.TryParse(TextBoxPaycheck.Text, out pay) && 
                    double.TryParse(TextBoxInflation.Text.Replace("%", string.Empty), out inflation));
            }
        }

        public string Employer
        {
            get { return TextBoxEmployer.Text; }
        }

        public decimal PayCheck
        {
            get {
                decimal pay = 0;
                decimal.TryParse(TextBoxPaycheck.Text, out pay);
                return pay;
            }
        }

        public double Inflation
        {
            get
            {
                double inflation = 0;
                double.TryParse(TextBoxInflation.Text.Replace("%", string.Empty), out inflation);
                return inflation;
            }
        }

        private void TextBoxEmployer_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableButtons();
        }

        private void TextBoxPaycheck_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableButtons();
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TextBoxEmployer.Focus();
        }

        private void TextBoxInflation_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableButtons();
        }

        private void TextBoxTemplate_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableButtons();
        }

        public string SampleData 
        {
            get { return TextBoxTemplate.Text; }
            set { TextBoxTemplate.Text = value; }
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
