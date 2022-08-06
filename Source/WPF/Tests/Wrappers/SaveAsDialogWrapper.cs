using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    public class SaveAsDialogWrapper : DialogWrapper
    {        
        internal SaveAsDialogWrapper(AutomationElement e) : base(e)
        {
        }

        public void SetFileName(string value) {
            this.Element.SetTextBox("1001", value);
        }

        public void ClickSave()
        {
            window.ClickButton("1");
        }

        public void ClickCancel()
        {
            window.ClickButton("2");
        }

    }
}
