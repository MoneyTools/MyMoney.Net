using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class ExpensesChartWrapper
    {
        AutomationElement e;

        public ExpensesChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
