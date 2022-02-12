using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Walkabout.Data;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for CategoryTransferDialog.xaml
    /// </summary>
    public partial class RecategorizeDialog : Window
    {
        MyMoney myMoney;
        Category from;

        public RecategorizeDialog(MyMoney money)
        {
            InitializeComponent();
            this.myMoney = money;

            var source = new ListCollectionView(((List<Category>)myMoney.Categories.SortedCategories).ToArray()); 
            ComboToCategory.ItemsSource = source;
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
        
        public Category FromCategory
        {
            get { return from; }
            set { from = value; ComboFromCategory.Text = value.GetFullName(); }
        }

        public Category ToCategory
        {
            get { return ComboToCategory.SelectedItem as Category; }
            set { ComboToCategory.SelectedItem = value; }
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
