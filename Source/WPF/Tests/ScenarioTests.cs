using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Walkabout.Tests.Wrappers;
using Walkabout.Tests.Interop;
using System.Windows.Automation;
using Walkabout.Data;
using LovettSoftware.DgmlTestModeling;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Walkabout.Setup;
using Walkabout.Dialogs;
using Ofx;
using System.Xml.Linq;

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
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
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
                random = new Random(seed);
                TestContext.WriteLine("Model Seed = " + seed);
                
                model = new DgmlTestModel(this, new TestLog(TestContext), random, vsDgmlMonitorTimeout);

                string fileName = FindTestModel("TestModel.dgml");                
                model.Load(fileName);
                Thread.Sleep(2000); // let graph load.
                int delay = 0; // 1000 is handy for debugging.
                model.Run(new Predicate<DgmlTestModel>((m) => { return m.StatesExecuted > 500; }), delay);
            }
            catch
            {
                string temp = Path.GetTempPath() + "\\Screen.png";
                Win32.CaptureScreen(temp, System.Drawing.Imaging.ImageFormat.Png);
                TestContext.WriteLine("ScreenCapture: " + temp);
                throw;
            }
            finally
            {
                Terminate();
            }
        }

        string FindTestModel(string filename)
        {
            string path = new Uri(this.GetType().Assembly.Location).LocalPath;

            // walk up to TestResults.
            while (System.IO.Path.GetFileName(path) != "TestResults")
            {
                path = System.IO.Path.GetDirectoryName(path);
            }

            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "Tests", filename);
        }

        #region Model State
        bool isLoaded;
        CreateDatabaseDialogWrapper createNewDatabaseDialog;
        static string databasePath;

        AccountsWrapper accounts;
        TransactionViewWrapper transactions;
        int creationTime;
        TransactionViewItem selectedTransaction;
        TransactionDetails editedValues;

        #endregion 

        #region Start 

        const string OfxServerUrl = "http://localhost:3000/ofx/test/";

        void Start()
        {
            if (testProcess == null)
            {
                EnsureCleanState();
                string exe = typeof(Walkabout.MainWindow).Assembly.Location;

                ResignAssembly(exe);

                // start ofx test server
                string serverExe = typeof(OfxTestServer).Assembly.Location;
                serverProcess = Process.Start(new ProcessStartInfo(serverExe));
                serverProcess.WaitForInputIdle();
                ofxServerWindow = OfxServerWindowWrapper.FindMainWindow(serverProcess.Id);

                // start money
                ProcessStartInfo psi = new ProcessStartInfo(exe, "/nosettings");
                testProcess = Process.Start(psi);
                testProcess.WaitForInputIdle();
                window = MainWindowWrapper.FindMainWindow(testProcess.Id);
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
            if (window.IsBlocked)
            {
                throw new Exception("Main window is blocked by an unexpected modal dialog");
            }
            window.WaitForInputIdle(500);
            if (window.IsNotResponding)
            {
                throw new Exception("Main window is hung!");
            }
        }

        void Terminate()
        {
            Cleanup();
            model.Stop();
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
                return model.StatesExecuted > creationTime + 50;
            }
        }


        #endregion 

        #region Database

        void CreateNewDatabase()
        {
            createNewDatabaseDialog = CreateDatabaseDialogWrapper.FindCreateDatabaseDialogWindow(testProcess.Id, 1, false);
            if (createNewDatabaseDialog == null)
            {
                // send "File.New" command.
                window.New();
                createNewDatabaseDialog = CreateDatabaseDialogWrapper.FindCreateDatabaseDialogWindow(testProcess.Id, 5, true);
                if (createNewDatabaseDialog == null)
                {
                    throw new Exception("Why didn't Window.New work?");
                }
                isLoaded = false; // stop us from exiting until dialog is fulfilled.
            }
            creationTime = model.StatesExecuted;
        }

        void EnterCreateDatabase()
        {
            ClearTransactionViewState();
            window.ResetReport();
            this.hasOnlineAccounts = false;
            this.onlineAccounts = null;
            sampleData = false;
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
            return database.Load(null);
        }

        string GetFreeDatabase(string baseNamePattern)
        {
            int index = 2;
            string databasePath = System.IO.Path.GetFullPath(string.Format(baseNamePattern, ""));
            while (!DeleteFileWithRetries(databasePath, 1))
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
            string databasePath = GetFreeDatabase("TestDatabase{0}.mmdb");
            createNewDatabaseDialog.CreateSqliteDatabase(databasePath);
            isLoaded = true;
            createNewDatabaseDialog = null;
            Database = new SqliteDatabase() { DatabasePath = databasePath };
        }

        void CreateXmlDatabase()
        {
            databasePath = GetFreeDatabase("TestDatabase{0}.xml");
            DeleteFileWithRetries(databasePath, 1);
            createNewDatabaseDialog.CreateXmlDatabase(databasePath);
            isLoaded = true;
            createNewDatabaseDialog = null;
            Database = new XmlStore(databasePath, null);
        }

        void CreateBinaryXmlDatabase()
        {
            databasePath = GetFreeDatabase("TestDatabase.bxml");
            DeleteFileWithRetries(databasePath, 1);
            createNewDatabaseDialog.CreateBinaryXmlDatabase(databasePath);
            isLoaded = true;
            createNewDatabaseDialog = null;
            Database = new BinaryXmlStore(databasePath, null);
            ClearTransactionViewState();
        }

        void PopulateData()
        {
            // group
        }

        bool NoSampleData
        {
            get
            {
                return !sampleData;
            }
        }

        bool sampleData;

        void AddSampleData()
        {
            ContextMenu subMenu = window.MainMenu.OpenSubMenu("MenuHelp");
            subMenu.InvokeMenuItem("MenuSampleData");

            AutomationElement msgbox = window.FindChildWindow("Add Sample Data", 5);
            if (msgbox != null)
            {
                MessageBoxWrapper mbox = new MessageBoxWrapper(msgbox);
                mbox.ClickYes();
            }

            AutomationElement child = window.FindChildWindow("Sample Database Options", 10);
            if (child != null)
            {
                SampleDataDialogWrapper cd = new SampleDataDialogWrapper(child);
                cd.ClickOk();
            }

            Thread.Sleep(5000);
            window.WaitForInputIdle(5000);

            Save();

            sampleData = true;

            // give database time to flush...
            Thread.Sleep(2000);

            // now load the database and pull out the categories.
            MyMoney money = Load();

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

            ClearTransactionViewState();
        }

        bool IsDirty
        {
            get
            {
                return window.Title.Contains("*");
            }
        }

        void Save()
        {
            window.Save();
        }

        bool DatabaseLoaded
        {
            get
            {
                return isLoaded;
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
            onlineAccounts = window.DownloadAccounts();
        }

        internal bool NoAccountsDownloaded
        {
            get
            {
                return !hasOnlineAccounts;
            }
        }

        internal bool AccountsDownloaded
        {
            get
            {
                return hasOnlineAccounts;
            }
        }

        const string OnlineBankName = "Last Chance Bank Of Hope";

        PasswordDialogWrapper passwordDialog;

        internal void ConnectToBank()
        {
            onlineAccounts.WaitForGetBankList();

            for (int retries = 5; retries > 0; retries--)
            {
                onlineAccounts.Name = OnlineBankName;
                onlineAccounts.Institution = "bankofhope";
                onlineAccounts.FID = "1234";
                onlineAccounts.OfxAddress = OfxServerUrl;
                onlineAccounts.AppId = "QWIN";
                onlineAccounts.AppVersion = "1700";

                if (onlineAccounts.IsButtonEnabled("ButtonVerify"))
                {
                    // give connect button time to react...
                    passwordDialog = onlineAccounts.ClickConnect();

                    if (passwordDialog != null)
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
            ofxServerWindow.UserName = passwordDialog.UserName = "test";
            ofxServerWindow.Password = passwordDialog.Password = "1234";

            bool mfa = false;
            if (random.Next(0, 2) == 0)
            {
                // turn on MFA challenge
                ofxServerWindow.MFAChallengeRequired = mfa = true;
            }
            else
            {
                // turn it off by selecting something else.                
                ofxServerWindow.UseAdditionalCredentials = true;
            }

            passwordDialog.ClickOk();
            passwordDialog = null;

            // give the old password dialog time to go away!
            Thread.Sleep(1000);

            AutomationElement challenge = onlineAccounts.Element.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "PasswordDialog"), 10, 500);

            if (mfa && challenge == null)
            {
                throw new Exception("Where is the challenge dialog?");
            }

            if (challenge != null)
            {
                if (challenge.Current.Name.Contains("Multi"))
                {
                    challengeDialog = new PasswordDialogWrapper(challenge);
                }
                else if (mfa)
                {
                    throw new Exception("The challenge dialog didn't have the expected name, found: '" + challenge.Current.Name + "'");
                }
            }

        }

        internal bool IsChallenged { get { return challengeDialog != null; } }
        internal bool NoChallenge { get { return challengeDialog == null; } }

        internal void AnswerChallenge()
        {
            challengeDialog.SetUserDefinedField("MFA13", "1234");
            challengeDialog.SetUserDefinedField("123", "Newcastle");
            challengeDialog.SetUserDefinedField("MFA16", "Aston");

            challengeDialog.ClickOk();
            challengeDialog = null;
        }

        internal void AddOnlineAccounts()
        {
            for (int retries = 5; retries > 0; retries--)
            {
                foreach (var item in onlineAccounts.GetOnlineAccounts())
                {
                    if (item.HasAddButton)
                    {
                        item.ClickAdd();
                        hasOnlineAccounts = true;

                        string title = "Select Account for: " + item.Id;
                        MainWindowWrapper mainWindow = MainWindowWrapper.FindMainWindow(onlineAccounts.Element.Current.ProcessId);
                        AutomationElement child = mainWindow.FindChildWindow(title, 5);
                        if (child != null)
                        {
                            AccountPickerWrapper picker = new AccountPickerWrapper(child);
                            picker.ClickAddNewAccount();

                            AutomationElement child2 = mainWindow.FindChildWindow("Account", 5);
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
            onlineAccounts.ClickOk();
        }

        internal bool HasOnlineAccount
        {
            get { return hasOnlineAccounts; }
        }

        internal void DownloadTransactions()
        {
        }

        internal void Synchronize()
        {
            window.Synchronize();
        }

        internal void SelectDownloadTransactions()
        {
            var charts = window.GetChartsArea();
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
            var charts = window.GetChartsArea();
            charts.SelectTrends();
        }

        internal void ViewIncomes()
        {
            var charts = window.GetChartsArea();
            charts.SelectIncomes();
        }

        internal void ViewExpenses()
        {
            var charts = window.GetChartsArea();
            charts.SelectExpenses();
        }

        internal void ViewStock()
        {
            var charts = window.GetChartsArea();
            charts.SelectStock();
        }
        #endregion 

        #region Attachments

        AttachmentDialogWrapper attachmentDialog;

        void OpenAttachmentDialog()
        {
            AssertSelectedTransaction();

            attachmentDialog = this.selectedTransaction.ClickAttachmentsButton();
        }

        void PasteImageAttachment()
        {
            Assert.IsNotNull(attachmentDialog);

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

            attachmentDialog.ClickPaste();

            // verify image exists
            var image = attachmentDialog.FindImage();

            Assert.IsNotNull(image);
        }

        bool HasAttachmentImage
        {
            get
            {
                Assert.IsNotNull(attachmentDialog);
                var image = attachmentDialog.FindImage(0);
                return (image != null);
            }
        }

        void RotateAttachmentRight()
        {
            Assert.IsNotNull(attachmentDialog);
            attachmentDialog.ClickRotateRight();
        }

        void RotateAttachmentLeft()
        {
            Assert.IsNotNull(attachmentDialog);
            attachmentDialog.ClickRotateLeft();
        }

        void PasteTextAttachment()
        {
            Assert.IsNotNull(attachmentDialog);

            Clipboard.Clear();

            Clipboard.SetText(@"This is a test attachment
containing some random text
to make sure attachments work.");

            attachmentDialog.WaitForInputIdle(50);
            attachmentDialog.ClickPaste();

            // verify RichTextBox
            var box = attachmentDialog.FindRichText();

            Assert.IsNotNull(box);
        }

        void CloseAttachmentDialog()
        {
            Assert.IsNotNull(attachmentDialog);
            attachmentDialog.Close();
            attachmentDialog = null;
        }

        #endregion 

        #region Categories

        void ViewCategories()
        {
            window.ViewCategories();
        }

        void EnterCategories()
        {
            // noop
        }

        void SelectCategory()
        {
            CategoriesWrapper categories = window.ViewCategories();

            List<AutomationElement> topLevelCategories = categories.Categories;
            if (topLevelCategories.Count > 0)
            {
                int i = random.Next(0, topLevelCategories.Count);
                categories.Select(topLevelCategories[i]);
                window.ResetReport();
                window.WaitForInputIdle(500);
            }
        }

        bool HasNoCategories
        {
            get
            {
                return !window.HasCategories;
            }
        }

        bool IsCategorySelected
        {
            get
            {
                return window.IsCategorySelected;
            }
        }
        #endregion 

        #region Payees
        void ViewPayees()
        {
            window.ViewPayees();
        }

        bool IsPayeeSelected
        {
            get
            {
                return window.IsPayeeSelected;
            }
        }

        void SelectPayee()
        {
            PayeesWrapper payees = window.ViewPayees();
            if (payees.Count > 0)
            {
                int i = random.Next(0, payees.Count);
                payees.Select(i);
                window.ResetReport();
                window.WaitForInputIdle(500);
            }
        }

        #endregion 

        #region Securities

        void ViewSecurities()
        {
            window.ViewSecurities();
        }

        bool IsSecuritySelected
        {
            get
            {
                return window.IsSecuritySelected;
            }
        }

        void SelectSecurity()
        {
            SecuritiesWrapper securities = window.ViewSecurities();
            if (securities.Count > 0)
            {
                int i = random.Next(0, securities.Count);
                securities.Select(i);
                window.ResetReport();
                window.WaitForInputIdle(500);
            }
        }

        #endregion

        #region Accounts 

        void ViewAccounts()
        {
            accounts = window.ViewAccounts();
        }

        void EnterAccounts()
        {
            accounts = window.ViewAccounts();
        }

        bool HasAccounts
        {
            get
            {
                return accounts != null ? accounts.HasAccounts : false;
            }
        }

        bool IsAccountSelected
        {
            get
            {
                return window.IsAccountSelected;
            }
        }

        static string[] AccountTypes = new string[] { "Checking", "Credit", "Brokerage" };

        void AddAccount()
        {
            string type = AccountTypes[random.Next(0, AccountTypes.Length)];
            string name = (type == "Checking") ? "My Bank" : ((type == "Credit") ? "My Credit Card" : "My Investments");
            accounts.AddAccount(name, type);
            ClearTransactionViewState();
        }

        void DeleteAccount()
        {
            int index = random.Next(0, accounts.Accounts.Count);
            accounts.Select(index);
            string name = accounts.SelectedAccount;
            if (name != null && name.Contains(OnlineBankName))
            {
                hasOnlineAccounts = false;
            }
            accounts.DeleteAccount(index);
            ClearTransactionViewState();
        }

        void SelectAccount()
        {
            accounts.SelectAccount(random.Next(0, accounts.Accounts.Count));
            window.ResetReport();
            ClearTransactionViewState();
        }
        #endregion 

        #region Transaction View

        void TransactionView()
        {
            // group
        }

        void FocusTransactionView()
        {
            window.CloseReport();
            this.transactions = window.FindTransactionGrid();
            window.WaitForInputIdle(200);

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

            selectedTransaction = this.transactions.Select(random.Next(0, this.transactions.Count));
            this.editedValues = new TransactionDetails();
        }

        void SortByColumn()
        {
            // sort by a random column.
            if (this.transactions != null)
            {
                int i = random.Next(0, this.transactions.Columns.Count);
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

            this.transactions.Delete(random.Next(0, this.transactions.Count));
            selectedTransaction = null;
            this.editedValues = null;
        }

        void AddNewTransaction()
        {
            if (this.transactions == null)
            {
                throw new Exception("Cannot edit a transaction right now");
            }

            selectedTransaction = this.transactions.AddNew();
            this.editedValues = new TransactionDetails();
            selectedTransaction.Select();
        }

        void EditSelectedTransaction()
        {
            AssertSelectedTransaction();
            selectedTransaction.Focus();
        }

        private void AssertSelectedTransaction()
        {
            if (transactions == null)
            {
                FocusTransactionView();
            }

            // caller is about to operate on this selection, so make sure it's up to date!
            if (this.selectedTransaction == null)
            {
                this.selectedTransaction = transactions.Selection;
                if (this.selectedTransaction == null)
                {
                    throw new Exception("No selected transaction");
                }
                if (this.editedValues == null)
                {
                    this.editedValues = new TransactionDetails();
                }
            }

        }

        bool CanTransfer
        {
            get
            {
                AccountsWrapper accounts = window.ViewAccounts();
                return accounts.Accounts.Count > 1 && this.transactions != null && this.transactions.IsBankAccount;
            }
        }

        void AddTransfer()
        {
            AccountsWrapper accounts = window.ViewAccounts();
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
            string transferTo = names[random.Next(0, names.Count)];

            AssertSelectedTransaction();
            selectedTransaction.SetPayee("Transfer to: " + transferTo);
            selectedTransaction.SetCategory("");
            selectedTransaction.SetSalesTax(0);
            selectedTransaction.SetAmount(GetRandomDecimal(-500, 500));
        }

        bool RandomBoolean
        {
            get
            {
                return random.Next(0, 2) == 1;
            }
        }

        void EnterNewTransaction()
        {
        }

        void EditDate()
        {
            AssertSelectedTransaction();
            this.editedValues.Date = DateTime.Now.ToShortDateString();
            selectedTransaction.SetDate(this.editedValues.Date);
        }

        void EditPayee()
        {
            AssertSelectedTransaction();
            this.editedValues.Payee = GetRandomPayee();
            selectedTransaction.SetPayee(editedValues.Payee);
        }

        static string[] SamplePayees = new string[] {
            "Costco", "Safeway", "Bank of America", "ARCO", "McDonalds", "Starbucks", "Comcast", "State Farm Insurance", "Home Depot", "Amazon"
        };

        private string GetRandomPayee()
        {
            int index = random.Next(0, SamplePayees.Length);
            return SamplePayees[index];
        }

        void EditCategory()
        {
            AssertSelectedTransaction();
            string cat = GetRandomCategory();
            this.editedValues.Category = cat;
            selectedTransaction.Focus();
            selectedTransaction.SetCategory(cat);

            // now move focus to next field to trigger the new category dialog (if necessary)
            Thread.Sleep(30);
            Input.TapKey(System.Windows.Input.Key.Tab);
            Thread.Sleep(30);

            AutomationElement child = window.FindChildWindow("Category", 2);
            categoryDialogVisible = (child != null);
        }

        bool categoryDialogVisible;

        bool NoCategoryDialog
        {
            get { return !categoryDialogVisible; }
        }

        bool CategoryDialogShowing
        {
            get { return categoryDialogVisible; }
        }

        void CategoryDetails()
        {
            AutomationElement child = window.FindChildWindow("Category", 4);
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
            int index = random.Next(0, SampleCategories.Length);
            return SampleCategories[index];
        }


        void EditMemo()
        {
            AssertSelectedTransaction();
            this.editedValues.Memo = DateTime.Now.ToLongTimeString();
            selectedTransaction.SetMemo(this.editedValues.Memo);
        }

        void EditDeposit()
        {
            AssertSelectedTransaction();
            decimal amount = GetRandomDecimal(0, 10000);
            this.editedValues.Amount = amount;
            selectedTransaction.SetAmount(amount);
        }

        void EditPayment()
        {
            AssertSelectedTransaction();
            decimal amount = -GetRandomDecimal(0, 10000);
            this.editedValues.Amount = amount;
            selectedTransaction.SetAmount(amount);
        }

        void EditSalesTax()
        {
            AssertSelectedTransaction();
            decimal salesTax = GetRandomDecimal(0, 20);
            this.editedValues.SalesTax = salesTax;
            selectedTransaction.SetSalesTax(salesTax);
        }

        decimal GetRandomDecimal(double min, double max)
        {
            double range = Math.Abs(max - min);
            return (decimal)(Math.Round(range * random.NextDouble() * 100) / 100 + min);
        }

        void VerifyNewTransaction()
        {
            AssertSelectedTransaction();

            AreEqual(this.editedValues.SalesTax, selectedTransaction.GetSalesTax(), "Sales tax");
            AreEqual(this.editedValues.Amount, selectedTransaction.GetAmount(), "Amount");
            AreEqual(this.editedValues.Payee, selectedTransaction.GetPayee(), "Payee");
            AreEqual(this.editedValues.Category, selectedTransaction.GetCategory(), "Category");
            AreEqual(this.editedValues.Date, selectedTransaction.GetDate(), "Category");
            AreEqual(this.editedValues.Memo, selectedTransaction.GetMemo(), "Memo");
        }

        private void DumpChildren(AutomationElement e, string indent)
        {
            Debug.WriteLine(indent + e.Current.ClassName + ": " + e.Current.Name);

            AutomationElement child = TreeWalker.RawViewWalker.GetFirstChild(e);
            while (child != null)
            {
                DumpChildren(child, indent + "  ");
                child = TreeWalker.RawViewWalker.GetNextSibling(child);
            }
        }


        void NavigateTransfer()
        {
            AssertSelectedTransaction();
            transactions.NavigateTransfer();
            this.selectedTransaction = transactions.Selection;
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
                this.transactions = window.FindTransactionGrid();
                return this.transactions != null && this.transactions.IsEditable;
            }
        }

        bool HasTransaction
        {
            get
            {
                this.transactions = window.FindTransactionGrid();
                return this.transactions != null && this.transactions.HasTransactions;
            }
        }

        bool IsEmptyReadOnly
        {
            get
            {
                return !IsEditable && !HasTransaction;
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
            // todo: implement searching
        }

        void ClearTransactionViewState()
        {
            selectedTransaction = null;
            transactions = null;
        }

        #endregion

        #region Reports

        void NetWorthReport()
        {
            window.NetWorthReport();
            ClearTransactionViewState();
        }

        void TaxReport()
        {
            window.TaxReport();
            ClearTransactionViewState();
        }

        void PortfolioReport()
        {
            window.PortfolioReport();
            ClearTransactionViewState();
        }
        #endregion

        #region Helpers

        public bool HandleException(Exception e)
        {
            // See if it is our good friend the stock quote guy which happens when
            // internet is unavailable, or stock quote service is down.
            if (window != null)
            {
                AutomationElement msgbox = window.FindChildWindow("Error Fetching Stock Quotes", 3);
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
                    TestContext.WriteLine("### Error deleting file: " + fileName);
                    TestContext.WriteLine("### " + ex.Message);
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
                context.WriteLine(msg);
                Debug.WriteLine(msg);
            }
        }
        #endregion
    }
}
