using System;
using System.Collections.Generic;
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

namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for RetirementControl.xaml
    /// </summary>
    public partial class RetirementControl : UserControl
    {
        public RetirementControl()
        {
            InitializeComponent();
        }

        public decimal InflationRate
        {
            get
            {
                decimal result = 0;
                decimal.TryParse(this.InflationRateText.Text, out result);
                return result;
            }
            set { this.InflationRateText.Text = value.ToString(); }
        }

        public event EventHandler<decimal> InflationRateChanged;

        private void OnInflationRateChanged(object sender, string e)
        {       
            if (InflationRateChanged != null)
            {
                InflationRateChanged(this, this.InflationRate);
            }
        }

        public decimal RateOfReturn
        {
            get
            {
                decimal result = 0;
                decimal.TryParse(this.RateOfReturnText.Text, out result);
                return result;
            }
            set { this.RateOfReturnText.Text = value.ToString(); }
        }

        public event EventHandler<decimal> RateOfReturnChanged;

        private void OnRateOfReturnChanged(object sender, string e)
        {
            if (RateOfReturnChanged != null)
            {
                RateOfReturnChanged(this, this.RateOfReturn);
            }
        }

        public decimal DesiredIncome
        {
            get
            {
                decimal result = 0;
                decimal.TryParse(this.DesiredIncomeText.Text, out result);
                return result;
            }
            set { this.DesiredIncomeText.Text = value.ToString(); }
        }

        public event EventHandler<decimal> DesiredIncomeChanged;

        private void OnDesiredIncomeChanged(object sender, string e)
        {
            if (DesiredIncomeChanged != null)
            {
                DesiredIncomeChanged(this, this.DesiredIncome);
            }
        }

        public int GraduationAge
        {
            get
            {
                int result = 0;
                int.TryParse(this.GraduationAgeText.Text, out result);
                return result;
            }
            set { this.GraduationAgeText.Text = value.ToString(); }
        }

        public event EventHandler<int> GraduationAgeChanged;

        private void OnGraduationAgeChanged(object sender, string e)
        {
            if (GraduationAgeChanged != null)
            {
                GraduationAgeChanged(this, this.GraduationAge);
            }
        }

        public int CurrentAge
        {
            get
            {
                int result = 0;
                int.TryParse(this.CurrentAgeText.Text, out result);
                return result;
            }
            set { this.CurrentAgeText.Text = value.ToString(); }
        }

        public event EventHandler<int> CurrentAgeChanged;

        private void OnCurrentAgeChanged(object sender, string e)
        {
            if (CurrentAgeChanged != null)
            {
                CurrentAgeChanged(this, this.CurrentAge);
            }
        }
    }
}
