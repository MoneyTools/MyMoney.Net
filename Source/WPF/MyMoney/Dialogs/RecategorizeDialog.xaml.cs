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
    public partial class RecategorizeDialog : BaseDialog
    {
        private MyMoney myMoney;
        private Category from;

        public RecategorizeDialog(MyMoney money)
        {
            this.InitializeComponent();
            this.myMoney = money;

            var source = new ListCollectionView(((List<Category>)this.myMoney.Categories.SortedCategories).ToArray());
            this.ComboToCategory.ItemsSource = source;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.Commit();
            }
            else if (e.Key == Key.Escape)
            {
                this.Cancel();
            }
            base.OnPreviewKeyDown(e);
        }

        public Category FromCategory
        {
            get { return this.from; }
            set { this.from = value; this.ComboFromCategory.Text = value?.GetFullName(); }
        }

        public Category ToCategory
        {
            get { return this.ComboToCategory.SelectedItem as Category; }
            set { this.ComboToCategory.SelectedItem = value; }
        }

        private void buttonOk_Click(object sender, RoutedEventArgs e)
        {
            this.Commit();
        }

        private void Commit()
        {
            this.DialogResult = true;
            this.Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Cancel();
        }

        private void Cancel()
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
