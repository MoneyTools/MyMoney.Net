using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;
using System.IO;

namespace Walkabout.Tests.Wrappers
{
    class CreateDatabaseDialogWrapper
    {        
        AutomationElement window;

        private CreateDatabaseDialogWrapper(AutomationElement e) 
        {
            window = e;
        }

        public static CreateDatabaseDialogWrapper FindCreateDatabaseDialogWindow(int processId, int retries, bool throwIfNotFound)
        {
            for (int i = 0; i < retries; i++)
            {
                AutomationElement e = Win32.FindWindow(processId, "CreateDatabaseDialog");
                if (e != null)
                {
                    return new CreateDatabaseDialogWrapper(e);
                }

                Thread.Sleep(1000);
            }

            if (throwIfNotFound)
            {
                throw new Exception("CreateDatabaseDialog not found for process " + processId);
            }

            return null;
        }

        internal void CreateSqliteDatabase(string databasePath)
        {            
            SetTextBox("TextBoxSqliteDatabaseFile", databasePath);
            ClickButton("ButtonCreate");
        }
        

        private void ClickOkIfExists()
        {
            // if database exists, click "yes"...
            MainWindowWrapper main = MainWindowWrapper.FindMainWindow(window.Current.ProcessId);
            bool found = true;
            do
            {
                found = false;
                foreach (string title in new string[] { "Database Exists", "Need to Elevate" })
                {
                    AutomationElement child = main.FindChildWindow(title, 2);
                    if (child != null)
                    {
                        MessageBoxWrapper msg = new MessageBoxWrapper(child);
                        if (msg != null)
                        {
                            msg.ClickOk();
                        }
                        found = true;
                    }
                }
            }
            while (found);
        }

        internal void CreateXmlDatabase(string databasePath)
        {
            SelectTab("UseXmlTab");
            SetTextBox("TextBoxXmlFile", databasePath);
            ClickButton("ButtonCreate");
        }

        internal void CreateBinaryXmlDatabase(string databasePath)
        {
            SelectTab("UseBinaryXmlTab");
            SetTextBox("TextBoxBinaryXmlFile", databasePath);
            ClickButton("ButtonCreate");
        }

        private void ClickButton(string name)
        {
            AutomationElement tab = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (tab == null)
            {
                throw new Exception("Button '" + name + "' not found");
            }
            InvokePattern invoke = (InvokePattern)tab.GetCurrentPattern(InvokePattern.Pattern);
            invoke.Invoke();
        }

        private void SelectTab(string name)
        {
            AutomationElement tab = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (tab == null)
            {
                throw new Exception("Tab '" + name + "' not found");
            } 
            SelectionItemPattern selectionItem = (SelectionItemPattern)tab.GetCurrentPattern(SelectionItemPattern.Pattern);
            selectionItem.Select();
        }

        private bool IsControlEnabled(string name)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("Control '" + name + "' not found");
            } 
            return box.Current.IsEnabled;
        }

        private void SetTextBox(string name, string databasePath)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("TextBox '" + name + "' not found");
            } 
            try
            {
                ValuePattern value = (ValuePattern)box.GetCurrentPattern(ValuePattern.Pattern);
                value.SetValue(databasePath);
            }
            catch (Exception)
            {
                throw;
            }
        }

    }
}
