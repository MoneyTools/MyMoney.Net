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
            this.window.ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            this.window.ClickButton("ButtonCancel");
        }

        public void SetEmployer(string name)
        {
            this.window.SetTextBox("TextBoxEmployer", name);
        }

        public void SetPaycheck(string name)
        {
            this.window.SetTextBox("TextBoxPaycheck", name);
        }

        public void SetInflation(string name)
        {
            this.window.SetTextBox("TextBoxInflation", name);
        }
    }
}
