using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    public class CategoryPropertiesWrapper : DialogWrapper
    {
        internal CategoryPropertiesWrapper(AutomationElement e)
            : base(e)
        {
        }

        public void ClickOk()
        {
            this.window.ClickButton("okButton");
        }

        public void ClickCancel()
        {
            this.window.ClickButton("cancelButton");
        }


    }
}
