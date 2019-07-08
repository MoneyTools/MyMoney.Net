using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class StockChartWrapper
    {
        AutomationElement e;

        public StockChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
