using System;
using System.Collections;
using System.Windows;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Reports;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for RenamePayeeDialog.xaml
    /// </summary>
    public partial class ReportRangeDialog : Window
    {
        #region PROPERTIES

        public DateTime StartDate
        {
            get { return this.dateTimePicker1.SelectedDate ?? DateTime.Now; }
            set { this.dateTimePicker1.SelectedDate = value; }
        }

        public DateTime EndDate
        {
            get { return this.dateTimePicker2.SelectedDate ?? DateTime.Now; }
            set { this.dateTimePicker2.SelectedDate = value; }
        }

        public ReportInterval Interval
        {
            get { return (ReportInterval)comboBox1.SelectedItem; }
            set { comboBox1.SelectedItem = value; }
        }

        public bool EnableCategoriesSelection
        {
            get 
            {
                return CategoriesPicker.Visibility == System.Windows.Visibility.Visible;
            }

            set 
            {
                if (value == true)
                {
                    CategoriesPicker.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    CategoriesPicker.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }


        public IList Categories
        {
            get
            {
                ArrayList result = new ArrayList();
                foreach (CheckItem ci in this.checkedListBox1.Items)
                {
                    if (ci.IsChecked)
                    {
                        result.Add(ci.Content);
                    }
                }
                return result;
            }
            set
            {
                this.checkedListBox1.Items.Clear();
                foreach (Category c in value)
                {
                    CheckItem ci = new CheckItem() { IsChecked = true, Content = c };
                    this.checkedListBox1.Items.Add(ci);
                }
            }
        }
        #endregion



        /// <summary>
        /// Constructor
        /// </summary>
        public ReportRangeDialog()
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;

            comboBox1.Items.Add(ReportInterval.Days);
            comboBox1.Items.Add(ReportInterval.Months);
            comboBox1.Items.Add(ReportInterval.Years);
            comboBox1.SelectedIndex = 2;

            okButton.Click += new RoutedEventHandler(OnOkButton_Click);
        }



        void OnOkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

      
    }

    class CheckItem
    {
        public bool IsChecked { get; set; }
        public object Content { get; set; }
    }
}
