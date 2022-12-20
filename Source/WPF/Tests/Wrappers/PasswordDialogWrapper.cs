using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;
using System;

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
            get { return this.window.GetTextBox("PasswordBox"); }
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
