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
            this.InitializeComponent();
            Loaded += new RoutedEventHandler(this.OnLoaded);
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
            this.DatePicker.Focus();
        }

        public DateTime? SelectedDate
        {
            get
            {
                return this.DatePicker.SelectedDate;
            }
            set
            {
                if (value.HasValue)
                {
                    this.DatePicker.SelectedDate = value.Value;
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
            this.DatePicker.SelectedDate = null;
        }
    }
}
