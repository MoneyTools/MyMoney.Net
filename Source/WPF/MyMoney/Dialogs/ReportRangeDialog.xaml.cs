using System;
using System.Collections;
using System.Windows;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for RenamePayeeDialog.xaml
    /// </summary>
    public partial class ReportRangeDialog : BaseDialog
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
            get { return (ReportInterval)this.comboBoxInterval.SelectedItem; }
            set { this.comboBoxInterval.SelectedItem = value; }
        }

        public bool EnableCategoriesSelection
        {
            get
            {
                return this.CategoriesPicker.Visibility == System.Windows.Visibility.Visible;
            }

            set
            {
                if (value == true)
                {
                    this.CategoriesPicker.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    this.CategoriesPicker.Visibility = System.Windows.Visibility.Collapsed;
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
            this.InitializeComponent();
            this.Owner = Application.Current.MainWindow;

            this.comboBoxInterval.Visibility = Visibility.Visible;
            this.comboBoxInterval.Items.Add(ReportInterval.Days);
            this.comboBoxInterval.Items.Add(ReportInterval.Months);
            this.comboBoxInterval.Items.Add(ReportInterval.Years);
            this.comboBoxInterval.SelectedIndex = 2;

            this.okButton.Click += new RoutedEventHandler(this.OnOkButton_Click);
        }

        bool showInterval;

        public bool ShowInterval
        {
            get => this.showInterval;
            set
            {
                if (!value)
                {
                    this.intervalPrompt.Visibility = Visibility.Collapsed;
                    this.comboBoxInterval.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.intervalPrompt.Visibility = Visibility.Visible;
                }
                this.showInterval = value;
            }
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
