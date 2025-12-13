using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for PasswordControl.xaml
    /// </summary>
    public partial class PasswordControl : UserControl
    {
        public PasswordControl()
        {
            this.InitializeComponent();
        }

        public event RoutedEventHandler PasswordChanged;

        public string Password
        {
            get { return this.PasswordField.Password; }
            set { this.PasswordField.Password = value; }
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            this.PasswordTextBox.Text = this.PasswordField.Password;
            if (PasswordChanged != null)
            {
                PasswordChanged(sender, e);
            }
        }

        public void SelectAll()
        {
            this.PasswordField.SelectAll();
            this.PasswordField.Focus();
        }

        private void OnTogglePassword(object sender, RoutedEventArgs e)
        {
            if (this.PasswordTextBox.Visibility == System.Windows.Visibility.Visible)
            {
                this.PasswordTextBox.Visibility = System.Windows.Visibility.Collapsed;
                this.PasswordField.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                this.PasswordTextBox.Visibility = System.Windows.Visibility.Visible;
                this.PasswordField.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            this.PasswordField.Password = this.PasswordTextBox.Text;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            // Make sure we can use ValuePattern on this control for automation.
            var id = this.Name;
            if (this.PasswordTextBox.Visibility == System.Windows.Visibility.Visible)
            {
                this.PasswordTextBox.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, id);
                return new TextBoxAutomationPeer(this.PasswordTextBox);
            }
            else
            {
                this.PasswordField.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, id);
                return new PasswordBoxAutomationPeer(this.PasswordField);
            }
        }


    }

}
