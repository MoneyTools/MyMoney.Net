using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.StockQuotes;
using System.Windows.Documents;
using Walkabout.Utilities;
using System;
using System.IO;
using Walkabout.Configuration;
using Walkabout.Interfaces;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Walkabout.Ofx;


namespace Walkabout.Views.Controls
{
    public class OfxDocumentControlSelectionChangedEventArgs : EventArgs
    {
        public OfxDownloadData Data { get; set; }
    }

    /// <summary>
    /// Interaction logic for OfxDownloadControl.xaml
    /// </summary>
    public partial class OfxDownloadControl : UserControl
    {
        private List<OfxThread> syncThreads = new List<OfxThread>();
        private MyMoney myMoney;
        private List<OnlineAccount> accounts;
        private string[] ofxFiles;

        public OfxDownloadControl()
        {
            InitializeComponent();

            OfxEventTree.SelectedItemChanged += new RoutedPropertyChangedEventHandler<object>(OnSelectedItemChanged);
        }

        public event EventHandler<OfxDocumentControlSelectionChangedEventArgs> SelectionChanged;

        void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            OfxDownloadData selection = e.NewValue as OfxDownloadData;
            if (selection != null && selection.Added.Count > 0)
            {
                if (SelectionChanged != null)
                {
                    SelectionChanged(this, new OfxDocumentControlSelectionChangedEventArgs() { Data = selection });
                }
            }
        }

        public void BeginImport(MyMoney money, string[] files)
        {
            this.myMoney = money;
            this.ofxFiles = files;
            Start();
        }

        public void Cancel()
        {
            this.OfxEventTree.ItemsSource = null;

            this.Progress.Visibility = Visibility.Collapsed;

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
            Start();
        }

        private void Start()
        {
            OfxThread thread = new OfxThread(myMoney, accounts, this.ofxFiles, AccountHelper.PickAccount, this.Dispatcher);
            this.syncThreads.Add(thread);
            thread.Status += new OfxDownloadProgress(this.OnSyncUpdate);
            thread.Start();
        }

        private void SyncOneAccount(OnlineAccount account)
        {
            List<OnlineAccount> list = new List<OnlineAccount>();
            list.Add(account);
            OfxThread thread = new OfxThread(myMoney, list, null, AccountHelper.PickAccount, this.Dispatcher);
            this.syncThreads.Add(thread);
            thread.Status += new OfxDownloadProgress(this.OnSyncUpdate);
            thread.Start();
        }

        void OnSyncUpdate(int min, int max, int value, OfxDownloadEventArgs e)
        {
            this.Progress.Minimum = min;
            this.Progress.Maximum = max;
            if (value >= 0 && value < max)
            {
                this.Progress.Visibility = Visibility.Visible;
                this.Progress.Value = value;
            }
            else
            {
                this.Progress.Visibility = Visibility.Collapsed;
            }

            PreprocessEntries(e.Entries);

            this.OfxEventTree.ItemsSource = e.Entries;
        }

        void PreprocessEntries(IEnumerable<OfxDownloadData> list)
        {
            foreach (OfxDownloadData item in list)
            {
                //item.OfxError
                if (item.Children != null)
                {
                    PreprocessEntries(item.Children);
                }
                PreprocessEntry(item);
            }
        }

        void PreprocessEntry(OfxDownloadData entry)
        {
            switch (entry.OfxError)
            {
                case OfxErrorCode.AUTHTOKENRequired:
                    entry.LinkCaption = "Get Authentication Token";
                    break;
                case OfxErrorCode.MFAChallengeAuthenticationRequired:
                    entry.LinkCaption = "Provide More Authentication";
                    break;
                case OfxErrorCode.MustChangeUSERPASS:
                    entry.LinkCaption = "Change Password";
                    break;
                case OfxErrorCode.SignonInvalid:
                    entry.LinkCaption = "Login";
                    break;
            }
        }

        private void ButtonRemoveOnlineAccount_Clicked(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            if (b != null)
            {
                OfxDownloadData ofxData = b.DataContext as OfxDownloadData;

                if (ofxData != null)
                {
                    OnlineAccount oa = ofxData.OnlineAccount;

                    if (oa != null)
                    {
                        MessageBoxResult result = MessageBoxEx.Show("Permanently delete the online account \"" + ofxData.Caption + "\"", null, MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            oa.OnDelete();
                            foreach (Account a in myMoney.Accounts.GetAccounts())
                            {
                                if (a.OnlineAccount == oa)
                                {
                                    a.OnlineAccount = null;
                                }
                            }
                        }
                    }

                    ThreadSafeObservableCollection<OfxDownloadData> entries = this.OfxEventTree.ItemsSource as ThreadSafeObservableCollection<OfxDownloadData>;
                    entries.Remove(ofxData);
                }
            }
        }

        string tryAgainCaption;

        private void OnDetailsClick(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            OfxDownloadData ofxData = link.DataContext as OfxDownloadData;
            if (ofxData != null)
            {
                switch (ofxData.OfxError)
                {
                    case OfxErrorCode.AUTHTOKENRequired:
                    case OfxErrorCode.AUTHTOKENInvalid:
                        ofxData.LinkCaption = "";
                        ofxData.Message = "Getting authorization token...";
                        GetAuthenticationToken(ofxData, ofxData.OfxError);
                        return;
                    case OfxErrorCode.MFAChallengeAuthenticationRequired:
                        ofxData.LinkCaption = "";
                        ofxData.Message = "Getting MFA challenge questions...";
                        GetMFAChallenge(ofxData);
                        return;
                    case OfxErrorCode.MustChangeUSERPASS:
                        ofxData.LinkCaption = "";
                        ofxData.Message = "Getting new password...";
                        GetNewPassword(ofxData);
                        return;
                    case OfxErrorCode.SignonInvalid:
                        ofxData.LinkCaption = "";
                        ofxData.Message = "Logging in ...";
                        GetLogin(ofxData);
                        return;
                }

                if (ofxData.LinkCaption == tryAgainCaption)
                {
                    SyncOneAccount(ofxData.OnlineAccount);
                    return;
                }


                string template = ProcessHelper.GetEmbeddedResource("Walkabout.Ofx.OfxErrorTemplate.htm");
                // css uses curly brackets, so it must be substituted.
                string css = @"body, th, td { font-family: Verdana; font-size:10pt; }
h2 { font-size: 12pt; }";

                string response = null;
                string headers = null;
                string message = ofxData.Message;

                Exception error = ofxData.Error;
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
                OnlineAccount account = ofxData.OnlineAccount;
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

        private void GetNewPassword(OfxDownloadData ofxData)
        {
            var info = OfxRequest.GetSignonInfo(this.myMoney, ofxData.OnlineAccount);

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

        private void GetLogin(OfxDownloadData ofxData)
        {
            var info = OfxRequest.GetSignonInfo(this.myMoney, ofxData.OnlineAccount);

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

        OfxMfaChallengeRequest challenge;

        private void GetMFAChallenge(OfxDownloadData ofxData)
        {
            challenge = new OfxMfaChallengeRequest(ofxData.OnlineAccount, this.myMoney);
            challenge.UserData = ofxData;
            challenge.Completed += OnChallengeCompleted;
            challenge.BeginMFAChallenge();
        }

        void OnChallengeCompleted(object sender, EventArgs e)
        {
            OfxMfaChallengeRequest req = (OfxMfaChallengeRequest)sender;

            if (this.challenge != req)
            {
                // perhaps an old stale request just completed, so ignore it.
                return;
            }
            OfxDownloadData data = (OfxDownloadData)req.UserData;

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

        private void GetAuthenticationToken(OfxDownloadData ofxData, OfxErrorCode code)
        {
            var info = OfxRequest.GetSignonInfo(this.myMoney, ofxData.OnlineAccount);
            if (info != null)
            {
                PromptForAuthToken(ofxData, info, code);

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

        private void PromptForAuthToken(OfxDownloadData ofxData, OfxSignOnInfo info, OfxErrorCode code)
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

        private void BeginRotation(Image img)
        {
            RotateTransform rt = (RotateTransform)img.RenderTransform;
            DoubleAnimation animation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(5)));
            animation.RepeatBehavior = RepeatBehavior.Forever;
            rt.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void StopRotation(Image img)
        {
            RotateTransform rt = (RotateTransform)img.RenderTransform;
            rt.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        private void OnSynchronizeImageLoaded(object sender, RoutedEventArgs e)
        {
            Image img = (Image)sender;
            img.RenderTransform = new RotateTransform(0, 8, 8);
            BeginRotation(img);

            img.IsVisibleChanged += new DependencyPropertyChangedEventHandler((s, args) =>
            {
                if ((bool)args.NewValue)
                {
                    BeginRotation((Image)s);
                }
                else
                {
                    StopRotation((Image)s);
                }
            });
        }

    }
}
