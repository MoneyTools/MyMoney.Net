using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    public class SampleDataDialogWrapper : DialogWrapper
    {
        internal SampleDataDialogWrapper(AutomationElement e)
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

        public void SetEmployer(string name)
        {
            SetTextBox("TextBoxEmployer", name);
        }

        public void SetPaycheck(string name)
        {
            SetTextBox("TextBoxPaycheck", name);
        }

        public void SetInflation(string name)
        {
            SetTextBox("TextBoxInflation", name);
        }
    }
}
