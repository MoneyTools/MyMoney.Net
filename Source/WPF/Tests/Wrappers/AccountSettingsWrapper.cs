using System.Windows.Automation;

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
            this.window.ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            this.window.ClickButton("ButtonCancel");
        }

        public void ClickOnlineAccountDetails()
        {
            this.window.ClickButton("buttonOnlineAccountDetails");
        }

        public void ClickGoToWebSite()
        {
            this.window.ClickButton("ButtonGoToWebSite");
        }

        public string Name
        {
            get { return this.window.GetTextBox("TextBoxName"); }
            set { this.window.SetTextBox("TextBoxName", value); }
        }

        public string AccountNumber
        {
            get { return this.window.GetTextBox("TextBoxAccountNumber"); }
            set { this.window.SetTextBox("TextBoxAccountNumber", value); }
        }

        public string Description
        {
            get { return this.window.GetTextBox("TextBoxDescription"); }
            set { this.window.SetTextBox("TextBoxDescription", value); }
        }

        internal string AccountType
        {
            get { return this.window.GetComboBoxSelection("AccountTypeCombo"); }
            set { this.window.SetComboBox("AccountTypeCombo", value); }
        }

        public string OpeningBalance
        {
            get { return this.window.GetTextBox("TextBoxOpeningBalance"); }
            set { this.window.SetTextBox("TextBoxOpeningBalance", value); }
        }

        internal string OnlineAccount
        {
            get { return this.window.GetComboBoxSelection("comboBoxOnlineAccount"); }
            set { this.window.SetComboBox("comboBoxOnlineAccount", value); }
        }

        public string Currency
        {
            get { return this.window.GetTextBox("TextBoxCurrency"); }
            set { this.window.SetTextBox("TextBoxCurrency", value); }
        }

        public string WebSite
        {
            get { return this.window.GetTextBox("TextBoxWebSite"); }
            set { this.window.SetTextBox("TextBoxWebSite", value); }
        }

        public string ReconcileWarning
        {
            get { return this.window.GetTextBox("TextBoxReconcileWarning"); }
            set { this.window.SetTextBox("TextBoxReconcileWarning", value); }
        }

        public bool IncludeInBudget
        {
            get { return this.window.IsChecked("CheckBoxIncludeInBudget"); }
            set { this.window.SetChecked("CheckBoxIncludeInBudget", value); }
        }

        public bool Closed
        {
            get { return this.window.IsChecked("CheckBoxClosed"); }
            set { this.window.SetChecked("CheckBoxClosed", value); }
        }
    }

}
