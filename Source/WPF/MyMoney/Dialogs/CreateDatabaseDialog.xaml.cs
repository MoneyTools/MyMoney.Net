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

        public string DefaultPath
        {
            get
            {
                return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyMoney");
            }
        }

        public CreateDatabaseDialog()
        {
            InitializeComponent();
            EnableControls();
            this.TextBoxSqliteDatabaseFile.Text = System.IO.Path.Combine(DefaultPath, Environment.UserName + Walkabout.Data.SqliteDatabase.OfficialSqliteFileExtension);

            HideBackupPrompt();

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
                        HideBackupPrompt();
                        break;
                    case ConnectMode.Connect:
                        this.Title = "Connect Database";
                        this.ButtonCreate.Content = "_Connect";
                        HideBackupPrompt();
                        break;
                    case ConnectMode.Restore:
                        this.Title = "Restore Database";
                        this.ButtonCreate.Content = "_Restore";
                        ShowBackupPrompt();
                        break;
                }
            }
        }

        private void ShowBackupPrompt()
        {
            TextBlockSqliteBackupPrompt.Visibility = Visibility.Visible;
            TextBoxSqliteBackup.Visibility = Visibility.Visible;
            ButtonSqliteBrowseBackup.Visibility = Visibility.Visible;
        }

        private void HideBackupPrompt()
        {
            TextBlockSqliteBackupPrompt.Visibility = Visibility.Collapsed;
            TextBoxSqliteBackup.Visibility = Visibility.Collapsed;
            ButtonSqliteBrowseBackup.Visibility = Visibility.Collapsed;
        }

        public string Database
        {
            get
            {
                string fileName = null;
                fileName = TextBoxSqliteDatabaseFile.Text;
                fileName = fileName.Trim('"', '\''); // Remove any surrounding double quotes or single quotes
                return fileName;

            }
        }

        public string Password
        {
            get
            {
                return TextBoxSqlitePassword.Password;
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

        void EnableControls()
        {
            bool okEnabled = false;
            if (this.TextBoxSqliteDatabaseFile == null)
            {
                return; // InitializeComponent isn't finished yet.
            }
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
            this.ButtonCreate.IsEnabled = okEnabled;
        }

        private bool VerifyFileName(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                if (this.mode == ConnectMode.Create)
                {
                    // then create it!
                    Directory.CreateDirectory(dir);
                }
            }

            if (!(path.EndsWith(".mymoney.db", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mmdb", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBoxEx.Show("The SQL Lite file must end with the extension '.mmdb'", "File Name Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        #region SQL Lite

        private void TextBoxSqliteBackup_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableControls();
        }

        private OpenFileDialog InitializeOpenFileDialog(string title, string filter)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Title = title;
            fd.Filter = filter;
            if (this.mode == ConnectMode.Create)
            {
                fd.CheckFileExists = false;
            }
            else
            {
                fd.CheckFileExists = true;
            }
            fd.RestoreDirectory = true;
            return fd;
        }

        public string BackupPath
        {
            get
            {
                return TextBoxSqliteBackup.Text;
            }
            set
            {
                TextBoxSqliteBackup.Text = value;
            }
        }

        private void ButtonSqliteBrowseBackup_Click(object sender, RoutedEventArgs e)
        {
            // Restore SQL CE database from a backup.
            OpenFileDialog fd = InitializeOpenFileDialog("Restore Database", Properties.Resources.MoneySQLLiteFileFilter);
            if (!string.IsNullOrEmpty(BackupPath))
            {
                fd.InitialDirectory = System.IO.Path.GetDirectoryName(BackupPath);
            }
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
            OpenFileDialog fdlg = InitializeOpenFileDialog("MyMoney SQL Lite *.mmdb file",
                StringHelpers.CreateFileFilter(Properties.Resources.MoneySQLLiteFileFilter, Properties.Resources.AllFileFilter));
            fdlg.FilterIndex = 1;
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

    }
}
