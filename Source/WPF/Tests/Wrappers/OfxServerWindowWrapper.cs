using System;
using System.Threading;
using System.Windows.Automation;
using Walkabout.Tests.Interop;

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
                return this.window.Current.Name;
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
            get { return this.window.GetTextBox("UserName"); }
            set { this.window.SetTextBox("UserName", value); }
        }

        public string Password
        {
            get { return this.window.GetTextBox("Password"); }
            set { this.window.SetTextBox("Password", value); }
        }

        public bool UseAdditionalCredentials
        {
            get { return this.window.GetRadioButton("UseAdditionalCredentials"); }
            set { this.window.SetRadioButton("UseAdditionalCredentials", value); }
        }

        public bool AuthTokenRequired
        {
            get { return this.window.GetRadioButton("AuthTokenRequired"); }
            set { this.window.SetRadioButton("AuthTokenRequired", value); }
        }

        public bool MFAChallengeRequired
        {
            get { return this.window.GetRadioButton("MFAChallengeRequired"); }
            set { this.window.SetRadioButton("MFAChallengeRequired", value); }
        }

        public bool ChangePasswordRequired
        {
            get { return this.window.GetRadioButton("ChangePasswordRequired"); }
            set { this.window.SetRadioButton("ChangePasswordRequired", value); }
        }


        public string UserCred1Label
        {
            get { return this.window.GetTextBox("UserCred1Label"); }
            set { this.window.SetTextBox("UserCred1Label", value); }
        }

        public string UserCred1
        {
            get { return this.window.GetTextBox("UserCred1"); }
            set { this.window.SetTextBox("UserCred1", value); }
        }

        public string UserCred2Label
        {
            get { return this.window.GetTextBox("UserCred2Label"); }
            set { this.window.SetTextBox("UserCred2Label", value); }
        }

        public string UserCred2
        {
            get { return this.window.GetTextBox("UserCred2"); }
            set { this.window.SetTextBox("UserCred2", value); }
        }


        public string AuthTokenLabel
        {
            get { return this.window.GetTextBox("AuthTokenLabel"); }
            set { this.window.SetTextBox("AuthTokenLabel", value); }
        }

        public string AuthToken
        {
            get { return this.window.GetTextBox("AuthToken"); }
            set { this.window.SetTextBox("AuthToken", value); }
        }
    }
}
