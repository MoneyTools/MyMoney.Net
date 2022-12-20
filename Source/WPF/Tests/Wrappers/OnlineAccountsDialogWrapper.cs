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
            AutomationElement box = this.window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ComboBoxName"));
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
            get { return this.window.GetComboBoxText("ComboBoxName"); }
            set { this.window.SetComboBoxText("ComboBoxName", value); }
        }

        public string Institution
        {
            get { return this.window.GetTextBox("TextBoxInstitution"); }
            set { this.window.SetTextBox("TextBoxInstitution", value); }
        }

        public string FID
        {
            get { return this.window.GetTextBox("TextBoxFid"); }
            set { this.window.SetTextBox("TextBoxFid", value); }
        }

        public string BankId
        {
            get { return this.window.GetTextBox("TextBoxBankId"); }
            set { this.window.SetTextBox("TextBoxBankId", value); }
        }

        public string BranchId
        {
            get { return this.window.GetTextBox("TextBoxBranchId"); }
            set { this.window.SetTextBox("TextBoxBranchId", value); }
        }

        public string BrokerId
        {
            get { return this.window.GetTextBox("TextBoxBrokerId"); }
            set { this.window.SetTextBox("TextBoxBrokerId", value); }
        }

        public string OfxAddress
        {
            get { return this.window.GetTextBox("TextBoxOfxAddress"); }
            set { this.window.SetTextBox("TextBoxOfxAddress", value); }
        }

        public string OfxVersion
        {
            get { return this.window.GetComboBoxSelection("OfxVersions"); }
            set { this.window.SetComboBox("OfxVersions", value); }
        }

        public string AppId
        {
            get { return this.window.GetTextBox("TextBoxAppId"); }
            set { this.window.SetTextBox("TextBoxAppId", value); }
        }

        public string AppVersion
        {
            get { return this.window.GetTextBox("TextBoxAppVersion"); }
            set { this.window.SetTextBox("TextBoxAppVersion", value); }
        }

        public string ClientUid
        {
            get { return this.window.GetTextBox("TextBoxClientUid"); }
            set { this.window.SetTextBox("TextBoxClientUid", value); }
        }

        public PasswordDialogWrapper ClickConnect()
        {
            this.window.ClickButton("ButtonVerify");

            AutomationElement pswd = this.window.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "PasswordDialog"), 10, 500);

            if (pswd != null)
            {
                return new PasswordDialogWrapper(pswd);
            }

            return null;
        }

        #endregion 

        #region Profile

        public string ProfileName
        {
            get { return this.window.GetTextBox("TextBoxName"); }
            set { this.window.SetTextBox("TextBoxName", value); }
        }

        public string Address
        {
            get { return this.window.GetTextBox("TextBoxAddress"); }
            set { this.window.SetTextBox("TextBoxAddress", value); }
        }

        public string City
        {
            get { return this.window.GetTextBox("TextBoxCity"); }
            set { this.window.SetTextBox("TextBoxCity", value); }
        }

        public string Phone
        {
            get { return this.window.GetTextBox("TextBoxPhone"); }
            set { this.window.SetTextBox("TextBoxPhone", value); }
        }

        public string Url
        {
            get { return this.window.GetTextBox("TextBoxUrl"); }
            set { this.window.SetTextBox("TextBoxUrl", value); }
        }

        public string Email
        {
            get { return this.window.GetTextBox("TextBoxEmail"); }
            set { this.window.SetTextBox("TextBoxEmail", value); }
        }

        public string UserId
        {
            get { return this.window.GetTextBox("TextBoxUserId"); }
            set { this.window.SetTextBox("TextBoxUserId", value); }
        }

        public string Password
        {
            get { return this.window.GetTextBox("TextBoxPassword"); }
            set { this.window.SetTextBox("TextBoxPassword", value); }
        }

        public void ClickSignOn()
        {
            this.window.ClickButton("ButtonSignOn");
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
            this.window.ClickButton("ButtonOk");
        }

        public void ClickCancel()
        {
            this.window.ClickButton("ButtonCancel");
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
                    this.id = textBlock;
                }
                else if (i == 1)
                {
                    this.name = textBlock;
                }
                i++;
            }

            this.iconButton = item.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "IconButton"));
            if (this.iconButton == null)
            {
                throw new Exception("IconButton button not found");
            }
        }

        public string Id
        {
            get
            {
                if (this.id != null)
                {
                    return this.id.Current.Name;
                }
                return null;
            }
        }

        public string Name
        {
            get
            {
                if (this.name != null)
                {
                    return this.id.Current.Name;
                }
                return null;
            }
        }

        public bool HasAddButton
        {
            get
            {
                return this.iconButton != null;
            }
        }

        public void ClickAdd()
        {
            if (this.iconButton != null)
            {
                InvokePattern invoke = (InvokePattern)this.iconButton.GetCurrentPattern(InvokePattern.Pattern);
                invoke.Invoke();
                return;
            }
            throw new Exception("AddAccount Button not found");
        }
    }
}
