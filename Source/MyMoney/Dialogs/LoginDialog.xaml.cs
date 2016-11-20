using System.Windows;
using System.Windows.Input;
using Walkabout.Configuration;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for LoginDialog.xaml
    /// </summary>
    public partial class LoginDialog : Window
    {
        string password;

        public LoginDialog()
        {
            InitializeComponent();
            passwordBox.Focus();
        }

        public string Password
        {
            get { return password; }
            set { password = value; }
        }

        private void buttonOk_Click(object sender, RoutedEventArgs e)
        {
            this.errorMessage.Visibility = System.Windows.Visibility.Collapsed;
            string pswd = passwordBox.Password;
            if (pswd == null)
            {
                pswd = string.Empty;
            }
            if (password == pswd || password == pswd.Trim())
            {
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                this.errorMessage.Visibility = System.Windows.Visibility.Visible;
                this.passwordBox.SelectAll();
            }
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
            else if (e.Key == Key.Enter)
            {
                buttonOk_Click(this, e);
            }
            base.OnKeyDown(e);
        }
    }
}
