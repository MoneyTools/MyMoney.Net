using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LovettSoftware.Charts;
using Walkabout.Data;

namespace Walkabout.Charts
{
    /// <summary>
    /// Interaction logic for LoanChart.xaml
    /// </summary>
    public partial class LoanChart : UserControl
    {
        public class Payment
        {
            public decimal Principal { get; set; }
            public decimal Interest { get; set; }
            public string Label { get; set; }
        };

        public ObservableCollection<LoanPaymentAggregation> LoanPayements
        {
            get;
            set;
        }

        public LoanChart()
        {
            InitializeComponent();
            IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnIsVisibleChanged);
            Chart.ToolTipGenerator = OnGenerateToolTip;
        }

        private UIElement OnGenerateToolTip(ChartDataValue value)
        {
            var tip = new StackPanel() { Orientation = Orientation.Vertical };
            tip.Children.Add(new TextBlock() { Text = (string)value.UserData, FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = value.Label });
            tip.Children.Add(new TextBlock() { Text = value.Value.ToString("C0") });
            return tip;
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateChart();
        }


        public void UpdateChart()
        {
            if (!this.IsVisible || LoanPayements == null)
            {
                return;
            }

            try
            {
                decimal totalPrincipal = 0;
                decimal totalInterest = 0;

                Dictionary<int, Payment> cumulatedPayementsPerYear = CumulatePayementsPerYear();

                ChartDataSeries interestSeries = new ChartDataSeries();
                ChartDataSeries principalSeries = new ChartDataSeries();

                List<int> years = new List<int>(cumulatedPayementsPerYear.Keys);
                years.Sort();
                foreach (var year in years)
                {
                    var payment = cumulatedPayementsPerYear[year];
                    totalPrincipal += payment.Principal;
                    totalInterest += payment.Interest;
                    interestSeries.Data.Add(new ChartDataValue() { Label = payment.Label, Value = (double)payment.Interest, UserData = "interest" });
                    principalSeries.Data.Add(new ChartDataValue() { Label = payment.Label, Value = (double)payment.Principal, UserData = "principal" });
                }

                Chart.Series = new List<ChartDataSeries>()
                {
                    interestSeries, principalSeries
                };

                principalSeries.Name = string.Format("Principal {0:C}", Math.Abs(totalPrincipal));
                interestSeries.Name = string.Format("Interest {0:C}", Math.Abs(totalInterest));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
        }



        private void OnColumnHover(object sender, LovettSoftware.Charts.ChartDataValue e)
        {

        }

        private void OnColumnClicked(object sender, LovettSoftware.Charts.ChartDataValue e)
        {
            // todo: any kind of drill down or pivot possible here?
        }


        private Dictionary<int, Payment> CumulatePayementsPerYear()
        {
            Dictionary<int, Payment> cumulatePerYear = new Dictionary<int, Payment>();

            bool discardFirstItem = true;

            foreach (LoanPaymentAggregation payment in LoanPayements)
            {
                if (discardFirstItem)
                {
                    // The first item is use for setting the initial Principal value
                    discardFirstItem = false;
                }
                else
                {
                    int year = payment.Date.Year;

                    Payment currentCumulation = null;
                    cumulatePerYear.TryGetValue(year, out currentCumulation);


                    if (currentCumulation == null)
                    {
                        currentCumulation = new Payment() { Label = year.ToString() };
                    }
                    currentCumulation.Interest += payment.Interest;
                    currentCumulation.Principal += payment.Principal;
                    cumulatePerYear[year] = currentCumulation;
                }
            }
            return cumulatePerYear;
        }
    }
}
