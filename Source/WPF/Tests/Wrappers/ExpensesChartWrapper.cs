using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class ExpensesChartWrapper
    {
        private AutomationElement e;

        public ExpensesChartWrapper(AutomationElement e)
        {
            this.e = e;
        }
    }
}
