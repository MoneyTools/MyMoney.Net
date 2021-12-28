using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        public ObservableCollection<Payment> collection;

        public ObservableCollection<LoanPaymentAggregation> LoanPayements
        {
            get;
            set;
        }

        public LoanChart()
        {
            InitializeComponent();
            IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnIsVisibleChanged);

            collection = new ObservableCollection<Payment>();
            SeriePrincipal.ItemsSource = collection;
            SerieInterest.ItemsSource = collection;
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

                collection.Clear();

                foreach (Payment payment in cumulatedPayementsPerYear.Values)
                {
                    collection.Add(
                        new Payment()
                            {
                                Principal = Math.Abs(payment.Principal),
                                Interest = Math.Abs(payment.Interest),
                                Label = payment.Label
                            }
                        );

                    totalPrincipal += payment.Principal;
                    totalInterest += payment.Interest;
                }

                SeriePrincipal.Title = string.Format("Principal {0:C}", Math.Abs(totalPrincipal));
                SerieInterest.Title = string.Format("Interest {0:C}", Math.Abs(totalInterest));

                AreaChart.InvalidateArrange();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
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
