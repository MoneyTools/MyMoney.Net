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
    public class OfxServerWindowWrapper : DialogWrapper
    {

        private OfxServerWindowWrapper(AutomationElement e)
            : base(e)
        {
        }

        internal string Title
        {
            get
            {
                return window.Current.Name;
            }
        }

        public static OfxServerWindowWrapper FindMainWindow(int processId)
        {
            for (int i = 0; i < 10; i++)
            {
                AutomationElement e = Win32.FindWindow(processId, "OFXTestServerWindow");
                if (e != null)
                {
                    return new OfxServerWindowWrapper(e);
                }

                Thread.Sleep(1000);
            }

            throw new Exception("OfxServerWindow not found for process " + processId);
        }

        public string UserName
        {
            get { return window.GetTextBox("UserName"); }
            set { window.SetTextBox("UserName", value); }
        }

        public string Password
        {
            get { return window.GetTextBox("Password"); }
            set { window.SetTextBox("Password", value); }
        }

        public bool UseAdditionalCredentials
        {
            get { return window.GetRadioButton("UseAdditionalCredentials"); }
            set { window.SetRadioButton("UseAdditionalCredentials", value); }
        }

        public bool AuthTokenRequired
        {
            get { return window.GetRadioButton("AuthTokenRequired"); }
            set { window.SetRadioButton("AuthTokenRequired", value); }
        }

        public bool MFAChallengeRequired
        {
            get { return window.GetRadioButton("MFAChallengeRequired"); }
            set { window.SetRadioButton("MFAChallengeRequired", value); }
        }

        public bool ChangePasswordRequired
        {
            get { return window.GetRadioButton("ChangePasswordRequired"); }
            set { window.SetRadioButton("ChangePasswordRequired", value); }
        }


        public string UserCred1Label
        {
            get { return window.GetTextBox("UserCred1Label"); }
            set { window.SetTextBox("UserCred1Label", value); }
        }

        public string UserCred1
        {
            get { return window.GetTextBox("UserCred1"); }
            set { window.SetTextBox("UserCred1", value); }
        }

        public string UserCred2Label
        {
            get { return window.GetTextBox("UserCred2Label"); }
            set { window.SetTextBox("UserCred2Label", value); }
        }

        public string UserCred2
        {
            get { return window.GetTextBox("UserCred2"); }
            set { window.SetTextBox("UserCred2", value); }
        }


        public string AuthTokenLabel
        {
            get { return window.GetTextBox("AuthTokenLabel"); }
            set { window.SetTextBox("AuthTokenLabel", value); }
        }

        public string AuthToken
        {
            get { return window.GetTextBox("AuthToken"); }
            set { window.SetTextBox("AuthToken", value); }
        }
    }
}
