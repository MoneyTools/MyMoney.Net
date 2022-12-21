using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class HistoryChartWrapper
    {
        private AutomationElement e;

        public HistoryChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
