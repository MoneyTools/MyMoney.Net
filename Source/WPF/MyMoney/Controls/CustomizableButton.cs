using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Walkabout.Controls
{
    /// <summary>
    /// This class provides some additional properties on Button that is handy for themeing custom button ControlTemplates.
    /// </summary>
    public class CustomizableButton : Button
    {

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)this.GetValue(CornerRadiusProperty); }
            set { this.SetValue(CornerRadiusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CornerRadius.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(CustomizableButton), new PropertyMetadata(new CornerRadius(0)));

        public Brush MouseOverBackground
        {
            get { return (Brush)this.GetValue(MouseOverBackgroundProperty); }
            set { this.SetValue(MouseOverBackgroundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MouseOverBackground.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MouseOverBackgroundProperty =
            DependencyProperty.Register("MouseOverBackground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(null));


        public Brush MouseOverForeground
        {
            get { return (Brush)this.GetValue(MouseOverForegroundProperty); }
            set { this.SetValue(MouseOverForegroundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MouseOverForeground.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MouseOverForegroundProperty =
            DependencyProperty.Register("MouseOverForeground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(null));


        public Brush MouseOverBorder
        {
            get { return (Brush)this.GetValue(MouseOverBorderProperty); }
            set { this.SetValue(MouseOverBorderProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MouseOverBorder.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MouseOverBorderProperty =
            DependencyProperty.Register("MouseOverBorder", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(null));



        public Brush MousePressedBackground
        {
            get { return (Brush)this.GetValue(MousePressedBackgroundProperty); }
            set { this.SetValue(MousePressedBackgroundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MousePressedBackground.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MousePressedBackgroundProperty =
            DependencyProperty.Register("MousePressedBackground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(null));


        public Brush MousePressedBorder
        {
            get { return (Brush)this.GetValue(MousePressedBorderProperty); }
            set { this.SetValue(MousePressedBorderProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MousePressedBorder.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MousePressedBorderProperty =
            DependencyProperty.Register("MousePressedBorder", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(null));


        public Brush MousePressedForeground
        {
            get { return (Brush)this.GetValue(MousePressedForegroundProperty); }
            set { this.SetValue(MousePressedForegroundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MousePressedForeground.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MousePressedForegroundProperty =
            DependencyProperty.Register("MousePressedForeground", typeof(Brush), typeof(CustomizableButton), new PropertyMetadata(null));

    }
}
