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
