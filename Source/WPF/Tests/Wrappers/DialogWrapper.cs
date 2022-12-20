using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    public class DialogWrapper
    {
        /*
        SystemMenuBar
	        Restore: 61728
	        Move: 61456
	        Size: 61440
	        Minimize: 61472
	        Maximize: 61488
	        Close: 61536
        */
        protected AutomationElement window;

        public DialogWrapper(AutomationElement window)
        {
            this.window = window;
        }

        public AutomationElement Element { get { return this.window; } }

        public virtual void Close()
        {
            this.window.ClickButton("CloseButton");
        }

        public void Minimize()
        {
            this.window.ClickButton("MinimizeButton");
        }

        public void Maximize()
        {
            this.window.ClickButton("MaximizeRestoreButton");
        }

        public bool HasModalChildWindow
        {
            get
            {
                AutomationElement childWindow = this.window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "Window"));
                if (childWindow != null && !childWindow.Current.IsOffscreen)
                {
                    WindowPattern wp = (WindowPattern)childWindow.GetCurrentPattern(WindowPattern.Pattern);
                    return wp.Current.IsModal;
                }
                return false;
            }
        }

        public bool IsBlocked
        {
            get
            {
                return this.State == WindowInteractionState.BlockedByModalWindow || this.HasModalChildWindow;
            }
        }

        public bool IsInteractive
        {
            get
            {

                return this.State == WindowInteractionState.ReadyForUserInteraction || this.State == WindowInteractionState.Running && !this.HasModalChildWindow;
            }
        }

        public bool IsNotResponding
        {
            get
            {

                return this.State == WindowInteractionState.NotResponding;
            }
        }

        private WindowInteractionState State
        {
            get
            {
                WindowPattern wp = (WindowPattern)this.window.GetCurrentPattern(WindowPattern.Pattern);
                return wp.Current.WindowInteractionState;
            }
        }

        public void WaitForInputIdle(int milliseconds)
        {
            WindowPattern wp = (WindowPattern)this.window.GetCurrentPattern(WindowPattern.Pattern);
            wp.WaitForInputIdle(milliseconds);
        }
    }
}
