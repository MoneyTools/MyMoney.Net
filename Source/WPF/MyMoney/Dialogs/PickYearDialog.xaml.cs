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
    public partial class PickYearDialog : BaseDialog
    {
        public PickYearDialog()
        {
            InitializeComponent();

            int now = DateTime.Now.Year;

            for(int year = now - 10; year < now + 1; year++)
            {
                this.YearCombo.Items.Add(year);
                if (year == now)
                {
                    this.YearCombo.SelectedItem = year;
                }
            }

            this.Loaded += new RoutedEventHandler(OnLoaded);
        }

        public void SetPrompt(string text)
        {
            this.Prompt.Text = text;
        }

        public void SetTitle(string title)
        {
            this.Title = title;
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.YearCombo.Focus();
        }

        public int SelectedYear
        {
            get
            {
                if (YearCombo.SelectedItem is int year)
                {
                    return year;
                }
                return -1;
            }
            set
            {
                if (!YearCombo.Items.Contains(value))
                {
                    if (value < (int)this.YearCombo.Items[0])
                    {
                        // prepend more values!
                        for (int year = (int)this.YearCombo.Items[0] - 1; year > value - 5; year--)
                        {
                            this.YearCombo.Items.Insert(0, year);
                        }
                    }
                    else
                    {
                        // append more values!
                        for(int year = (int)this.YearCombo.Items[this.YearCombo.Items.Count - 1]; year <= value + 1; year++)
                        {
                            this.YearCombo.Items.Add(year);
                        }
                    }
                    
                }
                YearCombo.SelectedItem = value;
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
    }
}
