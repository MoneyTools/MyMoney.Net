using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class ChartsAreaWrapper
    {
        private readonly AutomationElement charts;

        public ChartsAreaWrapper(AutomationElement charts)
        {
            this.charts = charts;
        }

        private void SelectTab(AutomationElement e)
        {
            SelectionItemPattern sip = (SelectionItemPattern)e.GetCurrentPattern(SelectionItemPattern.Pattern);
            sip.Select();
        }

        public AutomationElement FindAndSelectTab(string id)
        {
            for (int retries = 5; retries > 0; retries--)
            {
                AutomationElement tab = this.charts.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, id));
                if (tab != null)
                {
                    this.SelectTab(tab);
                    return tab;
                }
                Thread.Sleep(1000);
            }

            throw new Exception("Tab named '" + id + "' not found");
        }

        public TrendChartWrapper SelectTrends()
        {
            AutomationElement tab = this.FindAndSelectTab("TabTrends");
            return new TrendChartWrapper(tab);
        }

        public HistoryChartWrapper SelectHistory()
        {
            AutomationElement tab = this.FindAndSelectTab("TabHistory");
            return new HistoryChartWrapper(tab);
        }

        public IncomeChartWrapper SelectIncomes()
        {
            AutomationElement tab = this.FindAndSelectTab("TabIncomes");
            return new IncomeChartWrapper(tab);
        }

        public ExpensesChartWrapper SelectExpenses()
        {
            AutomationElement tab = this.FindAndSelectTab("TabExpenses");
            return new ExpensesChartWrapper(tab);
        }

        public StockChartWrapper SelectStock()
        {
            AutomationElement tab = this.FindAndSelectTab("TabStock");
            return new StockChartWrapper(tab);
        }

        public DownloadDetailsWrapper SelectDownload()
        {
            AutomationElement tab = this.FindAndSelectTab("TabDownload");
            return new DownloadDetailsWrapper(tab);
        }
    }
}
