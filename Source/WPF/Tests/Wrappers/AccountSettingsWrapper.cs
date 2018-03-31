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
            ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            ClickButton("ButtonCancel");
        }

        public void ClickOnlineAccountDetails()
        {
            ClickButton("buttonOnlineAccountDetails");
        }

        public void ClickGoToWebSite()
        {
            ClickButton("ButtonGoToWebSite");
        }

        public string Name
        {
            get { return GetTextBox("TextBoxName"); }
            set { SetTextBox("TextBoxName", value); }
        }

        public string AccountNumber
        {
            get { return GetTextBox("TextBoxAccountNumber"); }
            set { SetTextBox("TextBoxAccountNumber", value); }
        }

        public string Description
        {
            get { return GetTextBox("TextBoxDescription"); }
            set { SetTextBox("TextBoxDescription", value); }
        }

        internal string AccountType
        {
            get { return GetComboBoxSelection("AccountTypeCombo"); }
            set { SetComboBox("AccountTypeCombo", value); }
        }

        public string OpeningBalance
        {
            get { return GetTextBox("TextBoxOpeningBalance"); }
            set { SetTextBox("TextBoxOpeningBalance", value); }
        }

        internal string OnlineAccount
        {
            get { return GetComboBoxSelection("comboBoxOnlineAccount"); }
            set { SetComboBox("comboBoxOnlineAccount", value); }
        }

        public string Currency
        {
            get { return GetTextBox("TextBoxCurrency"); }
            set { SetTextBox("TextBoxCurrency", value); }
        }

        public string WebSite
        {
            get { return GetTextBox("TextBoxWebSite"); }
            set { SetTextBox("TextBoxWebSite", value); }
        }

        public string ReconcileWarning
        {
            get { return GetTextBox("TextBoxReconcileWarning"); }
            set { SetTextBox("TextBoxReconcileWarning", value); }
        }

        public bool IncludeInBudget
        {
            get { return IsChecked("CheckBoxIncludeInBudget"); }
            set { SetChecked("CheckBoxIncludeInBudget", value); }
        }

        public bool Closed
        {
            get { return IsChecked("CheckBoxClosed"); }
            set { SetChecked("CheckBoxClosed", value); }
        }
    }

}
