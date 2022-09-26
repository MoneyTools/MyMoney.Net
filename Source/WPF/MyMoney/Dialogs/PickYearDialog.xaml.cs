using System;
using System.Windows;

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
            this.TaxDatePicker.Focus();
        }

        public DateTime? SelectedDate
        {
            get
            {
                return this.TaxDatePicker.SelectedDate;
            }
            set
            {
                if (value.HasValue)
                {
                    this.TaxDatePicker.SelectedDate = value.Value;
                }
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

        private void OnRemove(object sender, RoutedEventArgs e)
        {
            this.TaxDatePicker.SelectedDate = null;
        }
    }
}
