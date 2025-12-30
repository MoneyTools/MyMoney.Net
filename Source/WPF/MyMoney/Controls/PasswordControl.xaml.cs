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
        private bool synchronizing;

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
            if (!synchronizing)
            {
                this.synchronizing = true;
                this.PasswordTextBox.Text = this.PasswordField.Password;
                this.synchronizing = false;
            }
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
            if (!synchronizing)
            {
                synchronizing = true;
                this.PasswordField.Password = this.PasswordTextBox.Text;
                synchronizing = false;
            }
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
