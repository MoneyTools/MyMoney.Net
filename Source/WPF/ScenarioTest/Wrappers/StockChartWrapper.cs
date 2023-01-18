using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class StockChartWrapper
    {
        private readonly AutomationElement e;

        public StockChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
