using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
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
        string accountId;

        public string AccountId
        {
            get { return accountId; }
            set { accountId = value; OnPropertyChanged("AccountId"); }
        }
        string name;

        public string Name
        {
            get { return name; }
            set { name = value; OnPropertyChanged("Name"); }
        }
        bool isNew;

        public bool IsNew
        {
            get { return isNew; }
            set { isNew = value; OnPropertyChanged("IsNew"); }
        }

        bool userAdded;

        public bool UserAdded
        {
            get { return userAdded; }
            set { userAdded = value; OnPropertyChanged("UserAdded"); }
        }

        bool isDisconnected;

        public bool IsDisconnected
        {
            get { return isDisconnected; }
            set { isDisconnected = value; OnPropertyChanged("IsDisconnected"); }
        }

        bool warning;

        public bool HasWarning
        {
            get { return warning; }
            set { warning = value; OnPropertyChanged("HasWarning"); }
        }


        string tooltip;

        public string ToolTipMessage
        {
            get { return tooltip; }
            set { tooltip = value; OnPropertyChanged("WarningMessage"); }
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
        MyMoney money;
        Account account = new Account();
        OnlineAccount editing = new OnlineAccount();
        ObservableCollection<string> versions = new ObservableCollection<string>();
        ListCollectionView view;
        DispatcherTimer queueProcessor;
        List<AccountListItem> found; // found during "signup" process.
        TextBox editor;
        string successPrompt;
        ProfileResponse profile;
        bool debugging;
        IServiceProvider serviceProvider;

        public OnlineAccountDialog(MyMoney money, Account account, IServiceProvider sp)
        {
            this.serviceProvider = sp;
            this.debugging = System.Diagnostics.Debugger.IsAttached;
            this.money = money;
            this.account = account;
            InitializeComponent();

            OnlineAccount oa = this.account.OnlineAccount;
            if (oa != null)
            {
                editing = oa.ShallowCopy();
                editing.Id = 0;
            }

            // Hide any fields that don't apply to this account type.
            ShowHideFieldsForAccountType(account.Type);

            // add versions we support explicitly.
            versions.Add("1.0");
            versions.Add("2.0");

            foreach (OnlineAccount other in money.OnlineAccounts.GetOnlineAccounts())
            {
                string v = other.OfxVersion;
                InsertVersion(other.OfxVersion);                
            }

            OfxVersions.ItemsSource = versions;

            this.DataContext = editing;

            ComboBoxName.SelectionChanged += new SelectionChangedEventHandler(OnComboBoxNameSelectionChanged);

            Progress.Visibility = Visibility.Collapsed;
            AccountListPanel.Visibility = Visibility.Collapsed;

            ButtonVerify.IsEnabled = false;

            successPrompt = SignupResultPrompt.Text;

            this.Loaded += new RoutedEventHandler(OnLoaded);
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            GetBankListProgress.Visibility = System.Windows.Visibility.Visible;
            ComboBoxName.Visibility = System.Windows.Visibility.Collapsed;
            ThreadPool.QueueUserWorkItem(new WaitCallback(GetBankList));

            Dispatcher.BeginInvoke(new Action(() =>
            {
                OfxVersions.SelectedItem = versions[versions.Count - 1];
            }));
        }

        object pendingVerify;
        object pendingSignon;

        protected override void OnClosed(EventArgs e)
        {
            this.pendingVerify = null;
            this.pendingSignon = null;
            if (queueProcessor != null)
            {
                queueProcessor.Stop();
                queueProcessor = null;
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
                    TextBoxBrokerId.Visibility = BrokerIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    TextBoxBranchId.Visibility = BranchIdPrompt.Visibility = System.Windows.Visibility.Visible;
                    TextBoxBankId.Visibility = BankIdPrompt.Visibility = System.Windows.Visibility.Visible;
                    break;
                case AccountType.Credit:
                    TextBoxBrokerId.Visibility = BrokerIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    TextBoxBranchId.Visibility = BranchIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    TextBoxBankId.Visibility = BankIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    break;
                case AccountType.Brokerage:
                case AccountType.Retirement:
                    TextBoxBrokerId.Visibility = BrokerIdPrompt.Visibility = System.Windows.Visibility.Visible;
                    TextBoxBranchId.Visibility = BranchIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    TextBoxBankId.Visibility = BankIdPrompt.Visibility = System.Windows.Visibility.Collapsed;
                    break;
            }
        }


        /// <summary>
        /// Add another OFX version in numeric order.
        /// </summary>
        /// <returns>Returns the index into the versions array for the new version</returns>
        int InsertVersion(string version)
        {
            double x = 0;
            if (!string.IsNullOrEmpty(version) && double.TryParse(version, out x))
            {
                for (int i = 0, n = versions.Count; i < n; i++)
                {
                    double y = double.Parse(versions[i]);
                    if (y == x)
                    {
                        return i;
                    }
                    if (y > x)
                    {
                        versions.Insert(i, version);
                        return i;
                    }
                }
                versions.Add(version);
                return versions.Count - 1;
            }

            // invalid
            return -1;
        }
        bool updating;

        OfxInstitutionInfo FindProvider(string name)
        {
            if (providers != null)
            {
                foreach (OfxInstitutionInfo p in providers)
                {
                    if (string.Compare(p.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        List<OfxInstitutionInfo> providers;

        void GetBankList(object state)
        {
            // show the cached list first.
            providers = OfxInstitutionInfo.GetCachedBankList();
            ShowBankList();

            providers = OfxInstitutionInfo.GetRemoteBankList();
            ShowBankList();
        }

        void ShowBankList()
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                updating = true;

                OfxInstitutionInfo selection = null;

                GetBankListProgress.Visibility = System.Windows.Visibility.Collapsed;
                ComboBoxName.Visibility = System.Windows.Visibility.Visible;

                // add known onlineaccount providers.
                foreach (OnlineAccount other in money.OnlineAccounts.GetOnlineAccounts())
                {
                    OfxInstitutionInfo p = FindProvider(other.Name);
                    if (p == null)
                    {
                        p = new OfxInstitutionInfo() { 
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
                        providers.Add(p);
                    }
                    p.Existing = true;
                    p.OnlineAccount = other;
                }

                string saved = ComboBoxName.Text;
                if (string.IsNullOrEmpty(saved))
                {
                    saved = editing.Name;
                }

                foreach (var provider in providers)
                {
                    if (editing != null && provider.Name == saved)
                    {
                        selection = provider;
                    }
                }

                providers.Sort(new Comparison<OfxInstitutionInfo>((a, b) => { return string.Compare(a.Name, b.Name); }));

                this.view = new ListCollectionView(providers);

                if (!debugging)
                {
                    // don't show error items
                    this.view.Filter = new Predicate<object>((item) =>
                    {
                        OfxInstitutionInfo p = (OfxInstitutionInfo)item;
                        return !p.HasError;
                    });
                }

                ComboBoxName.ItemsSource = view;

                if (selection == null)
                {
                    ComboBoxName.Text = saved;
                }
                else
                {
                    ComboBoxName.SelectedItem = selection;
                    ComboBoxName.Text = saved;
                }

                ComboBoxName.Focus();
                updating = false;

                // now we have the bank list we can update the info
                Enqueue((OfxInstitutionInfo)ComboBoxName.SelectedItem);

                UpdateButtonState();
            }));
        }

        private void ComboBoxName_KeyUp(object sender, KeyEventArgs e)
        {            
            if (editor == null)
            {
                editor = this.ComboBoxName.Template.FindName("PART_EditableTextBox", this.ComboBoxName) as TextBox;
                if (editor != null)
                {
                    editor.TextChanged += new TextChangedEventHandler(OnComboBoxNameChanged);
                    OnComboBoxNameChanged();
                }
            }
        }

        void OnProcessQueue(object sender, EventArgs e)
        {
            OfxInstitutionInfo info = null;
            
            // drain the queue and only fetch the most recent request.
            while (true)
            {
                OfxInstitutionInfo next = null;
                if (fetchQueue.TryDequeue(out next))
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
                queueProcessor.Stop();
                queueProcessor = null;
                return;
            }

            ThreadPool.QueueUserWorkItem(new WaitCallback(GetUpdatedBankInfo), info);            
        }

        ConcurrentQueue<OfxInstitutionInfo> fetchQueue = new ConcurrentQueue<OfxInstitutionInfo>();

        void OnComboBoxNameSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!updating)
            {
                HideRightHandPanels();
                selected = (OfxInstitutionInfo)ComboBoxName.SelectedItem;
                if (selected != null)
                {
                    Enqueue(selected);
                }
                UpdateInstitutionInfo(selected);                
            }
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateButtonState();
            }));
        }

        void Enqueue(OfxInstitutionInfo info)
        {
            if (info != null)
            {
                fetchQueue.Enqueue(selected);

                if (queueProcessor == null)
                {
                    queueProcessor = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, new EventHandler(OnProcessQueue), this.Dispatcher);
                }
            }
        }

        void OnComboBoxNameChanged(object sender, TextChangedEventArgs e)
        {
            if (!updating)
            {
                OnComboBoxNameChanged();
            }
            UpdateButtonState();
        }

        string filter;

        void OnComboBoxNameChanged()
        {
            OfxInstitutionInfo ps = FindProvider(ComboBoxName.Text);
            // check for null here allows the user to rename this institution.
            if (ps != null)
            {
                UpdateInstitutionInfo(ps);
            }
            if (view == null || editor == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Update the filter on the combo so it shows only those banks matching what the user typed in so far.
                filter = editor.Text;
                if (string.IsNullOrEmpty(filter))
                {
                    view.Filter = null;
                }
                else if (editor.SelectionLength < filter.Length)
                {
                    if (editor.SelectionStart >= 0)
                    {
                        filter = filter.Substring(0, editor.SelectionStart);
                    }
                    if (string.IsNullOrEmpty(filter))
                    {
                        if (!debugging)
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
                            view.Filter = null;
                        }
                    }
                    else
                    {
                        if (!debugging)
                        {
                            // don't show error items
                            this.view.Filter = new Predicate<object>((item) =>
                            {
                                OfxInstitutionInfo p = (OfxInstitutionInfo)item;
                                return !p.HasError && p.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                            });
                        }
                        else
                        {
                            view.Filter = new Predicate<object>((item) => 
                            {
                                OfxInstitutionInfo p = (OfxInstitutionInfo)item;
                                return p.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                            });
                        }
                    }
                }
            }));
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            ButtonVerify.IsEnabled = !string.IsNullOrWhiteSpace(ComboBoxName.Text) &&
                    !string.IsNullOrWhiteSpace(TextBoxInstitution.Text) &&
                    !string.IsNullOrWhiteSpace(TextBoxFid.Text) &&
                    !string.IsNullOrWhiteSpace(TextBoxOfxAddress.Text);
        }


        void GetUpdatedBankInfo(object state)
        {
            OfxInstitutionInfo provider = (OfxInstitutionInfo)state;

            OfxInstitutionInfo ps = OfxInstitutionInfo.GetProviderInformation(provider);

            if (this.selected != state)
            {
                // user has moved on.
                return;
            }

            if (ps != null)
            {
                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateInstitutionInfo(ps);                    
                }));
            }

        }

        OfxInstitutionInfo selected;

        void UpdateInstitutionInfo(OfxInstitutionInfo ps)
        {
            if (ps == null)
            {
                //editing.FID = null;
                //editing.Institution = null;
                //editing.LogoUrl = null;
                //editing.BankId = null;
                editing.UserId = null;
                editing.Password = null;
                //editing.Ofx = null;
                editing.AccessKey = null;
                editing.AuthToken = null;
                editing.UserCred1 = null;
                editing.UserCred2 = null;
                editing.UserKey = null;
            }
            else
            {
                // update fields of dialog to match online information about this financial institution.
                string version = ps.OfxVersion;
                if (string.IsNullOrEmpty(version))
                {
                    version = "1.0";
                }
                editing.OfxVersion = version;
                OfxVersions.SelectedIndex = InsertVersion(version);
                editing.Ofx = ps.ProviderURL;
                editing.LogoUrl = string.IsNullOrEmpty(ps.SmallLogoURL) ? null : ps.SmallLogoURL;
                account.WebSite = ps.Website;
                editing.FID = ps.Fid;
                editing.Institution = ps.Org;
                editing.Name = ps.Name;
                
                OnlineAccount oa = ps.OnlineAccount;
                if (oa != null)
                {
                    // this is an existing online account, so copy the additional properties from there.
                    editing.BankId = oa.BankId;
                    editing.UserId = oa.UserId;
                    editing.Password = oa.Password;
                    editing.UserCred1 = oa.UserCred1;
                    editing.UserCred2 = oa.UserCred2;
                }
                else
                {
                    editing.AccessKey = null;
                    editing.AuthToken = null;
                    editing.UserCred1 = null;
                    editing.UserCred2 = null;
                    editing.UserKey = null;
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
            var oa = account.OnlineAccount;
            if (oa == null)
            {
                if (this.selected == null)
                {
                    // then the name entered by user didn't exist in the list of providers, so use the text box value
                    editing.Name = ComboBoxName.Text;
                }
                oa = this.money.OnlineAccounts.FindOnlineAccount(editing.Name);
                if (oa == null)
                {
                    oa = editing;
                    this.money.OnlineAccounts.AddOnlineAccount(editing);
                }
                account.OnlineAccount = oa;
            }

            if (oa.IsDeleted)
            {
                // user changed their mind.
                oa.Undelete();
            }

            // make sure we get edited values (don't use databinding here because we want to ensure Cancel leaves everything in clean state)
            if (oa != editing)
            {
                oa.Name = editing.Name;
                oa.Institution = editing.Institution;
                oa.FID = editing.FID;
                oa.BankId = editing.BankId;
                oa.BranchId = editing.BranchId;
                oa.BrokerId = editing.BrokerId;
                oa.Ofx = editing.Ofx;
                oa.OfxVersion = editing.OfxVersion;
                oa.AppId = editing.AppId;
                oa.AppVersion = editing.AppVersion;
                oa.UserId = editing.UserId;
                oa.LogoUrl = editing.LogoUrl;
                oa.Password = editing.Password;
                oa.UserCred1 = editing.UserCred1;
                oa.UserCred2 = editing.UserCred2;
                oa.ClientUid = editing.ClientUid;
                oa.AuthToken = editing.AuthToken;
                oa.AccessKey = editing.AccessKey;
            }

            // go through all accounts and add the ones the user wants added.
            if (this.found != null)
            {
                foreach (AccountListItem item in this.found)
                {
                    if (item.UserAdded)
                    {
                        OnAddAccount(item);
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
            if (provider != null && providers != null)
            {
                if (!string.IsNullOrEmpty(editing.Name))
                {
                    provider.Name = editing.Name;
                }
                if (!string.IsNullOrEmpty(editing.FID))
                {
                    provider.Fid = editing.FID;
                }
                if (!string.IsNullOrEmpty(editing.Institution))
                {
                    provider.Org = editing.Institution;
                }
                if (!string.IsNullOrEmpty(editing.BrokerId))
                {
                    provider.BrokerId = editing.BrokerId;
                }
                if (!string.IsNullOrEmpty(editing.OfxVersion))
                {
                    provider.OfxVersion = editing.OfxVersion;
                }
                OfxInstitutionInfo.SaveList(providers);
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

        OfxMfaChallengeRequest challenge;

        /// <summary>
        /// Background thread to connect to bank
        /// </summary>
        /// <param name="state"></param>
        void StartSignup(object state)
        {
            var id = new object();
            this.pendingSignon = id;
                  
            try
            {
                var info = OfxRequest.GetSignonInfo(this.money, this.editing);

                if (info.AuthTokenRequired.ConvertYesNoToBoolean() && string.IsNullOrEmpty(this.editing.AuthToken))
                {
                    bool result = (bool)Dispatcher.Invoke(new Func<bool>(() => {
                        return PromptForAuthToken(info, OfxErrorCode.AUTHTOKENRequired);
                    }));

                    if (!result)
                    {
                        // user cancelled.
                        return;
                    }
                }                
                
                if (info.MFAChallengeRequired.ConvertYesNoToBoolean())
                {
                    challenge = new OfxMfaChallengeRequest(this.editing, this.money);
                    challenge.Completed += OnMfaChallengeCompleted;
                    challenge.BeginMFAChallenge();
                }
                else
                {
                    Signup(null);
                }
            }
            catch (OfxException ex)
            {
                if (this.pendingSignon == id)
                {
                    this.pendingSignon = null;
                       Dispatcher.Invoke(new Action(() =>
                    {
                        HandleSignOnErrors(ex, new Action(() => {
                            ThreadPool.QueueUserWorkItem(new WaitCallback(StartSignup));
                        }));
                    }));
                }
            }
            catch (Exception ex)
            {
                if (this.pendingSignon == id)
                {
                    this.pendingSignon = null;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        ShowError(ex.GetType().Name, ex.Message);
                    }));
                }
            }
        }

        void OnMfaChallengeCompleted(object sender, EventArgs e)
        {
            OnChallengeCompleted(sender, e);

            if (this.editing.MfaChallengeAnswers != null)
            {
                // back to background thread.
                ThreadPool.QueueUserWorkItem(new WaitCallback(Signup));
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
                ShowError("Cancelled", "User cancelled");
                return false;
            }
        }

        object signupRequest;

        private void Signup(object state)
        {
            OfxRequest req = new OfxRequest( this.editing, this.money, AccountHelper.PickAccount);
            this.signupRequest = req;
            string logpath;
            OFX ofx = req.Signup(this.editing, out logpath);
            if (this.signupRequest == req)
            {
                this.signupRequest = null;
                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    Progress.Visibility = Visibility.Collapsed;
                    ShowResult(ofx);
                    OfxVersions.SelectedIndex = InsertVersion(this.editing.OfxVersion); // in case it was changed by the "Signup" method.
                }));
            }
        }

        OfxErrorCode GetSignOnCode(OFX ofx)
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
                code = ex.Message;
                if (string.IsNullOrEmpty(code))
                {
                    code = oe.Code;
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
                ShowHtmlError(code, msg);
            }
            else
            {
                ShowError(code, msg, errorLink);
            }
        }

        private void HandleSignOnErrors(OfxException ex, Action continuation)
        {
            OFX ofx = ex.Root;
            if (ofx != null)
            {
                OfxErrorCode code = GetSignOnCode(ex.Root);
                if ((code == OfxErrorCode.AUTHTOKENRequired && string.IsNullOrWhiteSpace(this.editing.AuthToken)) || code == OfxErrorCode.AUTHTOKENInvalid)
                {
                    var info = OfxRequest.GetSignonInfo(this.money, this.editing);
                    if (info != null)
                    {
                        if (!PromptForAuthToken(info, code))
                        {
                            // user cancelled.
                            return;
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(continuation);
                            return;
                        }
                    }
                }
                else if (code == OfxErrorCode.MFAChallengeAuthenticationRequired)
                {
                    // Begin the MFA Challenge Authentication process...
                    ShowError("Requesting More Information", "Server is requesting Multi-Factor Authentication...");

                    challenge = new OfxMfaChallengeRequest(this.editing, this.money);
                    challenge.UserData = continuation;
                    challenge.Completed += OnChallengeCompleted;
                    challenge.BeginMFAChallenge();                    
                    return;
                }
                else if (code == OfxErrorCode.MustChangeUSERPASS)
                {
                    PromptForNewPassword(continuation);
                    return;
                }
                else if (code == OfxErrorCode.SignonInvalid)
                {
                    PromptForPassword(OfxRequest.GetSignonInfo(this.money, this.editing), null, code, ex.Message, continuation);
                    return;
                }
            }

            HandleUnexpectedError(ex);
        }

        void PromptForPassword(OfxSignOnInfo info, List<Block> prompt, OfxErrorCode code, string errorMessage, Action continuation)
        {
            if (info != null && info.AuthTokenRequired.ConvertYesNoToBoolean() && string.IsNullOrEmpty(this.editing.AuthToken))
            {
                if (!PromptForAuthToken(info, code))
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
                ShowError("Cancelled", "User cancelled");
            }
        }

        void OnChallengeCompleted(object sender, EventArgs e)
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
                HandleUnexpectedError(req.Error);
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
                    ShowError("Error", "User cancelled");
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
                    HandleUnexpectedError(ex);
                }
                else
                {
                    Dispatcher.BeginInvoke(continuation);
                }
            }
            else
            {
                ShowError("Cancelled", "User cancelled");
            }
        }

        private void ShowError(string code, string message, Hyperlink link = null)
        {
            AccountListPanel.Visibility = Visibility.Visible;            
            SignupResultPrompt.Text = code;
            SignupResultPrompt.Foreground = Brushes.Red;
            OnlineResultList.Visibility = System.Windows.Visibility.Collapsed;

            ErrorMessage.Document.Blocks.Clear();

            Paragraph p = new Paragraph();
            p.Inlines.Add(new Run(message));
            if (link != null)
            {
                p.Inlines.Add(link);
            }
            ErrorMessage.Document.Blocks.Add(p);

            ErrorScroller.Visibility = System.Windows.Visibility.Visible;
            ErrorHtml.Visibility = System.Windows.Visibility.Collapsed;
            ErrorMessage.Visibility = System.Windows.Visibility.Visible;
            Progress.Visibility = Visibility.Collapsed;
        }

        private void ShowHtmlError(string code, string message)
        {
            AccountListPanel.Visibility = Visibility.Visible;
            SignupResultPrompt.Text = code;
            SignupResultPrompt.Foreground = Brushes.Red;
            OnlineResultList.Visibility = System.Windows.Visibility.Collapsed;

            ErrorHtml.NavigateToString(message);

            ErrorScroller.Visibility = System.Windows.Visibility.Visible;
            ErrorHtml.Visibility = System.Windows.Visibility.Visible;
            ErrorMessage.Visibility = System.Windows.Visibility.Collapsed;
            Progress.Visibility = Visibility.Collapsed;
        }

        private void HideRightHandPanels()
        {
            ErrorMessage.Visibility = Visibility.Collapsed;
            AccountListPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowResult(OFX ofx)
        {
            HideRightHandPanels();
            AccountListPanel.Visibility = Visibility.Visible;
            SignupResultPrompt.SetValue(TextBlock.ForegroundProperty, DependencyProperty.UnsetValue);
            SignupResultPrompt.Text = successPrompt;
            this.found = new List<AccountListItem>();

            var sup = ofx.SignUpMessageResponse;
            int index = 1;
            if (sup != null)
            {
                string org = this.editing.Institution;
                if (ofx.SignOnMessageResponse != null && ofx.SignOnMessageResponse.SignOnResponse != null && ofx.SignOnMessageResponse.SignOnResponse.FinancialInstitution != null)
                {
                    org = ofx.SignOnMessageResponse.SignOnResponse.FinancialInstitution.Organization;
                }
                var sor = sup.AccountInfoSet;
                if (sor != null)
                {
                    var list = sor.Accounts;
                    if (list != null)
                    {
                        foreach (var acct in list)
                        {
                            AccountListItem f = FindMatchingOnlineAccount(acct);
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
                                found.Add(f);
                            }
                        }
                    }
                }
            }
            OnlineResultList.ItemsSource = this.found;
            OnlineResultList.Visibility = System.Windows.Visibility.Visible;
            ErrorScroller.Visibility = System.Windows.Visibility.Collapsed;

            ResizeListColumns();
        }

        Typeface typeface;

        private double MeasureListString(string s)
        {
            double fontSize = (double)OnlineResultList.GetValue(ListView.FontSizeProperty);

            if (typeface == null)
            {
                typeface = new Typeface((FontFamily)OnlineResultList.GetValue(ListView.FontFamilyProperty),
                    (FontStyle)OnlineResultList.GetValue(ListView.FontStyleProperty),
                    (FontWeight)OnlineResultList.GetValue(ListView.FontWeightProperty),
                    (FontStretch)OnlineResultList.GetValue(ListView.FontStretchProperty));
            }

            FormattedText ft = new FormattedText(s, System.Globalization.CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
                typeface, fontSize, Brushes.Black);
            return ft.Width;
        }

        /// <summary>
        /// This is a workaround for a bug in WPF ListView, the columns do not resize correctly.
        /// </summary>
        private void ResizeListColumns()
        {
            GridView view = (GridView)OnlineResultList.View;
            foreach (GridViewColumn c in view.Columns)
            {
                c.Width = 0;              
            }

            OnlineResultList.InvalidateMeasure();
            OnlineResultList.InvalidateArrange();
            OnlineResultList.UpdateLayout();

            foreach (GridViewColumn c in view.Columns)
            {
                c.Width = double.NaN;
            }

            OnlineResultList.InvalidateMeasure();
            OnlineResultList.InvalidateArrange();
            OnlineResultList.UpdateLayout();


        }

        string GetResolveButtonCaption()
        {
            return "Click here to select an account to match with this online account";
        }

        AccountListItem FindMatchingOnlineAccount(AccountInfoResponse ar)
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
            else if (ar.InvAccountInfo != null )
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
                    if (a.OnlineAccount != null )
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
                tooltip = GetResolveButtonCaption();
                // create place holder for new account, this will be turned into a real account when
                // user clicks the "add" button.
                found = new Account();
                found.Name = ar.Description;
                found.AccountId = id;
                found.Type = type;                
                found.WebSite = this.account != null ? this.account.WebSite : null;
                if (string.IsNullOrEmpty(found.WebSite) && this.profile != null)
                {
                    found.WebSite = profile.CompanyUrl;
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
                        editing.Name, a.Name, item.CorrectType, a.Type.ToString()), "Account Type Mismatch", MessageBoxButton.YesNo, MessageBoxImage.Question))
                    {
                        a.Type = item.CorrectType;
                        item.HasWarning = false;
                    }
                }
                else if (item.IsNew)
                {
                    Account a = AccountHelper.PickAccount(this.money, item.Account);
                    if (a != null)
                    {
                        // user made a choice
                        item.Account = a;
                        item.UserAdded = true;
                        item.IsNew = false;
                        item.IsDisconnected = false;
                        e.ToolTip = "Click here to undo the last account add operation";
                    } else
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
                    e.ToolTip = GetResolveButtonCaption(); 
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
                    editing.Name = ComboBoxName.Text;
                }
                OnlineAccount oa = this.money.OnlineAccounts.FindOnlineAccount(editing.Name);
                if (oa == null)
                {
                    oa = editing;
                    this.money.OnlineAccounts.AddOnlineAccount(editing);
                }
                a.OnlineAccount = oa;
            }

        }

        private void OnButtonVerify(object sender, RoutedEventArgs e)
        {
            if (this.editing != null)
            {
                // copy the name back from combo box in case the user has edited it.
                editing.Name = ComboBoxName.Text;
                
                if (string.IsNullOrWhiteSpace(editing.OfxVersion))
                {
                    editing.OfxVersion = "1.0";
                }

                AccountListPanel.Visibility = Visibility.Collapsed;                
                SignupResultPrompt.Text = "Connecting to your financial institution...";
                SignupResultPrompt.SetValue(TextBlock.ForegroundProperty, DependencyProperty.UnsetValue);
                OnlineResultList.Visibility = System.Windows.Visibility.Collapsed;
                ErrorMessage.Document.Blocks.Clear();
                Progress.Visibility = Visibility.Visible;
                Progress.IsIndeterminate = true;
                HideRightHandPanels();

                System.Threading.ThreadPool.QueueUserWorkItem(new WaitCallback(StartVerify));
            }  
        }

        /// <summary>
        /// Background thread to connect to bank and verify OFX support
        /// </summary>
        /// <param name="state"></param>
        void StartVerify(object state)
        {            
            if (this.pendingVerify != null)
            {
                // hmmm, what does this mean?
                return;
            }
            string fetchingName = this.editing.Name;

            OfxRequest req = new OfxRequest( this.editing, this.money, AccountHelper.PickAccount);
            this.pendingVerify = req;

            string cachePath;
            try
            {

                // see if we can get the server profile first...
                ProfileResponseMessageSet profile = req.GetProfile(this.editing, out cachePath);

                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    if (this.pendingVerify == req && fetchingName == this.editing.Name)
                    {
                        this.pendingVerify = null;
                        Progress.Visibility = Visibility.Collapsed;
                        HandleProfileResponse(profile, cachePath);
                        OfxVersions.SelectedIndex = InsertVersion(this.editing.OfxVersion); // in case it was changed by the "GetProfile" method.
                    }
                }));
            }
            catch (OfxException ex)
            {
                if (this.pendingVerify == req)
                {
                    this.pendingVerify = null;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        OnGotProfile(ex.Message);
                        HandleSignOnErrors(ex, new Action(() =>
                        {
                            ThreadPool.QueueUserWorkItem(new WaitCallback(StartVerify), state);
                        }));
                    }));
                }
            }
            catch (HtmlResponseException he)
            {
                if (this.pendingVerify == req)
                {
                    this.pendingVerify = null;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        OnHtmlResponse(he.Html);
                    }));
                }
            }
            catch (Exception ex)
            {
                if (this.pendingVerify == req)
                {
                    this.pendingVerify = null;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        OnGotProfile(ex.Message);
                        ShowError(ex.GetType().Name, ex.Message);
                    }));
                }
            }
        }

        private void OnHtmlResponse(string html)
        {            
            ShowHtmlError("Connect Error", html);
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

                    p.Inlines.Add( new Run("We received the following information from " + this.editing.Name + ". "));
                    p.Inlines.Add( new Run("Please check this is who you think it is then enter your login information below:"));

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
                                string enrollInstructions = signup.OtherEnrollInfo.Message.Trim();
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
                
                OnGotProfile(error);

                error += "\n\nIf you are need help please email the following file to chris@lovettsoftware.com: ";

                ShowError("Connect Error", error, InternetExplorer.GetOpenFileHyperlink("Unexpected XML Response", cachePath));
            }
            else
            {
                OnGotProfile(null);

                PromptForPassword(info, prompt, statusCode, error, new Action(() =>
                {
                    if (retryProfile)
                    {
                        System.Threading.ThreadPool.QueueUserWorkItem(new WaitCallback(StartVerify));
                    }
                    else
                    {
                        SignOn();
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
                providers.Add(institution);
            }

            if (institution != null && providers.Contains(institution))
            {
                // Save any user edited fields back to the selected OfxInstututionInfo.
                institution.OfxVersion = editing.OfxVersion;
                institution.ProviderURL = editing.Ofx;
                institution.Fid = editing.FID;
                institution.Org = editing.Institution;
                institution.Name = editing.Name;

                institution.LastError = error;
                if (error == null)
                {
                    institution.LastConnection = DateTime.Now;
                }
                OfxInstitutionInfo.SaveList(providers);
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
                AccountListPanel.Visibility = Visibility.Visible;
                SignupResultPrompt.Text = "Signing on to online banking server...";
                SignupResultPrompt.SetValue(TextBlock.ForegroundProperty, DependencyProperty.UnsetValue);
                OnlineResultList.Visibility = System.Windows.Visibility.Collapsed;
                ErrorMessage.Document.Blocks.Clear();
                Progress.Visibility = Visibility.Visible;
                Progress.IsIndeterminate = true;
                HideRightHandPanels();

                System.Threading.ThreadPool.QueueUserWorkItem(new WaitCallback(StartSignup));
            }
        }

    }

}
