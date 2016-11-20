using System;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Data;
using Walkabout.Controls;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for AddLoginDialog.xaml
    /// </summary>
    public partial class AddLoginDialog : Window
    {
        SqlServerDatabase database;

        public AddLoginDialog()
        {
            InitializeComponent();
            UserNameBox.GotFocus += new RoutedEventHandler(OnTextBoxGotFocus);
            PasswordBox.GotFocus += new RoutedEventHandler(OnTextBoxGotFocus);
            ConfirmPasswordBox.GotFocus += new RoutedEventHandler(OnTextBoxGotFocus);
        }

        void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
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
            string user = ValidateInput(this.UserNameBox, "user name");
            if (user == null) return;
            string pswd = ValidateInput(this.PasswordBox, "password");
            if (pswd == null) return;
            string confirmed = ValidateInput(this.ConfirmPasswordBox, "password");
            if (confirmed == null) return;
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
