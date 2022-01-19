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
            get { return GetTextBox("UserName"); }
            set { SetTextBox("UserName", value); }
        }

        public string Password
        {
            get { return GetTextBox("Password"); }
            set { SetTextBox("Password", value); }
        }

        public bool UseAdditionalCredentials
        {
            get { return GetRadioButton("UseAdditionalCredentials"); }
            set { SetRadioButton("UseAdditionalCredentials", value); }
        }

        public bool AuthTokenRequired
        {
            get { return GetRadioButton("AuthTokenRequired"); }
            set { SetRadioButton("AuthTokenRequired", value); }
        }

        public bool MFAChallengeRequired
        {
            get { return GetRadioButton("MFAChallengeRequired"); }
            set { SetRadioButton("MFAChallengeRequired", value); }
        }

        public bool ChangePasswordRequired
        {
            get { return GetRadioButton("ChangePasswordRequired"); }
            set { SetRadioButton("ChangePasswordRequired", value); }
        }


        public string UserCred1Label
        {
            get { return GetTextBox("UserCred1Label"); }
            set { SetTextBox("UserCred1Label", value); }
        }

        public string UserCred1
        {
            get { return GetTextBox("UserCred1"); }
            set { SetTextBox("UserCred1", value); }
        }

        public string UserCred2Label
        {
            get { return GetTextBox("UserCred2Label"); }
            set { SetTextBox("UserCred2Label", value); }
        }

        public string UserCred2
        {
            get { return GetTextBox("UserCred2"); }
            set { SetTextBox("UserCred2", value); }
        }


        public string AuthTokenLabel
        {
            get { return GetTextBox("AuthTokenLabel"); }
            set { SetTextBox("AuthTokenLabel", value); }
        }

        public string AuthToken
        {
            get { return GetTextBox("AuthToken"); }
            set { SetTextBox("AuthToken", value); }
        }
    }
}
