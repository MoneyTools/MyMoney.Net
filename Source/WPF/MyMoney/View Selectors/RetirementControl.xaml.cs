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
using Walkabout.Reports;

namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for RetirementControl.xaml
    /// </summary>
    public partial class RetirementControl : UserControl
    {
        public RetirementControl()
        {
            this.InitializeComponent();
            TaxDeferredRow.Visibility = Visibility.Collapsed;
            this.UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            var visibility = this.MarriedFilingJointly ? Visibility.Visible : Visibility.Collapsed;
            SpouseAgeLabel.Visibility = SpouseAgeBorder.Visibility = visibility;
            TaxDeferredDetails.Visibility = this.TaxDeferredStrategy != "None" ? Visibility.Visible : Visibility.Collapsed;
            SocialSecuritySpouseDetails.Visibility = this.MarriedFilingJointly ? Visibility.Visible : Visibility.Collapsed;
        }

        public decimal InflationRate
        {
            get
            {
                decimal result = 0;
                decimal.TryParse(this.InflationRateText.Text, out result);
                return result / 100.0M;
            }
            set { this.InflationRateText.Text = (value * 100.0M).ToString(); }
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
                return result / 100.0M;
            }
            set { this.RateOfReturnText.Text = (value * 100.0M).ToString(); }
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

        public int RetirementAge
        {
            get
            {
                int result = 0;
                int.TryParse(this.RetirementAgeText.Text, out result);
                return result;
            }
            set { this.RetirementAgeText.Text = value.ToString(); }
        }

        public event EventHandler<int> RetirementAgeChanged;

        private void OnRetirementAgeChanged(object sender, string e)
        {
            if (RetirementAgeChanged != null)
            {
                RetirementAgeChanged(this, this.RetirementAge);
            }
        }

        public ComboBox ShowTaxDeferredRow()
        {
            TaxDeferredRow.Visibility = Visibility.Visible;
            return this.TaxDeferredStrategyCombo;
        }

        public string TaxDeferredStrategy
        {
            get
            {
                return this.TaxDeferredStrategyCombo.SelectedItem as string;
            }
            set
            {
                this.TaxDeferredStrategyCombo.SelectedItem = value;
            }
        }

        public event EventHandler<string> TaxDeferredStrategyChanged;

        private void OnTaxDeferredStrategyChanged(object sender, SelectionChangedEventArgs e)
        {
            this.UpdateVisibility();
            if (TaxDeferredStrategyChanged != null)
            {
                TaxDeferredStrategyChanged(this, this.TaxDeferredStrategy);
            }
        }

        public int TaxDeferredStrategyYears
        {
            get
            {
                int result = 0;
                int.TryParse(this.TaxDeferredStrategyYearsText.Text, out result);
                return result;
            }
            set { this.TaxDeferredStrategyYearsText.Text = value.ToString(); }
        }


        public event EventHandler<int> TaxDeferredStrategyYearsChanged;


        private void OnTaxDeferredStrategyYearsChanged(object sender, string e)
        {
            if (TaxDeferredStrategyYearsChanged != null)
            {
                TaxDeferredStrategyYearsChanged(this, this.TaxDeferredStrategyYears);
            }
        }

        public int TaxDeferredStrategyAge
        {
            get
            {
                int result = 0;
                int.TryParse(this.TaxDeferredStrategyAgeText.Text, out result);
                return result;
            }
            set { this.TaxDeferredStrategyAgeText.Text = value.ToString(); }
        }


        public event EventHandler<int> TaxDeferredStrategyAgeChanged;

        private void OnTaxDeferredStrategyAgeChanged(object sender, string e)
        {
            if (TaxDeferredStrategyAgeChanged != null)
            {
                TaxDeferredStrategyAgeChanged(this, this.TaxDeferredStrategyAge);
            }
        }

        public decimal SocialSecurityAmount
        {
            get
            {
                decimal result = 0;
                decimal.TryParse(this.SocialSecurityText.Text, out result);
                return result;
            }
            set
            {
                this.SocialSecurityText.Text = value.ToString();
            }
        }

        public event EventHandler<decimal> SocialSecurityAmountChanged;

        private void OnSocialSecurityTextChanged(object sender, string e)
        {
            if (SocialSecurityAmountChanged != null)
            {
                SocialSecurityAmountChanged(this, this.SocialSecurityAmount);
            }
        }

        public int SocialSecurityAge
        {
            get
            {
                int result = 0;
                int.TryParse(this.SocialSecurityAgeText.Text, out result);
                return result;
            }
            set
            {
                this.SocialSecurityAgeText.Text = value.ToString();
            }
        }

        public event EventHandler<int> SocialSecurityAgeChanged;

        private void OnSocialSecurityAgeTextChanged(object sender, string e)
        {
            if (SocialSecurityAgeChanged != null)
            {
                SocialSecurityAgeChanged(this, this.SocialSecurityAge);
            }
        }

        public int SpouseAge
        {
            get
            {
                int result = 0;
                int.TryParse(this.SpouseAgeText.Text, out result);
                return result;
            }
            set
            {
                this.SpouseAgeText.Text = value.ToString();
                this.UpdateVisibility();
            }
        }

        public event EventHandler<int> SpouseAgeChanged;

        private void OnSpouseAgeChanged(object sender, string e)
        {
            if (SpouseAgeChanged != null)
            {
                SpouseAgeChanged(this, this.SpouseAge);
            }
        }

        public bool MarriedFilingJointly
        {
            get => this.FilingJointlyCheckbox.IsChecked == true;
            set
            {
                this.FilingJointlyCheckbox.IsChecked = value;
            }
        }

        public event EventHandler<bool> MarriedFilingJointlyChanged;

        private void OnMarriedFilingJointlyChanged(object sender, RoutedEventArgs e)
        {
            this.UpdateVisibility();
            if (MarriedFilingJointlyChanged != null)
            {
                MarriedFilingJointlyChanged(this, this.MarriedFilingJointly);
            }
        }

        public bool StackedBars
        {
            get { return this.StackedBarsCheckbox.IsChecked == true; }
            set
            {
                this.StackedBarsCheckbox.IsChecked = value;
            }
        }

        public event EventHandler<bool> StackedBarsChanged;

        private void OnStackedChanged(object sender, RoutedEventArgs e)
        {
            if (this.StackedBarsChanged != null)
            {
                this.StackedBarsChanged(this, this.StackedBars);
            }

        }

        public decimal SocialSecuritySpouseAmount
        {
            get
            {
                decimal result = 0;
                decimal.TryParse(this.SocialSecuritySpouseText.Text, out result);
                return result;
            }
            set
            {
                this.SocialSecuritySpouseText.Text = value.ToString();
            }
        }

        public event EventHandler<decimal> SocialSecuritySpouseAmountChanged;


        private void OnSocialSecuritySpouseTextChanged(object sender, string e)
        {
            if (this.SocialSecuritySpouseAmountChanged != null)
            {
                this.SocialSecuritySpouseAmountChanged(this, this.SocialSecuritySpouseAmount);
            }
        }

        public int SocialSecuritySpouseAge
        {
            get
            {
                int result = 0;
                int.TryParse(this.SocialSecuritySpouseAgeText.Text, out result);
                return result;
            }
            set
            {
                this.SocialSecuritySpouseAgeText.Text = value.ToString();
            }
        }

        public event EventHandler<int> SocialSecuritySpouseAgeChanged;


        private void OnSocialSecuritySpouseAgeTextChanged(object sender, string e)
        {

            if (this.SocialSecuritySpouseAgeChanged != null)
            {
                this.SocialSecuritySpouseAgeChanged(this, this.SocialSecuritySpouseAge);
            }
        }
    }
}
