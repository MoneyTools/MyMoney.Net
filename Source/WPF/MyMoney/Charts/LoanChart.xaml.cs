using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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

        ObservableCollection<LoanPaymentAggregation> payments;

        public ObservableCollection<LoanPaymentAggregation> LoanPayments
        {
            get => payments;
            set
            {
                payments = value;
                UpdateChart();
            }
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
            if (!this.IsVisible || LoanPayments == null)
            {
                Chart.Data = null;
                return;
            }

            try
            {
                decimal totalPrincipal = 0;
                decimal totalInterest = 0;

                Dictionary<int, Payment> cumulatedPayementsPerYear = CumulatePayementsPerYear();

                ChartDataSeries interestSeries = new ChartDataSeries() { Name = "Interest" };
                ChartDataSeries principalSeries = new ChartDataSeries() { Name = "Principal" };

                List<int> years = new List<int>(cumulatedPayementsPerYear.Keys);
                years.Sort();
                foreach (var year in years)
                {
                    var payment = cumulatedPayementsPerYear[year];
                    totalPrincipal += payment.Principal;
                    totalInterest += payment.Interest;
                    interestSeries.Values.Add(new ChartDataValue() { Label = payment.Label, Value = -(double)payment.Interest, UserData = "interest" });
                    principalSeries.Values.Add(new ChartDataValue() { Label = payment.Label, Value = -(double)payment.Principal, UserData = "principal" });
                }

                var data = new ChartData();
                data.AddSeries(interestSeries);
                data.AddSeries(principalSeries);
                Chart.Data = data;

                this.TextBoxPrincipal.Text = string.Format("{0:C}", Math.Abs(totalPrincipal));
                this.TextBoxInterest.Text = string.Format("{0:C}", Math.Abs(totalInterest));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
        }



        private void OnColumnHover(object sender, ChartDataValue e)
        {

        }

        private void OnColumnClicked(object sender, ChartDataValue e)
        {
            // todo: any kind of drill down or pivot possible here?
        }


        private Dictionary<int, Payment> CumulatePayementsPerYear()
        {
            Dictionary<int, Payment> cumulatePerYear = new Dictionary<int, Payment>();

            bool discardFirstItem = true;

            foreach (LoanPaymentAggregation payment in LoanPayments)
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
