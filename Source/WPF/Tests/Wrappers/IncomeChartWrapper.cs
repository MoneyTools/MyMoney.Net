using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class IncomeChartWrapper
    {
        AutomationElement e;

        public IncomeChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
