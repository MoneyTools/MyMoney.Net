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
            window.ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            window.ClickButton("ButtonCancel");
        }

        public void SetEmployer(string name)
        {
            window.SetTextBox("TextBoxEmployer", name);
        }

        public void SetPaycheck(string name)
        {
            window.SetTextBox("TextBoxPaycheck", name);
        }

        public void SetInflation(string name)
        {
            window.SetTextBox("TextBoxInflation", name);
        }
    }
}
