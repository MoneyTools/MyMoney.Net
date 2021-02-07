using System;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;


namespace XMoney
{
    public class SettingsViewModel : BaseViewModel
    {
        public SettingsViewModel()
        {
            Title = "Settings";

            OpenWebCommand = new Command(() => Launcher.OpenAsync(new Uri("http://vteam.com/platform")));
        }

        public int SliderValue
        {
            get => Settings.Get().RefreshRate;
            set
            {
                Settings.Get().RefreshRate = value;
                OnPropertyChanged("SliderValue");
            }
        }

        public ICommand OpenWebCommand { get; }
    }
}