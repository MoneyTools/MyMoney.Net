using System;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for AddLoginDialog.xaml
    /// </summary>
    public partial class AddLoginDialog : BaseDialog
    {
        SqlServerDatabase database;

        public AddLoginDialog()
        {
            this.InitializeComponent();
            this.UserNameBox.GotFocus += new RoutedEventHandler(this.OnTextBoxGotFocus);
            this.PasswordBox.GotFocus += new RoutedEventHandler(this.OnPasswordBoxGotFocus);
            this.ConfirmPasswordBox.GotFocus += new RoutedEventHandler(this.OnPasswordBoxGotFocus);
        }

        void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            textBox.SelectAll();
        }

        void OnPasswordBoxGotFocus(object sender, RoutedEventArgs e)
        {
            PasswordBox textBox = (PasswordBox)sender;
            textBox.SelectAll();
        }

        public SqlServerDatabase Database
        {
            get { return this.database; }
            set { this.database = value; }
        }

        public string UserName
        {
            get { return this.UserNameBox.Text; }
        }

        public string Password
        {
            get { return this.PasswordBox.Password; }
        }

        private void buttonOk_Click(object sender, RoutedEventArgs e)
        {
            string user = this.ValidateInput(this.UserNameBox, "user name");
            if (user == null)
            {
                return;
            }

            string pswd = this.ValidateInput(this.PasswordBox, "password");
            if (pswd == null)
            {
                return;
            }

            string confirmed = this.ValidateInput(this.ConfirmPasswordBox, "password");
            if (confirmed == null)
            {
                return;
            }

            if (pswd != confirmed)
            {
                MessageBoxEx.Show("The confirmation does not match the original password", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.ConfirmPasswordBox.Focus();
                this.ConfirmPasswordBox.SelectAll();
                return;
            }
            try
            {
                this.database.AddLogin(user, pswd);
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.Message, "Error", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                this.UserNameBox.Focus();
                this.UserNameBox.SelectAll();
                return;
            }
            this.DialogResult = true;
            this.Close();
        }



        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private string ValidateInput(TextBox box, string boxName)
        {
            string user = box.Text;
            if (string.IsNullOrEmpty(user))
            {
                MessageBoxEx.Show(string.Format("Please provide non-empty {0}", boxName), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                box.Focus();
                box.SelectAll();
                return null;
            }
            return box.Text;
        }

        private string ValidateInput(PasswordBox box, string boxName)
        {
            string user = box.Password;
            if (string.IsNullOrEmpty(user))
            {
                MessageBoxEx.Show(string.Format("Please provide non-empty {0}", boxName), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                box.Focus();
                box.SelectAll();
                return null;
            }
            return box.Password;
        }
    }
}
