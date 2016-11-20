using System.Data;
using System.Windows;
using Microsoft.Win32;
using Walkabout.Data;
using Walkabout.Controls;
using System;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for FreeStyleQueryDialog.xaml
    /// </summary>
    public partial class FreeStyleQueryDialog : Window
    {
        IDatabase database;
        MyMoney myMoney;
        DataSet dataSet = new DataSet();

        public FreeStyleQueryDialog(MyMoney m, IDatabase database)
        {
            this.database = database;
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            myMoney = m;
        }

        public string Query
        {
            get { return textBoxQuery.Text; }
            set { textBoxQuery.Text = value; }
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
                if (dataSet != null)
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
