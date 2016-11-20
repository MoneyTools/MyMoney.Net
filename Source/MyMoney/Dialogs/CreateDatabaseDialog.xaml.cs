using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Walkabout.Data;
using Walkabout.Controls;
using Walkabout.Utilities;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Collections.Generic;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for CreateDatabaseDialog.xaml.  THis dialog is used to create
    /// the money database and also "Restore" a database backup.
    /// </summary>
    public partial class CreateDatabaseDialog : BaseDialog
    {
        private ConnectMode mode;
        private AsyncSqlQuery query;
        private bool updating;

        const int TabIndexSqlite = 0;
        const int TabIndexSqlCe = 1;
        const int TabIndexSqlServer = 2;
        const int TabIndexXml = 3;
        const int TabIndexBinaryXml = 4;

        const int TabIndexDefault = TabIndexSqlite;

        public string DefaultPath
        {
            get
            {
                return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyMoney");
            }
        }

        public CreateDatabaseDialog()
        {
            this.query = new AsyncSqlQuery();
            this.query.Completed += new EventHandler<SqlQueryResultArgs>(OnAsyncQueryCompleted);
            InitializeComponent();
            EnableControls();
            this.TextBoxCeDatabaseFile.Text = System.IO.Path.Combine(DefaultPath, Environment.UserName + Walkabout.Data.SqlCeDatabase.OfficialSqlCeFileExtension);
            this.TextBoxSqliteDatabaseFile.Text = System.IO.Path.Combine(DefaultPath, Environment.UserName + Walkabout.Data.SqliteDatabase.OfficialSqliteFileExtension);
            this.TextBoxBinaryXmlFile.Text = System.IO.Path.Combine(DefaultPath, "MyMoney.bxml");
            this.TextBoxXmlFile.Text = System.IO.Path.Combine(DefaultPath, "MyMoney.xml");
            
            HideBackupPrompt();

            // Toggle the default DataBase flavor, this will update the UI and disable the appropriat controls
            this.RadioSqlAuthentication.IsChecked = false;
            this.RadioWindowsAuthentication.IsChecked = true;

            CheckSqlCEInstalled();
            CheckSqlExpressInstalled();

            ComboBoxSqlDataBaseName.Text = "MyMoney";            
            TextBoxSqlDatabasePath.Text = DefaultPath;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            RegisterEvents();
        }

        void RegisterEvents()
        {
            ComboBoxSqlDataBaseName.ApplyTemplate();
            TextBox box = (TextBox)ComboBoxSqlDataBaseName.Template.FindName("PART_EditableTextBox", ComboBoxSqlDataBaseName);
            if (box != null)
            {
                box.TextChanged += new TextChangedEventHandler(ComboBoxSqlDataBaseNameTextChanged);
            }
        }

        void ComboBoxSqlDataBaseNameTextChanged(object sender, TextChangedEventArgs e)
        {
            CheckExistingPath();
        }

        bool CheckSqlCEInstalled()
        {
            if (Walkabout.Data.SqlCeDatabase.IsSqlCEInstalled)
            {
                CeNotInstalled.Visibility = System.Windows.Visibility.Collapsed;
                SqlCeOptions.Visibility = System.Windows.Visibility.Visible;                
            }
            else
            {
                CeNotInstalled.Visibility = System.Windows.Visibility.Visible;
                SqlCeOptions.Visibility = System.Windows.Visibility.Collapsed;
            }
            return Walkabout.Data.SqlCeDatabase.IsSqlCEInstalled;
        }

        bool IsSqlServerInstalled
        {
            get
            {
                return Walkabout.Data.SqlServerDatabase.IsSqlExpressInstalled ||
                    Walkabout.Data.SqlServerDatabase.IsSqlLocalDbInstalled;
            }
        }

        bool CheckSqlExpressInstalled()
        {
            bool sqlExpress = Walkabout.Data.SqlServerDatabase.IsSqlExpressInstalled;
            bool localDb = Walkabout.Data.SqlServerDatabase.IsSqlLocalDbInstalled;

            if (sqlExpress || localDb)
            {
                TextBoxSqlServer.Text = sqlExpress ? ".\\SQLEXPRESS" : "(LocalDB)\\v11.0";
                SqlServerNotInstalled.Visibility = System.Windows.Visibility.Collapsed;
                SqlServerOptions.Visibility = System.Windows.Visibility.Visible;                
            }
            else 
            {
                SqlServerNotInstalled.Visibility = System.Windows.Visibility.Visible;
                SqlServerOptions.Visibility = System.Windows.Visibility.Collapsed;                
            }
            return sqlExpress || localDb;                           
        }

        void TabControlSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EnableControls();
            if (UseSqlServer)
            {
                UpdateDatabaseDropDown();
            }
        }

        public ConnectMode Mode
        {
            get { return this.mode; }
            set
            {
                this.mode = value;
                switch (mode)
                {
                    case ConnectMode.Create:
                        this.Title = "Create Database";
                        this.ButtonCreate.Content = "_Create";
                        this.LabelSqlCEPrompt.Text = "Please provide the full path of the SQL CE database file that you would like to create to store your money records:";
                        this.LabelSqlPrompt.Text = "Please provide the SQL Server you would like to use and the name of the database that you would like to create to store your money records:";
                        this.LabelXmlPrompt.Text = "Please provide the full path of the XML file that you would like to create to store your money records:";
                        this.LabelBinaryXmlPrompt.Text = "Please provide the full path of the Binary XML file that you would like to create to store your money records:";
                        HideBackupPrompt();
                        break;
                    case ConnectMode.Connect:
                        this.Title = "Connect Database";
                        this.ButtonCreate.Content = "_Connect";
                        this.LabelSqlCEPrompt.Text = "Please specify the name of the SQL CE database file that you would like to use:";
                        this.LabelSqlPrompt.Text = "Please provide the SQL Server you would like to use and the name of the database that you would like to connect to:";
                        this.LabelXmlPrompt.Text = "Please provide the full path of the XML file that you would like to use:";
                        this.LabelBinaryXmlPrompt.Text = "Please provide the full path of the Binary XML file that you would like to create to use:";
                        HideBackupPrompt();
                        break;
                    case ConnectMode.Restore:
                        this.Title = "Restore Database";
                        this.ButtonCreate.Content = "_Restore";
                        this.LabelSqlCEPrompt.Text = "Please specify the name of the SQL CE database file that you would like to restore from and the new SQL CE database file that you would like to create from that backup:";
                        this.LabelSqlPrompt.Text = "Please provide the backup file you would like to restore from and the SQL Server you would like to use and the name of the database that you would like to restore your backup into:";
                        this.LabelXmlPrompt.Text = "Please provide the full path of the XML file that you would like to restore from and the new XML file that you would like to create from that backup:";
                        this.LabelBinaryXmlPrompt.Text = "Please provide the full path of the Binary XML file that you would like to restore from and the new binary XML file that you would like to create from that backup:";
                        ShowBackupPrompt();
                        break;
                }
            }
        }

        private void ShowBackupPrompt()
        {
            this.TextBlockCeBackupPrompt.Visibility = TextBoxSqlCeBackup.Visibility = ButtonCeBrowseBackup.Visibility = Visibility.Visible;
            this.TextBoxSqlBackup.Visibility = TextBlockBackupPrompt.Visibility = ButtonBrowseBackup.Visibility = Visibility.Visible;
            this.TextBoxXmlBackup.Visibility = TextBlockXmlBackupPrompt.Visibility = ButtonXmlBrowse.Visibility = Visibility.Visible;
            this.TextBoxBinaryXmlBackup.Visibility = TextBlockBinaryXmlBackupPrompt.Visibility = ButtonBinaryXmlBrowse.Visibility = Visibility.Visible;
        }

        private void HideBackupPrompt()
        {
            this.TextBlockCeBackupPrompt.Visibility = TextBoxSqlCeBackup.Visibility = ButtonCeBrowseBackup.Visibility = Visibility.Collapsed;
            this.TextBoxSqlBackup.Visibility = TextBlockBackupPrompt.Visibility = ButtonBrowseBackup.Visibility = Visibility.Collapsed;
            this.TextBoxXmlBackup.Visibility = TextBlockXmlBackupPrompt.Visibility = ButtonXmlBrowseBackup.Visibility = Visibility.Collapsed;
            this.TextBoxBinaryXmlBackup.Visibility = TextBlockBinaryXmlBackupPrompt.Visibility = ButtonBinaryXmlBrowseBackup.Visibility = Visibility.Collapsed;
        }

        public string Server
        {
            get
            {
                if (UseSqlServer)
                {
                    return TextBoxSqlServer.Text;
                }
                return null;
            }
        }

        public string Database
        {
            get
            {
                string fileName = null;
                if (UseSqlCe)
                {
                    fileName = TextBoxCeDatabaseFile.Text;
                }
                else if (UseSqlite)
                {
                    fileName = TextBoxSqliteDatabaseFile.Text;                    
                }
                else if (UseXml)
                {
                    fileName = TextBoxXmlFile.Text;                    
                }
                else if (UseBinaryXml)
                {
                    fileName = TextBoxBinaryXmlFile.Text;
                }
                else
                {
                    fileName = TextBoxSqlDatabasePath.Text;
                    fileName = fileName.Trim('"', '\''); // Remove any surrounding double quotes or single quotes
                    string path = fileName;
                    if (File.Exists(fileName))
                    {
                        path = Path.GetDirectoryName(fileName);
                    }
                    return Path.Combine(path, this.ComboBoxSqlDataBaseName.Text + ".mdf");

                }

                fileName = fileName.Trim('"', '\''); // Remove any surrounding double quotes or single quotes
                return fileName;

            }
        }

        public string UserId
        {
            get
            {
                if (UseSqlServer)
                {
                    return TextBoxSqlServerUserName.Text;
                }
                return null;
            }
        }

        public string Password
        {
            get
            {
                if (UseSqlCe)
                {
                    return TextBoxCePassword.Password;
                }
                else if (UseSqlite)
                {
                    return TextBoxSqlitePassword.Password;
                }
                else if (UseSqlServer)
                {
                    return TextBoxSqlServerPassword.Password;
                }
                else if (UseBinaryXml)
                {
                    return TextBoxBinayXmlPassword.Password;
                }
                return null;
            }
        }

        public string BackupPath
        {
            get
            {
                if (UseSqlCe)
                {
                    return TextBoxSqlCeBackup.Text;
                }
                else if (UseSqlite)
                {
                    return TextBoxSqliteBackup.Text;
                }
                else if (UseXml)
                {
                    return TextBoxXmlBackup.Text;
                }
                else if (UseBinaryXml)
                {
                    return TextBoxBinaryXmlBackup.Text;
                }
                else
                {
                    return TextBoxSqlBackup.Text;
                }
            }
            set
            {
                if (UseSqlCe)
                {
                    TextBoxSqlCeBackup.Text = value;
                }
                else if (UseSqlite)
                {
                    TextBoxSqliteBackup.Text = value;
                }
                else if (UseXml)
                {
                    TextBoxXmlBackup.Text = value;
                }
                else if (UseBinaryXml)
                {
                    TextBoxBinaryXmlBackup.Text = value;
                }
                else
                {
                    TextBoxSqlBackup.Text = value;
                }
            }
        }

        private void ButtonCreate_Click(object sender, RoutedEventArgs e)
        {
            CreateOrConnect(Mode);
        }

        
        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            CreateOrConnect(ConnectMode.Connect);
            if (this.DialogResult == true)
            {
                Mode = ConnectMode.Connect;
            }
        }

        private bool CheckPathExists()
        {
            if (string.IsNullOrEmpty(this.Database))
            {
                return false;
            }
            string path = Path.GetDirectoryName(this.Database);
            if (!Directory.Exists(path))
            {
                if (MessageBoxEx.Show("The directory does not exist, do you want to create it ?", "New Directory", MessageBoxButton.OKCancel, MessageBoxImage.Error) != MessageBoxResult.OK)
                {
                    return false;
                }
                Directory.CreateDirectory(path);
            }
            return true;
        }

        private void CreateOrConnect(ConnectMode mode)
        {
            bool result = true;

            try
            {
                if (UseSqlCe)
                {
                    switch (mode)
                    {
                        case ConnectMode.Create:
                            if (!CheckPathExists())
                            {
                                return;
                            }
                            if (File.Exists(this.Database))
                            {
                                if (MessageBoxEx.Show("The file already exists.  Are you sure you want to overwrite it?", "Create Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                                {
                                    return;
                                }
                                File.Delete(this.Database);
                            }
                            var sql = new SqlCeDatabase()
                            {
                                DatabasePath = this.Database,
                                Password = this.Password
                            };
                            sql.Create();
                            result = true;
                            break;
                        case ConnectMode.Connect:
                            if (File.Exists(this.Database) == false)
                            {
                                MessageBoxEx.Show("The file doesn't exist.  In order to open a database you must specify a SQL CE database file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            break;
                        case ConnectMode.Restore:
                            if (File.Exists(this.Database) == false)
                            {
                                MessageBoxEx.Show("The file doesn't exist.  In order to restore the database you must specify a SQL CE backup file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            break;
                    }
                }
                else if (UseSqlite)
                {
                    switch (mode)
                    {
                        case ConnectMode.Create:
                            if (!CheckPathExists())
                            {
                                return;
                            }
                            if (File.Exists(this.Database))
                            {
                                if (MessageBoxEx.Show("The file already exists.  Are you sure you want to overwrite it?", "Create Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                                {
                                    return;
                                }
                                File.Delete(this.Database);
                            }
                            var sql = new SqliteDatabase()
                            {
                                DatabasePath = this.Database,
                                Password = this.Password
                            };
                            sql.Create();
                            result = true;
                            break;
                        case ConnectMode.Connect:
                            if (File.Exists(this.Database) == false)
                            {
                                MessageBoxEx.Show("The file doesn't exist.  In order to open a database you must specify a SQL Lite database file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            break;
                        case ConnectMode.Restore:
                            if (File.Exists(this.Database) == false)
                            {
                                MessageBoxEx.Show("The file doesn't exist.  In order to restore the database you must specify a SQL Lite backup file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            break;
                    }
                }
                else if (UseXml)
                {
                    switch (mode)
                    {
                        case ConnectMode.Create:
                            if (!CheckPathExists())
                            {
                                return;
                            }
                            if (File.Exists(this.Database))
                            {
                                if (MessageBoxEx.Show("The file already exists.  Are you sure you want to overwrite it?", "Create Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                                {
                                    return;
                                }
                            }
                            break;
                        case ConnectMode.Connect:
                            if (File.Exists(this.Database) == false)
                            {
                                MessageBoxEx.Show("The file doesn't exist.  In order to open a database you must specify an XML file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            break;
                        case ConnectMode.Restore:
                            if (File.Exists(this.Database) == false)
                            {
                                MessageBoxEx.Show("The file doesn't exist.  In order to restore the database you must specify an XML backup file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            break;
                    }
                }
                else if (UseBinaryXml)
                {
                    switch (mode)
                    {
                        case ConnectMode.Create:
                            if (!CheckPathExists())
                            {
                                return;
                            }
                            if (File.Exists(this.Database))
                            {
                                if (MessageBoxEx.Show("The file already exists.  Are you sure you want to overwrite it?", "Create Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                                {
                                    return;
                                }
                            }
                            break;
                        case ConnectMode.Connect:
                            if (File.Exists(this.Database) == false)
                            {
                                MessageBoxEx.Show("The file doesn't exist.  In order to open a database you must specify a Binary XML file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            break;
                        case ConnectMode.Restore:
                            if (File.Exists(this.Database) == false)
                            {
                                MessageBoxEx.Show("The file doesn't exist.  In order to restore the database you must specify a Binary XML backup file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            break;
                    }
                }
                else
                {
                    if (mode == ConnectMode.Create && !CheckPathExists())
                    {
                        return;
                    }
                    string server = this.TextBoxSqlServer.Text;
                    string database = this.ComboBoxSqlDataBaseName.Text;
                    SqlServerDatabase sql = new SqlServerDatabase()
                    {
                        Server = this.Server,
                        DatabasePath = this.Database,
                        UserId = this.UserId,
                        Password = this.Password,
                        BackupPath = this.BackupPath,
                        SecurityService = new SecurityService()
                    };

                    if ((mode == ConnectMode.Create || mode == ConnectMode.Restore) && sql.Exists)
                    {
                        string msg = (mode == ConnectMode.Create) ?
                            string.Format("The target database '{0}' exists, do you want to delete that database so you can create a new empty one with that name?", database) :
                            string.Format("The target database '{0}' exists, do you want to delete that database so you can restore the backup version?", database);

                        if (MessageBoxEx.Show(msg, "Database Exists", MessageBoxButton.OKCancel, MessageBoxImage.Error) == MessageBoxResult.OK)
                        {
                            sql.Delete();
                        }
                        else
                        {
                            return;
                        }
                    }

                    if (mode == ConnectMode.Connect)
                    {
                        sql.Attach();
                    }
                    else if (mode == ConnectMode.Create)
                    {
                        this.Cursor = Cursors.Wait;
                        sql.Create();
                        result = true;
                    }
                }

            }
            catch (OperationCanceledException)
            {
                result = false;
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.Message, "Error Creating Database", MessageBoxButton.OK, MessageBoxImage.Error);
                result = false;
            }

            this.Cursor = Cursors.Arrow;
            if (result)
            {
                this.DialogResult = result;
            }
        }

        private void ShowConnectButton(bool show)
        {
            if (show)
            {
                ButtonOpen.Visibility = Visibility.Visible;
                ButtonOpen.BeginAnimation(Button.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(500))));
            }
            else
            {
                ButtonOpen.Visibility = Visibility.Hidden;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            this.query.Stop();
            base.OnClosed(e);
        }

        void EnableControls()
        {
            bool okEnabled = false;
            if (this.TextBoxSqlServerUserName == null)
            {
                return; // InitializeComponent isn't finished yet.
            }
            this.TextBoxSqlServerUserName.IsEnabled = false;
            this.TextBoxSqlServerPassword.IsEnabled = false;
            if (UseSqlCe)
            {
                if (string.IsNullOrEmpty(this.TextBoxCeDatabaseFile.Text) == false)
                {
                    okEnabled = true;
                }
                if (Mode == ConnectMode.Restore && !File.Exists(TextBoxSqlCeBackup.Text))
                {
                    okEnabled = false;
                }
                if (Mode == ConnectMode.Create)
                {
                    ShowConnectButton(File.Exists(TextBoxCeDatabaseFile.Text));
                }
                if (!Walkabout.Data.SqlCeDatabase.IsSqlCEInstalled)
                {
                    okEnabled = false;
                }
            }
            else  if (UseSqlite)
            {
                if (string.IsNullOrEmpty(this.TextBoxSqliteDatabaseFile.Text) == false)
                {
                    okEnabled = true;
                }
                if (Mode == ConnectMode.Restore && !File.Exists(TextBoxSqliteBackup.Text))
                {
                    okEnabled = false;
                }
                if (Mode == ConnectMode.Create)
                {
                    ShowConnectButton(File.Exists(TextBoxSqliteDatabaseFile.Text));
                }
            }
            else if (UseXml)
            {
                if (string.IsNullOrEmpty(this.TextBoxXmlFile.Text) == false)
                {
                    okEnabled = true;
                }
                if (Mode == ConnectMode.Restore && !File.Exists(TextBoxXmlBackup.Text))
                {
                    okEnabled = false;
                }
                if (Mode == ConnectMode.Create)
                {
                    ShowConnectButton(File.Exists(TextBoxXmlFile.Text));
                }
            }
            else if (UseBinaryXml)
            {
                if (string.IsNullOrEmpty(this.TextBoxBinaryXmlFile.Text) == false)
                {
                    okEnabled = true;
                }
                if (Mode == ConnectMode.Restore && !File.Exists(TextBoxBinaryXmlBackup.Text))
                {
                    okEnabled = false;
                }
                if (Mode == ConnectMode.Create)
                {
                    ShowConnectButton(File.Exists(TextBoxBinaryXmlFile.Text));
                }
            }
            else
            {
                bool enabled = this.RadioWindowsAuthentication.IsChecked == false;
                this.LabelUserName.IsEnabled = this.LabelPassword.IsEnabled = enabled;
                this.TextBoxSqlServerUserName.IsEnabled = this.TextBoxSqlServerPassword.IsEnabled = enabled;
                okEnabled = !string.IsNullOrEmpty(this.TextBoxSqlServer.Text) &&
                                 !string.IsNullOrEmpty(this.ComboBoxSqlDataBaseName.Text) &&
                                 !string.IsNullOrEmpty(this.TextBoxSqlDatabasePath.Text);
                if (!this.RadioWindowsAuthentication.IsChecked == true)
                {
                    string user = this.TextBoxSqlServerUserName.Text;
                    if (string.IsNullOrEmpty(user))
                    {
                        okEnabled = false;
                    }
                }
                if (Mode == ConnectMode.Restore && !File.Exists(TextBoxSqlBackup.Text))
                {
                    okEnabled = false;
                }

                if (!IsSqlServerInstalled)
                {
                    okEnabled = false;
                } 
                else if (Mode == ConnectMode.Create)
                {
                    string server = this.TextBoxSqlServer.Text;
                    string database = this.ComboBoxSqlDataBaseName.Text;
                    SqlServerDatabase sql = new SqlServerDatabase()
                    {
                        Server = this.Server,
                        DatabasePath = this.Database,
                        UserId = this.UserId,
                        Password = this.Password,
                        BackupPath = this.BackupPath,
                        SecurityService = new SecurityService()
                    };

                    ShowConnectButton(sql.Exists);
                }
            }

            this.ButtonCreate.IsEnabled = okEnabled;
        }

        private bool VerifyFileName(string path)
        {
            if (UseSqlCe && !path.EndsWith(".mymoney.sdf", StringComparison.OrdinalIgnoreCase))
            {
                MessageBoxEx.Show("The SQL CE file must end with the extension '.MyMoney.sdf'", "File Name Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            
            if (UseSqlite  && !path.EndsWith(".mymoney.db", StringComparison.OrdinalIgnoreCase))
            {
                MessageBoxEx.Show("The SQL Lite file must end with the extension '.MyMoney.db'", "File Name Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            
            if (UseXml && !path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                MessageBoxEx.Show("The XML file must end with the extension '.xml'", "File Name Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            
            if (UseBinaryXml && !path.EndsWith(".bxml", StringComparison.OrdinalIgnoreCase))
            {
                MessageBoxEx.Show("The Binary XML file must end with the extension '.bxml'", "File Name Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        #region SQL Server

        private void RadioWindowsAuthentication_Checked(object sender, RoutedEventArgs e)
        {
            Reset();
            EnableControls();
        }

        private void RadioSqlAuthentication_Checked(object sender, RoutedEventArgs e)
        {
            Reset();
            EnableControls();
        }

        private void TextBoxSqlServer_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        private void TextBoxSqlServer_KeyDown(object sender, KeyEventArgs e)
        {
        }

        private void TextBoxSqlDatabasePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();
        }

        private void ButtonSqlServerBrowse_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TextBoxSqlDatabasePath.Text))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TextBoxSqlDatabasePath.Text));
                fd.SelectedPath = TextBoxSqlDatabasePath.Text;
            }
            fd.ShowNewFolderButton = true;
            if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBoxSqlDatabasePath.Text = fd.SelectedPath;
            }
        }

        private void ComboBoxSqlDataBaseName_DropDownOpened(object sender, EventArgs e)
        {
            if (!this.updating)
            {
                this.updating = true;
            }
        }

        string GetRequiredText(TextBox box, string msg)
        {
            string value = box.Text;
            if (string.IsNullOrEmpty(value))
            {
                MessageBoxEx.Show(msg, "Required Value Missing", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                box.Focus();
            }
            return value;
        }

        void UpdateDatabaseDropDown()
        {
            if (lookupExisting)
            {
                CheckExistingPath();
                return;
            }
            string server = this.TextBoxSqlServer.Text;
            if (string.IsNullOrEmpty(server))
            {
                return;
            }

            if (IsSqlServerInstalled)
            {
                string constr = Walkabout.Data.SqlServerDatabase.GetConnectionString(this.Server, null, this.UserId, this.Password);
                if (constr != null)
                {
                    lookupExisting = true;
                    this.ProgressSqlConnect.Visibility = System.Windows.Visibility.Visible;
                    this.query.BeginRunQuery(constr, "select name, filename from sysdatabases");
                }
            }
        }

        void OnAsyncQueryCompleted(object sender, SqlQueryResultArgs args)
        {
            if (args.Error != null)
            {
                ShowError(args.Error);
            }
            else
            {
                ShowNames(args.DataReader);
            }
        }

        bool ShowError(Exception ex)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                this.ProgressSqlConnect.Visibility = Visibility.Collapsed;
            }));
            this.updating = false;
            return true;
        }

        bool lookupExisting;
        SortedDictionary<string, string> existing = new SortedDictionary<string, string>();

        void CheckExistingPath()
        {
            string name = ComboBoxSqlDataBaseName.Text;
            string path = null;
            if (!string.IsNullOrEmpty(name) && existing.TryGetValue(name, out path))
            {
                TextBoxSqlDatabasePath.Text = path;
                TextBoxSqlDatabasePath.IsEnabled = false;
            }
            else
            {
                TextBoxSqlDatabasePath.Text = DefaultPath;
                TextBoxSqlDatabasePath.IsEnabled = true;
            }
        }

        bool ShowNames(IDataReader reader)
        {
            this.ProgressSqlConnect.Visibility = Visibility.Collapsed;
            existing = new SortedDictionary<string, string>();
            if (reader != null)
            {
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    if (name != "msdb" && name != "tempdb" && name != "master" && name != "model")
                    {
                        if (!reader.IsDBNull(1))
                        {
                            string path = reader.GetString(1);
                            existing[name] = path;
                        }
                    }
                }            
            }

            HashSet<string> showing = new HashSet<string>();
            foreach (string name in ComboBoxSqlDataBaseName.Items)
            {
                showing.Add(name);
            }

            foreach (string key in existing.Keys)
            {
                if (!showing.Contains(key))
                {
                    ComboBoxSqlDataBaseName.Items.Add(key);
                }
            }

            foreach (string key in showing)
            {
                if (!existing.ContainsKey(key))
                {
                    ComboBoxSqlDataBaseName.Items.Remove(key);
                }
            }

            CheckExistingPath();
            this.updating = false;
            return true;
        }

        void Reset()
        {
            this.query.Stop();
            this.updating = false;
            if (ComboBoxSqlDataBaseName != null)
            {
                this.ComboBoxSqlDataBaseName.Items.Clear();
                this.ProgressSqlConnect.Visibility = Visibility.Collapsed;
            }
        }

        private void TextBoxSqlServerUserName_TextChanged(object sender, TextChangedEventArgs e)
        {
            Reset();
            EnableControls();

            // todo: figure out SQL name, and notice we have separate user names here...
            //string name = GetDatabaseName(TextBoxCeDatabaseFile.Text);
            //TextBoxCePassword.Password = "" + DatabaseSecurity.LoadDatabasePassword(name);  
        }

        private void TextBoxSqlServerPassword_TextChanged(object sender, RoutedEventArgs e)
        {
            Reset();
            EnableControls();
        }

        private void ComboBoxSqlDataBaseName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EnableControls();
                CheckExistingPath();
            }));
        }

        private void TextBoxSqlServer_TextChanged(object sender, TextChangedEventArgs e)
        {
            Reset();
            EnableControls();
        }


        private void ButtonBrowseBackup_Click(object sender, RoutedEventArgs e)
        {
            // Restore SQL Express database from a backup.
            OpenFileDialog fd = new OpenFileDialog();
            fd.Title = "Restore Database";
            fd.Filter = Properties.Resources.SqlServerBackupFileFilter;
            if (!string.IsNullOrEmpty(BackupPath))
            {
                fd.InitialDirectory = System.IO.Path.GetDirectoryName(BackupPath);
            }
            fd.CheckFileExists = true;
            if (fd.ShowDialog(this) != true)
            {
                return;
            }
            BackupPath = fd.FileName;
        }
        
        public bool UseSqlServer
        {
            get { return this.tabControl1.SelectedIndex == TabIndexSqlServer; }
            set
            {
                if (value)
                {
                    this.tabControl1.SelectedIndex = TabIndexSqlServer;
                }
                else
                {
                    this.tabControl1.SelectedIndex = TabIndexDefault;
                }
            }
        }

        #endregion 

        #region SQL CE 
        
        public bool UseSqlCe
        {
            get { return this.tabControl1.SelectedIndex == TabIndexSqlCe; }
            set
            {
                if (value)
                {
                    this.tabControl1.SelectedIndex = TabIndexSqlCe;
                }
                else
                {
                    this.tabControl1.SelectedIndex = TabIndexDefault;
                }
            }
        }

        private void ButtonSqlCEBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fdlg = new OpenFileDialog();
            fdlg.Title = "MyMoney SQLCE *.MyMoney.SDF file";
            fdlg.Filter = StringHelpers.CreateFileFilter(Properties.Resources.MoneySQLCEFileFilter, Properties.Resources.AllFileFilter);
            fdlg.FilterIndex = 1;
            fdlg.RestoreDirectory = true;

            if (fdlg.ShowDialog(this) == true)
            {
                string path = fdlg.FileName;
                if (VerifyFileName(path))
                {
                    this.TextBoxCeDatabaseFile.Text = fdlg.FileName;
                }
            }
        }

        private void TextBoxCeDatabaseFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();

            try
            {
                TextBoxCePassword.Password = "" + DatabaseSecurity.LoadDatabasePassword(TextBoxCeDatabaseFile.Text);
            }
            catch { }
        }

        private void TextBoxSqlBackup_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();
        }

        private void TextBoxSqlCeBackup_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();
        }

        private void ButtonCeBrowseBackup_Click(object sender, RoutedEventArgs e)
        {
            // Restore SQL CE database from a backup.
            OpenFileDialog fd = new OpenFileDialog();
            fd.Title = "Restore Database";
            fd.Filter = Properties.Resources.MoneySQLCEFileFilter;
            if (!string.IsNullOrEmpty(BackupPath))
            {
                fd.InitialDirectory = System.IO.Path.GetDirectoryName(BackupPath);
            }
            fd.CheckFileExists = true;
            if (fd.ShowDialog(this) != true)
            {
                return;
            }
            if (VerifyFileName(fd.FileName))
            {
                BackupPath = fd.FileName;
            }
        }

        private void OnDownloadLink(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            InternetExplorer.OpenUrl(IntPtr.Zero, link.NavigateUri);
        }

        private void OnTryAgain(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            
            if (link.Tag != null && link.Tag.ToString() == "SqlCE")
            {
                if (!CheckSqlCEInstalled())
                {
                    MessageBoxEx.Show("SQL Compact Edition is still not installed", "SQL CE Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                if (!CheckSqlExpressInstalled())
                {
                    MessageBoxEx.Show("SQL Express is still not installed", "SQL Express Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            EnableControls();
        }

        #endregion

        #region SQL Lite

        public bool UseSqlite
        {
            get { return this.tabControl1.SelectedIndex == TabIndexSqlite; }
            set
            {
                if (value)
                {
                    this.tabControl1.SelectedIndex = TabIndexSqlite;
                }
                else
                {
                    this.tabControl1.SelectedIndex = TabIndexBinaryXml;
                }
            }
        }

        private void TextBoxSqliteBackup_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();
        }

        private void ButtonSqliteBrowseBackup_Click(object sender, RoutedEventArgs e)
        {
            // Restore SQL CE database from a backup.
            OpenFileDialog fd = new OpenFileDialog();
            fd.Title = "Restore Database";
            fd.Filter = Properties.Resources.MoneySQLLiteFileFilter;
            if (!string.IsNullOrEmpty(BackupPath))
            {
                fd.InitialDirectory = System.IO.Path.GetDirectoryName(BackupPath);
            }
            fd.CheckFileExists = true;
            if (fd.ShowDialog(this) != true)
            {
                return;
            }
            if (VerifyFileName(fd.FileName))
            {
                BackupPath = fd.FileName;
            }
        }

        private void ButtonSqliteBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fdlg = new OpenFileDialog();
            fdlg.Title = "MyMoney SQL Lite *.MyMoney.db file";
            fdlg.Filter = StringHelpers.CreateFileFilter(Properties.Resources.MoneySQLLiteFileFilter, Properties.Resources.AllFileFilter);
            fdlg.FilterIndex = 1;
            fdlg.RestoreDirectory = true;

            if (fdlg.ShowDialog(this) == true)
            {
                string path = fdlg.FileName;
                if (VerifyFileName(path))
                {
                    this.TextBoxSqliteDatabaseFile.Text = fdlg.FileName;
                }
            }
        }

        private void TextBoxSqliteDatabaseFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();

            try
            {
                TextBoxSqlitePassword.Password = "" + DatabaseSecurity.LoadDatabasePassword(TextBoxSqliteDatabaseFile.Text);
            }
            catch { }
        }

        #endregion 

        #region Xml Files

        public bool UseXml
        {
            get { return this.tabControl1.SelectedIndex == TabIndexXml; }
            set
            {
                if (value)
                {
                    this.tabControl1.SelectedIndex = TabIndexXml;
                }
                else
                {
                    this.tabControl1.SelectedIndex = TabIndexDefault;
                }
            }
        }

        private void TextBoxXmlBackup_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();
        }

        private void ButtonXmlBrowseBackup_Click(object sender, RoutedEventArgs e)
        {
            // Restore XML database from a backup.
            OpenFileDialog fd = new OpenFileDialog();
            fd.Title = "Restore XML File";
            fd.Filter = Properties.Resources.XmlFileFilter;
            if (!string.IsNullOrEmpty(BackupPath))
            {
                fd.InitialDirectory = System.IO.Path.GetDirectoryName(BackupPath);
            }
            fd.CheckFileExists = true;
            if (fd.ShowDialog(this) != true)
            {
                return;
            }
            if (VerifyFileName(fd.FileName))
            {
                BackupPath = fd.FileName;
            }
        }

        private void TextBoxXmlFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();
        }

        private void ButtonXmlBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fdlg = new OpenFileDialog();
            fdlg.Title = "XML file";
            fdlg.Filter = StringHelpers.CreateFileFilter(Properties.Resources.XmlFileFilter, Properties.Resources.AllFileFilter);
            fdlg.FilterIndex = 1;
            fdlg.RestoreDirectory = true;

            if (fdlg.ShowDialog(this) == true)
            {
                string path = fdlg.FileName;
                if (VerifyFileName(path))
                {
                    this.TextBoxXmlFile.Text = fdlg.FileName;
                }
            }
        }
        #endregion 

        #region BinaryXml

        public bool UseBinaryXml
        {
            get { return this.tabControl1.SelectedIndex == TabIndexBinaryXml; }
            set
            {
                if (value)
                {
                    this.tabControl1.SelectedIndex = TabIndexBinaryXml;
                }
                else
                {
                    this.tabControl1.SelectedIndex = TabIndexDefault;
                }
            }
        }
        private void TextBoxBinaryXmlBackup_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();
        }

        private void ButtonBinaryXmlBrowseBackup_Click(object sender, RoutedEventArgs e)
        {
            // Restore Binary XML  from a backup.
            OpenFileDialog fd = new OpenFileDialog();
            fd.Title = "Restore Binary XML File";
            fd.Filter = Properties.Resources.BinaryXmlFileFilter;
            if (!string.IsNullOrEmpty(BackupPath))
            {
                fd.InitialDirectory = System.IO.Path.GetDirectoryName(BackupPath);
            }
            fd.CheckFileExists = true;
            if (fd.ShowDialog(this) != true)
            {
                return;
            }
            if (VerifyFileName(fd.FileName))
            {
                BackupPath = fd.FileName;
            }
        }

        private void TextBoxBinaryXmlFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();

            try
            {
                TextBoxBinayXmlPassword.Password = "" + DatabaseSecurity.LoadDatabasePassword(TextBoxBinaryXmlFile.Text);
            }
            catch
            {
            }
        }


        private void ButtonBinaryXmlBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fdlg = new OpenFileDialog();
            fdlg.Title = "Binary XML file";
            fdlg.Filter = StringHelpers.CreateFileFilter(Properties.Resources.BinaryXmlFileFilter,
                Properties.Resources.AllFileFilter);
            fdlg.FilterIndex = 1;
            fdlg.RestoreDirectory = true;

            if (fdlg.ShowDialog(this) == true)
            {
                string path = fdlg.FileName;
                if (VerifyFileName(path))
                {
                    this.TextBoxBinaryXmlFile.Text = fdlg.FileName;
                }
            }
        }

        #endregion 

    
        public object DatabaseExists { get; set; }


    }
}
