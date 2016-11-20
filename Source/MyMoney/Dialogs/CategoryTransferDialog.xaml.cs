using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Walkabout.Data;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for CategoryTransferDialog.xaml
    /// </summary>
    public partial class CategoryTransferDialog : Window
    {
        public CategoryTransferDialog()
        {
            InitializeComponent();
            TextBoxAmount.Loaded += new RoutedEventHandler(TextBoxAmount_Loaded);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Commit();
            }
            else if (e.Key == Key.Escape)
            {
                Cancel();
            }
            base.OnPreviewKeyDown(e);
        }

        void TextBoxAmount_Loaded(object sender, RoutedEventArgs e)
        {
            TextBoxAmount.Focus();
        }

        public List<Category> Categories
        {
            set
            {
                ComboFromCategory.ItemsSource = value;
                ComboToCategory.ItemsSource = value;
            }
        }

        public Category FromCategory
        {
            get { return ComboFromCategory.SelectedItem as Category; }
            set { ComboFromCategory.SelectedItem = value; }
        }

        public Category ToCategory
        {
            get { return ComboToCategory.SelectedItem as Category; }
            set { ComboToCategory.SelectedItem = value; }
        }

        public decimal Amount
        {
            get
            {
                decimal amount = 0;
                if (decimal.TryParse(TextBoxAmount.Text, out amount))
                {
                    return amount;
                }
                return 0;
            }
            set { TextBoxAmount.Text = value.ToString("N2"); }
        }

        private void buttonOk_Click(object sender, RoutedEventArgs e)
        {
            Commit();
        }

        private void Commit()
        {
            this.DialogResult = true;
            this.Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void Cancel()
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
