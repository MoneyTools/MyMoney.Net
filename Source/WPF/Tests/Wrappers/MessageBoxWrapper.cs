using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    public class MessageBoxWrapper : DialogWrapper
    {
        internal MessageBoxWrapper(AutomationElement e) : base(e)
        {
        }

        public void ClickOk()
        {
            this.window.ClickButton("ButtonOK");
        }

        public void ClickCancel()
        {
            this.window.ClickButton("ButtonCancel");
        }

        internal void ClickNo()
        {
            this.window.ClickButton("ButtonNo");
        }

        internal void ClickYes()
        {
            this.window.ClickButton("ButtonYes");
        }

    }
}
