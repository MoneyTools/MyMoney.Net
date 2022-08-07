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
            this.WaitForInputIdle(100);
        }

        public void ClickSave()
        {
            window.ClickButton("1");
            try
            {
                if (!this.window.Current.IsOffscreen)
                {
                    this.WaitForInputIdle(100);
                    for (int retries = 5; retries > 0 && 
                        !this.window.Current.IsOffscreen; retries--)
                    {
                        if (this.IsBlocked)
                        {
                            var c = this.window.FindChildWindow("Save As", 5);
                            if (c != null)
                            {
                                c.ClickButtonByName("Yes");
                            }
                        }
                        else
                        {
                            // then folder moved, so we have to click save again!
                            window.ClickButton("1");
                        }
                        this.WaitForInputIdle(100);
                    }
                }
            } 
            catch (System.Windows.Automation.ElementNotAvailableException)
            {
                // window is gone then.
            }
        }

        public void ClickCancel()
        {
            window.ClickButton("2");
        }

    }
}
