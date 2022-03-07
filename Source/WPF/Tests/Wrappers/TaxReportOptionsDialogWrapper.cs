using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    public class TaxReportOptionsDialogWrapper : DialogWrapper
    {
        public TaxReportOptionsDialogWrapper(AutomationElement window) : base(window)
        {
        }

        public void ClickOk()
        {
            window.ClickButton("OK");
        }

        public void ClickCancel()
        {
            window.ClickButton("Cancel");
        }

        public string ReportYear
        {
            get { return window.GetTextBox("YearText"); }
            set { window.SetTextBox("YearText", value); }
        }

        public bool CapitalGainsOnly
        {
            get { return window.IsChecked("CapitalGainsOnlyCheckBox"); }
            set { window.SetChecked("CapitalGainsOnlyCheckBox", value); }
        }

        public string ConsolidateBy
        {
            get { return window.GetComboBoxSelection("ConsolidateSecuritiesCombo"); }
            set { window.SetComboBox("ConsolidateSecuritiesCombo", value); }
        }
    }

}
