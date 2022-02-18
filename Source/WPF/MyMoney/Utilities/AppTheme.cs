
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Walkabout.Utilities
{ 
    internal class AppTheme {
        private string _name;
        private ResourceDictionary _theme;
        public event EventHandler<string> ThemeChanged;
        private Dictionary<string, SolidColorBrush> dynamicBrushes = new Dictionary<string, SolidColorBrush>();
        private static AppTheme _instance;

        public static AppTheme Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AppTheme();
                }
                return _instance;
            }
        }

        public string GetTheme() { return _name; }

        public void SetTheme(string name)
        {
            if (name != null)
            {                
                Uri themeUri = new Uri(name, UriKind.Relative);
                try
                {
                    ResourceDictionary theme = (ResourceDictionary)Application.LoadComponent(themeUri);
                    Application.Current.Resources.MergedDictionaries.Add(theme);
                    if (_theme != null)
                    {
                        // Note: must remove the old theme AFTER adding the new one because some update
                        // events trigger when removing this theme and we don't want that code to hit
                        // brush not found exceptions.
                        Application.Current.Resources.MergedDictionaries.Remove(_theme);
                    }
                    _theme = theme;
                    _name = name;
                    UpdateDynamicBrushes();
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

        private void UpdateDynamicBrushes()
        {
            foreach(var pair in this.dynamicBrushes)
            {
                var name = pair.Key;
                var brush = pair.Value;

                var newBrush = Application.Current.TryFindResource(name) as Brush;
                if (newBrush == null)
                {
                    Debug.WriteLine($"Dynamic brush '{name}' not found after theme change!");
                }
                if (newBrush is SolidColorBrush solid)
                {
                    brush.Color = solid.Color;
                }
                else
                {
                    throw new Exception("Can only theme SolidColorBrush");
                }
            }
        }

        public Brush GetThemedBrush(string name)
        {
            if (this.dynamicBrushes.ContainsKey(name))
            {
                return this.dynamicBrushes[name];
            }

            var brush = Application.Current.TryFindResource(name) as Brush;
            if (brush == null)
            {
                throw new Exception($"Resource {name} not found!");
            }

            if (brush is SolidColorBrush solid)
            {
                // make sure it's not frozen!
                SolidColorBrush clone = new SolidColorBrush() { Color = solid.Color, Opacity = solid.Opacity };
                this.dynamicBrushes[name] = clone;
                return clone;
            }
            else
            {
                throw new Exception("Can only theme SolidColorBrush");
            }
        }
    }
}