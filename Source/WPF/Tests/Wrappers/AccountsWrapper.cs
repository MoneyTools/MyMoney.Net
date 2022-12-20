using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    class AccountsWrapper : ListViewWrapper
    {
        public AccountsWrapper(AutomationElement e) : base(e)
        {
        }

        public bool HasAccounts
        {
            get
            {
                return this.Accounts.Count > 0;
            }
        }

        public List<string> Accounts
        {
            get
            {
                List<string> names = new List<string>();
                foreach (AutomationElement e in this.Items)
                {
                    string name = e.Current.Name;
                    if (!name.Contains("AccountSectionHeader"))
                    {
                        names.Add(e.Current.AutomationId);
                    }
                }

                return names;
            }
        }

        public bool IsAccountSelected
        {
            get
            {
                return !string.IsNullOrEmpty(this.SelectedAccount);
            }
        }

        public string SelectedAccount
        {
            get
            {
                foreach (AutomationElement e in this.Selection)
                {
                    string name = e.Current.Name;
                    if (!name.Contains("AccountSectionHeader"))
                    {
                        return e.Current.AutomationId;
                    }
                }
                return null;
            }
        }

        public void AddAccount(string baseName, string type)
        {
            string name = this.GetUniqueCaption(baseName);

            ContextMenu menu = new ContextMenu(this.Element, true);
            menu.InvokeMenuItem("NewAccount");

            MainWindowWrapper mainWindow = MainWindowWrapper.FindMainWindow(this.Element.Current.ProcessId);
            AutomationElement child = mainWindow.Element.FindChildWindow("Account", 5);
            if (child != null)
            {
                AccountSettingsWrapper settings = new AccountSettingsWrapper(child);
                settings.Name = name;
                settings.AccountType = type;
                settings.ClickOk();
                return;
            }

            throw new Exception("AccountSettings dialog is not appearing!");
        }

        internal bool DeleteAccount(int index)
        {
            AutomationElement item = this.Select(index);
            string name = item.Current.Name;
            if (!name.Contains("AccountSectionHeader"))
            {
                ContextMenu menu = new ContextMenu(item, true);
                menu.InvokeMenuItem("DeleteAccount");

                MainWindowWrapper mainWindow = MainWindowWrapper.FindMainWindow(this.Element.Current.ProcessId);

                AutomationElement child = mainWindow.Element.FindChildWindow("Delete Account: " + item.Current.AutomationId, 5);
                if (child != null)
                {
                    MessageBoxWrapper msg = new MessageBoxWrapper(child);
                    msg.ClickYes();
                }
                else
                {
                    throw new Exception("Why is there no message box?");
                }
                return true;
            }
            else
            {
                return false;
            }

        }

        internal void SelectAccount(int index)
        {
            this.Select(index);
        }
    }
}
