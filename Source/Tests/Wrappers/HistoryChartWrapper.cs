using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class HistoryChartWrapper
    {
        AutomationElement e;

        public HistoryChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
