using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Microsoft.VisualStudio.PerformanceGraph
{
    internal class HoverGesture
    {
        private DispatcherTimer hover;
        private uint lastMoveTime;
        private MouseEventArgs lastMoveEvent;
        private FrameworkElement target;

        public HoverGesture(FrameworkElement target)
        {
            this.target = target;
            this.target.MouseMove += new MouseEventHandler(this.OnMouseMove);
            this.target.MouseLeave += new MouseEventHandler(this.OnMouseLeave);
        }

        public event MouseEventHandler Hover;
        public event EventHandler Hidden;

        protected void OnMouseMove(object sender, MouseEventArgs e)
        {
            this.HidePopup();
            if (this.hover == null)
            {
                this.hover = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, this.OnHoverTick, this.target.Dispatcher);
            }
            this.lastMoveEvent = e;
            this.lastMoveTime = this.TickCount;
            this.hover.Start();
        }

        private uint TickCount
        {
            get { return (uint)Environment.TickCount; }
        }


        private void OnHoverTick(object sender, EventArgs e)
        {
            if (this.lastMoveTime != 0 && this.TickCount - this.lastMoveTime >= 300)
            {
                this.OnHover(this.lastMoveEvent);
                if (this.hover != null)
                {
                    this.hover.Stop();
                }
            }
        }

        protected void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!this.PopupContainsMouse(e))
            {
                this.HidePopup();
            }
            this.lastMoveTime = 0;
        }

        private bool PopupContainsMouse(MouseEventArgs e)
        {
            if (this.popup == null)
            {
                return false;
            }

            Rect bounds = new Rect(new Point(0, 0), this.popup.RenderSize);
            return bounds.Contains(e.GetPosition(this.popup));
        }

        protected void OnHover(MouseEventArgs e)
        {
            if (Hover != null)
            {
                Hover(this, e);
            }
        }

        public Popup CreatePopup(FrameworkElement content)
        {
            Border border;
            if (this.popup == null)
            {
                this.popup = new Popup();
                border = new Border();
                border.Background = Brushes.LemonChiffon;
                this.popup.Child = border;
            }

            border = (Border)this.popup.Child;
            border.Child = content;
            this.popup.Placement = PlacementMode.Mouse;
            return this.popup;
        }

        private Popup popup;

        public void HidePopup()
        {
            if (this.popup != null)
            {
                this.popup.IsOpen = false;
            }
            if (Hidden != null)
            {
                Hidden(this, EventArgs.Empty);
            }
        }

    }
}
