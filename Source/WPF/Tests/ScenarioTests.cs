using LovettSoftware.DgmlTestModeling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Walkabout.Data;
using Walkabout.Tests.Interop;
using Walkabout.Tests.Wrappers;

namespace Walkabout.Tests
{
    [TestClass]
    public class ScenarioTests
    {
        static Process testProcess;
        MainWindowWrapper window;
        OfxServerWindowWrapper ofxServerWindow;
        Random random;
        static Process serverProcess;
        private TestContext testContextInstance;
        private const int vsDgmlMonitorTimeout = 3000;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return this.testContextInstance;
            }
            set
            {
                this.testContextInstance = value;
            }
        }

        // Use ClassCleanup to run code after all tests in a class have run
        [ClassCleanup()]
        public static void MyClassCleanup()
        {
            Cleanup();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            Console.WriteLine("TestInitialize");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Console.WriteLine("TestCleanup");
        }

        DgmlTestModel model;

        [TestMethod]
        public void RunModel()
        {
            // This test executes a model of what and end user might want to do with this application.
            // The model is described in DGML
            try
            {
                int seed = Environment.TickCount;
                // int seed = 272222602; // Bug with ListCollectionView: 'Sorting' is not allowed during an AddNew or EditItem transaction.
                // seed = 313591431;  // Another variation of the above, sometimes even CancelEdit throws!
                this.random = new Random(seed);
                this.TestContext.WriteLine("Model Seed = " + seed);

                this.model = new DgmlTestModel(this, new TestLog(this.TestContext), this.random, vsDgmlMonitorTimeout);

                string fileName = this.FindTestModel("TestModel.dgml");
                this.model.Load(fileName);
                Thread.Sleep(2000); // let graph load.
                int delay = 0; // 1000 is handy for debugging.
                this.model.Run(new Predicate<DgmlTestModel>((m) => { return m.StatesExecuted > 500; }), delay);
            }
            catch
            {
                string temp = Path.GetTempPath() + "\\Screen.png";
                Win32.CaptureScreen(temp, System.Drawing.Imaging.ImageFormat.Png);
                this.TestContext.WriteLine("ScreenCapture: " + temp);
                throw;
            }
            finally
            {
                this.Terminate();
            }
        }

        string FindTestModel(string filename)
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

        #region Model State
        bool isLoaded;
        CreateDatabaseDialogWrapper createNewDatabaseDialog;
        static string databasePath;

        AccountsWrapper accounts;
        TransactionViewWrapper transactions;
        QuickFilterWrapper quickFilter;
        int creationTime;
        TransactionViewItem selectedTransaction;
        TransactionDetails editedValues;

        #endregion 

        #region Start 

        const string OfxServerUrl = "http://localhost:3000/ofx/test/";

        string FindFileInParent(string start, string name)
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

        void Start()
        {
            if (testProcess == null)
            {
                this.EnsureCleanState();
                string exe = typeof(Walkabout.MainWindow).Assembly.Location;

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

        string GetSqlServerName()
        {
            bool sqlExpress = Walkabout.Data.SqlServerDatabase.IsSqlExpressInstalled;
            bool localDb = Walkabout.Data.SqlServerDatabase.IsSqlLocalDbInstalled;

            if (sqlExpress)
            {
                return ".\\SQLEXPRESS";
            }
            else if (localDb)
            {
                return "(LocalDB)\\v11.0";
            }
            return null;
        }

        void EnsureCleanState()
        {
        }

        void Interactive()
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

        void Terminate()
        {
            Cleanup();
            this.model.Stop();
        }

        static void Cleanup()
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

        bool StartOver
        {
            get
            {
                return this.model.StatesExecuted > this.creationTime + 50;
            }
        }


        #endregion 

        #region Database

        void CreateNewDatabase()
        {
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

        void EnterCreateDatabase()
        {
            this.ClearTransactionViewState();
            this.window.ResetReport();
            this.hasOnlineAccounts = false;
            this.onlineAccounts = null;
            this.sampleData = false;
        }

        static IDatabase database;

        static IDatabase Database
        {
            get { return database; }
            set
            {
                if (database != null)
                {
                    try
                    {
                        database.Delete();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("### Error deleting database: " + ex.Message);
                    }
                }
                database = value;
            }
        }

        private MyMoney Load()
        {
            MyMoney result = database.Load(null);
            database.Disconnect();
            return result;
        }

        string GetFreeDatabase(string baseNamePattern)
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

        bool IsSqlCeInstalled
        {
            get
            {
                return SqlCeDatabase.IsSqlCEInstalled;
            }
        }

        bool IsSqlExpressInstalled
        {
            get
            {
                return SqlServerDatabase.IsSqlExpressInstalled || SqlServerDatabase.IsSqlLocalDbInstalled;
            }
        }

        void CreateSqliteDatabase()
        {
            string databasePath = this.GetFreeDatabase("TestDatabase{0}.mmdb");
            this.createNewDatabaseDialog.CreateSqliteDatabase(databasePath);
            this.isLoaded = true;
            this.createNewDatabaseDialog = null;
            Database = new SqliteDatabase() { DatabasePath = databasePath };
        }

        void CreateXmlDatabase()
        {
            databasePath = this.GetFreeDatabase("TestDatabase{0}.xml");
            this.DeleteFileWithRetries(databasePath, 1);
            this.createNewDatabaseDialog.CreateXmlDatabase(databasePath);
            this.isLoaded = true;
            this.createNewDatabaseDialog = null;
            Database = new XmlStore(databasePath, null);
        }

        void CreateBinaryXmlDatabase()
        {
            databasePath = this.GetFreeDatabase("TestDatabase.bxml");
            this.DeleteFileWithRetries(databasePath, 1);
            this.createNewDatabaseDialog.CreateBinaryXmlDatabase(databasePath);
            this.isLoaded = true;
            this.createNewDatabaseDialog = null;
            Database = new BinaryXmlStore(databasePath, null);
            this.ClearTransactionViewState();
        }

        void PopulateData()
        {
            // group
        }

        bool NoSampleData
        {
            get
            {
                return !this.sampleData;
            }
        }

        bool sampleData;

        void AddSampleData()
        {
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

        bool IsDirty
        {
            get
            {
                return this.window.Title.Contains("*");
            }
        }

        void Save()
        {
            this.window.Save();
        }

        bool DatabaseLoaded
        {
            get
            {
                return this.isLoaded;
            }
        }

        bool CreateDatabasePrompt
        {
            get
            {
                return CreateDatabaseDialogWrapper.FindCreateDatabaseDialogWindow(testProcess.Id, 1, false) != null;
            }
        }

        OnlineAccountsDialogWrapper onlineAccounts;
        bool hasOnlineAccounts;

        void EnterDownloadedAccounts()
        {
        }

        void DownloadAccounts()
        {
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

        const string OnlineBankName = "Last Chance Bank Of Hope";

        PasswordDialogWrapper passwordDialog;

        internal void ConnectToBank()
        {
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

        PasswordDialogWrapper challengeDialog;

        internal void SignOnToBank()
        {
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
            this.challengeDialog.SetUserDefinedField("MFA13", "1234");
            this.challengeDialog.SetUserDefinedField("123", "Newcastle");
            this.challengeDialog.SetUserDefinedField("MFA16", "HigginBothum");

            this.challengeDialog.ClickOk();
            this.challengeDialog = null;
        }

        internal void AddOnlineAccounts()
        {
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
            this.onlineAccounts.ClickOk();
        }

        internal bool HasOnlineAccount
        {
            get { return this.hasOnlineAccounts; }
        }

        internal void DownloadTransactions()
        {
        }

        internal void Synchronize()
        {
            this.window.Synchronize();
        }

        internal void SelectDownloadTransactions()
        {
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
        }

        internal void ViewTrends()
        {
            var charts = this.window.GetChartsArea();
            charts.SelectTrends();
        }

        internal void ViewIncomes()
        {
            var charts = this.window.GetChartsArea();
            charts.SelectIncomes();
        }

        internal void ViewExpenses()
        {
            var charts = this.window.GetChartsArea();
            charts.SelectExpenses();
        }

        internal void ViewStock()
        {
            if (this.IsSecuritySelected)
            {
                var charts = this.window.GetChartsArea();
                charts.SelectStock();
            }
        }
        internal void ViewHistory()
        {
            if (this.window.IsCategorySelected || this.window.IsPayeeSelected)
            {
                var charts = this.window.GetChartsArea();
                charts.SelectHistory();
            }
        }
        #endregion 

        #region Attachments

        AttachmentDialogWrapper attachmentDialog;

        void OpenAttachmentDialog()
        {
            this.AssertSelectedTransaction();
            this.attachmentDialog = this.selectedTransaction.ClickAttachmentsButton();
        }

        void PasteImageAttachment()
        {
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
                Margin = new Thickness(10)
            };

            border.Arrange(new Rect(0, 0, 300, 100));
            border.UpdateLayout();

            RenderTargetBitmap bitmap = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(border);
            Clipboard.SetImage(bitmap);

            this.attachmentDialog.ClickPaste();

            // verify image exists
            var image = this.attachmentDialog.ScrollViewer.FindImage();

            Assert.IsNotNull(image);
        }

        bool HasAttachmentImage
        {
            get
            {
                Assert.IsNotNull(this.attachmentDialog);
                var image = this.attachmentDialog.ScrollViewer.FindImage(0);
                return (image != null);
            }
        }

        void RotateAttachmentRight()
        {
            Assert.IsNotNull(this.attachmentDialog);
            this.attachmentDialog.ClickRotateRight();
        }

        void RotateAttachmentLeft()
        {
            Assert.IsNotNull(this.attachmentDialog);
            this.attachmentDialog.ClickRotateLeft();
        }

        void PasteTextAttachment()
        {
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

        void CloseAttachmentDialog()
        {
            Assert.IsNotNull(this.attachmentDialog);
            this.attachmentDialog.Close();
            this.attachmentDialog = null;
        }

        #endregion 

        #region Categories

        void ViewCategories()
        {
            this.window.ViewCategories();
        }

        void EnterCategories()
        {
            // noop
        }

        void SelectCategory()
        {
            CategoriesWrapper categories = this.window.ViewCategories();

            List<AutomationElement> topLevelCategories = categories.Categories;
            if (topLevelCategories.Count > 0)
            {
                int i = this.random.Next(0, topLevelCategories.Count);
                categories.Select(topLevelCategories[i]);
                this.window.ResetReport();
                this.window.WaitForInputIdle(500);
            }
        }

        bool HasNoCategories
        {
            get
            {
                return !this.window.HasCategories;
            }
        }

        bool IsCategorySelected
        {
            get
            {
                return this.window.IsCategorySelected;
            }
        }
        #endregion 

        #region Payees
        void ViewPayees()
        {
            this.window.ViewPayees();
        }

        bool IsPayeeSelected
        {
            get
            {
                return this.window.IsPayeeSelected;
            }
        }

        void SelectPayee()
        {
            PayeesWrapper payees = this.window.ViewPayees();
            if (payees.Count > 0)
            {
                int i = this.random.Next(0, payees.Count);
                payees.Select(i);
                this.window.ResetReport();
                this.window.WaitForInputIdle(500);
            }
        }

        #endregion 

        #region Securities

        void ViewSecurities()
        {
            this.window.ViewSecurities();
        }

        bool IsSecuritySelected
        {
            get
            {
                return this.window.IsSecuritySelected;
            }
        }

        void SelectSecurity()
        {
            SecuritiesWrapper securities = this.window.ViewSecurities();
            if (securities.Count > 0)
            {
                int i = this.random.Next(0, securities.Count);
                securities.Select(i);
                this.window.ResetReport();
                this.window.WaitForInputIdle(500);
            }
        }

        #endregion

        #region Accounts 

        void ViewAccounts()
        {
            this.accounts = this.window.ViewAccounts();
        }

        void EnterAccounts()
        {
            this.accounts = this.window.ViewAccounts();
        }

        bool HasAccounts
        {
            get
            {
                return this.accounts != null ? this.accounts.HasAccounts : false;
            }
        }

        bool IsAccountSelected
        {
            get
            {
                return this.window.IsAccountSelected;
            }
        }

        static string[] AccountTypes = new string[] { "Checking", "Credit", "Brokerage" };

        void AddAccount()
        {
            string type = AccountTypes[this.random.Next(0, AccountTypes.Length)];
            string name = (type == "Checking") ? "My Bank" : ((type == "Credit") ? "My Credit Card" : "My Investments");
            this.accounts.AddAccount(name, type);
            this.ClearTransactionViewState();
        }

        void DeleteAccount()
        {
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

        void SelectAccount()
        {
            this.accounts.SelectAccount(this.random.Next(0, this.accounts.Accounts.Count));
            this.window.ResetReport();
            this.ClearTransactionViewState();
        }
        #endregion 

        #region Transaction View

        void TransactionView()
        {
            // group
        }

        void FocusTransactionView()
        {
            this.window.CloseReport();
            this.transactions = this.window.FindTransactionGrid();
            this.quickFilter = null;
            this.window.WaitForInputIdle(200);

            var selection = this.transactions.Selection;
            if (selection == null && this.transactions.Count > 0)
            {
                this.transactions.Select(this.transactions.Count - 1);
                this.transactions.ScrollToEnd();
                selection = this.transactions.Selection;
            }
            if (selection != null)
            {
                selection.Focus();
            }
            this.selectedTransaction = selection;
            this.editedValues = new TransactionDetails();
        }

        void SelectTransaction()
        {
            if (this.transactions == null || !this.transactions.HasTransactions)
            {
                throw new Exception("Cannot select a transaction right now");
            }

            this.selectedTransaction = this.transactions.Select(this.random.Next(0, this.transactions.Count));
            this.editedValues = new TransactionDetails();
        }

        void SortByColumn()
        {
            // sort by a random column.
            if (this.transactions != null)
            {
                int i = this.random.Next(0, this.transactions.Columns.Count);
                TransactionViewColumn column = this.transactions.Columns.GetColumn(i);

                this.transactions.SortBy(column);
            }
        }

        void DeleteSelectedTransaction()
        {
            if (this.transactions == null || !this.transactions.HasTransactions)
            {
                throw new Exception("Cannot delete a transaction right now");
            }

            this.transactions.Delete(this.random.Next(0, this.transactions.Count));
            this.selectedTransaction = null;
            this.editedValues = null;
        }

        void AddNewTransaction()
        {
            if (this.transactions == null)
            {
                throw new Exception("Cannot edit a transaction right now");
            }

            this.selectedTransaction = this.transactions.AddNew();
            this.editedValues = new TransactionDetails();
            this.selectedTransaction.Select();
        }

        void EditSelectedTransaction()
        {
            this.AssertSelectedTransaction();
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
                this.SelectTransaction();
                if (this.selectedTransaction == null)
                {
                    throw new Exception("No selected transaction");
                }
            }
            if (this.editedValues == null)
            {
                this.editedValues = new TransactionDetails();
            }
        }

        bool CanTransfer
        {
            get
            {
                AccountsWrapper accounts = this.window.ViewAccounts();
                return accounts.Accounts.Count > 1 && this.transactions != null && this.transactions.IsBankAccount;
            }
        }

        void AddTransfer()
        {
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

            this.AssertSelectedTransaction();
            this.selectedTransaction.SetPayee("Transfer to: " + transferTo);
            this.selectedTransaction.SetCategory("");
            this.selectedTransaction.SetSalesTax(0);
            this.selectedTransaction.SetAmount(this.GetRandomDecimal(-500, 500));
        }

        bool RandomBoolean
        {
            get
            {
                return this.random.Next(0, 2) == 1;
            }
        }

        void EnterNewTransaction()
        {
        }

        void EditDate()
        {
            this.AssertSelectedTransaction();
            this.editedValues.Date = DateTime.Now.ToShortDateString();
            this.selectedTransaction.SetDate(this.editedValues.Date);
        }

        void EditPayee()
        {
            this.AssertSelectedTransaction();
            this.editedValues.Payee = this.GetRandomPayee();
            this.selectedTransaction.SetPayee(this.editedValues.Payee);
        }

        static string[] SamplePayees = new string[] {
            "Costco", "Safeway", "Bank of America", "ARCO", "McDonalds", "Starbucks", "Comcast", "State Farm Insurance", "Home Depot", "Amazon"
        };

        private string GetRandomPayee()
        {
            int index = this.random.Next(0, SamplePayees.Length);
            return SamplePayees[index];
        }

        void EditCategory()
        {
            this.AssertSelectedTransaction();
            string cat = this.GetRandomCategory();
            this.editedValues.Category = cat;
            this.selectedTransaction.Focus();
            this.selectedTransaction.SetCategory(cat);

            // now move focus to next field to trigger the new category dialog (if necessary)
            Thread.Sleep(30);
            Input.TapKey(System.Windows.Input.Key.Tab);
            Thread.Sleep(30);

            AutomationElement child = this.window.Element.FindChildWindow("Category", 2);
            this.categoryDialogVisible = (child != null);
        }

        bool categoryDialogVisible;

        bool NoCategoryDialog
        {
            get { return !this.categoryDialogVisible; }
        }

        bool CategoryDialogShowing
        {
            get { return this.categoryDialogVisible; }
        }

        void CategoryDetails()
        {
            AutomationElement child = this.window.Element.FindChildWindow("Category", 4);
            if (child != null)
            {
                // todo: edit more of the category properties...
                CategoryPropertiesWrapper cd = new CategoryPropertiesWrapper(child);
                cd.ClickOk();
            }
        }

        static string[] SampleCategories = new string[] {
            "Home:Supplies", "Food:Groceries", "Home:Mortgage", "Auto:Fuel", "Food:Dinner", "Food:Treats", "Internet", "Insurance:Home", "Home:Repairs", "Education:Books"
        };

        private string GetRandomCategory()
        {
            int index = this.random.Next(0, SampleCategories.Length);
            return SampleCategories[index];
        }


        void EditMemo()
        {
            this.AssertSelectedTransaction();
            this.editedValues.Memo = DateTime.Now.ToLongTimeString();
            this.selectedTransaction.SetMemo(this.editedValues.Memo);
        }

        void EditDeposit()
        {
            this.AssertSelectedTransaction();
            decimal amount = this.GetRandomDecimal(0, 10000);
            this.editedValues.Amount = amount;
            this.selectedTransaction.SetAmount(amount);
        }

        void EditPayment()
        {
            this.AssertSelectedTransaction();
            decimal amount = -this.GetRandomDecimal(0, 10000);
            this.editedValues.Amount = amount;
            this.selectedTransaction.SetAmount(amount);
        }

        void EditSalesTax()
        {
            this.AssertSelectedTransaction();
            decimal salesTax = this.GetRandomDecimal(0, 20);
            this.editedValues.SalesTax = salesTax;
            this.selectedTransaction.SetSalesTax(salesTax);
        }

        decimal GetRandomDecimal(double min, double max)
        {
            double range = Math.Abs(max - min);
            return (decimal)(Math.Round(range * this.random.NextDouble() * 100) / 100 + min);
        }

        void VerifyNewTransaction()
        {
            this.AssertSelectedTransaction();

            this.AreEqual(this.editedValues.SalesTax, this.selectedTransaction.GetSalesTax(), "Sales tax");
            this.AreEqual(this.editedValues.Amount, this.selectedTransaction.GetAmount(), "Amount");
            this.AreEqual(this.editedValues.Payee, this.selectedTransaction.GetPayee(), "Payee");
            this.AreEqual(this.editedValues.Category, this.selectedTransaction.GetCategory(), "Category");

            // Ensure that the date is in the same format as we expect it 
            string dateInTransactionAsString = this.selectedTransaction.GetDate();
            string dateTransactionAsNormalizedString = DateTime.Parse(dateInTransactionAsString).ToShortDateString();
            string dateEditedAsNormalizedString = DateTime.Parse(this.editedValues.Date).ToShortDateString();
            this.AreEqual(dateEditedAsNormalizedString, dateTransactionAsNormalizedString, "Category");

            this.AreEqual(this.editedValues.Memo, this.selectedTransaction.GetMemo(), "Memo");
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


        void NavigateTransfer()
        {
            this.AssertSelectedTransaction();
            this.transactions.NavigateTransfer();
            this.selectedTransaction = this.transactions.Selection;
        }

        void AreEqual(string expected, string actual, string name)
        {
            if (expected != actual)
            {
                throw new Exception(string.Format("{0} does not match, expected {0}, but found '{1}'", name, expected, actual));
            }
        }
        void AreEqual(decimal expected, decimal actual, string name)
        {
            if (expected != actual)
            {
                throw new Exception(string.Format("{0} does not match, expected {0}, but found '{1}'", name, expected, actual));
            }
        }

        class TransactionDetails
        {
            public string Date;
            public string Payee;
            public decimal SalesTax;
            public decimal Amount;
            public string Category;
            public string Memo;
        }

        bool IsEditable
        {
            get
            {
                this.transactions = this.window.FindTransactionGrid();
                this.quickFilter = null;
                return this.transactions != null && this.transactions.IsEditable;
            }
        }

        bool HasTransaction
        {
            get
            {
                this.transactions = this.window.FindTransactionGrid();
                this.quickFilter = null;
                return this.transactions != null && this.transactions.HasTransactions;
            }
        }

        bool IsEmptyReadOnly
        {
            get
            {
                return !this.IsEditable && !this.HasTransaction;
            }
        }

        bool HasEnoughTransactions
        {
            get
            {
                return this.transactions != null && this.transactions.Count > 5;
            }
        }

        bool IsTransferTransactionSelected
        {
            get
            {
                return (this.selectedTransaction != null && this.selectedTransaction.IsTransfer);
            }
        }

        bool HasAttachableTransaction
        {
            get
            {
                return this.selectedTransaction != null && this.transactions != null && this.transactions.HasTransactions &&
                    this.transactions.IsBankAccount;
            }
        }


        bool HasSelectedTransaction
        {
            get
            {
                return this.selectedTransaction != null && this.transactions != null && this.transactions.HasTransactions;
            }
        }

        void SearchTransactionView()
        {
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

        void ClearTransactionViewState()
        {
            this.selectedTransaction = null;
            this.transactions = null;
        }

        #endregion

        #region Reports

        void NetWorthReport()
        {
            this.window.NetWorthReport();
            this.ClearTransactionViewState();
        }

        void TaxReport()
        {
            this.window.TaxReport();
            this.ClearTransactionViewState();
        }

        void PortfolioReport()
        {
            this.window.PortfolioReport();
            this.ClearTransactionViewState();
        }
        #endregion

        #region Export
        void Export()
        {
            this.FocusTransactionView();
        }

        void ExportCsv()
        {
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

                var excel = ExcelWindowWrapper.FindExcelWindow("test.csv - Excel", 10, true);
                excel.Close();
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

        public static string GetEmbeddedResource(string name)
        {
            using (Stream s = typeof(ScenarioTests).Assembly.GetManifestResourceStream(name))
            {
                StreamReader reader = new StreamReader(s);
                return reader.ReadToEnd();
            }
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
                    this.TestContext.WriteLine("### Error deleting file: " + fileName);
                    this.TestContext.WriteLine("### " + ex.Message);
                }
            }
            return false;
        }

        class TestLog : TextWriter
        {
            TestContext context;

            public TestLog(TestContext context)
            {
                this.context = context;
            }

            public override Encoding Encoding
            {
                get { return Encoding.Unicode; }
            }

            public override void WriteLine(string msg)
            {
                this.context.WriteLine(msg);
                Debug.WriteLine(msg);
            }
        }
        #endregion
    }
}
