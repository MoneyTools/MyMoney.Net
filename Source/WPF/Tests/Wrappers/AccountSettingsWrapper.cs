using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    public class AccountSettingsWrapper : DialogWrapper
    {
        internal AccountSettingsWrapper(AutomationElement e)
            : base(e)
        {
        }

        public void ClickOk()
        {
            window.ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            window.ClickButton("ButtonCancel");
        }

        public void ClickOnlineAccountDetails()
        {
            window.ClickButton("buttonOnlineAccountDetails");
        }

        public void ClickGoToWebSite()
        {
            window.ClickButton("ButtonGoToWebSite");
        }

        public string Name
        {
            get { return window.GetTextBox("TextBoxName"); }
            set { window.SetTextBox("TextBoxName", value); }
        }

        public string AccountNumber
        {
            get { return window.GetTextBox("TextBoxAccountNumber"); }
            set { window.SetTextBox("TextBoxAccountNumber", value); }
        }

        public string Description
        {
            get { return window.GetTextBox("TextBoxDescription"); }
            set { window.SetTextBox("TextBoxDescription", value); }
        }

        internal string AccountType
        {
            get { return window.GetComboBoxSelection("AccountTypeCombo"); }
            set { window.SetComboBox("AccountTypeCombo", value); }
        }

        public string OpeningBalance
        {
            get { return window.GetTextBox("TextBoxOpeningBalance"); }
            set { window.SetTextBox("TextBoxOpeningBalance", value); }
        }

        internal string OnlineAccount
        {
            get { return window.GetComboBoxSelection("comboBoxOnlineAccount"); }
            set { window.SetComboBox("comboBoxOnlineAccount", value); }
        }

        public string Currency
        {
            get { return window.GetTextBox("TextBoxCurrency"); }
            set { window.SetTextBox("TextBoxCurrency", value); }
        }

        public string WebSite
        {
            get { return window.GetTextBox("TextBoxWebSite"); }
            set { window.SetTextBox("TextBoxWebSite", value); }
        }

        public string ReconcileWarning
        {
            get { return window.GetTextBox("TextBoxReconcileWarning"); }
            set { window.SetTextBox("TextBoxReconcileWarning", value); }
        }

        public bool IncludeInBudget
        {
            get { return window.IsChecked("CheckBoxIncludeInBudget"); }
            set { window.SetChecked("CheckBoxIncludeInBudget", value); }
        }

        public bool Closed
        {
            get { return window.IsChecked("CheckBoxClosed"); }
            set { window.SetChecked("CheckBoxClosed", value); }
        }
    }

}
