using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    public class PasswordDialogWrapper : DialogWrapper
    {
        internal PasswordDialogWrapper(AutomationElement e)
            : base(e)
        {
        }

        #region Fields 

        public string UserName
        {
            get { return this.window.GetComboBoxText("TextBoxUserName"); }
            set { this.window.SetComboBoxText("TextBoxUserName", value); }
        }

        public string Password
        {
            set { this.window.SetTextBox("PasswordBox", value); }
        }

        internal void SetUserDefinedField(string id, string value)
        {
            this.window.SetTextBox("TextBox" + id, value);
        }

        #endregion 

        public void ClickOk()
        {
            this.window.ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            this.window.ClickButton("ButtonCancel");
        }


    }

}
