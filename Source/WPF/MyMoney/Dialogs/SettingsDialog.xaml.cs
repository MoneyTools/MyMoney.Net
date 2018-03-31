using System.Windows;
using System;
using System.IO;
using Walkabout.Configuration;
using Microsoft.Win32;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        Settings settings;

        /// <summary>
        /// Constructor
        /// </summary>
        public SettingsDialog()
        {
            InitializeComponent();

            this.Owner = Application.Current.MainWindow;

            settings = Settings.TheSettings;

            this.editBoxLocation.Text = settings.AttachmentDirectory;
            this.checkBoxRentalSupport.IsChecked = settings.RentalManagement;

            okButton.Click += new RoutedEventHandler(OnOkButton_Click);
        }

        public string Password
        {
            get { return this.editPasswordBox.Password; }
            set { this.editPasswordBox.Password = value; }
        }
            

        void OnOkButton_Click(object sender, RoutedEventArgs e)
        {
            string newPassword = this.editPasswordBox.Password;

            UpdateAttachmentPath();
            settings.RentalManagement = this.checkBoxRentalSupport.IsChecked == true;
            this.DialogResult = true;
            this.Close();
        }

        private void UpdateAttachmentPath()
        {
            bool exists = false;
            string path = this.editBoxLocation.Text.Trim();
            string existing = settings.AttachmentDirectory;
            bool hasValue = !string.IsNullOrEmpty(path);
            if (hasValue)
            {
                try
                {
                    if (!string.IsNullOrEmpty(existing) && Directory.Exists(existing) &&
                        string.Compare(path, existing, StringComparison.OrdinalIgnoreCase) != 0 &&
                        !Directory.Exists(path))
                    {
                        if (MessageBox.Show(this, "Would you like to move the existing directory to this new path?", "Rename Directory", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                        {
                            Directory.Move(existing, path);
                        }
                    }

                    exists = Directory.Exists(path);
                    if (!exists)
                    {
                        if (MessageBox.Show(this, "The storage location does not exist, would you like to create it?", "Create Directory", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                        {
                            Directory.CreateDirectory(path);
                        }
                    }
                    settings.AttachmentDirectory = path;
                }
                catch (Exception ex)
                {
                    exists = false;
                    MessageBox.Show(this, "Unexpected error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Browse(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog();
            string path = this.editBoxLocation.Text;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                fd.SelectedPath = path;
            }
            if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.editBoxLocation.Text = fd.SelectedPath;
            }
        }

    }
}
