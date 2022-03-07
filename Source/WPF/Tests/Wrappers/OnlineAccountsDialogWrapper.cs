using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;
using System;

namespace Walkabout.Tests.Wrappers
{
    public class OnlineAccountsDialogWrapper : DialogWrapper
    {
        internal OnlineAccountsDialogWrapper(AutomationElement e)
            : base(e)
        {
        }

        #region Main 

        public void WaitForGetBankList()
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ComboBoxName"));
            if (box == null)
            {
                throw new Exception("ComboBoxName not found");
            }
            while (box.Current.IsOffscreen)
            {
                Thread.Sleep(100);
            }
            // the list does something async so we have to wait more...
            Thread.Sleep(500);
        }

        public string Name
        {
            get { return window.GetComboBoxText("ComboBoxName"); }
            set { window.SetComboBoxText("ComboBoxName", value);  }
        }

        public string Institution
        {
            get { return window.GetTextBox("TextBoxInstitution"); }
            set { window.SetTextBox("TextBoxInstitution", value); }
        }

        public string FID
        {
            get { return window.GetTextBox("TextBoxFid"); }
            set { window.SetTextBox("TextBoxFid", value); }
        }

        public string BankId
        {
            get { return window.GetTextBox("TextBoxBankId"); }
            set { window.SetTextBox("TextBoxBankId", value); }
        }

        public string BranchId
        {
            get { return window.GetTextBox("TextBoxBranchId"); }
            set { window.SetTextBox("TextBoxBranchId", value); }
        }

        public string BrokerId
        {
            get { return window.GetTextBox("TextBoxBrokerId"); }
            set { window.SetTextBox("TextBoxBrokerId", value); }
        }

        public string OfxAddress
        {
            get { return window.GetTextBox("TextBoxOfxAddress"); }
            set { window.SetTextBox("TextBoxOfxAddress", value); }
        }

        public string OfxVersion
        {
            get { return window.GetComboBoxSelection("OfxVersions"); }
            set { window.SetComboBox("OfxVersions", value); }
        }

        public string AppId
        {
            get { return window.GetTextBox("TextBoxAppId"); }
            set { window.SetTextBox("TextBoxAppId", value); }
        }

        public string AppVersion
        {
            get { return window.GetTextBox("TextBoxAppVersion"); }
            set { window.SetTextBox("TextBoxAppVersion", value); }
        }

        public string ClientUid
        {
            get { return window.GetTextBox("TextBoxClientUid"); }
            set { window.SetTextBox("TextBoxClientUid", value); }
        }

        public PasswordDialogWrapper ClickConnect()
        {
            window.ClickButton("ButtonVerify");

            AutomationElement pswd = window.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "PasswordDialog"), 10, 500);
            
            if (pswd != null) {
                return new PasswordDialogWrapper(pswd);
            }

            return null;
        }

        #endregion 

        #region Profile

        public string ProfileName
        {
            get { return window.GetTextBox("TextBoxName"); }
            set { window.SetTextBox("TextBoxName", value); }
        }

        public string Address
        {
            get { return window.GetTextBox("TextBoxAddress"); }
            set { window.SetTextBox("TextBoxAddress", value); }
        }

        public string City
        {
            get { return window.GetTextBox("TextBoxCity"); }
            set { window.SetTextBox("TextBoxCity", value); }
        }

        public string Phone
        {
            get { return window.GetTextBox("TextBoxPhone"); }
            set { window.SetTextBox("TextBoxPhone", value); }
        }

        public string Url
        {
            get { return window.GetTextBox("TextBoxUrl"); }
            set { window.SetTextBox("TextBoxUrl", value); }
        }

        public string Email
        {
            get { return window.GetTextBox("TextBoxEmail"); }
            set { window.SetTextBox("TextBoxEmail", value); }
        }

        public string UserId
        {
            get { return window.GetTextBox("TextBoxUserId"); }
            set { window.SetTextBox("TextBoxUserId", value); }
        }

        public string Password
        {
            get { return window.GetTextBox("TextBoxPassword"); }
            set { window.SetTextBox("TextBoxPassword", value); }
        }

        public void ClickSignOn()
        {
            window.ClickButton("ButtonSignOn");
        }

        #endregion 

        #region 

        // OnlineResultList datagrid
        public IEnumerable<OnlineAccountItem> GetOnlineAccounts()
        {
            List<OnlineAccountItem> list = new List<OnlineAccountItem>();
            AutomationElement grid = this.Element.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "OnlineResultList"));
            if (grid == null)
            {
                return list;
            }

            foreach (AutomationElement e in grid.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
            {
                list.Add(new OnlineAccountItem(e));
            }

            return list;
        }


        #endregion 

        public void ClickOk()
        {
            window.ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            window.ClickButton("ButtonCancel");
        }

    }

    public class OnlineAccountItem 
    {
        AutomationElement item;
        AutomationElement id;
        AutomationElement name;
        AutomationElement iconButton;

        public OnlineAccountItem(AutomationElement item) 
        {
            this.item = item;
            int i = 0;
            foreach (AutomationElement textBlock in item.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
            {
                if (i == 0)
                {
                    id = textBlock;
                }
                else if (i == 1)
                {
                    name = textBlock;
                }
                i++;
            }

            iconButton = item.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "IconButton"));
            if (iconButton == null)
            {
                throw new Exception("IconButton button not found");
            }
        }

        public string Id
        {
            get
            {
                if (id != null)
                {
                    return id.Current.Name;
                }
                return null;                
            }
        }

        public string Name
        {
            get
            {
                if (name != null)
                {
                    return id.Current.Name;
                }
                return null;
            }
        }

        public bool HasAddButton
        {
            get
            {
                return iconButton != null;
            }
        }

        public void ClickAdd()
        {
            if (iconButton != null)
            {
                InvokePattern invoke = (InvokePattern)iconButton.GetCurrentPattern(InvokePattern.Pattern);
                invoke.Invoke();
                return;
            }                
            throw new Exception("AddAccount Button not found");
        }
    }
}
