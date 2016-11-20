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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;

namespace Walkabout.Charts
{
    /// <summary>
    /// Interaction logic for OvertimeChartControl.xaml
    /// </summary>
    public partial class RentalChart : UserControl
    {
        public List<RentalData> ProfitsAndLostEntries = new List<RentalData>();

        public RentalChart()
        {
            InitializeComponent();
        }

        double highestMarkValue = 0;

        public void RenderChart()
        {
            MaingGrid.Children.Clear();
            MaingGrid.ColumnDefinitions.Clear();

            SetHightValue();

            int columnIndex = 0;
            foreach (RentalData pl in ProfitsAndLostEntries)
            {
                ColumnDefinition cd = new ColumnDefinition();
                cd.Width = new GridLength(100, GridUnitType.Star);
                MaingGrid.ColumnDefinitions.Add(cd);

                ColumnDefinition seperator = new ColumnDefinition();
                seperator.Width = new GridLength(10, GridUnitType.Star);
                MaingGrid.ColumnDefinitions.Add(seperator);

                RentalChartColumn rentalChartColumn = new RentalChartColumn();
                rentalChartColumn.MyRentalData = pl;

                Grid.SetColumn(rentalChartColumn, columnIndex);
                MaingGrid.Children.Add(rentalChartColumn);
                columnIndex+=2;
            }

            UpdateChartLayout();
        }

        private void SetHightValue()
        {
            highestMarkValue = 0;

            foreach (RentalData pl in ProfitsAndLostEntries)
            {
                highestMarkValue = Math.Max(this.highestMarkValue, pl.Income);
                highestMarkValue = Math.Max(this.highestMarkValue, pl.Expense);
            }
        }

        private void MaingGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateChartLayout();
        }

        int whatExpenseIsAtTheBottom = 0;

        private void UpdateChartLayout()
        {
            foreach (RentalChartColumn pfcol in this.MaingGrid.Children)
            {
                double maxHeight = this.ActualHeight * .90;
                pfcol.BarForIncome.Height = pfcol.MyRentalData.Income / this.highestMarkValue * maxHeight;
                pfcol.BarForExpense.Height = pfcol.MyRentalData.Expense / this.highestMarkValue * maxHeight;
                pfcol.SetExpensesDistribution(whatExpenseIsAtTheBottom);
            }
        }

        private void MaingGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            whatExpenseIsAtTheBottom--;
            if (whatExpenseIsAtTheBottom <0)
            {
                whatExpenseIsAtTheBottom = 4;
            }
            UpdateChartLayout();
        }
    }

   


}
