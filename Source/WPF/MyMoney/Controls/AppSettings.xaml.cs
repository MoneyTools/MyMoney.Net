using System.Windows;
using System;
using System.IO;
using Walkabout.Configuration;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Input;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class AppSettings : UserControl
    {
        private Settings settings;
        private IDictionary<string, string> themes = new SortedDictionary<string, string>() { 
            { "Light", "Light" },
            { "Dark", "Dark" }
        };

        /// <summary>
        /// Constructor
        /// </summary>
        public AppSettings()
        {
            InitializeComponent();

            settings = Settings.TheSettings;
            this.IsVisibleChanged += OnIsVisibleChanged;

            int year = DateTime.Now.Year;
            for(int i = 0; i < 12; i++)
            {
                var month = new DateTime(year, i + 1, 1);
                var label = month.ToString("MMMM");
                this.comboBoxFiscalYear.Items.Add(label);
            }

            foreach (var theme in themes.Keys)
            {
                comboBoxTheme.Items.Add(theme);
            }
            comboBoxTheme.SelectedItem = settings.Theme;

        }

        public event EventHandler Closed;

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool b)
            {
                if (b)
                {

                    this.checkBoxRentalSupport.IsChecked = settings.RentalManagement;
                    this.checkBoxPlaySounds.IsChecked = settings.PlaySounds;
                    this.comboBoxFiscalYear.SelectedIndex = settings.FiscalYearStart;

                    foreach (string theme in comboBoxTheme.Items)
                    {
                        if (themes[theme] == settings.Theme)
                        {
                            comboBoxTheme.SelectedIndex = comboBoxTheme.Items.IndexOf(theme);
                        }
                    }
                }
                else
                {
                    if (Closed != null)
                    {
                        Closed(this, EventArgs.Empty);
                    }
                }
            }
        }

        public event EventHandler PasswordChanged;

        public string Password
        {
            get { return this.editPasswordBox.Password; }
            set { this.editPasswordBox.Password = value; }
        }
            
        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordChanged != null)
            {
                PasswordChanged(this, e);
            }
        }

        private void OnFiscalYearChanged(object sender, SelectionChangedEventArgs e)
        {
            settings.FiscalYearStart = comboBoxFiscalYear.SelectedIndex;
        }

        private void OnRentalSupportChanged(object sender, RoutedEventArgs e)
        {
            settings.RentalManagement = this.checkBoxRentalSupport.IsChecked == true;
        }

        private void OnPlaySoundsChanged(object sender, RoutedEventArgs e)
        {
            settings.PlaySounds = this.checkBoxPlaySounds.IsChecked == true;
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string name = (string)e.AddedItems[0];
                if (themes.TryGetValue(name, out string theme))
                {
                    settings.Theme = theme;
                    ModernWpf.ThemeManager.Current.ApplicationTheme = theme == "Dark" ? ModernWpf.ApplicationTheme.Dark : ModernWpf.ApplicationTheme.Light;
                }
            }
        }
    }
}
