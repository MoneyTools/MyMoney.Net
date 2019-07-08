using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    class ChartsAreaWrapper
    {
        AutomationElement charts;

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
                AutomationElement tab = charts.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, id));
                if (tab != null)
                {
                    SelectTab(tab);
                    return tab;
                }
                Thread.Sleep(1000);
            } 
            
            throw new Exception("Tab named '" + id + "' not found");   
        }

        public TrendChartWrapper SelectTrends()
        {
            AutomationElement tab = FindAndSelectTab("TabTrends");
            return new TrendChartWrapper(tab);
        }

        public HistoryChartWrapper SelectHistory()
        {
            AutomationElement tab = FindAndSelectTab("TabHistory");
            return new HistoryChartWrapper(tab);
        }

        public IncomeChartWrapper SelectIncomes()
        {
            AutomationElement tab = FindAndSelectTab("TabIncomes");
            return new IncomeChartWrapper(tab);
        }

        public ExpensesChartWrapper SelectExpenses()
        {
            AutomationElement tab = FindAndSelectTab("TabExpenses");
            return new ExpensesChartWrapper(tab);
        }

        public StockChartWrapper SelectStock()
        {
            AutomationElement tab = FindAndSelectTab("TabStock");
            return new StockChartWrapper(tab);
        }

        public DownloadDetailsWrapper SelectDownload()
        {
            AutomationElement tab = FindAndSelectTab("TabDownload");
            return new DownloadDetailsWrapper(tab);
        }
    }
}
