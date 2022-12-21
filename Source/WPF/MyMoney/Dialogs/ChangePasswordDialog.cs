using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using Walkabout.Data;
using Walkabout.Ofx;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// This class prompts user for new password, AND attempts to actually change the password
    /// with the online server, it will close the dialog when change is complete or if an error occurs.
    /// </summary>
    public class ChangePasswordDialog : PasswordWindow
    {
        private readonly OnlineAccount account;
        private readonly MyMoney money;
        private readonly string newPasswordFieldId = "NewPassword";
        private readonly string confirmPasswordFieldId = "ConfirmNewPassword";

        public ChangePasswordDialog(OfxSignOnInfo info, OnlineAccount account, MyMoney money)
        {
            this.account = account;
            this.money = money;
            this.UserNamePrompt = "User Name";
            this.UserName = account.UserId;
            this.Title = "Change Password";

            RichTextBox intro = this.IntroMessagePrompt;
            intro.Visibility = System.Windows.Visibility.Visible;

            intro.Document.Blocks.Clear();
            Paragraph p = new Paragraph();
            intro.Document.Blocks.Add(p);

            this.AddUserDefinedField(this.newPasswordFieldId, "New Password");
            this.AddUserDefinedField(this.confirmPasswordFieldId, "Confirm New Password");

            p.Inlines.Add(new Run(account.Name + " is requesting that you enter a new password."));

            OkClicked += new EventHandler<OkEventArgs>((s, e) =>
            {
                string newpswd = this.GetUserDefinedField("NewPassword");
                string newpswdconfirm = this.GetUserDefinedField("ConfirmNewPassword");
                if (newpswd != newpswdconfirm)
                {
                    e.Cancel = true;
                    e.Error = "New passwords do not match";
                    return;
                }
                if (info != null && newpswd.Length < info.MinimumLength)
                {
                    e.Cancel = true;
                    e.Error = "New password must be at least " + info.MinimumLength + " characters";
                    return;
                }
                if (info != null && info.MaximumLength != 0 && newpswd.Length > info.MaximumLength)
                {
                    e.Cancel = true;
                    e.Error = "New password cannot be more than " + info.MaximumLength + " characters";
                    return;
                }

                // capture value so thread can use it.
                this.newPassword = this.NewPassword;

                account.UserId = this.UserName;
                account.Password = this.PasswordConfirmation;

                Task.Run(this.ChangePassword);
                e.Error = "Sending new password information to " + account.Name + ".\nPlease do NOT close this dialog until we get a response.";
                e.Cancel = true;

                // stop user from hitting OK again and stop user from trying to cancel, 
                // cancel is problematic because we won't know if the server
                // received the password change or not!
                this.DisableButtons();
            });

        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            this.cancelled = true;
        }

        private string newPassword; // so thread can access it.
        private bool cancelled;
        private string logFile;

        // background thread
        private void ChangePassword()
        {
            OfxRequest req = new OfxRequest(this.account, this.money, null);
            try
            {
                req.ChangePassword(this.account, this.newPassword, out this.logFile);
            }
            catch (Exception e)
            {
                this.Error = e;
            }
            if (this.cancelled)
            {
                return;
            }

            UiDispatcher.BeginInvoke(new Action(() =>
            {
                if (this.Error != null)
                {
                    // The error might be about the new password being invalid for some reason, so show
                    // the message and let user try again.
                    this.ShowError(this.Error.Message);
                    this.EnableButtons();
                }
                else
                {
                    this.DialogResult = true;
                    this.account.Password = this.GetUserDefinedField("NewPassword");
                    this.Close();
                }

            }));
        }

        /// <summary>
        /// This is returned if the change password request fails for some reason.
        /// </summary>
        public Exception Error { get; set; }

        /// <summary>
        /// Get the new password that was confirmed and registered with the online account
        /// </summary>
        public string NewPassword
        {
            get
            {
                return this.GetUserDefinedField(this.newPasswordFieldId);
            }
        }

        /// <summary>
        /// Get the OFX error log.
        /// </summary>
        public string OfxLogFile { get { return this.logFile; } }
    }
}
