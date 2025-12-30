using System;
using System.Collections.Generic;
using System.Windows;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Importers;
using Walkabout.Utilities;
using Walkabout.Views.Controls;

namespace Walkabout.Ofx
{
    public class OfxDownloadController
    {
        private DownloadControl control;
        private readonly List<OfxThread> syncThreads = new List<OfxThread>();
        private MyMoney myMoney;
        private List<OnlineAccount> accounts;
        private string[] ofxFiles;
        private string tryAgainCaption;


        public OfxDownloadController(DownloadControl control)
        {
            this.control = control;
            control.DetailsClicked += this.OnDetailsClicked;
        }

        private void OnDetailsClicked(object sender, DownloadData details)
        {
            if (details != null)
            {
                switch (details.OfxError)
                {
                    case OfxErrorCode.AUTHTOKENRequired:
                    case OfxErrorCode.AUTHTOKENInvalid:
                        details.LinkCaption = "";
                        details.Message = "Getting authorization token...";
                        this.GetAuthenticationToken(details, details.OfxError);
                        return;
                    case OfxErrorCode.MFAChallengeAuthenticationRequired:
                        details.LinkCaption = "";
                        details.Message = "Getting MFA challenge questions...";
                        this.GetMFAChallenge(details);
                        return;
                    case OfxErrorCode.MustChangeUSERPASS:
                        details.LinkCaption = "";
                        details.Message = "Getting new password...";
                        this.GetNewPassword(details);
                        return;
                    case OfxErrorCode.SignonInvalid:
                        details.LinkCaption = "";
                        details.Message = "Logging in ...";
                        this.GetLogin(details);
                        return;
                }

                if (details.LinkCaption == this.tryAgainCaption)
                {
                    this.SyncOneAccount(details.OnlineAccount);
                    return;
                }


                string template = ProcessHelper.GetEmbeddedResource("Walkabout.Ofx.OfxErrorTemplate.htm");
                // css uses curly brackets, so it must be substituted.
                string css = @"body, th, td { font-family: Verdana; font-size:10pt; }
h2 { font-size: 12pt; }";

                string response = null;
                string headers = null;
                string message = details.Message;

                Exception error = details.Error;
                if (error != null)
                {
                    HtmlResponseException htmlError = error as HtmlResponseException;
                    if (htmlError != null)
                    {
                        message = htmlError.Message;
                        response = htmlError.Html;
                        headers = "";
                    }
                    else
                    {
                        OfxException ofxerror = error as OfxException;
                        if (ofxerror != null)
                        {
                            message = ofxerror.Message;
                            response = ofxerror.Response;
                            headers = ofxerror.HttpHeaders;
                        }
                        else
                        {
                            message = error.GetType().FullName + ": " + error.Message;
                            response = error.StackTrace;
                        }
                    }
                }
                OnlineAccount account = details.OnlineAccount;
                string result = string.Format(template,
                    css,
                    account != null ? account.Ofx : "",
                    account != null ? account.Institution : "",
                    message,
                    response,
                    headers);

                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DownloadError.htm");
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(tempPath))
                {
                    sw.WriteLine(result);
                }

                InternetExplorer.OpenUrl(IntPtr.Zero, tempPath);
            }
        }

        public void BeginImport(MyMoney money, string[] files)
        {
            this.myMoney = money;
            this.ofxFiles = files;
            this.Start();
        }

        public void Cancel()
        {
            this.control.DownloadEventTree.ItemsSource = null;

            this.control.Progress.Visibility = Visibility.Collapsed;

            foreach (OfxThread thread in this.syncThreads)
            {
                thread.Stop();
            }
            this.syncThreads.Clear();
        }

        public void BeginDownload(MyMoney money, List<OnlineAccount> accounts)
        {
            this.myMoney = money;
            this.accounts = accounts;
            this.ofxFiles = null;
            this.Start();
        }

        private void Start()
        {
            OfxThread thread = new OfxThread(this.myMoney, this.accounts, this.ofxFiles, AccountHelper.PickAccount, this.control.Dispatcher);
            this.syncThreads.Add(thread);
            thread.Status += new DownloadProgress(this.OnSyncUpdate);
            thread.Start();
        }

        private void SyncOneAccount(OnlineAccount account)
        {
            List<OnlineAccount> list = new List<OnlineAccount>();
            list.Add(account);
            OfxThread thread = new OfxThread(this.myMoney, list, null, AccountHelper.PickAccount, this.control.Dispatcher);
            this.syncThreads.Add(thread);
            thread.Status += new DownloadProgress(this.OnSyncUpdate);
            thread.Start();
        }

        private void OnSyncUpdate(int min, int max, int value, DownloadEventArgs e)
        {
            this.control.Progress.Minimum = min;
            this.control.Progress.Maximum = max;
            if (value >= 0 && value < max)
            {
                this.control.Progress.Visibility = Visibility.Visible;
                this.control.Progress.Value = value;
            }
            else
            {
                this.control.Progress.Visibility = Visibility.Collapsed;
            }

            this.control.DownloadEventTree.ItemsSource = e.Entries;
        }

        private OfxMfaChallengeRequest challenge;

        private void GetMFAChallenge(DownloadData ofxData)
        {
            this.challenge = new OfxMfaChallengeRequest(ofxData.OnlineAccount, this.myMoney);
            this.challenge.UserData = ofxData;
            this.challenge.Completed += this.OnChallengeCompleted;
            this.challenge.BeginMFAChallenge();
        }

        private void OnChallengeCompleted(object sender, EventArgs e)
        {
            OfxMfaChallengeRequest req = (OfxMfaChallengeRequest)sender;

            if (this.challenge != req)
            {
                // perhaps an old stale request just completed, so ignore it.
                return;
            }
            DownloadData data = (DownloadData)req.UserData;

            if (req.Error != null)
            {
                data.Message = "MFA Challenge Failed with error message: " + req.Error.Message;
            }
            else if (req.UserChallenges.Count > 0)
            {
                MfaChallengeDialog dialog = new MfaChallengeDialog();
                dialog.Owner = Application.Current.MainWindow; ;
                dialog.SetupQuestions(req.UserChallenges);
                if (dialog.ShowDialog() == true)
                {
                    var answers = req.BuiltInAnswers;
                    // add user answers
                    dialog.GetAnswers(req.UserChallenges, answers);

                    // store answers for use in next OFX request.
                    data.OnlineAccount.MfaChallengeAnswers = answers;
                }
                else
                {
                    // user cancelled.
                    data.Message = "User cancelled MFA Challenge";
                }
            }
            else
            {
                // store any built in answers.
                data.OnlineAccount.MfaChallengeAnswers = req.BuiltInAnswers;
            }

            if (data.OnlineAccount.MfaChallengeAnswers != null && data.OnlineAccount.MfaChallengeAnswers.Count > 0)
            {
                data.Message = "MFA Challenge Answers are ready to use";
                data.LinkCaption = this.tryAgainCaption = "Try Download Again";
                data.OfxError = OfxErrorCode.None;
                data.Error = null;
            }
        }

        private void GetAuthenticationToken(DownloadData ofxData, OfxErrorCode code)
        {
            var req = new OfxRequest(ofxData.OnlineAccount, this.myMoney, null);
            var info = req.GetSignonInfo();
            if (info != null)
            {
                this.PromptForAuthToken(ofxData, info, code);

                if (string.IsNullOrWhiteSpace(ofxData.OnlineAccount.AuthToken))
                {
                    // user cancelled.
                    ofxData.Message = "User cancelled";
                    return;
                }
                else
                {
                    ofxData.Message = "Your authorization token is ready to use";
                    ofxData.LinkCaption = this.tryAgainCaption = "Try Download Again";
                    ofxData.OfxError = OfxErrorCode.None;
                    ofxData.Error = null;
                }
            }
        }

        private void PromptForAuthToken(DownloadData ofxData, OfxSignOnInfo info, OfxErrorCode code)
        {
            ofxData.OnlineAccount.AuthToken = null;
            ofxData.OnlineAccount.AccessKey = null;

            AuthTokenDialog dialog = new AuthTokenDialog(info, code);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                ofxData.OnlineAccount.AuthToken = dialog.AuthorizationToken;
            }
        }

        private void GetNewPassword(DownloadData ofxData)
        {
            var req = new OfxRequest(ofxData.OnlineAccount, this.myMoney, null);
            var info = req.GetSignonInfo();

            ChangePasswordDialog dialog = new ChangePasswordDialog(info, ofxData.OnlineAccount, this.myMoney);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                ofxData.Message = "New password is accepted";
                ofxData.LinkCaption = this.tryAgainCaption = "Try Download Again";
                ofxData.OfxError = OfxErrorCode.None;
                ofxData.Error = null;
            }
            else
            {
                Exception ex = dialog.Error;
                if (ex != null)
                {
                    ofxData.Message = ex.Message;
                }
                else
                {
                    ofxData.Message = "User cancelled";
                }
            }
        }

        private void GetLogin(DownloadData ofxData)
        {
            var req = new OfxRequest(ofxData.OnlineAccount, this.myMoney, null);
            var info = req.GetSignonInfo();

            string msg = (ofxData.Error != null) ? ofxData.Error.Message : null;

            OfxLoginDialog login = new OfxLoginDialog(info, ofxData.OnlineAccount, null, ofxData.OfxError, msg);
            login.Owner = Application.Current.MainWindow;

            if (login.ShowDialog() == true)
            {
                ofxData.Message = "New credentials are ready.";
                ofxData.LinkCaption = this.tryAgainCaption = "Try Download Again";
                ofxData.OfxError = OfxErrorCode.None;
                ofxData.Error = null;
            }
            else
            {
                ofxData.Message = "User cancelled";
            }
        }

    }
}