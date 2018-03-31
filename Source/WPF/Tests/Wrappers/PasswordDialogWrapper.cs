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
            get { return GetComboBoxText("TextBoxUserName"); }
            set { SetComboBoxText("TextBoxUserName", value); }
        }

        public string Password
        {
            get { return GetTextBox("PasswordBox"); }
            set { SetTextBox("PasswordBox", value); }
        }

        internal void SetUserDefinedField(string id, string value)
        {
            SetTextBox("TextBox" + id, value);
        }

        #endregion 

        public void ClickOk()
        {
            ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            ClickButton("ButtonCancel");
        }


    }

}
