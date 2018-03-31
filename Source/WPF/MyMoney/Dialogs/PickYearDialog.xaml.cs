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
    /// Interaction logic for PickYearDialog.xaml
    /// </summary>
    public partial class PickYearDialog : Window
    {
        public PickYearDialog()
        {
            InitializeComponent();

            this.Year = DateTime.Now.Year;

            this.Loaded += new RoutedEventHandler(OnLoaded);
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.YearText.Focus();
            this.YearText.SelectionStart = YearText.Text.Length;
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
