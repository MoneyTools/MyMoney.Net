
using System;
using System.Windows;

namespace Walkabout.Utilities
{ 
    static class AppTheme {
        private static string _name;
        private static ResourceDictionary _theme;
        public static event EventHandler<string> ThemeChanged;

        public static string GetTheme() { return _name; }

        public static void SetTheme(string name)
        {
            if (name != null)
            {                
                Uri themeUri = new Uri(name, UriKind.Relative);
                try
                {
                    ResourceDictionary theme = (ResourceDictionary)Application.LoadComponent(themeUri);
                    if (_theme != null){
                        Application.Current.Resources.MergedDictionaries.Remove(_theme);
                    }
                    Application.Current.Resources.MergedDictionaries.Add(theme);
                    _theme = theme;
                    _name = name;
                    if (ThemeChanged != null)
                    {
                        ThemeChanged(null, name);
                    }
                }
                catch
                {
                    // Survive not find the theme set by the user
                }
            }
        }
    }
}