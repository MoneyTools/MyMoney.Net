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
            this.InitializeComponent();
        }

        void SaveNormalColors()
        {
            if (this.normalBackground == null)
            {
                this.normalBackground = this.Background;
                this.normalForeground = this.Foreground;
                this.normalBorder = this.BorderBrush;
            }
        }

        void UpdateColors()
        {
            this.SaveNormalColors();

            Brush background = this.normalBackground;
            Brush foreground = this.normalForeground;
            Brush border = this.normalBorder;

            if (this.IsMouseOver)
            {
                background = this.MouseOverBackground;
                border = this.MouseOverBorder;
                foreground = this.MouseOverForeground;
            }

            if (this.IsPressed)
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
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.UpdateColors();
            }));
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.UpdateColors();
            }));
        }

        protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.UpdateColors();
            }));
        }

        protected override void OnMouseUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.UpdateColors();
            }));
        }
    }

}
