using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class IncomeChartWrapper
    {
        AutomationElement e;

        public IncomeChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
