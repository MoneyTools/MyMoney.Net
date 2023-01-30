using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Walkabout.Data;
using Walkabout.Utilities;

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
            this.InitializeComponent();
            this.EnableControls();
            this.TextBoxFile.Text = System.IO.Path.Combine(this.DefaultPath, Environment.UserName + Walkabout.Data.SqliteDatabase.OfficialSqliteFileExtension);
        }

        public ConnectMode Mode
        {
            get { return this.mode; }
            set
            {
                this.mode = value;
                switch (this.mode)
                {
                    case ConnectMode.Create:
                        this.Title = "Create Database";
                        this.ButtonCreate.Content = "_Create";
                        break;
                    case ConnectMode.Connect:
                        this.Title = "Connect Database";
                        this.ButtonCreate.Content = "_Connect";
                        break;
                }
            }
        }

        public string Database
        {
            get
            {
                string fileName = null;
                fileName = this.TextBoxFile.Text;
                fileName = fileName.Trim('"', '\''); // Remove any surrounding double quotes or single quotes
                return fileName;

            }
        }

        public string Password
        {
            get
            {
                return this.TextBoxPassword.Password;
            }
        }

        private void ButtonCreate_Click(object sender, RoutedEventArgs e)
        {
            this.CreateOrConnect(this.Mode);
        }


        private void ButtonOpen_Click(object sender, RoutedEventArgs e)
        {
            this.CreateOrConnect(ConnectMode.Connect);
            if (this.DialogResult == true)
            {
                this.Mode = ConnectMode.Connect;
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
                        if (!this.CheckPathExists())
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
                        break;
                    case ConnectMode.Connect:
                        if (File.Exists(this.Database) == false)
                        {
                            MessageBoxEx.Show("The file doesn't exist.  In order to open a database you must specify a SQL Lite database file that exists", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                this.ButtonOpen.Visibility = Visibility.Visible;
                this.ButtonOpen.BeginAnimation(Button.OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(500))));
            }
            else
            {
                this.ButtonOpen.Visibility = Visibility.Hidden;
            }
        }

        private void EnableControls()
        {
            bool okEnabled = false;
            if (this.TextBoxFile == null)
            {
                return; // InitializeComponent isn't finished yet.
            }
            this.ShowStatus("");
            bool passwordEnabled = false;
            if (string.IsNullOrEmpty(this.TextBoxFile.Text) == false)
            {
                okEnabled = true;
                string ext = System.IO.Path.GetExtension(this.TextBoxFile.Text);
                switch (ext.ToLowerInvariant())
                {
                    case ".bxml":
                        passwordEnabled = true;
                        break;
                    case ".mmdb":
                        this.ShowStatus("Password is no longer available on .mmdb SQL lite databases.  If you had a password you need to use an older version of MyMoney to remove that password before opening the database in this version of the app");
                        break;
                }
            }

            var visibility = passwordEnabled ? Visibility.Visible : Visibility.Hidden;
            this.PromptPassword.Visibility = this.TextBoxPassword.Visibility = visibility;

            if (this.Mode == ConnectMode.Create)
            {
                this.ShowConnectButton(File.Exists(this.TextBoxFile.Text));
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

            return true;
        }

        private void ShowStatus(string message)
        {
            this.Status.Text = message;
        }

        #region SQL Lite

        private void TextBoxSqliteBackup_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.EnableControls();
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

        private void ButtonFileNameBrowse_Click(object sender, RoutedEventArgs e)
        {
            List<string> fileTypes = new List<string>();
            var filter = StringHelpers.CreateFileFilter(Properties.Resources.MoneySQLLiteFileFilter,
                Properties.Resources.XmlFileFilter, Properties.Resources.BinaryXmlFileFilter,
                Properties.Resources.AllFileFilter);

            OpenFileDialog fdlg = this.InitializeOpenFileDialog("MyMoney database file", filter);
            fdlg.FilterIndex = 0;
            fdlg.CheckFileExists = true;
            if (fdlg.ShowDialog(this) == true)
            {
                string path = fdlg.FileName;
                if (this.VerifyFileName(path))
                {
                    this.TextBoxFile.Text = fdlg.FileName;
                    this.EnableControls();
                }
            }
        }

        private void TextBoxFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.EnableControls();

            try
            {
                this.TextBoxPassword.Password = "" + DatabaseSecurity.LoadDatabasePassword(this.TextBoxFile.Text);
            }
            catch { }
        }

        #endregion 

    }
}
