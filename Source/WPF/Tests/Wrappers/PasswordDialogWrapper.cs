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
            get { return window.GetComboBoxText("TextBoxUserName"); }
            set { window.SetComboBoxText("TextBoxUserName", value); }
        }

        public string Password
        {
            get { return window.GetTextBox("PasswordBox"); }
            set { window.SetTextBox("PasswordBox", value); }
        }

        internal void SetUserDefinedField(string id, string value)
        {
            window.SetTextBox("TextBox" + id, value);
        }

        #endregion 

        public void ClickOk()
        {
            window.ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            window.ClickButton("ButtonCancel");
        }


    }

}
