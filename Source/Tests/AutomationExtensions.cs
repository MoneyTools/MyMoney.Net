using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Threading;

namespace Walkabout.Tests
{
    static class AutomationExtensions
    {
        internal static AutomationElement FindFirstWithRetries(this AutomationElement parent, TreeScope scope, Condition condition, int retries = 5, int millisecondDelay = 200)
        {
            AutomationElement result = null;
            while (retries > 0 && result == null)
            {
                result = parent.FindFirst(scope, condition);
                if (result == null)
                {
                    Thread.Sleep(millisecondDelay);
                }
                retries--;
            }
            return result;
        }
    }
}
