using System;
using System.Windows;

namespace Walkabout.Charts
{
    /// <summary>
    /// Accessor for static styles in Styles.xaml
    /// </summary>
    internal static class StyleResources
    {
        private static readonly ResourceDictionary _resources;

        /// <summary>
        /// Construct static resource dictionary.
        /// </summary>
        static StyleResources()
        {
            try
            {
                Uri uri = new Uri("pack://application:,,,/MyMoney;component/Charts/styles.xaml");
                _resources = new ResourceDictionary();
                _resources.Source = uri;
            }
            catch
            {
                // not a WPF app, so we can't load the built in resources
                _resources = null;
            }
        }

        /// <summary>
        /// Get static XAML style
        /// </summary>
        /// <param name="name">Name of resource to fetch</param>
        /// <returns>The requested object or null if it was not found</returns>
        internal static object GetResource(string name)
        {
            return _resources[name];
        }


    }
}
