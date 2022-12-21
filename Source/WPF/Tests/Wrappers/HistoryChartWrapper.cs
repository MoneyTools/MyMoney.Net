using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class HistoryChartWrapper
    {
        private readonly AutomationElement e;

        public HistoryChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
