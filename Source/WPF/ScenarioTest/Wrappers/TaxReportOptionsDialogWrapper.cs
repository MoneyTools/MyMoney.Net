using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    public class TaxReportOptionsDialogWrapper : DialogWrapper
    {
        public TaxReportOptionsDialogWrapper(AutomationElement window) : base(window)
        {
        }

        public void ClickOk()
        {
            this.window.ClickButton("OK");
        }

        public void ClickCancel()
        {
            this.window.ClickButton("Cancel");
        }

        public string ReportYear
        {
            get { return this.window.GetTextBox("YearText"); }
            set { this.window.SetTextBox("YearText", value); }
        }

        public bool CapitalGainsOnly
        {
            get { return this.window.IsChecked("CapitalGainsOnlyCheckBox"); }
            set { this.window.SetChecked("CapitalGainsOnlyCheckBox", value); }
        }

        public string ConsolidateBy
        {
            get { return this.window.GetComboBoxSelection("ConsolidateSecuritiesCombo"); }
            set { this.window.SetComboBox("ConsolidateSecuritiesCombo", value); }
        }
    }

}
