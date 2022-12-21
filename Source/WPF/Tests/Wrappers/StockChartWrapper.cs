using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class StockChartWrapper
    {
        private AutomationElement e;

        public StockChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
