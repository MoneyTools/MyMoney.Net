using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Walkabout.Utilities;

namespace Walkabout.Help
{
    internal class HelpService : DependencyObject
    {
        static string WebPath = @"https://github.com/clovett/MyMoney.Net/wiki/";

        static List<WeakReference> dependencyObjects = new List<WeakReference>();

        internal static void Initialize()
        {            
        }

        public static void OpenHelpPage(string name)
        {
            InternetExplorer.OpenUrl(IntPtr.Zero, WebPath + name);
        }

        public static string GetHelpKeyword(DependencyObject obj)
        {
            return (string)obj.GetValue(HelpKeywordProperty);
        }

        public static void SetHelpKeyword(DependencyObject obj, string value)
        {            
            obj.SetValue(HelpKeywordProperty, value);
        }

        // Using a DependencyProperty as the backing store for HelpKeyword.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HelpKeywordProperty =
            DependencyProperty.RegisterAttached("HelpKeyword", typeof(string), typeof(HelpService), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnHelpKeywordChanged)));

        private static void OnHelpKeywordChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DependencyObject listener = sender as DependencyObject;
            if (listener == null)
            {
                throw new Exception("HelpKeyword must be defined on a DependencyObject");
            }

            lock (dependencyObjects)
            {
                List<WeakReference> dead = new List<WeakReference>();
                foreach (WeakReference w in dependencyObjects) 
                {
                    if (w.IsAlive)
                    {
                        try
                        {
                            object target = w.Target;
                            if (target == sender)
                            {
                                return;
                            }
                        }
                        catch
                        {
                            // must have died in between the IsAlive check and the .Target accessor.
                            dead.Add(w);
                        }
                    }
                    else
                    {
                        dead.Add(w);
                    }
                }
                foreach (WeakReference w in dead)
                {
                    dependencyObjects.Remove(w);
                }

                // must listen to key down on a new object (and NOT tie it to this static HelpService,
                // otherwise the event handler would keep every target object alive).
                HelpKeyEventRouter router = new HelpKeyEventRouter(listener);

                // remember that we are listening to this object so we don't add another event handler
                // every time the help keyword changes.
                var r = new WeakReference(listener);                
                dependencyObjects.Add(r);
            }
        }

        /// <summary>
        /// This class listens to the F1 help key in a way that does not keep the target object alive.
        /// </summary>
        class HelpKeyEventRouter 
        {
            public HelpKeyEventRouter(DependencyObject listener)
            {
                Keyboard.AddKeyDownHandler(listener, OnKeyDown);
            }

            private void OnKeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.F1)
                {
                    DependencyObject w = (DependencyObject)sender;
                    string keyword = GetHelpKeyword(w);
                    if (string.IsNullOrEmpty(keyword))
                    {
                        keyword = "Home";
                    }
                    OpenHelpPage(keyword);
                    e.Handled = true;
                }
            }
        }
    }
}
