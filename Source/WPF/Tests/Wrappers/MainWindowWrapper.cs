using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;
using System.Windows.Input;
using System.Diagnostics;

namespace Walkabout.Tests.Wrappers
{
    public class MainWindowWrapper : DialogWrapper
    {
        AccountsWrapper accounts;
        CategoriesWrapper categories;
        PayeesWrapper payees;
        SecuritiesWrapper securities;

        private MainWindowWrapper(AutomationElement e) : base(e)
        {
        }

        public static MainWindowWrapper FindMainWindow(int processId)
        {
            for (int i = 0; i < 10; i++)
            {
                AutomationElement e = Win32.FindWindow(processId, "MoneyWindow");
                if (e != null)
                {
                    return new MainWindowWrapper(e);
                }

                Thread.Sleep(1000);
            }

            throw new Exception("MainWindow not found for process " + processId);            
        }

        /// <summary>
        /// Send File/New command.
        /// </summary>
        internal void New()
        {
            ContextMenu subMenu = MainMenu.OpenSubMenu("MenuFile");
            subMenu.InvokeMenuItem("MenuFileNew");

            Thread.Sleep(300);

            AutomationElement child = this.FindChildWindow("Save Changes", 1);
            if (child != null)
            {
                MessageBoxWrapper msg = new MessageBoxWrapper(child);
                msg.ClickNo();
                Thread.Sleep(300);
            }

            child = this.FindChildWindow("New Database", 1);
            if (child != null)
            {
                MessageBoxWrapper msg = new MessageBoxWrapper(child);
                msg.ClickOk();
                Thread.Sleep(300);
            }
        }

        internal string Title
        {
            get
            {
                return window.Current.Name;
            }
        }        

        internal ContextMenu MainMenu
        {
            get 
            {
                AutomationElement mainMenu = window.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "MainMenu"));
                if (mainMenu == null)
                {
                    throw new Exception("MainMenu not found");
                }
                return new ContextMenu(mainMenu, false);
            }
        }

        internal AccountsWrapper ViewAccounts()
        {
            accounts = new AccountsWrapper(Expand("AccountsSelector"));
            categories = null;
            payees = null;
            securities = null;
            WaitForInputIdle(500);
            return accounts;
        }

        internal CategoriesWrapper ViewCategories()
        {
            categories = new CategoriesWrapper(Expand("CategoriesSelector"));
            //accounts = null;
            payees = null;
            securities = null;
            WaitForInputIdle(500);
            return categories;
        }

        internal PayeesWrapper ViewPayees()
        {
            payees = new PayeesWrapper(Expand("PayeesSelector"));
            //accounts = null;
            categories = null;
            securities = null;
            WaitForInputIdle(500);
            return payees;
        }

        internal SecuritiesWrapper ViewSecurities()
        {
            securities = new SecuritiesWrapper(Expand("SecuritiesSelector"));
            //accounts = null;
            categories = null;
            payees = null;
            WaitForInputIdle(500);
            return securities;
        }

        public bool IsAccountSelected {
            get
            {
                if (accounts != null) return accounts.IsAccountSelected;
                return false;
            }
        }

        public bool IsCategorySelected
        {
            get
            {
                if (categories != null) return categories.IsCategorySelected;
                return false;
            }
        }

        public bool HasCategories
        {
            get
            {
                if (categories != null) return categories.HasCategories;
                return false;
            }
        }

        public bool IsPayeeSelected
        {
            get
            {
                if (payees != null) return payees.IsPayeeSelected;
                return false;
            }
        }

        public bool IsSecuritySelected
        {
            get
            {
                if (securities != null) return securities.IsSecuritySelected;
                return false;
            }
        }

        public static AutomationElement FindTransactionGrid(AutomationElement window, int retries = 5)
        {
            for (; retries > 0; retries--)
            {
                foreach (AutomationElement e in window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid)))
                {
                    if (!e.Current.IsOffscreen)
                    {
                        Debug.WriteLine("### Found visible TransactionGrid named: " + e.Current.AutomationId);
                        return e;
                    }
                }

                Thread.Sleep(250);
            }
            return null;
        }

        public TransactionViewWrapper FindTransactionGrid()
        {
            CloseReport();

            AutomationElement e = FindTransactionGrid(this.window);
            if (e == null)
            {
                throw new Exception("Couldn't find a visible data grid");
            }

            return new TransactionViewWrapper(this, e);
        }

        internal void Save()
        {
            ContextMenu menu = MainMenu.OpenSubMenu("MenuFile");
            menu.InvokeMenuItem("MenuFileSave");
            Thread.Sleep(1000);
            WaitForInputIdle(1000);
        }

        internal OnlineAccountsDialogWrapper DownloadAccounts()
        {
            ContextMenu menu = MainMenu.OpenSubMenu("MenuOnline");
            menu.InvokeMenuItem("MenuOnlineDownloadAccounts");

            AutomationElement window = this.FindChildWindow("Online Account", 5);
            return new OnlineAccountsDialogWrapper(window);
        }


        internal void Synchronize()
        {
            ClickButton("ButtonSynchronize");
        }

        internal ChartsAreaWrapper GetChartsArea()
        {
            // 
            AutomationElement charts = window.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "TabForGraphs"));
            if (charts == null)
            {
                throw new Exception("Tab named 'TabForGraphs' not found");
            }
            return new ChartsAreaWrapper(charts);
        }

        bool reportOpen;

        public void ResetReport()
        {
            reportOpen = false;
        }

        public void CloseReport()
        {
            if (reportOpen)
            {
                AutomationElement closeButton = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "CloseReport"));
                if (closeButton != null)
                {
                    InvokePattern invoke = (InvokePattern)closeButton.GetCurrentPattern(InvokePattern.Pattern);
                    invoke.Invoke();
                }
                else
                {
                    Debug.WriteLine("### Close button named 'CloseReport' not found");                
                }
                reportOpen = false;
            }
        }

        public AutomationElement NetWorthReport()
        {
            ContextMenu subMenu = MainMenu.OpenSubMenu("MenuViewReports");
            subMenu.InvokeMenuItem("MenuReportsNetWorth");
            reportOpen = true;
            return FindReport("ReportNetworth");
        }

        public AutomationElement TaxReport()
        {
            ContextMenu subMenu = MainMenu.OpenSubMenu("MenuViewReports");
            subMenu.InvokeMenuItem("MenuReportsTaxReport");
            reportOpen = true;
            return FindReport("ReportTaxes");
        }

        public AutomationElement PortfolioReport()
        {
            ContextMenu subMenu = MainMenu.OpenSubMenu("MenuViewReports");
            subMenu.InvokeMenuItem("MenuReportsInvestment");
            reportOpen = true;
            return FindReport("ReportPortfolio");
        }

        public AutomationElement FindReport(string name)
        {
            AutomationElement report = window.FindFirstWithRetries(TreeScope.Children,
                new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (report == null)
            {
                throw new Exception(string.Format("Report '{0}' not found", name));
            }
            return report;
        }

    }
}
