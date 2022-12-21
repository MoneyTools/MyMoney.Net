using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class IncomeChartWrapper
    {
        private AutomationElement e;

        public IncomeChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
