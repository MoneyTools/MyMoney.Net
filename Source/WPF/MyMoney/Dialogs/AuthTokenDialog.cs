using System.Windows.Controls;
using System.Windows.Documents;
using Walkabout.Ofx;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    public class AuthTokenDialog : PasswordWindow
    {
        string customFieldId = "AuthToken";

        public AuthTokenDialog(OfxSignOnInfo info, OfxErrorCode code)
        {
            this.PasswordPrompt = null;

            this.Title = "Authentication Token Required";
            
            string label = info.AuthTokenLabel;

            if (string.IsNullOrEmpty(info.AuthTokenLabel))
            {
                label = "Authentication token";
            }

            this.AddUserDefinedField(customFieldId, info.AuthTokenLabel);

            string url = info.AuthTokenInfoUrl;

            // add instructions pointing to AuthTokenInfoUrl
            RichTextBox box = this.IntroMessagePrompt;
            box.Document.Blocks.Clear();

            if (code == OfxErrorCode.AUTHTOKENInvalid)
            {
                this.ShowError("Your authentication token is invalid");                
            }

            Paragraph p = new Paragraph();
            p.Inlines.Add(new Run("An authentication token is intended to be used in conjunction with your user name and password " +
                "to help your financial institution ensure that your account information is not being accessed by an unauthorized user."));
            box.Document.Blocks.Add(p);

            p = new Paragraph();
            p.Inlines.Add(new Run("Please "));
            if (!string.IsNullOrEmpty(url))
            {
                p.Inlines.Add(InternetExplorer.GetOpenFileHyperlink("click on this link", info.AuthTokenInfoUrl));
                p.Inlines.Add(new Run(" for instructions on how get a new authentication token."));
            }
            else
            {
                p.Inlines.Add(new Run("Please call your bank for instructions on how to get a new authentication token."));
            }
            box.Document.Blocks.Add(p);

            box.Visibility = System.Windows.Visibility.Visible;
        }

        public string AuthorizationToken
        {
            get
            {
                return this.GetUserDefinedField(customFieldId);
            }
        }
    }
}
