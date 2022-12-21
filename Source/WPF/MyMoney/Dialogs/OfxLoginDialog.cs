using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Walkabout.Data;
using Walkabout.Ofx;

namespace Walkabout.Dialogs
{
    internal class OfxLoginDialog : PasswordWindow
    {
        private readonly OnlineAccount account;

        public OfxLoginDialog(OfxSignOnInfo info, OnlineAccount account, List<Block> prompt, OfxErrorCode code, string error)
        {
            this.account = account;

            this.UserNamePrompt = "User Name";
            this.UserName = this.account.UserId;

            // provide what we know already...
            this.UserName = this.account.UserId;
            this.PasswordConfirmation = this.account.Password;

            RichTextBox intro = this.IntroMessagePrompt;
            intro.Visibility = System.Windows.Visibility.Visible;

            intro.Document.Blocks.Clear();

            if (error != null)
            {
                this.ShowError(error);
            }
            else if (code == OfxErrorCode.SignonInvalid)
            {
                this.ShowError("Your sign on is invalid");
            }

            if (prompt == null)
            {
                prompt = new List<Block>();
            }
            if (prompt.Count == 0)
            {
                Paragraph instructions = new Paragraph();
                prompt.Add(instructions);
                instructions.Inlines.Add(new Run("Please enter your online banking credentials for "));
                instructions.Inlines.Add(new Run(this.account.Name) { FontWeight = FontWeights.Bold });
                instructions.Inlines.Add(new Run("."));
            }

            // try and gain the user's trust...            
            Uri url = new Uri(this.account.Ofx);
            Paragraph trust = new Paragraph();
            prompt.Add(trust);
            trust.Inlines.Add(new Run("These credentials will be sent securely using HTTPS to the OFX server at "));
            trust.Inlines.Add(new Run(url.Host) { FontWeight = FontWeights.Bold });
            trust.Inlines.Add(new Run(".  Click cancel if you are not sure that this is the right address."));

            foreach (Block b in prompt)
            {
                intro.Document.Blocks.Add(b);
            }

            if (info != null && !string.IsNullOrWhiteSpace(info.UserCredentialLabel1))
            {
                this.AddUserDefinedField("UserCred1", info.UserCredentialLabel1);
                this.SetUserDefinedField("UserCred1", this.account.UserCred1);
            }
            if (info != null && !string.IsNullOrWhiteSpace(info.UserCredentialLabel2))
            {
                this.AddUserDefinedField("UserCred2", info.UserCredentialLabel2);
                this.SetUserDefinedField("UserCred2", this.account.UserCred2);
            }

            OkClicked += new EventHandler<OkEventArgs>((s, e) =>
            {
                this.account.UserId = this.UserName;
                this.account.Password = this.PasswordConfirmation;

                if (info != null && !string.IsNullOrWhiteSpace(info.UserCredentialLabel1))
                {
                    this.account.UserCred1 = this.GetUserDefinedField("UserCred1");
                }
                if (info != null && !string.IsNullOrWhiteSpace(info.UserCredentialLabel2))
                {
                    this.account.UserCred2 = this.GetUserDefinedField("UserCred2");
                }

            });
        }
    }
}
