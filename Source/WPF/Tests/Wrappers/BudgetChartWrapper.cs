using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class BudgetChartWrapper
    {
        AutomationElement e;

        public BudgetChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
