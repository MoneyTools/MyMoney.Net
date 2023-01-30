using System;
using System.Windows.Automation;
using Walkabout.Tests.Interop;

namespace Walkabout.Tests.Wrappers
{
    public class QuickFilterWrapper
    {
        private readonly AutomationElement e;

        public QuickFilterWrapper(AutomationElement e)
        {
            this.e = e;
        }

        public string GetFilter()
        {
            return this.e.GetTextBox("InputFilterText");
        }

        public void SetFilter(string filter)
        {
            var box = this.e.SetTextBox("InputFilterText", filter);
            Win32.SetFocus(Win32.HWND.Cast(new IntPtr(box.Current.NativeWindowHandle)));
            Input.TapKey(System.Windows.Input.Key.Enter);
        }

        public void ClearSearch()
        {
            this.e.ClickButton("ClearFilter");
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
