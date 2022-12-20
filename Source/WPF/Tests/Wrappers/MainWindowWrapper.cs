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
            ContextMenu subMenu = this.MainMenu.OpenSubMenu("MenuFile");
            subMenu.InvokeMenuItem("MenuFileNew");

            Thread.Sleep(300);

            AutomationElement child = this.Element.FindChildWindow("Save Changes", 1);
            if (child != null)
            {
                MessageBoxWrapper msg = new MessageBoxWrapper(child);
                msg.ClickNo();
                Thread.Sleep(300);
            }

            child = this.Element.FindChildWindow("New Database", 1);
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
                return this.window.Current.Name;
            }
        }

        internal ContextMenu MainMenu
        {
            get
            {
                AutomationElement mainMenu = this.window.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "MainMenu"));
                if (mainMenu == null)
                {
                    throw new Exception("MainMenu not found");
                }
                return new ContextMenu(mainMenu, false);
            }
        }

        internal AccountsWrapper ViewAccounts()
        {
            this.accounts = new AccountsWrapper(this.Element.Expand("AccountsSelector"));
            this.categories = null;
            this.payees = null;
            this.securities = null;
            this.WaitForInputIdle(500);
            return this.accounts;
        }

        internal CategoriesWrapper ViewCategories()
        {
            this.categories = new CategoriesWrapper(this.Element.Expand("CategoriesSelector"));
            //accounts = null;
            this.payees = null;
            this.securities = null;
            this.WaitForInputIdle(500);
            return this.categories;
        }

        internal PayeesWrapper ViewPayees()
        {
            this.payees = new PayeesWrapper(this.Element.Expand("PayeesSelector"));
            //accounts = null;
            this.categories = null;
            this.securities = null;
            this.WaitForInputIdle(500);
            return this.payees;
        }

        internal SecuritiesWrapper ViewSecurities()
        {
            this.securities = new SecuritiesWrapper(this.Element.Expand("SecuritiesSelector"));
            //accounts = null;
            this.categories = null;
            this.payees = null;
            this.WaitForInputIdle(500);
            return this.securities;
        }

        public bool IsAccountSelected
        {
            get
            {
                if (this.accounts != null) return this.accounts.IsAccountSelected;
                return false;
            }
        }

        public bool IsCategorySelected
        {
            get
            {
                if (this.categories != null) return this.categories.IsCategorySelected;
                return false;
            }
        }

        public bool HasCategories
        {
            get
            {
                if (this.categories != null) return this.categories.HasCategories;
                return false;
            }
        }

        public bool IsPayeeSelected
        {
            get
            {
                if (this.payees != null) return this.payees.IsPayeeSelected;
                return false;
            }
        }

        public bool IsSecuritySelected
        {
            get
            {
                if (this.securities != null) return this.securities.IsSecuritySelected;
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
            this.CloseReport();

            AutomationElement e = FindTransactionGrid(this.window);
            if (e == null)
            {
                throw new Exception("Couldn't find a visible data grid");
            }

            return new TransactionViewWrapper(this, e);
        }

        internal void Save()
        {
            ContextMenu menu = this.MainMenu.OpenSubMenu("MenuFile");
            menu.InvokeMenuItem("MenuFileSave");
            Thread.Sleep(1000);
            this.WaitForInputIdle(1000);
        }

        internal OnlineAccountsDialogWrapper DownloadAccounts()
        {
            ContextMenu menu = this.MainMenu.OpenSubMenu("MenuOnline");
            menu.InvokeMenuItem("MenuOnlineDownloadAccounts");

            AutomationElement window = this.Element.FindChildWindow("Online Account", 5);
            return new OnlineAccountsDialogWrapper(window);
        }


        internal void Synchronize()
        {
            this.Element.ClickButton("ButtonSynchronize");
        }

        internal ChartsAreaWrapper GetChartsArea()
        {
            // 
            AutomationElement charts = this.window.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "TabForGraphs"));
            if (charts == null)
            {
                throw new Exception("Tab named 'TabForGraphs' not found");
            }
            return new ChartsAreaWrapper(charts);
        }

        bool reportOpen;

        public void ResetReport()
        {
            if (this.reportOpen)
            {
                AutomationElement closeButton = this.window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "CloseReport"));
                if (closeButton == null || closeButton.Current.IsOffscreen)
                {
                    this.reportOpen = false;
                }
            }
        }

        public void CloseReport()
        {
            if (this.reportOpen)
            {
                AutomationElement closeButton = this.window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "CloseReport"));
                if (closeButton != null)
                {
                    InvokePattern invoke = (InvokePattern)closeButton.GetCurrentPattern(InvokePattern.Pattern);
                    invoke.Invoke();
                }
                else
                {
                    Debug.WriteLine("### Close button named 'CloseReport' not found");
                }
                this.reportOpen = false;
            }
        }

        public AutomationElement NetWorthReport()
        {
            ContextMenu subMenu = this.MainMenu.OpenSubMenu("MenuViewReports");
            subMenu.InvokeMenuItem("MenuReportsNetWorth");
            this.reportOpen = true;
            return this.FindReport("ReportNetworth");
        }

        public AutomationElement TaxReport()
        {
            ContextMenu subMenu = this.MainMenu.OpenSubMenu("MenuViewReports");
            subMenu.InvokeMenuItem("MenuReportsTaxReport");
            this.reportOpen = true;
            return this.FindReport("ReportTaxes");
        }

        public AutomationElement PortfolioReport()
        {
            ContextMenu subMenu = this.MainMenu.OpenSubMenu("MenuViewReports");
            subMenu.InvokeMenuItem("MenuReportsInvestment");
            this.reportOpen = true;
            return this.FindReport("ReportPortfolio");
        }

        public AutomationElement FindReport(string name)
        {
            AutomationElement report = this.window.FindFirstWithRetries(TreeScope.Children,
                new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (report == null)
            {
                throw new Exception(string.Format("Report '{0}' not found", name));
            }
            return report;
        }

    }
}
