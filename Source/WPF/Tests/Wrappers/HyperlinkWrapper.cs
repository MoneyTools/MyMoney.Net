using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class HyperlinkWrapper
    {
        AutomationElement e;

        public HyperlinkWrapper(AutomationElement e)
        {
            this.e = e;
        }

        public void Invoke()
        {
            InvokePattern invoke = (InvokePattern)this.e.GetCurrentPattern(InvokePattern.Pattern);
            invoke.Invoke();
        }

        public bool IsVisible
        {
            get
            {
                return !this.e.Current.IsOffscreen;
            }
        }
    }
}
