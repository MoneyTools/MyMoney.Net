using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    public class SaveAsDialogWrapper : DialogWrapper
    {
        internal SaveAsDialogWrapper(AutomationElement e) : base(e)
        {
        }

        public void SetFileName(string value)
        {
            this.Element.SetTextBox("1001", value);
            this.WaitForInputIdle(100);
        }

        public void ClickSave()
        {
            this.WaitForInputIdle(100);
            this.WaitForInteractive();

            var saveButton = this.window.FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, "Save"));
            if (saveButton != null && saveButton.TryGetCurrentPattern(InvokePattern.Pattern, out object o) &&
                o is InvokePattern ip)
            {
                ip.Invoke();
            }

        }

        public void ClickCancel()
        {
            this.window.ClickButton("2");
        }

    }
}
