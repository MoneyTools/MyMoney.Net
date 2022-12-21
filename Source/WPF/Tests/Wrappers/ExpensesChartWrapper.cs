using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class ExpensesChartWrapper
    {
        AutomationElement e;

        public ExpensesChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
