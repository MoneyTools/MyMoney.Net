using Microsoft.Win32;
using System;
using System.Data;
using System.Windows;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for FreeStyleQueryDialog.xaml
    /// </summary>
    public partial class FreeStyleQueryDialog : BaseDialog
    {
        private readonly IDatabase database;
        private readonly MyMoney myMoney;
        private DataSet dataSet = new DataSet();

        public FreeStyleQueryDialog(MyMoney m, IDatabase database)
        {
            this.database = database;
            this.InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.myMoney = m;
        }

        public string Query
        {
            get { return this.textBoxQuery.Text; }
            set { this.textBoxQuery.Text = value; }
        }


        private void OnMenuItem_RunQuery_Click(object sender, RoutedEventArgs e)
        {
            string query = this.textBoxQuery.Text.Trim();
            if (query.Length == 0)
            {
                MessageBoxEx.Show("Please enter a SQL query", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                this.dataSet = this.database.QueryDataSet(query);
                if (this.dataSet != null)
                {

                    this.dataGrid1.ItemsSource = this.dataSet.Tables["Results"].DefaultView;

                    //                this.dataGrid1.ItemsSource = this.dataSet.Tables["Results"].GetEnumerator();
                }
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnMenuItemSave_Clicked(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sd = new SaveFileDialog();
            sd.Filter = Properties.Resources.XmlFileFilter;
            if (sd.ShowDialog(this) == true)
            {
                this.dataSet.WriteXml(sd.OpenFile(), XmlWriteMode.WriteSchema);
            }
        }


    }
}
