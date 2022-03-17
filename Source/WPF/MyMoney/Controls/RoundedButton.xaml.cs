using System;
using System.Windows.Media;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for CloseBox.xaml
    /// </summary>
    public partial class RoundedButton : CustomizableButton
    {
        Brush normalBackground;
        Brush normalBorder;
        Brush normalForeground;

        public RoundedButton()
        {
            InitializeComponent();
        }

        void SaveNormalColors()
        {
            if (normalBackground == null)
            {
                normalBackground = this.Background;
                normalForeground = this.Foreground;
                normalBorder = this.BorderBrush;
            }
        }

        void UpdateColors()
        {
            SaveNormalColors();

            Brush background = normalBackground;
            Brush foreground = normalForeground;
            Brush border = normalBorder;

            if (IsMouseOver)
            {
                background = this.MouseOverBackground;
                border = this.MouseOverBorder;
                foreground = this.MouseOverForeground;
            }

            if (IsPressed)
            {
                background = this.MousePressedBackground;
                border = this.MousePressedBorder;
                foreground = this.MousePressedForeground;
            }

            this.Foreground = foreground;
            this.Background = background;
            this.BorderBrush = border;
        }

        protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs args)
        {
            base.OnMouseEnter(args);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateColors();
            }));
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateColors();
            }));
        }

        protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateColors();
            }));
        }

        protected override void OnMouseUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateColors();
            }));
        }
    }

}
