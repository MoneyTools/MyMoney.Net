using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Walkabout.Data;
using Walkabout.Ofx;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{

    /// <summary>
    /// This is the View Model wrapping the Account Model in an MVVM architecture
    /// and OnlineAccountDialog is the View.
    /// </summary>
    public class AccountListItem : INotifyPropertyChanged
    {
        private string accountId;

        public string AccountId
        {
            get { return this.accountId; }
            set { this.accountId = value; this.OnPropertyChanged("AccountId"); }
        }

        private string name;

        public string Name
        {
            get { return this.name; }
            set { this.name = value; this.OnPropertyChanged("Name"); }
        }

        private bool isNew;

        public bool IsNew
        {
            get { return this.isNew; }
            set { this.isNew = value; this.OnPropertyChanged("IsNew"); }
        }

        private bool userAdded;

        public bool UserAdded
        {
            get { return this.userAdded; }
            set { this.userAdded = value; this.OnPropertyChanged("UserAdded"); }
        }

        private bool isDisconnected;

        public bool IsDisconnected
        {
            get { return this.isDisconnected; }
            set { this.isDisconnected = value; this.OnPropertyChanged("IsDisconnected"); }
        }

        private bool warning;

        public bool HasWarning
        {
            get { return this.warning; }
            set { this.warning = value; this.OnPropertyChanged("HasWarning"); }
        }

        private string tooltip;

        public string ToolTipMessage
        {
            get { return this.tooltip; }
            set { this.tooltip = value; this.OnPropertyChanged("WarningMessage"); }
        }

        public AccountType CorrectType { get; set; }

        public Account Account { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }

        }
    }

    /// <summary>
    /// Interaction logic for AccountDialog.xaml
    /// </summary>
    public partial class OnlineAccountDialog : BaseDialog
    {
        private readonly MyMoney money;
        private readonly Account account = new Account();
        private readonly OnlineAccount editing = new OnlineAccount();
        private readonly ObservableCollection<string> versions = new ObservableCollection<string>();
        private ListCollectionView view;
        private DispatcherTimer queueProcessor;
        private List<AccountListItem> found; // found during "signup" process.
        private TextBox editor;
        private readonly string successPrompt;
        private ProfileResponse profile;
        private readonly bool debugging;
        private readonly IServiceProvider serviceProvider;
        private bool closed;
        private CancellationTokenSource cancellation;

        public OnlineAccountDialog(MyMoney money, Account account, IServiceProvider sp)
        {
            this.serviceProvider = sp;
            this.debugging = System.Diagnostics.Debugger.IsAttached;
            this.money = money;
            this.account = account;
            this.InitializeComponent();
            this.cancellation = new CancellationTokenSource();

            OnlineAccount oa = this.account.OnlineAccount;
            if (oa != null)
            {
                this.editing = oa.ShallowCopy();
                this.editing.Id = 0;
            }

            // Hide any fields that don't apply to this account type.
            this.ShowHideFieldsForAccountType(account.Type);

            // add versions we support explicitly.
            this.versions.Add("1.0");
            this.versions.Add("2.0");

            foreach (OnlineAccount other in money.OnlineAccounts.GetOnlineAccounts())
            {
                string v = other.OfxVersion;
                this.InsertVersion(other.OfxVersion);
            }

            this.OfxVersions.ItemsSource = this.versions;

            this.DataContext = this.editing;

            this.ComboBoxName.SelectionChanged += new SelectionChangedEventHandler(this.OnComboBoxNameSelectionChanged);

            this.Progress.Visibility = Visibility.Collapsed;
            this.AccountListPanel.Visibility = Visibility.Collapsed;

            this.ButtonVerify.IsEnabled = false;

            this.successPrompt = this.SignupResultPrompt.Text;

            Loaded += new RoutedEventHandler(this.OnLoaded);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.GetBankListProgress.Visibility = System.Windows.Visibility.Visible;
            this.ComboBoxName.Visibility = System.Windows.Visibility.Collapsed;
            Task.Run(this.GetBankList);

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.OfxVersions.SelectedItem = this.versions[this.versions.Count - 1];
            }));
        }

        private object pendingVerify;
        private object pendingSignon;

        protected override void OnClosed(EventArgs e)
        {
            this.closed = true;
            this.cancellation.Cancel();
            this.pendingVerify = null;
            this.pendingSignon = null;
            if (this.queueProcessor != null)
            {
                this.queueProcessor.Stop();
                this.queueProcessor = null;
            }
            base.OnClosed(e);
        }

        private void ShowHideFieldsForAccountType(AccountType type)
        {
            switch (type)
            {
                case AccountType.Savings:
                case AccountType.Checking:
                case AccountType.MoneyMarket:
                case AccountType.CreditLine:
                    this.TextBoxBrokerId.Visibility = this.BrokerIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    this.TextBoxBranchId.Visibility = this.BranchIdPrompt.Visibility = System.Windows.Visibility.Visible;
                    this.TextBoxBankId.Visibility = this.BankIdPrompt.Visibility = System.Windows.Visibility.Visible;
                    break;
                case AccountType.Credit:
                    this.TextBoxBrokerId.Visibility = this.BrokerIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    this.TextBoxBranchId.Visibility = this.BranchIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    this.TextBoxBankId.Visibility = this.BankIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case AccountType.Brokerage:
                case AccountType.Retirement:
                    this.TextBoxBrokerId.Visibility = this.BrokerIdPrompt.Visibility = System.Windows.Visibility.Visible;
                    this.TextBoxBranchId.Visibility = this.BranchIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    this.TextBoxBankId.Visibility = this.BankIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    break;
            }
        }


        /// <summary>
        /// Add another OFX version in numeric order.
        /// </summary>
        /// <returns>Returns the index into the versions array for the new version</returns>
        private int InsertVersion(string version)
        {
            double x = 0;
            if (!string.IsNullOrEmpty(version) && double.TryParse(version, out x))
            {
                for (int i = 0, n = this.versions.Count; i < n; i++)
                {
                    double y = double.Parse(this.versions[i]);
                    if (y == x)
                    {
                        return i;
                    }
                    if (y > x)
                    {
                        this.versions.Insert(i, version);
                        return i;
                    }
                }
                this.versions.Add(version);
                return this.versions.Count - 1;
            }

            // invalid
            return -1;
        }

        private bool updating;

        private OfxInstitutionInfo FindProvider(string name)
        {
            if (this.providers != null)
            {
                foreach (OfxInstitutionInfo p in this.providers)
                {
                    if (string.Compare(p.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        private List<OfxInstitutionInfo> providers;

        private async void GetBankList()
        {
            // show the cached list first.
            this.providers = OfxInstitutionInfo.GetCachedBankList();
            this.ShowBankList();

            try
            {
                this.providers = await OfxInstitutionInfo.GetRemoteBankList(this.cancellation.Token);
                if (!this.closed)
                {
                    this.ShowBankList();
                }
            }
            catch { }
        }

        private void ShowBankList()
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                this.updating = true;

                OfxInstitutionInfo selection = null;

                this.GetBankListProgress.Visibility = System.Windows.Visibility.Collapsed;
                this.ComboBoxName.Visibility = System.Windows.Visibility.Visible;

                // add known onlineaccount providers.
                foreach (OnlineAccount other in this.money.OnlineAccounts.GetOnlineAccounts())
                {
                    OfxInstitutionInfo p = this.FindProvider(other.Name);
                    if (p == null)
                    {
                        p = new OfxInstitutionInfo()
                        {
                            Name = other.Name,
                            BankId = other.BankId,
                            BrokerId = other.BrokerId,
                            Fid = other.FID,
                            OfxVersion = other.OfxVersion,
                            Org = other.Institution,
                            ProviderURL = other.Ofx,
                            SmallLogoURL = other.LogoUrl,
                            AppId = other.AppId,
                            AppVer = other.AppVersion
                        };
                        this.providers.Add(p);
                    }
                    p.Existing = true;
                    p.OnlineAccount = other;
                }

                string saved = this.ComboBoxName.Text;
                if (string.IsNullOrEmpty(saved))
                {
                    saved = this.editing.Name;
                }

                foreach (var provider in this.providers)
                {
                    if (this.editing != null && provider.Name == saved)
                    {
                        selection = provider;
                    }
                }

                this.providers.Sort(new Comparison<OfxInstitutionInfo>((a, b) => { return string.Compare(a.Name, b.Name); }));

                this.view = new ListCollectionView(this.providers);

                if (!this.debugging)
                {
                    // don't show error items
                    this.view.Filter = new Predicate<object>((item) =>
                    {
                        OfxInstitutionInfo p = (OfxInstitutionInfo)item;
                        return !p.HasError;
                    });
                }

                this.ComboBoxName.ItemsSource = this.view;

                if (selection == null)
                {
                    this.ComboBoxName.Text = saved;
                }
                else
                {
                    this.ComboBoxName.SelectedItem = selection;
                    this.ComboBoxName.Text = saved;
                }

                this.ComboBoxName.Focus();
                this.updating = false;

                // now we have the bank list we can update the info
                this.Enqueue((OfxInstitutionInfo)this.ComboBoxName.SelectedItem);

                this.UpdateButtonState();
            }));
        }

        private void ComboBoxName_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.editor == null)
            {
                this.editor = this.ComboBoxName.Template.FindName("PART_EditableTextBox", this.ComboBoxName) as TextBox;
                if (this.editor != null)
                {
                    this.editor.TextChanged -= new TextChangedEventHandler(this.OnComboBoxNameChanged);
                    this.editor.TextChanged += new TextChangedEventHandler(this.OnComboBoxNameChanged);
                    this.OnComboBoxNameChanged();
                }
            }
        }

        private void OnProcessQueue(object sender, EventArgs e)
        {
            OfxInstitutionInfo info = null;

            // drain the queue and only fetch the most recent request.
            while (true)
            {
                OfxInstitutionInfo next = null;
                if (this.fetchQueue.TryDequeue(out next))
                {
                    info = next;
                }
                else
                {
                    break;
                }
            }

            if (info == null)
            {
                this.queueProcessor.Stop();
                this.queueProcessor = null;
                return;
            }

            _ = this.GetUpdatedBankInfo(info);
        }

        private readonly ConcurrentQueue<OfxInstitutionInfo> fetchQueue = new ConcurrentQueue<OfxInstitutionInfo>();

        private void OnComboBoxNameSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.updating)
            {
                this.HideRightHandPanels();
                this.selected = (OfxInstitutionInfo)this.ComboBoxName.SelectedItem;
                if (this.selected != null)
                {
                    this.Enqueue(this.selected);
                }
                this.UpdateInstitutionInfo(this.selected);
            }
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.UpdateButtonState();
            }));
        }

        private void Enqueue(OfxInstitutionInfo info)
        {
            if (info != null)
            {
                this.fetchQueue.Enqueue(this.selected);

                if (this.queueProcessor == null)
                {
                    this.queueProcessor = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, new EventHandler(this.OnProcessQueue), this.Dispatcher);
                }
            }
        }

        private void OnComboBoxNameChanged(object sender, TextChangedEventArgs e)
        {
            if (!this.updating)
            {
                this.OnComboBoxNameChanged();
            }
            this.UpdateButtonState();
        }

        private string filter;

        private void OnComboBoxNameChanged()
        {
            OfxInstitutionInfo ps = this.FindProvider(this.ComboBoxName.Text);
            // check for null here allows the user to rename this institution.
            if (ps != null)
            {
                this.UpdateInstitutionInfo(ps);
            }
            if (this.view == null || this.editor == null)
            {
                return;
            }

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Update the filter on the combo so it shows only those banks matching what the user typed in so far.
                this.filter = this.editor.Text;
                if (string.IsNullOrEmpty(this.filter))
                {
                    this.view.Filter = null;
                }
                else if (this.editor.SelectionLength < this.filter.Length)
                {
                    if (this.editor.SelectionStart >= 0)
                    {
                        this.filter = this.filter.Substring(0, this.editor.SelectionStart);
                    }
                    if (string.IsNullOrEmpty(this.filter))
                    {
                        if (!this.debugging)
                        {
                            // don't show error items
                            this.view.Filter = new Predicate<object>((item) =>
                            {
                                OfxInstitutionInfo p = (OfxInstitutionInfo)item;
                                return !p.HasError;
                            });
                        }
                        else
                        {
                            this.view.Filter = null;
                        }
                    }
                    else
                    {
                        if (!this.debugging)
                        {
                            // don't show error items
                            this.view.Filter = new Predicate<object>((item) =>
                            {
                                OfxInstitutionInfo p = (OfxInstitutionInfo)item;
                                return !p.HasError && p.Name.IndexOf(this.filter, StringComparison.OrdinalIgnoreCase) >= 0;
                            });
                        }
                        else
                        {
                            this.view.Filter = new Predicate<object>((item) =>
                            {
                                OfxInstitutionInfo p = (OfxInstitutionInfo)item;
                                return p.Name.IndexOf(this.filter, StringComparison.OrdinalIgnoreCase) >= 0;
                            });
                        }
                    }
                }
            }));
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            this.UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            this.ButtonVerify.IsEnabled = !string.IsNullOrWhiteSpace(this.ComboBoxName.Text) &&
                    !string.IsNullOrWhiteSpace(this.TextBoxInstitution.Text) &&
                    !string.IsNullOrWhiteSpace(this.TextBoxFid.Text) &&
                    !string.IsNullOrWhiteSpace(this.TextBoxOfxAddress.Text);
        }

        private async Task GetUpdatedBankInfo(OfxInstitutionInfo provider)
        {
            OfxInstitutionInfo ps = await OfxInstitutionInfo.GetProviderInformation(provider, this.cancellation.Token);

            if (this.selected != provider)
            {
                // user has moved on.
                return;
            }

            if (ps != null)
            {
                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    this.UpdateInstitutionInfo(ps);
                }));
            }

        }

        private OfxInstitutionInfo selected;

        private void UpdateInstitutionInfo(OfxInstitutionInfo ps)
        {
            if (ps == null)
            {
                //editing.FID = null;
                //editing.Institution = null;
                //editing.LogoUrl = null;
                //editing.BankId = null;
                this.editing.UserId = null;
                this.editing.Password = null;
                //editing.Ofx = null;
                this.editing.AccessKey = null;
                this.editing.AuthToken = null;
                this.editing.UserCred1 = null;
                this.editing.UserCred2 = null;
                this.editing.UserKey = null;
            }
            else
            {
                // update fields of dialog to match online information about this financial institution.
                string version = ps.OfxVersion;
                if (string.IsNullOrEmpty(version))
                {
                    version = "1.0";
                }
                this.editing.OfxVersion = version;
                this.OfxVersions.SelectedIndex = this.InsertVersion(version);
                this.editing.Ofx = ps.ProviderURL;
                this.editing.LogoUrl = string.IsNullOrEmpty(ps.SmallLogoURL) ? null : ps.SmallLogoURL;
                this.account.WebSite = ps.Website;
                this.editing.FID = ps.Fid;
                this.editing.Institution = ps.Org;
                this.editing.Name = ps.Name;

                OnlineAccount oa = ps.OnlineAccount;
                if (oa != null)
                {
                    // this is an existing online account, so copy the additional properties from there.
                    this.editing.BankId = oa.BankId;
                    this.editing.UserId = oa.UserId;
                    this.editing.Password = oa.Password;
                    this.editing.UserCred1 = oa.UserCred1;
                    this.editing.UserCred2 = oa.UserCred2;
                }
                else
                {
                    this.editing.AccessKey = null;
                    this.editing.AuthToken = null;
                    this.editing.UserCred1 = null;
                    this.editing.UserCred2 = null;
                    this.editing.UserKey = null;
                }
            }
        }

        private void OnButtonCancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnButtonOk(object sender, RoutedEventArgs e)
        {
            // create the online account
            var oa = this.account.OnlineAccount;
            if (oa == null)
            {
                if (this.selected == null)
                {
                    // then the name entered by user didn't exist in the list of providers, so use the text box value
                    this.editing.Name = this.ComboBoxName.Text;
                }
                oa = this.money.OnlineAccounts.FindOnlineAccount(this.editing.Name);
                if (oa == null)
                {
                    oa = this.editing;
                    this.money.OnlineAccounts.AddOnlineAccount(this.editing);
                }
                this.account.OnlineAccount = oa;
            }

            if (oa.IsDeleted)
            {
                // user changed their mind.
                oa.Undelete();
            }

            // make sure we get edited values (don't use databinding here because we want to ensure Cancel leaves everything in clean state)
            if (oa != this.editing)
            {
                oa.Name = this.editing.Name;
                oa.Institution = this.editing.Institution;
                oa.FID = this.editing.FID;
                oa.BankId = this.editing.BankId;
                oa.BranchId = this.editing.BranchId;
                oa.BrokerId = this.editing.BrokerId;
                oa.Ofx = this.editing.Ofx;
                oa.OfxVersion = this.editing.OfxVersion;
                oa.AppId = this.editing.AppId;
                oa.AppVersion = this.editing.AppVersion;
                oa.UserId = this.editing.UserId;
                oa.LogoUrl = this.editing.LogoUrl;
                oa.Password = this.editing.Password;
                oa.UserCred1 = this.editing.UserCred1;
                oa.UserCred2 = this.editing.UserCred2;
                oa.ClientUid = this.editing.ClientUid;
                oa.AuthToken = this.editing.AuthToken;
                oa.AccessKey = this.editing.AccessKey;
            }

            // go through all accounts and add the ones the user wants added.
            if (this.found != null)
            {
                foreach (AccountListItem item in this.found)
                {
                    if (item.UserAdded)
                    {
                        this.OnAddAccount(item);
                    }
                    else if (item.IsDisconnected)
                    {
                        if (item.Account.OnlineAccount != null && item.Account.OnlineAccount.Name == oa.Name)
                        {
                            // disconnect it!
                            item.Account.OnlineAccount = null;
                        }
                    }
                    else if (!item.IsNew)
                    {
                        // connect it!
                        item.Account.OnlineAccount = oa;
                    }
                }
            }

            OfxInstitutionInfo provider = this.selected;
            if (provider != null && this.providers != null)
            {
                if (!string.IsNullOrEmpty(this.editing.Name))
                {
                    provider.Name = this.editing.Name;
                }
                if (!string.IsNullOrEmpty(this.editing.FID))
                {
                    provider.Fid = this.editing.FID;
                }
                if (!string.IsNullOrEmpty(this.editing.Institution))
                {
                    provider.Org = this.editing.Institution;
                }
                if (!string.IsNullOrEmpty(this.editing.BrokerId))
                {
                    provider.BrokerId = this.editing.BrokerId;
                }
                if (!string.IsNullOrEmpty(this.editing.OfxVersion))
                {
                    provider.OfxVersion = this.editing.OfxVersion;
                }
                OfxInstitutionInfo.SaveList(this.providers);
            }

            this.DialogResult = true;
            this.Close();
        }

        private void OnShowXml(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            Uri uri = link.NavigateUri;
            if (uri != null)
            {
                InternetExplorer.OpenUrl(IntPtr.Zero, uri.ToString());
            }
        }

        private OfxMfaChallengeRequest challenge;

        /// <summary>
        /// Background thread to connect to bank
        /// </summary>
        /// <param name="state"></param>
        private async Task StartSignup()
        {
            var id = new object();
            this.pendingSignon = id;

            try
            {
                var info = OfxRequest.GetSignonInfo(this.money, this.editing);

                if (info.AuthTokenRequired.ConvertYesNoToBoolean() && string.IsNullOrEmpty(this.editing.AuthToken))
                {
                    bool result = this.Dispatcher.Invoke(new Func<bool>(() =>
                    {
                        return this.PromptForAuthToken(info, OfxErrorCode.AUTHTOKENRequired);
                    }));

                    if (!result)
                    {
                        // user cancelled.
                        return;
                    }
                }

                if (info.MFAChallengeRequired.ConvertYesNoToBoolean())
                {
                    this.challenge = new OfxMfaChallengeRequest(this.editing, this.money);
                    this.challenge.Completed += this.OnMfaChallengeCompleted;
                    this.challenge.BeginMFAChallenge();
                }
                else
                {
                    await this.Signup();
                }
            }
            catch (OfxException ex)
            {
                if (this.pendingSignon == id)
                {
                    this.pendingSignon = null;
                    this.Dispatcher.Invoke(new Action(() =>
                 {
                     this.HandleSignOnErrors(ex, new Action(() =>
                     {
                         Task.Run(this.StartSignup);
                     }));
                 }));
                }
            }
            catch (Exception ex)
            {
                if (this.pendingSignon == id)
                {
                    this.pendingSignon = null;
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.ShowError(ex.GetType().Name, ex.Message);
                    }));
                }
            }
        }

        private void OnMfaChallengeCompleted(object sender, EventArgs e)
        {
            this.OnChallengeCompleted(sender, e);

            if (this.editing.MfaChallengeAnswers != null)
            {
                // back to background thread.
                Task.Run(this.Signup);
            }

        }

        private bool PromptForAuthToken(OfxSignOnInfo info, OfxErrorCode code)
        {
            this.editing.AuthToken = null;
            this.editing.AccessKey = null;

            AuthTokenDialog dialog = new AuthTokenDialog(info, code);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                this.editing.AuthToken = dialog.AuthorizationToken;
                return true;
            }
            else
            {
                this.ShowError("Cancelled", "User cancelled");
                return false;
            }
        }

        private object signupRequest;

        private async Task Signup()
        {
            OfxRequest req = new OfxRequest(this.editing, this.money, AccountHelper.PickAccount);
            this.signupRequest = req;
            OFX ofx = await req.Signup(this.editing);
            string logpath = req.OfxCachePath;
            if (this.signupRequest == req)
            {
                this.signupRequest = null;
                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    this.Progress.Visibility = Visibility.Collapsed;
                    this.ShowResult(ofx);
                    this.OfxVersions.SelectedIndex = this.InsertVersion(this.editing.OfxVersion); // in case it was changed by the "Signup" method.
                }));
            }
        }

        private OfxErrorCode GetSignOnCode(OFX ofx)
        {
            if (ofx != null)
            {
                var signOnResponseMsg = ofx.SignOnMessageResponse;
                if (signOnResponseMsg != null)
                {
                    var signOnResponse = signOnResponseMsg.SignOnResponse;
                    if (signOnResponse != null)
                    {
                        OfxStatus status = signOnResponse.OfxStatus;
                        if (status != null)
                        {
                            return (OfxErrorCode)status.Code;
                        }
                    }
                }
            }
            return OfxErrorCode.None;
        }

        private void HandleUnexpectedError(Exception ex)
        {
            string code = ex.GetType().FullName;
            string msg = ex.Message;

            Hyperlink errorLink = null;
            OfxException oe = ex as OfxException;

            if (oe != null)
            {
                code = oe.Code;
                if (string.IsNullOrEmpty(code))
                {
                    code = "Error";
                }

                if (oe.Response != null)
                {
                    msg = oe.Response;
                }

                if (oe.HelpLink != null)
                {
                    errorLink = InternetExplorer.GetOpenFileHyperlink("XML Log File", oe.HelpLink);
                }
            }
            if (msg.Contains("<HTML>"))
            {
                this.ShowHtmlError(code, msg);
            }
            else
            {
                this.ShowError(code, msg, errorLink);
            }
        }

        private void HandleSignOnErrors(OfxException ex, Action continuation)
        {
            OFX ofx = ex.Root;
            if (ofx != null)
            {
                OfxErrorCode code = this.GetSignOnCode(ex.Root);
                if ((code == OfxErrorCode.AUTHTOKENRequired && string.IsNullOrWhiteSpace(this.editing.AuthToken)) || code == OfxErrorCode.AUTHTOKENInvalid)
                {
                    var info = OfxRequest.GetSignonInfo(this.money, this.editing);
                    if (info != null)
                    {
                        if (!this.PromptForAuthToken(info, code))
                        {
                            // user cancelled.
                            return;
                        }
                        else
                        {
                            this.Dispatcher.BeginInvoke(continuation);
                            return;
                        }
                    }
                }
                else if (code == OfxErrorCode.MFAChallengeAuthenticationRequired)
                {
                    // Begin the MFA Challenge Authentication process...
                    this.ShowError("Requesting More Information", "Server is requesting Multi-Factor Authentication...");

                    this.challenge = new OfxMfaChallengeRequest(this.editing, this.money);
                    this.challenge.UserData = continuation;
                    this.challenge.Completed += this.OnChallengeCompleted;
                    this.challenge.BeginMFAChallenge();
                    return;
                }
                else if (code == OfxErrorCode.MustChangeUSERPASS)
                {
                    this.PromptForNewPassword(continuation);
                    return;
                }
                else if (code == OfxErrorCode.SignonInvalid)
                {
                    this.PromptForPassword(OfxRequest.GetSignonInfo(this.money, this.editing), null, code, ex.Message, continuation);
                    return;
                }
            }

            this.HandleUnexpectedError(ex);
        }

        private void PromptForPassword(OfxSignOnInfo info, List<Block> prompt, OfxErrorCode code, string errorMessage, Action continuation)
        {
            if (info != null && info.AuthTokenRequired.ConvertYesNoToBoolean() && string.IsNullOrEmpty(this.editing.AuthToken))
            {
                if (!this.PromptForAuthToken(info, code))
                {
                    // user cancelled.
                    return;
                }
                continuation();
                return;
            }

            OfxLoginDialog login = new OfxLoginDialog(info, this.editing, prompt, code, errorMessage);
            login.Owner = this;

            if (login.ShowDialog() == true)
            {
                continuation();
            }
            else
            {
                this.ShowError("Cancelled", "User cancelled");
            }
        }

        private void OnChallengeCompleted(object sender, EventArgs e)
        {
            OfxMfaChallengeRequest req = (OfxMfaChallengeRequest)sender;
            Action continuation = (Action)req.UserData;

            if (this.challenge != req)
            {
                // perhaps an old stale request just completed, so ignore it.
                return;
            }

            if (req.Error != null)
            {
                this.HandleUnexpectedError(req.Error);
                return;
            }
            else if (req.UserChallenges.Count > 0)
            {
                MfaChallengeDialog dialog = new MfaChallengeDialog();
                dialog.Owner = this;
                dialog.SetupQuestions(req.UserChallenges);
                if (dialog.ShowDialog() == true)
                {
                    var answers = req.BuiltInAnswers;
                    // add user answers
                    dialog.GetAnswers(req.UserChallenges, answers);

                    // store answers for use in next OFX request.
                    this.editing.MfaChallengeAnswers = answers;

                }
                else
                {
                    // user cancelled.
                    this.ShowError("Error", "User cancelled");
                    return;
                }
            }
            else
            {
                this.editing.MfaChallengeAnswers = req.BuiltInAnswers;
            }

            if (continuation != null)
            {
                continuation();
            }
        }


        private void PromptForNewPassword(Action continuation)
        {
            var info = OfxRequest.GetSignonInfo(this.money, this.editing);

            ChangePasswordDialog dialog = new ChangePasswordDialog(info, this.editing, this.money);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                Exception ex = dialog.Error;
                if (ex != null)
                {
                    this.HandleUnexpectedError(ex);
                }
                else
                {
                    this.Dispatcher.BeginInvoke(continuation);
                }
            }
            else
            {
                this.ShowError("Cancelled", "User cancelled");
            }
        }

        private void ShowError(string code, string message, Hyperlink link = null)
        {
            this.AccountListPanel.Visibility = Visibility.Visible;
            this.SignupResultPrompt.Text = code;
            this.SignupResultPrompt.Foreground = Brushes.Red;
            this.OnlineResultList.Visibility = System.Windows.Visibility.Collapsed;

            this.ErrorMessage.Document.Blocks.Clear();

            Paragraph p = new Paragraph();
            p.Inlines.Add(new Run(message));
            if (link != null)
            {
                p.Inlines.Add(link);
            }
            this.ErrorMessage.Document.Blocks.Add(p);

            this.ErrorScroller.Visibility = System.Windows.Visibility.Visible;
            this.ErrorHtml.Visibility = System.Windows.Visibility.Collapsed;
            this.ErrorMessage.Visibility = System.Windows.Visibility.Visible;
            this.Progress.Visibility = Visibility.Collapsed;
        }

        private void ShowHtmlError(string code, string message)
        {
            this.AccountListPanel.Visibility = Visibility.Visible;
            this.SignupResultPrompt.Text = code;
            this.SignupResultPrompt.Foreground = Brushes.Red;
            this.OnlineResultList.Visibility = System.Windows.Visibility.Collapsed;

            this.ErrorHtml.NavigateToString(message);

            this.ErrorScroller.Visibility = System.Windows.Visibility.Visible;
            this.ErrorHtml.Visibility = System.Windows.Visibility.Visible;
            this.ErrorMessage.Visibility = System.Windows.Visibility.Collapsed;
            this.Progress.Visibility = Visibility.Collapsed;
        }

        private void HideRightHandPanels()
        {
            this.ErrorMessage.Visibility = Visibility.Collapsed;
            this.AccountListPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowResult(OFX ofx)
        {
            this.HideRightHandPanels();
            this.AccountListPanel.Visibility = Visibility.Visible;
            this.SignupResultPrompt.SetValue(TextBlock.ForegroundProperty, DependencyProperty.UnsetValue);
            this.SignupResultPrompt.Text = this.successPrompt;
            this.found = new List<AccountListItem>();

            var sup = ofx.SignUpMessageResponse;
            int index = 1;
            if (sup != null)
            {
                string org = this.editing.Institution;
                if (ofx.SignOnMessageResponse != null && ofx.SignOnMessageResponse.SignOnResponse != null && ofx.SignOnMessageResponse.SignOnResponse.FinancialInstitution != null)
                {
                    org = ofx.SignOnMessageResponse.SignOnResponse.FinancialInstitution.Organization;
                    if (string.IsNullOrEmpty(org))
                    {
                        org = this.editing.Institution;
                    }
                }
                var sor = sup.AccountInfoSet;
                if (sor != null)
                {
                    var list = sor.Accounts;
                    if (list != null)
                    {
                        foreach (var acct in list)
                        {
                            AccountListItem f = this.FindMatchingOnlineAccount(acct);
                            if (f != null)
                            {
                                if (string.IsNullOrWhiteSpace(f.Name))
                                {
                                    f.Name = org;
                                    if (index > 1)
                                    {
                                        f.Name += " " + index;
                                    }
                                    index++;
                                }
                                this.found.Add(f);
                            }
                        }
                    }
                }
            }
            this.OnlineResultList.ItemsSource = this.found;
            this.OnlineResultList.Visibility = System.Windows.Visibility.Visible;
            this.ErrorScroller.Visibility = System.Windows.Visibility.Collapsed;

            this.ResizeListColumns();
        }

        private Typeface typeface;

        private double MeasureListString(string s)
        {
            double fontSize = (double)this.OnlineResultList.GetValue(ListView.FontSizeProperty);

            if (this.typeface == null)
            {
                this.typeface = new Typeface((FontFamily)this.OnlineResultList.GetValue(ListView.FontFamilyProperty),
                    (FontStyle)this.OnlineResultList.GetValue(ListView.FontStyleProperty),
                    (FontWeight)this.OnlineResultList.GetValue(ListView.FontWeightProperty),
                    (FontStretch)this.OnlineResultList.GetValue(ListView.FontStretchProperty));
            }
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            FormattedText ft = new FormattedText(s, System.Globalization.CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
                this.typeface, fontSize, Brushes.Black, pixelsPerDip);
            return ft.Width;
        }

        /// <summary>
        /// This is a workaround for a bug in WPF ListView, the columns do not resize correctly.
        /// </summary>
        private void ResizeListColumns()
        {
            GridView view = (GridView)this.OnlineResultList.View;
            foreach (GridViewColumn c in view.Columns)
            {
                c.Width = 0;
            }

            this.OnlineResultList.InvalidateMeasure();
            this.OnlineResultList.InvalidateArrange();
            this.OnlineResultList.UpdateLayout();

            foreach (GridViewColumn c in view.Columns)
            {
                c.Width = double.NaN;
            }

            this.OnlineResultList.InvalidateMeasure();
            this.OnlineResultList.InvalidateArrange();
            this.OnlineResultList.UpdateLayout();


        }

        private string GetResolveButtonCaption()
        {
            return "Click here to select an account to match with this online account";
        }

        private AccountListItem FindMatchingOnlineAccount(AccountInfoResponse ar)
        {
            AccountType type = AccountType.Checking;
            string id = null;
            if (ar.BankAccountInfo != null)
            {
                type = AccountType.Checking;
                var from = ar.BankAccountInfo.BankAccountFrom;
                if (from != null && ar.BankAccountInfo.SupportsDownload == "Y")
                {
                    id = from.AccountId;
                    switch (from.AccountType)
                    {
                        case "CHECKING":
                            type = AccountType.Checking;
                            break;
                        case "SAVINGS":
                            type = AccountType.Savings;
                            break;
                        case "MONEYMRKT":
                            type = AccountType.MoneyMarket;
                            break;
                        case "CREDITLINE":
                            type = AccountType.CreditLine;
                            break;
                    }
                    this.editing.BankId = from.BankId;
                    this.editing.BranchId = from.BranchId;
                }
            }
            else if (ar.CreditCardAccountInfo != null && ar.CreditCardAccountInfo.SupportsDownload == "Y")
            {
                type = AccountType.Credit;
                var from = ar.CreditCardAccountInfo.CreditCardAccountFrom;
                if (from != null)
                {
                    id = from.AccountId;
                }
            }
            else if (ar.InvAccountInfo != null)
            {
                type = AccountType.Brokerage;
                var from = ar.InvAccountInfo.InvAccountFrom;
                if (from != null)
                {
                    id = from.AccountId;
                    this.editing.BrokerId = from.BrokerId;
                }
            }

            bool isNew = false;
            bool hasWarning = false;
            string tooltip = null;
            bool connected = false;
            Account found = null;

            if (string.IsNullOrEmpty(id))
            {
                // weird.
                // warning = string.Format("Online account '{0}' provided no account id for this account '{1}'", editing.Name, ar.Description);
                return null;
            }

            foreach (Account a in this.money.Accounts.GetAccounts())
            {
                if (a.AccountId == id || a.OfxAccountId == id)
                {
                    if (a.OnlineAccount != null)
                    {
                        if (a.OnlineAccount.Name != this.editing.Name)
                        {
                            connected = true;
                            tooltip = "Click here to disconnect this account from the selected online account";
                        }
                        else
                        {
                            connected = true;
                            tooltip = "Click here to connect this account to the selected online account";
                        }
                    }

                    if (type != a.Type)
                    {
                        hasWarning = true;
                        tooltip = string.Format("Click here to change your account type from {0} to {1}", a.Type.ToString(), type);
                    }
                    found = a;
                    break;
                }
            }

            if (found == null)
            {
                isNew = true;
                tooltip = this.GetResolveButtonCaption();
                // create place holder for new account, this will be turned into a real account when
                // user clicks the "add" button.
                found = new Account();
                found.Name = ar.Description;
                found.AccountId = id;
                found.Type = type;
                found.WebSite = this.account != null ? this.account.WebSite : null;
                if (string.IsNullOrEmpty(found.WebSite) && this.profile != null)
                {
                    found.WebSite = this.profile.CompanyUrl;
                }
            }
            return new AccountListItem()
            {
                Account = found,
                Name = found.Name,
                AccountId = found.AccountId,
                IsNew = isNew,
                HasWarning = hasWarning,
                ToolTipMessage = tooltip,
                CorrectType = type,
                IsDisconnected = !connected
            };
        }

        private void OnIconButtonClick(object sender, RoutedEventArgs args)
        {
            FrameworkElement e = (FrameworkElement)sender;
            AccountListItem item = e.DataContext as AccountListItem;
            if (item != null)
            {
                if (item.HasWarning)
                {
                    Account a = item.Account;
                    if (MessageBoxResult.Yes == MessageBoxEx.Show(string.Format("Online account '{0}' says account {1} is of type '{2}' account, but you have it as '{3}', do you want to fix your local account type?",
                        this.editing.Name, a.Name, item.CorrectType, a.Type.ToString()), "Account Type Mismatch", MessageBoxButton.YesNo, MessageBoxImage.Question))
                    {
                        a.Type = item.CorrectType;
                        item.HasWarning = false;
                    }
                }
                else if (item.IsNew)
                {
                    string prompt = string.Format("We found a reference to an unknown account number '{0}'. Please select the account that you want to use or click the Add New Account button at the bottom of this window:",
                        item.Account.AccountId);
                    Account a = AccountHelper.PickAccount(this.money, item.Account, prompt);
                    if (a != null)
                    {
                        // user made a choice
                        item.Account = a;
                        item.UserAdded = true;
                        item.IsNew = false;
                        item.IsDisconnected = false;
                        e.ToolTip = "Click here to undo the last account add operation";
                    }
                    else
                    {
                        // user cancelled
                    }
                }
                else if (item.UserAdded)
                {
                    item.Account = null;
                    item.UserAdded = false;
                    item.IsNew = true;
                    item.IsDisconnected = true;
                    e.ToolTip = this.GetResolveButtonCaption();
                }
                else if (item.IsDisconnected)
                {
                    item.IsDisconnected = false;
                    e.ToolTip = "Click here to disconnect this account to the selected online account";
                }
                else
                {
                    item.IsDisconnected = true;
                    e.ToolTip = "Click here to connect this account to the selected online account";
                }
            }
        }

        private void OnAddAccount(AccountListItem item)
        {
            if (item != null && item.UserAdded)
            {
                Account a = item.Account;
                string id = a.AccountId;

                if (this.selected == null)
                {
                    // then the name entered by user didn't exist in the list of providers, so use the text box value
                    this.editing.Name = this.ComboBoxName.Text;
                }
                OnlineAccount oa = this.money.OnlineAccounts.FindOnlineAccount(this.editing.Name);
                if (oa == null)
                {
                    oa = this.editing;
                    this.money.OnlineAccounts.AddOnlineAccount(this.editing);
                }
                a.OnlineAccount = oa;
            }

        }

        private void OnButtonVerify(object sender, RoutedEventArgs e)
        {
            if (this.editing != null)
            {
                // copy the name back from combo box in case the user has edited it.
                this.editing.Name = this.ComboBoxName.Text;

                if (string.IsNullOrWhiteSpace(this.editing.OfxVersion))
                {
                    this.editing.OfxVersion = "1.0";
                }

                this.AccountListPanel.Visibility = Visibility.Collapsed;
                this.SignupResultPrompt.Text = "Connecting to your financial institution...";
                this.SignupResultPrompt.SetValue(TextBlock.ForegroundProperty, DependencyProperty.UnsetValue);
                this.OnlineResultList.Visibility = System.Windows.Visibility.Collapsed;
                this.ErrorMessage.Document.Blocks.Clear();
                this.Progress.Visibility = Visibility.Visible;
                this.Progress.IsIndeterminate = true;
                this.HideRightHandPanels();

                Task.Run(this.StartVerify);
            }
        }

        /// <summary>
        /// Background thread to connect to bank and verify OFX support
        /// </summary>
        private async Task StartVerify()
        {
            if (this.pendingVerify != null)
            {
                // hmmm, what does this mean?
                return;
            }
            string fetchingName = this.editing.Name;

            OfxRequest req = new OfxRequest(this.editing, this.money, AccountHelper.PickAccount);
            this.pendingVerify = req;

            string cachePath;
            try
            {

                // see if we can get the server profile first...
                ProfileResponseMessageSet profile = await req.GetProfile(this.editing);
                cachePath = req.OfxCachePath;

                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    if (this.pendingVerify == req && fetchingName == this.editing.Name)
                    {
                        this.pendingVerify = null;
                        this.Progress.Visibility = Visibility.Collapsed;
                        this.HandleProfileResponse(profile, cachePath);
                        this.OfxVersions.SelectedIndex = this.InsertVersion(this.editing.OfxVersion); // in case it was changed by the "GetProfile" method.
                    }
                }));
            }
            catch (OfxException ex)
            {
                if (this.pendingVerify == req)
                {
                    this.pendingVerify = null;
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.OnGotProfile(ex.Message);
                        this.HandleSignOnErrors(ex, new Action(() =>
                        {
                            Task.Run(this.StartVerify);
                        }));
                    }));
                }
            }
            catch (HtmlResponseException he)
            {
                if (this.pendingVerify == req)
                {
                    this.pendingVerify = null;
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.OnHtmlResponse(he.Html);
                    }));
                }
            }
            catch (Exception ex)
            {
                if (this.pendingVerify == req)
                {
                    this.pendingVerify = null;
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        this.OnGotProfile(ex.Message);
                        this.ShowError(ex.GetType().Name, ex.Message);
                    }));
                }
            }
        }

        private void OnHtmlResponse(string html)
        {
            this.ShowHtmlError("Connect Error", html);
        }

        private void HandleProfileResponse(ProfileResponseMessageSet profile, string cachePath)
        {
            string error = null;
            bool retryProfile = false; // set to true if we need some credentials to get the profile.

            List<Block> prompt = new List<Block>();
            OfxErrorCode statusCode = OfxErrorCode.None;
            OfxSignOnInfo info = null;
            ProfileMessageResponse msgResponse = profile.ProfileMessageResponse;
            if (msgResponse != null)
            {
                ProfileResponse response = msgResponse.OfxProfile;

                if (response != null)
                {
                    this.profile = response;


                    Paragraph p = new Paragraph();
                    prompt.Add(p);

                    p.Inlines.Add(new Run("We received the following information from " + this.editing.Name + ". "));
                    p.Inlines.Add(new Run("Please check this is who you think it is then enter your login information below:"));

                    p = GetAddressParagraph(response);
                    p.FontWeight = FontWeights.Bold;

                    Table t = new Table();
                    t.Margin = new Thickness(20, 0, 0, 0);
                    t.Columns.Add(new TableColumn() { Width = GridLength.Auto });
                    t.Columns.Add(new TableColumn() { Width = GridLength.Auto });
                    var rg = new TableRowGroup();
                    t.RowGroups.Add(rg);
                    TableRow row = new TableRow();
                    rg.Rows.Add(row);
                    TableCell address = new TableCell(p);
                    row.Cells.Add(address);

                    prompt.Add(t);

                    string logo = this.editing.LogoUrl;
                    if (!string.IsNullOrEmpty(logo))
                    {
                        Image img = new Image();
                        img.Margin = new Thickness(5);
                        img.Height = 80;
                        img.SetBinding(Image.SourceProperty, new Binding("LogoUrl"));
                        img.DataContext = this.editing;
                        TableCell cell = new TableCell(new BlockUIContainer(img));
                        row.Cells.Insert(0, cell);
                    }

                    Paragraph instructions = new Paragraph();
                    prompt.Add(instructions);

                    if (response.MessageSetList != null && response.MessageSetList.SignUpMessageSet != null && response.MessageSetList.SignUpMessageSet.SignUpMessageSetV1 != null)
                    {
                        var signup = response.MessageSetList.SignUpMessageSet.SignUpMessageSetV1;

                        var core = signup.MessageSetCore;

                        if (signup != null)
                        {
                            string prefix = "If you are not sure you have a userid and password for online banking then please ";
                            if (signup.OtherEnrollInfo != null && !string.IsNullOrWhiteSpace(signup.OtherEnrollInfo.Message))
                            {
                                string enrollInstructions = signup.OtherEnrollInfo.Message;
                                instructions.Inlines.Add(new Run(prefix + "read the following instructions.  " + enrollInstructions + "  "));
                            }
                            else if (signup.WebEnrollInfo != null && !string.IsNullOrWhiteSpace(signup.WebEnrollInfo.Url) && signup.WebEnrollInfo.Url != "N")
                            {
                                string url = signup.WebEnrollInfo.Url;
                                instructions.Inlines.Add(new Run(prefix + "visit "));
                                instructions.Inlines.Add(InternetExplorer.GetOpenFileHyperlink(url, url));
                                instructions.Inlines.Add(new Run(" to enroll in online banking.  "));
                            }
                            else if (!string.IsNullOrWhiteSpace(response.CustomerServicePhone) && response.CustomerServicePhone != "N")
                            {
                                instructions.Inlines.Add(new Run(prefix + string.Format("call {0} to enroll in online banking.  ", response.CustomerServicePhone)));
                            }
                            else if (!string.IsNullOrWhiteSpace(response.CompanyUrl))
                            {
                                string url = response.CompanyUrl;
                                instructions.Inlines.Add(new Run(prefix + "visit "));
                                instructions.Inlines.Add(InternetExplorer.GetOpenFileHyperlink(url, url));
                                instructions.Inlines.Add(new Run(" and enroll in online banking.  "));
                            }
                            else
                            {
                                instructions.Inlines.Add(new Run(prefix + "call your financial institution and ask them about enrolling in online banking."));
                            }
                        }

                        if (core != null)
                        {
                            if (core.SecurityLevel == "NONE")
                            {
                                // great, this is what we support.
                            }
                            else
                            {
                                error = string.Format("Your bank requires security level '{0}' which is not supported.",
                                    core.SecurityLevel, core.TransportLevelSecurity);
                            }
                        }
                    }

                    OfxSignOnInfoList infoList = response.OfxSignOnInfoList;
                    if (infoList != null)
                    {
                        OfxSignOnInfo[] infos = infoList.OfxSignOnInfo;
                        if (infos.Length > 0)
                        {
                            info = infos[0];
                        }
                    }
                }
                else if (msgResponse.OfxStatus != null)
                {
                    statusCode = (OfxErrorCode)msgResponse.OfxStatus.Code;
                    if (statusCode == OfxErrorCode.SignonInvalid)
                    {
                        retryProfile = true;
                    }
                    else if (statusCode != 0)
                    {
                        error = "Unexpected error from server " + statusCode.ToString() + "\n" + msgResponse.OfxStatus.Message;
                    }
                }
            }
            else
            {
                error = "Missing PROFRS tag.";
            }

            if (!string.IsNullOrEmpty(error))
            {
                error = "Did not get the expected response from your financial instutition.  " + error;

                this.OnGotProfile(error);

                error += "\n\nIf you are need help please email the following file to chris@lovettsoftware.com: ";

                this.ShowError("Connect Error", error, InternetExplorer.GetOpenFileHyperlink("Unexpected XML Response", cachePath));
            }
            else
            {
                this.OnGotProfile(null);

                this.PromptForPassword(info, prompt, statusCode, error, new Action(() =>
                {
                    if (retryProfile)
                    {
                        Task.Run(this.StartVerify);
                    }
                    else
                    {
                        this.SignOn();
                    }
                }));
            }

        }

        private void OnGotProfile(string error)
        {
            OfxInstitutionInfo institution = this.selected;

            if (string.IsNullOrEmpty(error) && institution == null)
            {
                // add new entry, the end-user just found a bank that works!
                institution = new OfxInstitutionInfo();
                this.providers.Add(institution);
            }

            if (institution != null && this.providers.Contains(institution))
            {
                // Save any user edited fields back to the selected OfxInstututionInfo.
                institution.OfxVersion = this.editing.OfxVersion;
                institution.ProviderURL = this.editing.Ofx;
                institution.Fid = this.editing.FID;
                institution.Org = this.editing.Institution;
                institution.Name = this.editing.Name;

                institution.LastError = error;
                if (error == null)
                {
                    institution.LastConnection = DateTime.Now;
                }
                OfxInstitutionInfo.SaveList(this.providers);
            }
        }

        private static Paragraph GetAddressParagraph(ProfileResponse response)
        {
            var p = new Paragraph();

            p.Inlines.Add(new Run(response.FinancialInstitutionName));
            p.Inlines.Add(new LineBreak());

            IntuitBrokerId intuit = response.IntuitBrokerId;

            if (string.IsNullOrEmpty(response.Address1) && string.IsNullOrEmpty(response.City) &&
                intuit != null &&
                !string.IsNullOrEmpty(intuit.Address1) &&
                !string.IsNullOrEmpty(intuit.Address1))
            {
                // then use the intuit information
                p.Inlines.Add(new Run(intuit.Address1));
                p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run(intuit.City + " " + intuit.State + " " + intuit.PostalCode));
            }
            else
            {
                p.Inlines.Add(new Run(response.Address1));
                if (!string.IsNullOrEmpty(response.Address2))
                {
                    p.Inlines.Add(new LineBreak());
                    p.Inlines.Add(new Run(response.Address2));
                }
                if (!string.IsNullOrEmpty(response.Address3))
                {
                    p.Inlines.Add(new LineBreak());
                    p.Inlines.Add(new Run(response.Address3));
                }
                p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run(response.City + " " + response.State + " " + response.PostalCode));
            }

            p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new Run(response.CustomerServicePhone));

            p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new Run(response.Email));



            return p;
        }

        private void SignOn()
        {
            if (this.editing != null)
            {
                this.AccountListPanel.Visibility = Visibility.Visible;
                this.SignupResultPrompt.Text = "Signing on to online banking server...";
                this.SignupResultPrompt.SetValue(TextBlock.ForegroundProperty, DependencyProperty.UnsetValue);
                this.OnlineResultList.Visibility = System.Windows.Visibility.Collapsed;
                this.ErrorMessage.Document.Blocks.Clear();
                this.Progress.Visibility = Visibility.Visible;
                this.Progress.IsIndeterminate = true;
                this.HideRightHandPanels();

                Task.Run(this.StartSignup);
            }
        }

    }

}
