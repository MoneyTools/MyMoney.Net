using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            this.InitializeComponent();
        }

        private double highestMarkValue = 0;

        public void RenderChart()
        {
            this.MaingGrid.Children.Clear();
            this.MaingGrid.ColumnDefinitions.Clear();

            this.SetHightValue();

            int columnIndex = 0;
            foreach (RentalData pl in this.ProfitsAndLostEntries)
            {
                ColumnDefinition cd = new ColumnDefinition();
                cd.Width = new GridLength(100, GridUnitType.Star);
                this.MaingGrid.ColumnDefinitions.Add(cd);

                ColumnDefinition seperator = new ColumnDefinition();
                seperator.Width = new GridLength(10, GridUnitType.Star);
                this.MaingGrid.ColumnDefinitions.Add(seperator);

                RentalChartColumn rentalChartColumn = new RentalChartColumn();
                rentalChartColumn.MyRentalData = pl;

                Grid.SetColumn(rentalChartColumn, columnIndex);
                this.MaingGrid.Children.Add(rentalChartColumn);
                columnIndex += 2;
            }

            this.UpdateChartLayout();
        }

        private void SetHightValue()
        {
            this.highestMarkValue = 0;

            foreach (RentalData pl in this.ProfitsAndLostEntries)
            {
                this.highestMarkValue = Math.Max(this.highestMarkValue, pl.Income);
                this.highestMarkValue = Math.Max(this.highestMarkValue, pl.Expense);
            }
        }

        private void MaingGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateChartLayout();
        }

        private int whatExpenseIsAtTheBottom = 0;

        private void UpdateChartLayout()
        {
            foreach (RentalChartColumn pfcol in this.MaingGrid.Children)
            {
                double maxHeight = this.ActualHeight * .90;
                pfcol.BarForIncome.Height = pfcol.MyRentalData.Income / this.highestMarkValue * maxHeight;
                pfcol.BarForExpense.Height = pfcol.MyRentalData.Expense / this.highestMarkValue * maxHeight;
                pfcol.SetExpensesDistribution(this.whatExpenseIsAtTheBottom);
            }
        }

        private void MaingGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.whatExpenseIsAtTheBottom--;
            if (this.whatExpenseIsAtTheBottom < 0)
            {
                this.whatExpenseIsAtTheBottom = 4;
            }
            this.UpdateChartLayout();
        }
    }




}
