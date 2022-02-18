using System.Windows;

namespace Walkabout.Utilities
{
    public class WpfAnnotations : DependencyObject
    {
        /// <summary>
        /// This property is used to annotate a style or template in a ResourceDictionary to note that the resource
        /// is being referenced from code.
        /// </summary>
        public static string GetCodeRef(DependencyObject obj)
        {
            return (string)obj.GetValue(CodeRefProperty);
        }

        public static void SetCodeRef(DependencyObject obj, string value)
        {
            obj.SetValue(CodeRefProperty, value);
        }

        // Using a DependencyProperty as the backing store for CodeRef.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CodeRefProperty =
            DependencyProperty.RegisterAttached("CodeRef", typeof(string), typeof(WpfAnnotations), new PropertyMetadata(null));


    }
}
