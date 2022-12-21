using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class TrendChartWrapper
    {
        private AutomationElement e;

        public TrendChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
