using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class TrendChartWrapper
    {
        AutomationElement e;

        public TrendChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
