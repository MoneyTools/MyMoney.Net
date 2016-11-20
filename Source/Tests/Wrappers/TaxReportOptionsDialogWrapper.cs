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
            ClickButton("OK");
        }

        public void ClickCancel()
        {
            ClickButton("Cancel");
        }

        public string ReportYear
        {
            get { return GetTextBox("YearText"); }
            set { SetTextBox("YearText", value); }
        }

        public bool CapitalGainsOnly
        {
            get { return IsChecked("CapitalGainsOnlyCheckBox"); }
            set { SetChecked("CapitalGainsOnlyCheckBox", value); }
        }

        public string ConsolidateBy
        {
            get { return GetComboBoxSelection("ConsolidateSecuritiesCombo"); }
            set { SetComboBox("ConsolidateSecuritiesCombo", value); }
        }
    }

}
