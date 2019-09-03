using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class AccountPickerWrapper : DialogWrapper
    {
        internal AccountPickerWrapper(AutomationElement e)
               : base(e)
        {
        }

        public void ClickAddNewAccount()
        {
            ClickButton("ButtonAdd");
        }

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
