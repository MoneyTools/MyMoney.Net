﻿using System;
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
                return Accounts.Count > 0;
            }
        }

        public List<string> Accounts
        {
            get
            {
                List<string> names = new List<string>();
                foreach (AutomationElement e in Items)
                {
                    string name = e.Current.Name;
                    if (!name.Contains("AccountSectionHeader"))
                    {
                        names.Add(name);
                    }
                }

                return names;
            }
        }

        public bool IsAccountSelected
        {
            get
            {
                return !string.IsNullOrEmpty(SelectedAccount);
            }
        }

        public string SelectedAccount
        {
            get
            {
                foreach (AutomationElement e in Selection)
                {
                    string name = e.Current.Name;
                    if (!name.Contains("AccountSectionHeader"))
                    {
                        return name;
                    }
                }
                return null;
            }
        }

        public void AddAccount(string baseName, string type)
        {
            string name = GetUniqueCaption(baseName);

            ContextMenu menu = new ContextMenu(this.Element, true);
            menu.InvokeMenuItem("NewAccount");

            MainWindowWrapper mainWindow = MainWindowWrapper.FindMainWindow(Element.Current.ProcessId);
            AutomationElement child = mainWindow.FindChildWindow("Account", 5);
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
            AutomationElement item = Select(index);
            string name = item.Current.Name;
            if (name != "Walkabout.Data.AccountSectionHeader")
            {
                ContextMenu menu = new ContextMenu(item, true);
                menu.InvokeMenuItem("DeleteAccount");

                MainWindowWrapper mainWindow = MainWindowWrapper.FindMainWindow(Element.Current.ProcessId);

                AutomationElement child = mainWindow.FindChildWindow("Delete Account: " + name, 5);
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
            AutomationElement e = Select(index);            
        }
    }
}
