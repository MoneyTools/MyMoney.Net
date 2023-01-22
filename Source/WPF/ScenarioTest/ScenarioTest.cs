using LovettSoftware.DgmlTestModeling;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Walkabout.Data;
using Walkabout.Tests;
using Walkabout.Tests.Interop;
using Walkabout.Tests.Wrappers;
using Brushes = System.Windows.Media.Brushes;
using Clipboard = System.Windows.Clipboard;

namespace ScenarioTest
{
    [SetUpFixture]
    public class SetupTrace
    {
        [OneTimeSetUp]
        public void StartTest()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        [OneTimeTearDown]
        public void EndTest()
        {
            Trace.Flush();
        }
    }

    public class Tests
    {
        private static Process testProcess;
        private MainWindowWrapper window;
        private OfxServerWindowWrapper ofxServerWindow;
        private Random random;
        private static Process serverProcess;
        private const int vsDgmlMonitorTimeout = 3000;
        private DgmlTestModel model;
        private const int ScenarioTestSteps = 500; // number of model actions to perform
        private Exception testError;

        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void Teardown()
        {
            if (testProcess != null)
            {
                testProcess.Kill();
                testProcess = null;
            }
            if (serverProcess != null)
            {
                try
                {
                    serverProcess.Kill();
                }
                catch
                {
                }
                serverProcess = null;
            }

            Database = null;
        }

        void WriteLine(string msg)
        {
            Trace.WriteLine(msg);
        }

        [Test]
        public void Test1()
        {
            // The test must run in an STAThread in order for the Clipboard functions to work.
            var thread = new Thread(() => TestUI());
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (testError != null)
            {
                throw testError;
            }
        }

        private void TestUI()
        {
            // This test executes a model of what and end user might want to do with this application.
            // The model is described in DGML
            try
            {
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
                int seed = Environment.TickCount;
                // int seed = 272222602; // Bug with ListCollectionView: 'Sorting' is not allowed during an AddNew or EditItem transaction.
                // seed = 313591431;  // Another variation of the above, sometimes even CancelEdit throws!
                this.random = new Random(seed);
                this.WriteLine("Model Seed = " + seed);

                // new TestLog(this.TestContext)
                this.model = new DgmlTestModel(this, Console.Out, this.random, vsDgmlMonitorTimeout);

                string fileName = this.FindTestModel("TestModel.dgml");
                this.model.Load(fileName);
                Thread.Sleep(2000); // let graph load.
                int delay = 0; // 1000 is handy for debugging.
                this.model.Run(new Predicate<DgmlTestModel>((m) => { return m.StatesExecuted > ScenarioTestSteps; }), delay);
            }
            catch (Exception ex)
            {
                testError = ex;
                string temp = Path.Combine(Path.GetTempPath(), "Screen.png");
                Win32.CapturePrimaryScreen(temp, System.Drawing.Imaging.ImageFormat.Png);
                this.WriteLine("ScreenCapture: " + temp);
            }
            finally
            {
                this.model.Stop();
                Teardown();
            }
        }

        private string FindTestModel(string filename)
        {
            string path = new Uri(this.GetType().Assembly.Location).LocalPath;

            // walk up to TestResults.
            while (path != null)
            {
                var file = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), filename);
                if (File.Exists(file))
                {
                    return file;
                }
                path = System.IO.Path.GetDirectoryName(path);
            }

            throw new Exception(filename + " not found");
        }

        #region Start 

        private const string OfxServerUrl = "http://localhost:3000/ofx/test/";

        private string FindFileInParent(string start, string name)
        {
            do
            {
                string path = Path.Combine(start, name);
                if (File.Exists(path))
                {
                    return path;
                }

                var parent = Path.GetDirectoryName(start);
                if (parent == start)
                {
                    throw new FileNotFoundException(name);
                }
                start = parent;
            } while (true);
        }

        private void Start()
        {
            this.WriteLine("Start");
            if (testProcess == null)
            {
                this.EnsureCleanState();
                string exe = typeof(Walkabout.MainWindow).Assembly.Location;
                exe = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + ".exe");

                this.ResignAssembly(exe);

                // find the ofx test server, this file is created by the OfxTestServer project.
                string path = this.FindFileInParent(Path.GetDirectoryName(exe), "ServerConfig.txt");
                string serverExePath = File.ReadAllText(path).Trim();

                // start ofx test server
                string serverExe = Path.Combine(Path.GetDirectoryName(serverExePath), Path.GetFileNameWithoutExtension(serverExePath) + ".exe");
                serverProcess = Process.Start(new ProcessStartInfo(serverExe));
                serverProcess.WaitForInputIdle();
                this.ofxServerWindow = OfxServerWindowWrapper.FindMainWindow(serverProcess.Id);

                // start money
                ProcessStartInfo psi = new ProcessStartInfo(exe, "/nosettings");
                testProcess = Process.Start(psi);
                testProcess.WaitForInputIdle();
                this.window = MainWindowWrapper.FindMainWindow(testProcess.Id);
            }
        }

        /// <summary>
        /// CodeCoverage screws up the resigning because it thinks the .snk file has a password.
        /// </summary>
        /// <param name="exe"></param>
        private void ResignAssembly(string exe)
        {
        }

        private void EnsureCleanState()
        {
        }

        private void Interactive()
        {
            if (this.window.IsBlocked)
            {
                throw new Exception("Main window is blocked by an unexpected modal dialog");
            }
            this.window.WaitForInputIdle(500);
            if (this.window.IsNotResponding)
            {
                throw new Exception("Main window is hung!");
            }
        }

        private bool StartOver
        {
            get
            {
                return this.model.StatesExecuted > this.creationTime + 50;
            }
        }

        #endregion

        #region Database

        private static IDatabase database;
        private bool isLoaded;
        private CreateDatabaseDialogWrapper createNewDatabaseDialog;
        private static string databasePath;
        private bool sampleData;
        private OnlineAccountsDialogWrapper onlineAccounts;
        private bool hasOnlineAccounts;
        private const string OnlineBankName = "Last Chance Bank Of Hope";
        private PasswordDialogWrapper passwordDialog;
        private PasswordDialogWrapper challengeDialog;
        private int creationTime;
        private bool dataChangedSinceExport;

        private void CreateNewDatabase()
        {
            this.WriteLine("CreateNewDatabase");
            this.createNewDatabaseDialog = CreateDatabaseDialogWrapper.FindCreateDatabaseDialogWindow(testProcess.Id, 1, false);
            if (this.createNewDatabaseDialog == null)
            {
                // send "File.New" command.
                this.window.New();
                this.createNewDatabaseDialog = CreateDatabaseDialogWrapper.FindCreateDatabaseDialogWindow(testProcess.Id, 5, true);
                if (this.createNewDatabaseDialog == null)
                {
                    throw new Exception("Why didn't Window.New work?");
                }
                this.isLoaded = false; // stop us from exiting until dialog is fulfilled.
            }
            this.creationTime = this.model.StatesExecuted;
        }

        private void EnterCreateDatabase()
        {
            this.WriteLine("EnterCreateDatabase");
            this.ClearTransactionViewState();
            this.window.CloseReport();
            this.hasOnlineAccounts = false;
            this.onlineAccounts = null;
            this.sampleData = false;
        }

        private static IDatabase Database
        {
            get { return database; }
            set
            {
                if (database != null)
                {
                    DeleteDatabase(database);
                }
                database = value;
            }
        }

        private static void DeleteDatabase(IDatabase database)
        {
            Exception error = null;
            for (int retries = 5; retries > 0; retries--)
            {
                try
                {
                    database.Delete();
                    return;
                }
                catch (Exception ex)
                {
                    error = ex;
                    Thread.Sleep(100);
                }
            }

            Console.WriteLine("### Error deleting database: " + error.Message);
        }

        private MyMoney Load()
        {
            MyMoney result = database.Load(null);
            database.Disconnect();
            return result;
        }

        private string GetFreeDatabase(string baseNamePattern)
        {
            int index = 2;
            string databasePath = System.IO.Path.GetFullPath(string.Format(baseNamePattern, ""));
            while (!this.DeleteFileWithRetries(databasePath, 1))
            {
                databasePath = System.IO.Path.GetFullPath(string.Format(baseNamePattern, index.ToString()));
                index++;
            }


            string attachmentPath = System.IO.Path.GetFullPath(System.IO.Path.GetFileNameWithoutExtension(databasePath)) + ".Attachments";
            if (Directory.Exists(attachmentPath))
            {
                Directory.Delete(attachmentPath, true);
            }

            return databasePath;
        }

        private void CreateSqliteDatabase()
        {
            this.WriteLine("CreateSqliteDatabase");
            string databasePath = this.GetFreeDatabase("TestDatabase{0}.mmdb");
            this.createNewDatabaseDialog.CreateSqliteDatabase(databasePath);
            this.isLoaded = true;
            this.createNewDatabaseDialog = null;
            Database = new SqliteDatabase() { DatabasePath = databasePath };
        }

        private void CreateXmlDatabase()
        {
            this.WriteLine("CreateXmlDatabase");
            databasePath = this.GetFreeDatabase("TestDatabase{0}.xml");
            this.DeleteFileWithRetries(databasePath, 1);
            this.createNewDatabaseDialog.CreateXmlDatabase(databasePath);
            this.isLoaded = true;
            this.createNewDatabaseDialog = null;
            Database = new XmlStore(databasePath, null);
        }

        private void CreateBinaryXmlDatabase()
        {
            this.WriteLine("CreateBinaryXmlDatabase");
            databasePath = this.GetFreeDatabase("TestDatabase.bxml");
            this.DeleteFileWithRetries(databasePath, 1);
            this.createNewDatabaseDialog.CreateBinaryXmlDatabase(databasePath);
            this.isLoaded = true;
            this.createNewDatabaseDialog = null;
            Database = new BinaryXmlStore(databasePath, null);
            this.ClearTransactionViewState();
        }

        private void PopulateData()
        {
            // group
        }

        private bool NoSampleData
        {
            get
            {
                return !this.sampleData;
            }
        }

        private void AddSampleData()
        {
            this.WriteLine("AddSampleData");
            ContextMenu subMenu = this.window.MainMenu.OpenSubMenu("MenuHelp");
            subMenu.InvokeMenuItem("MenuSampleData");

            AutomationElement msgbox = this.window.Element.FindChildWindow("Add Sample Data", 5);
            if (msgbox != null)
            {
                MessageBoxWrapper mbox = new MessageBoxWrapper(msgbox);
                mbox.ClickYes();
            }

            AutomationElement child = this.window.Element.FindChildWindow("Sample Database Options", 10);
            if (child != null)
            {
                SampleDataDialogWrapper cd = new SampleDataDialogWrapper(child);
                cd.ClickOk();
            }

            Thread.Sleep(5000);
            this.window.WaitForInputIdle(5000);

            this.Save();

            this.window.WaitForInputIdle(5000);

            this.sampleData = true;
            this.WriteLine("dataChangedSinceExport reset because of AddSampleData");
            this.dataChangedSinceExport = true;

            // give database time to flush...
            Thread.Sleep(2000);
            int retries = 5;
            MyMoney money = null;
            Exception ex = null;
            while (retries > 0 && money == null)
            {
                // now load the database and pull out the categories.
                retries--;
                try
                {
                    money = this.Load();
                }
                catch (Exception e)
                {
                    // could be that the test process is still writing!
                    // so try again in a bit.
                    ex = e;
                    Thread.Sleep(1000);
                }
            }

            Assert.IsNotNull(money, "Could not load money database!");

            List<string> categories = new List<string>();
            foreach (Category c in money.Categories)
            {
                string cat = c.GetFullName();
                categories.Add(cat);
            }
            categories.Sort();
            SampleCategories = categories.ToArray();


            List<string> payees = new List<string>();
            foreach (Payee p in money.Payees)
            {
                payees.Add(p.Name);
            }
            payees.Sort();
            SamplePayees = payees.ToArray();

            this.ClearTransactionViewState();
        }

        private bool IsDirty
        {
            get
            {
                return this.window.Title.Contains("*");
            }
        }

        private void Save()
        {
            this.WriteLine("Save");
            this.window.Save();
        }

        private bool DatabaseLoaded
        {
            get
            {
                return this.isLoaded;
            }
        }

        private bool CreateDatabasePrompt
        {
            get
            {
                return CreateDatabaseDialogWrapper.FindCreateDatabaseDialogWindow(testProcess.Id, 1, false) != null;
            }
        }


        private void DownloadAccounts()
        {
            this.WriteLine("DownloadAccounts");
            this.onlineAccounts = this.window.DownloadAccounts();
        }

        internal bool NoAccountsDownloaded
        {
            get
            {
                return !this.hasOnlineAccounts;
            }
        }

        internal bool AccountsDownloaded
        {
            get
            {
                return this.hasOnlineAccounts;
            }
        }

        internal void ConnectToBank()
        {
            this.WriteLine("ConnectToBank");
            this.onlineAccounts.WaitForGetBankList();

            for (int retries = 5; retries > 0; retries--)
            {
                this.onlineAccounts.Name = OnlineBankName;
                this.onlineAccounts.Institution = "bankofhope";
                this.onlineAccounts.FID = "1234";
                this.onlineAccounts.OfxAddress = OfxServerUrl;
                this.onlineAccounts.AppId = "QWIN";
                this.onlineAccounts.AppVersion = "1700";

                if (this.onlineAccounts.Element.IsButtonEnabled("ButtonVerify"))
                {
                    // give connect button time to react...
                    this.passwordDialog = this.onlineAccounts.ClickConnect();

                    if (this.passwordDialog != null)
                    {
                        return;
                    }
                }
                else
                {
                    // sometimes the setting of the properties doesn't work!
                    Thread.Sleep(250);
                }

            }
            throw new Exception("Can't seem to get the connect button to work");
        }

        internal void SignOnToBank()
        {
            this.WriteLine("SignOnToBank");
            this.ofxServerWindow.UserName = this.passwordDialog.UserName = "test";
            this.ofxServerWindow.Password = this.passwordDialog.Password = "1234";

            bool mfa = false;
            if (this.random.Next(0, 2) == 0)
            {
                // turn on MFA challenge
                this.ofxServerWindow.MFAChallengeRequired = mfa = true;
            }
            else
            {
                // turn it off by selecting something else.                
                this.ofxServerWindow.UseAdditionalCredentials = true;
            }

            this.passwordDialog.ClickOk();
            this.passwordDialog = null;

            // give the old password dialog time to go away!
            Thread.Sleep(1000);

            AutomationElement challenge = this.onlineAccounts.Element.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "PasswordDialog"), 10, 500);

            if (mfa && challenge == null)
            {
                throw new Exception("Where is the challenge dialog?");
            }

            if (challenge != null)
            {
                if (challenge.Current.Name.Contains("Multi"))
                {
                    this.challengeDialog = new PasswordDialogWrapper(challenge);
                }
                else if (mfa)
                {
                    throw new Exception("The challenge dialog didn't have the expected name, found: '" + challenge.Current.Name + "'");
                }
            }

        }

        internal bool IsChallenged { get { return this.challengeDialog != null; } }
        internal bool NoChallenge { get { return this.challengeDialog == null; } }

        internal void AnswerChallenge()
        {
            this.WriteLine("AnswerChallenge");
            this.challengeDialog.SetUserDefinedField("MFA13", "1234");
            this.challengeDialog.SetUserDefinedField("123", "Newcastle");
            this.challengeDialog.SetUserDefinedField("MFA16", "HigginBothum");

            this.challengeDialog.ClickOk();
            this.challengeDialog = null;
        }

        internal void AddOnlineAccounts()
        {
            this.WriteLine("AddOnlineAccounts");
            for (int retries = 5; retries > 0; retries--)
            {
                foreach (var item in this.onlineAccounts.GetOnlineAccounts())
                {
                    if (item.HasAddButton)
                    {
                        item.ClickAdd();
                        this.hasOnlineAccounts = true;

                        string title = "Select Account for: " + item.Id;
                        MainWindowWrapper mainWindow = MainWindowWrapper.FindMainWindow(this.onlineAccounts.Element.Current.ProcessId);
                        AutomationElement child = mainWindow.Element.FindChildWindow(title, 5);
                        if (child != null)
                        {
                            AccountPickerWrapper picker = new AccountPickerWrapper(child);
                            picker.ClickAddNewAccount();

                            AutomationElement child2 = mainWindow.Element.FindChildWindow("Account", 5);
                            if (child2 != null)
                            {
                                AccountSettingsWrapper settings = new AccountSettingsWrapper(child2);
                                settings.ClickOk();
                            }
                        }
                        return;
                    }
                }
                Thread.Sleep(1000);
            }
            throw new Exception("Where are the online accounts?");
        }

        internal void DismissOnlineAccounts()
        {
            this.WriteLine("DismissOnlineAccounts");
            this.onlineAccounts.ClickOk();
        }

        internal bool HasOnlineAccount
        {
            get { return this.hasOnlineAccounts; }
        }

        internal void DownloadTransactions()
        {
            this.WriteLine("DownloadTransactions");
        }

        internal void Synchronize()
        {
            this.WriteLine("Synchronize");
            this.window.Synchronize();
        }

        internal void SelectDownloadTransactions()
        {
            this.WriteLine("SelectDownloadTransactions");
            var charts = this.window.GetChartsArea();
            DownloadDetailsWrapper details = charts.SelectDownload();

            for (int retries = 5; retries > 0; retries--)
            {
                foreach (var oa in details.GetOnlineAccounts())
                {
                    foreach (var acct in oa.GetAccounts())
                    {
                        acct.Select();
                        details.Close();
                        return;
                    }
                }
                Thread.Sleep(1000);
            }

            // bugbug: no downloaded account info still?
        }

        #endregion

        #region Charts

        internal void ViewCharts()
        {
            this.WriteLine("ViewCharts");
        }

        internal void ViewTrends()
        {
            this.WriteLine("ViewTrends");
            var charts = this.window.GetChartsArea();
            charts.SelectTrends();
        }

        internal void ViewIncomes()
        {
            this.WriteLine("ViewIncomes");
            var charts = this.window.GetChartsArea();
            charts.SelectIncomes();
        }

        internal void ViewExpenses()
        {
            this.WriteLine("ViewExpenses");
            var charts = this.window.GetChartsArea();
            charts.SelectExpenses();
        }

        internal void ViewStock()
        {
            this.WriteLine("ViewStock");
            if (this.IsSecuritySelected)
            {
                var charts = this.window.GetChartsArea();
                charts.SelectStock();
            }
        }
        internal void ViewHistory()
        {
            this.WriteLine("ViewHistory");
            if (this.window.IsCategorySelected || this.window.IsPayeeSelected)
            {
                var charts = this.window.GetChartsArea();
                charts.SelectHistory();
            }
        }

        #endregion

        #region Attachments
        private AttachmentDialogWrapper attachmentDialog;

        private void OpenAttachmentDialog()
        {
            this.WriteLine("OpenAttachmentDialog");
            this.EnsureSelectedTransaction();
            this.attachmentDialog = this.selectedTransaction.ClickAttachmentsButton();
        }

        private void PasteImageAttachment()
        {
            this.WriteLine("PasteImageAttachment");
            Assert.IsNotNull(this.attachmentDialog);

            var border = new System.Windows.Controls.Border()
            {
                Width = 300,
                Height = 100,
                Background = Brushes.LightGreen,
                BorderBrush = Brushes.Green,
                BorderThickness = new Thickness(1)
            };
            border.Child = new System.Windows.Shapes.Ellipse()
            {
                Width = 250,
                Height = 90,
                Fill = Brushes.ForestGreen,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(5)
            };

            border.Arrange(new Rect(0, 0, 300, 100));
            border.UpdateLayout();

            RenderTargetBitmap bitmap = new RenderTargetBitmap(300, 100, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(border);
            Clipboard.SetImage(bitmap);

            this.attachmentDialog.ClickPaste();

            // verify image exists
            var image = this.attachmentDialog.ScrollViewer.FindImage();

            Assert.IsNotNull(image);
        }

        private bool HasAttachmentImage
        {
            get
            {
                var image = this.attachmentDialog?.ScrollViewer.FindImage(0);
                return image != null;
            }
        }

        private void RotateAttachmentRight()
        {
            this.WriteLine("RotateAttachmentRight");
            Assert.IsNotNull(this.attachmentDialog);
            this.attachmentDialog.ClickRotateRight();
        }

        private void RotateAttachmentLeft()
        {
            this.WriteLine("RotateAttachmentLeft");
            Assert.IsNotNull(this.attachmentDialog);
            this.attachmentDialog.ClickRotateLeft();
        }

        private void PasteTextAttachment()
        {
            this.WriteLine("PasteTextAttachment");
            Assert.IsNotNull(this.attachmentDialog);

            Clipboard.Clear();

            Clipboard.SetText(@"This is a test attachment
containing some random text
to make sure attachments work.");

            this.attachmentDialog.WaitForInputIdle(50);
            this.attachmentDialog.ClickPaste();

            // verify RichTextBox
            var box = this.attachmentDialog.ScrollViewer.FindRichText();

            Assert.IsNotNull(box);
        }

        private void CloseAttachmentDialog()
        {
            this.WriteLine("CloseAttachmentDialog");
            Assert.IsNotNull(this.attachmentDialog);
            this.attachmentDialog.Close();
            this.attachmentDialog = null;
        }

        #endregion

        #region Categories

        private void ViewCategories()
        {
            this.WriteLine("ViewCategories");
            this.window.ViewCategories();
        }

        private void EnterCategories()
        {
            // noop
        }

        private void SelectCategory()
        {
            this.WriteLine("SelectCategory");
            CategoriesWrapper categories = this.window.ViewCategories();

            List<AutomationElement> topLevelCategories = categories.Categories;
            if (topLevelCategories.Count > 0)
            {
                int i = this.random.Next(0, topLevelCategories.Count);
                categories.Select(topLevelCategories[i]);
                this.window.WaitForInputIdle(500);
                this.WriteLine("dataChangedSinceExport reset because of SelectCategory " + i);
                this.dataChangedSinceExport = true;
            }
        }

        private bool HasNoCategories
        {
            get
            {
                return !this.window.HasCategories;
            }
        }

        private bool IsCategorySelected
        {
            get
            {
                return this.window.IsCategorySelected;
            }
        }
        #endregion

        #region Payees
        private void ViewPayees()
        {
            this.WriteLine("ViewPayees");
            this.window.ViewPayees();
        }

        private bool IsPayeeSelected
        {
            get
            {
                return this.window.IsPayeeSelected;
            }
        }

        private void SelectPayee()
        {
            this.WriteLine("SelectPayee");
            PayeesWrapper payees = this.window.ViewPayees();
            if (payees.Count > 0)
            {
                int i = this.random.Next(0, payees.Count);
                payees.Select(i);
                this.window.WaitForInputIdle(500);
                this.WriteLine("dataChangedSinceExport reset because of SelectPayee " + i);
                this.dataChangedSinceExport = true;
            }
        }

        #endregion

        #region Securities

        private void ViewSecurities()
        {
            this.WriteLine("ViewSecurities");
            this.window.ViewSecurities();
        }

        private bool IsSecuritySelected
        {
            get
            {
                return this.window.IsSecuritySelected;
            }
        }

        private void SelectSecurity()
        {
            this.WriteLine("SelectSecurity");
            SecuritiesWrapper securities = this.window.ViewSecurities();
            if (securities.Count > 0)
            {
                int i = this.random.Next(0, securities.Count);
                securities.Select(i);
                this.window.WaitForInputIdle(500);
                this.WriteLine("dataChangedSinceExport reset because of SelectSecurity " + i);
                this.dataChangedSinceExport = true;
            }
        }

        #endregion

        #region Accounts 
        private AccountsWrapper accounts;

        private void ViewAccounts()
        {
            this.WriteLine("ViewAccounts");
        }

        private void EnterAccounts()
        {
            this.accounts = this.window.ViewAccounts();
        }

        private bool HasAccounts
        {
            get
            {
                return this.accounts != null ? this.accounts.HasAccounts : false;
            }
        }

        private bool IsAccountSelected
        {
            get
            {
                return this.window.IsAccountSelected;
            }
        }

        private static readonly string[] AccountTypes = new string[] { "Checking", "Credit", "Brokerage" };

        private void AddAccount()
        {
            this.WriteLine("AddAccount");
            string type = AccountTypes[this.random.Next(0, AccountTypes.Length)];
            string name = (type == "Checking") ? "My Bank" : ((type == "Credit") ? "My Credit Card" : "My Investments");
            this.accounts.AddAccount(name, type);
            this.ClearTransactionViewState();
        }

        private void DeleteAccount()
        {
            this.WriteLine("DeleteAccount");
            int index = this.random.Next(0, this.accounts.Accounts.Count);
            this.accounts.Select(index);
            string name = this.accounts.SelectedAccount;
            if (name != null && name.Contains(OnlineBankName))
            {
                this.hasOnlineAccounts = false;
            }
            this.accounts.DeleteAccount(index);
            this.ClearTransactionViewState();
        }

        private void SelectAccount()
        {
            this.WriteLine("SelectAccount");
            var i = this.random.Next(0, this.accounts.Accounts.Count);
            this.accounts.SelectAccount(i);
            this.ClearTransactionViewState();
            this.WriteLine("dataChangedSinceExport reset because of SelectAccount " + i);
            this.dataChangedSinceExport = true;
        }
        #endregion

        #region Transaction View
        private TransactionViewWrapper transactions;
        private QuickFilterWrapper quickFilter;
        private TransactionViewRow selectedTransaction;
        private TransactionDetails editedValues;

        private void TransactionView()
        {
            // group
        }

        private void FocusTransactionView()
        {
            this.WriteLine("FocusTransactionView");
            this.transactions = this.window.FindTransactionGrid();
            this.quickFilter = null;
            this.window.WaitForInputIdle(200);

            if (this.transactions == null || !this.transactions.HasTransactions)
            {
                // then this might be an empty read-only view which we cannot edit anyway, so do nothing.
                return;
            }

            this.EnsureSelectedTransaction();
        }

        private void SelectTransaction()
        {
            this.WriteLine("SelectTransaction");
            if (this.transactions == null || !this.transactions.HasTransactions)
            {
                throw new Exception("Cannot select a transaction right now");
            }

            var selection = this.transactions.Select(this.random.Next(0, this.transactions.CountNoPlaceholder));
            if (selection == null)
            {
                throw new Exception("Cannot select a transaction right now");
            }
            selection.ScrollIntoView();
            Thread.Sleep(30);
            selection.Focus();
            this.selectedTransaction = selection;
        }


        private void SortByColumn()
        {
            this.WriteLine("SortByColumn");
            // sort by a random column.
            if (this.transactions != null)
            {
                int i = this.random.Next(0, this.transactions.Columns.Count);
                TransactionViewColumn column = this.transactions.Columns.GetColumn(i);

                this.transactions.SortBy(column);
            }
        }

        private void DeleteSelectedTransaction()
        {
            this.WriteLine("DeleteSelectedTransaction");
            if (this.transactions == null || !this.transactions.HasTransactions)
            {
                throw new Exception("Cannot delete a transaction right now");
            }

            this.transactions.Delete(this.random.Next(0, this.transactions.CountNoPlaceholder));
            this.selectedTransaction = null;
        }

        private void AddNewTransaction()
        {
            this.WriteLine("AddNewTransaction");
            if (this.transactions == null)
            {
                throw new Exception("Cannot edit a transaction right now");
            }

            var selection = this.transactions.AddNew();
            this.VerifySelection(selection);
            this.WriteLine("dataChangedSinceExport reset because of AddNewTransaction");
            this.dataChangedSinceExport = true;
        }

        private void VerifySelection(TransactionViewRow selection)
        {
            selection.Select();

            // Sometimes AddNew results in an editable row, but with no selection
            // So we read the screen to figure out if this is happening here.
            for (int retries = 5; retries > 0; retries--)
            {
                var bounds = selection.Bounds;
                var color = ScreenReader.GetAverageColor(new Rect(bounds.Left, bounds.Top, 10, 10));
                var background = ScreenReader.GetAverageColor(new Rect(bounds.Right - 10, bounds.Top, 10, 10));

                // List selection background is based on 60% blend with system accent color.
                var systemAccent = System.Windows.SystemParameters.WindowGlassColor;
                var expected = ScreenReader.Blend(systemAccent, background, 0.6);

                var accentDiff = ScreenReader.ColorDistance(expected, color);
                var backgroundDiff = ScreenReader.ColorDistance(background, color);
                if (accentDiff > backgroundDiff)
                {
                    this.WriteLine("Correcting missing row selection...");
                    Input.MoveToAndLeftClick(new System.Windows.Point(bounds.Left + 2, bounds.Top + 2));
                    Thread.Sleep(50);
                    // try again to see if we fixed it...
                }
                else
                {
                    if (retries != 5)
                    {
                        this.WriteLine("Corrected missing row selection.");
                    }
                    // all good then!
                    return;
                }
            }

            throw new Exception("Cannot seem to select this row");
        }

        private void EditSelectedTransaction()
        {
            this.WriteLine("EditSelectedTransaction");
            this.AssertSelectedTransaction();
            this.editedValues = new TransactionDetails();
            this.selectedTransaction.Focus();
        }

        private void AssertSelectedTransaction()
        {
            if (this.transactions == null)
            {
                this.FocusTransactionView();
            }

            // caller is about to operate on this selection, so make sure it's up to date!            
            this.selectedTransaction = this.transactions.Selection;
            if (this.selectedTransaction == null)
            {
                throw new Exception("No selected transaction");
            }
            if (this.selectedTransaction.Bounds.IsEmpty)
            {
                this.selectedTransaction = this.selectedTransaction.Refresh();
            }
            if (this.selectedTransaction.Bounds.IsEmpty)
            {
                throw new Exception("Selected has no bounds");
            }
        }

        private void EnsureSelectedTransaction()
        {
            if (this.transactions == null)
            {
                this.FocusTransactionView();
            }

            // caller is about to operate on this selection, so make sure it's up to date!            
            this.selectedTransaction = this.transactions.Selection;
            if (this.selectedTransaction == null)
            {
                this.SelectTransaction();
                if (this.selectedTransaction == null)
                {
                    throw new Exception("Cannot find any transaction to select!");
                }
            }

        }


        private bool CanTransfer
        {
            get
            {
                AccountsWrapper accounts = this.window.ViewAccounts();
                return accounts.Accounts.Count > 1 && this.transactions != null && this.transactions.IsBankAccount;
            }
        }

        private void AddTransfer()
        {
            this.WriteLine("AddTransfer");
            AccountsWrapper accounts = this.window.ViewAccounts();
            List<string> names = accounts.Accounts;
            string sel = accounts.SelectedAccount;
            if (sel != null)
            {
                names.Remove(sel);
            }
            if (names.Count == 0 || sel == null)
            {
                throw new Exception("There is a bug in the model, it should not have attempted to add a transfer at this time since there are not enough accounts");
            }
            string transferTo = names[this.random.Next(0, names.Count)];

            this.EnsureSelectedTransaction();
            this.selectedTransaction.SetPayee("Transfer to: " + transferTo);
            this.selectedTransaction.SetCategory("");
            this.selectedTransaction.SetSalesTax(0);
            this.selectedTransaction.SetAmount(this.GetRandomDecimal(-500, 500));
            this.selectedTransaction.CommitEdit();
            // bugbug: seems to need some time to settle before NavigateTransfer.
            Thread.Sleep(50);
        }

        private bool RandomBoolean
        {
            get
            {
                return this.random.Next(0, 2) == 1;
            }
        }

        private void EditDate()
        {
            this.WriteLine("EditDate");
            this.AssertSelectedTransaction();
            Assert.IsNotNull(this.editedValues);
            this.editedValues.Date = DateTime.Now.ToShortDateString();
            this.selectedTransaction.SetDate(this.editedValues.Date);
        }

        private void EditPayee()
        {
            this.WriteLine("EditPayee");
            this.AssertSelectedTransaction();
            Assert.IsNotNull(this.editedValues);
            this.editedValues.Payee = this.GetRandomPayee();
            this.selectedTransaction.SetPayee(this.editedValues.Payee);
        }

        private static string[] SamplePayees = new string[] {
            "Costco", "Safeway", "Bank of America", "ARCO", "McDonalds", "Starbucks", "Comcast", "State Farm Insurance", "Home Depot", "Amazon"
        };

        private string GetRandomPayee()
        {
            int index = this.random.Next(0, SamplePayees.Length);
            return SamplePayees[index];
        }

        private void EditCategory()
        {
            this.WriteLine("EditCategory");
            this.AssertSelectedTransaction();
            Assert.IsNotNull(this.editedValues);
            string cat = this.GetRandomCategory();
            this.editedValues.Category = cat;
            this.selectedTransaction.Focus();
            this.selectedTransaction.SetCategory(cat);

            // now move focus to next field to trigger the new category dialog (if necessary)
            Thread.Sleep(30);
            Input.TapKey(System.Windows.Input.Key.Tab);
            Thread.Sleep(30);

            AutomationElement child = this.window.Element.FindChildWindow("Category", 2);
            this.categoryDialogVisible = child != null;
        }

        private bool categoryDialogVisible;

        private bool NoCategoryDialog
        {
            get { return !this.categoryDialogVisible; }
        }

        private bool CategoryDialogShowing
        {
            get { return this.categoryDialogVisible; }
        }

        private void CategoryDetails()
        {
            this.WriteLine("CategoryDetails");
            AutomationElement child = this.window.Element.FindChildWindow("Category", 4);
            if (child != null)
            {
                // todo: edit more of the category properties...
                CategoryPropertiesWrapper cd = new CategoryPropertiesWrapper(child);
                cd.ClickOk();
            }
        }

        private static string[] SampleCategories = new string[] {
            "Home:Supplies", "Food:Groceries", "Home:Mortgage", "Auto:Fuel", "Food:Dinner", "Food:Treats", "Internet", "Insurance:Home", "Home:Repairs", "Education:Books"
        };

        private string GetRandomCategory()
        {
            int index = this.random.Next(0, SampleCategories.Length);
            return SampleCategories[index];
        }

        private void EditMemo()
        {
            this.WriteLine("EditMemo");
            this.AssertSelectedTransaction();
            Assert.IsNotNull(this.editedValues);
            this.editedValues.Memo = DateTime.Now.ToLongTimeString();
            this.selectedTransaction.SetMemo(this.editedValues.Memo);
        }

        private void EditDeposit()
        {
            this.WriteLine("EditDeposit");
            this.AssertSelectedTransaction();
            Assert.IsNotNull(this.editedValues);
            decimal amount = this.GetRandomDecimal(0, 10000);
            this.editedValues.Amount = amount;
            this.selectedTransaction.SetAmount(amount);
        }

        private void EditPayment()
        {
            this.WriteLine("EditPayment");
            this.AssertSelectedTransaction();
            Assert.IsNotNull(this.editedValues);
            decimal amount = -this.GetRandomDecimal(0, 10000);
            this.editedValues.Amount = amount;
            this.selectedTransaction.SetAmount(amount);
        }

        private void EditSalesTax()
        {
            this.WriteLine("EditSalesTax");
            this.AssertSelectedTransaction();
            Assert.IsNotNull(this.editedValues);
            decimal salesTax = this.GetRandomDecimal(0, 20);
            this.editedValues.SalesTax = salesTax;
            this.selectedTransaction.SetSalesTax(salesTax);
        }

        private decimal GetRandomDecimal(double min, double max)
        {
            double range = Math.Abs(max - min);
            return (decimal)((Math.Round(range * this.random.NextDouble() * 100) / 100) + min);
        }

        private void VerifyNewTransaction()
        {
            this.WriteLine("VerifyNewTransaction");
            this.AssertSelectedTransaction();

            var selection = this.selectedTransaction;
            this.transactions.CommitEdit();
            if (this.transactions.Selection == null)
            {
                // Hmmm, sometimes commit clears the selection, so try and bring it back.
                var lastRow = this.transactions.GetNewRow();
                lastRow.Select();
                this.selectedTransaction = lastRow;
            }

            // commit changes the automation object.
            Assert.IsNotNull(this.editedValues);
            this.AreEqual(this.editedValues.SalesTax, this.selectedTransaction.GetSalesTax(), "Sales tax");
            this.AreEqual(this.editedValues.Amount, this.selectedTransaction.GetAmount(), "Amount");
            this.AreEqual(this.editedValues.Payee, this.selectedTransaction.GetPayee(), "Payee");
            this.AreEqual(this.editedValues.Category, this.selectedTransaction.GetCategory(), "Category");

            // Ensure that the date is in the same format as we expect it 
            string dateInTransactionAsString = this.selectedTransaction.GetDate();
            string dateTransactionAsNormalizedString = DateTime.Parse(dateInTransactionAsString).ToShortDateString();
            string dateEditedAsNormalizedString = DateTime.Parse(this.editedValues.Date).ToShortDateString();
            this.AreEqual(dateEditedAsNormalizedString, dateTransactionAsNormalizedString, "Category");

            // When we use the InvokePattern trick to commit the edit this Memo field being the last thing
            // edited is not yet committed, so the value does not show up yet until we switch to a new transaction.
            // this.AreEqual(this.editedValues.Memo, this.selectedTransaction.GetMemo(), "Memo");
            this.editedValues = null;
        }

        private void DumpChildren(AutomationElement e, string indent)
        {
            Debug.WriteLine(indent + e.Current.ClassName + ": " + e.Current.Name);

            AutomationElement child = TreeWalker.RawViewWalker.GetFirstChild(e);
            while (child != null)
            {
                this.DumpChildren(child, indent + "  ");
                child = TreeWalker.RawViewWalker.GetNextSibling(child);
            }
        }

        private void NavigateTransfer()
        {
            this.WriteLine("NavigateTransfer");
            this.AssertSelectedTransaction();
            this.VerifySelection(this.selectedTransaction);
            this.transactions.NavigateTransfer();
            this.selectedTransaction = this.transactions.Selection;
            this.WriteLine("dataChangedSinceExport reset because of NavigateTransfer");
            this.dataChangedSinceExport = true;
        }

        private void AreEqual(string expected, string actual, string name)
        {
            if (expected != actual)
            {
                throw new Exception(string.Format("{0} does not match, expected {1}, but found '{2}'", name, expected, actual));
            }
        }

        private void AreEqual(decimal expected, decimal actual, string name)
        {
            if (expected != actual)
            {
                throw new Exception(string.Format("{0} does not match, expected {1}, but found '{2}'", name, expected, actual));
            }
        }

        private class TransactionDetails
        {
            public string Date;
            public string Payee;
            public decimal SalesTax;
            public decimal Amount;
            public string Category;
            public string Memo;
        }

        private bool IsEditable
        {
            get
            {
                this.transactions = this.window.FindTransactionGrid();
                this.quickFilter = null;
                return this.transactions != null && this.transactions.IsEditable;
            }
        }

        private bool HasTransaction
        {
            get
            {
                this.transactions = this.window.FindTransactionGrid();
                this.quickFilter = null;
                return this.transactions != null && this.transactions.HasTransactions;
            }
        }

        private bool IsEmptyReadOnly
        {
            get
            {
                return !this.IsEditable && !this.HasTransaction;
            }
        }

        private bool HasEnoughTransactions
        {
            get
            {
                return this.transactions != null && this.transactions.Count > 5;
            }
        }

        private bool IsTransferTransactionSelected
        {
            get
            {
                return this.selectedTransaction != null && this.selectedTransaction.IsTransfer;
            }
        }

        private bool HasAttachableTransaction
        {
            get
            {
                return this.selectedTransaction != null && this.transactions != null && this.transactions.HasTransactions &&
                    this.transactions.IsBankAccount && !this.selectedTransaction.IsPlaceholder;
            }
        }

        private bool HasSelectedTransaction
        {
            get
            {
                return this.selectedTransaction != null && this.transactions != null && this.transactions.HasTransactions;
            }
        }

        private void SearchTransactionView()
        {
            this.WriteLine("SearchTransactionView");
            if (this.transactions != null && this.quickFilter == null)
            {
                this.quickFilter = this.window.Element.FindQuickFilter();
                Assert.IsNotNull(this.quickFilter, "Cannot find quick filter control");
            }
            if (!string.IsNullOrEmpty(this.quickFilter.GetFilter()))
            {
                this.quickFilter.ClearSearch();
            }
            else
            {
                this.quickFilter.SetFilter("the");
            }
        }

        private void ClearTransactionViewState()
        {
            this.selectedTransaction = null;
            this.transactions = null;
        }

        #endregion

        #region Reports

        private void NetWorthReport()
        {
            this.WriteLine("NetWorthReport");
            var report = this.window.NetWorthReport();
            var found = report.FindText("Net Worth");
            Assert.AreEqual("Net Worth Statement", found);
            this.ClearTransactionViewState();
        }

        private void TaxReport()
        {
            this.WriteLine("TaxReport");
            var report = this.window.TaxReport();
            var found = report.FindText("Tax");
            Assert.IsTrue(found.Contains("Tax Report"), "Tax Report heading not found");
            this.ClearTransactionViewState();
        }

        private void PortfolioReport()
        {
            this.WriteLine("PortfolioReport");
            var report = this.window.PortfolioReport();
            var found = report.FindText("Portfolio");
            Assert.AreEqual("Investment Portfolio Summary", found);
            this.ClearTransactionViewState();
        }

        private void ChangeReportDate()
        {
            this.WriteLine("ChangeReportDate");
            // can only happen right after one of the above reports is run, so this.report is set.
            var report = this.window.GetReport();
            Assert.IsNotNull(report);

            var generated = report.FindText("Generated");
            var date = report.GetDate();

            date = date.AddYears(-1);
            report.SetDate(date);

            bool updated = false;
            for (int i = 0; i < 10; i++)
            {
                var generated2 = report.FindText("Generated");
                if (generated2.Contains(date.Year.ToString()))
                {
                    updated = true;
                    break;
                }
                Thread.Sleep(500);
            }

            Assert.IsTrue(updated, "Report was not updated for new date: " + date.ToShortDateString());
        }
        #endregion

        #region Export
        private void Export()
        {
            this.FocusTransactionView();
        }

        private bool CanExportTransactions
        {
            get
            {
                if (this.dataChangedSinceExport && this.transactions != null)
                {
                    this.WriteLine("CanExportTransactions is true");
                    return true;
                }
                this.WriteLine("CanExportTransactions is false");
                return false;
            }
        }

        private void ExportCsv()
        {
            this.WriteLine("ExportCsv");
            if (this.transactions != null)
            {

                var name = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test.csv");
                this.DeleteFileWithRetries(name, 10);
                this.transactions.Export(name);

                AutomationElement child = this.window.Element.FindChildWindow("Save As", 10);
                if (child != null)
                {
                    SaveAsDialogWrapper sd = new SaveAsDialogWrapper(child);
                    sd.SetFileName(name);
                    sd.ClickSave();
                }

                // first try with the CSV extension and without
                var possibleWindowNames = new List<string> { "test.csv - Excel", "test - Excel" };
                var excel = ExcelWindowWrapper.FindExcelWindow(
                    possibleWindowNames.ToArray(),
                    5,
                    true);
                excel.Close();

                // don't do this again until data changes.
                this.WriteLine("dataChangedSinceExport false because of export");
                this.dataChangedSinceExport = false;
            }
        }

        #endregion

        #region Helpers

        public bool HandleException(Exception e)
        {
            // See if it is our good friend the stock quote guy which happens when
            // internet is unavailable, or stock quote service is down.
            if (this.window != null)
            {
                AutomationElement msgbox = this.window.Element.FindChildWindow("Error Fetching Stock Quotes", 3);
                if (msgbox != null)
                {
                    MessageBoxWrapper mbox = new MessageBoxWrapper(msgbox);
                    mbox.ClickOk();
                    return true;
                }
            }
            return false;
        }

        private bool DeleteFileWithRetries(string fileName, int retries)
        {
            if (!File.Exists(fileName))
            {
                return true;
            }
            while (retries-- > 0)
            {
                Thread.Sleep(100);
                try
                {
                    File.Delete(fileName);
                    return true; // done!
                }
                catch (Exception ex)
                {
                    this.WriteLine("### Error deleting file: " + fileName);
                    this.WriteLine("### " + ex.Message);
                }
            }
            return false;
        }

        #endregion 
    }
}