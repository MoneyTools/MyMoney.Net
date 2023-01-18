using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class IncomeChartWrapper
    {
        private readonly AutomationElement e;

        public IncomeChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
