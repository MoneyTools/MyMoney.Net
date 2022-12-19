using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    public class MessageBoxWrapper : DialogWrapper
    {
        internal MessageBoxWrapper(AutomationElement e) : base(e)
        {
        }

        public void ClickOk()
        {
            window.ClickButton("ButtonOK");
        }

        public void ClickCancel()
        {
            window.ClickButton("ButtonCancel");
        }

        internal void ClickNo()
        {
            window.ClickButton("ButtonNo");
        }

        internal void ClickYes()
        {
            window.ClickButton("ButtonYes");
        }

    }
}
