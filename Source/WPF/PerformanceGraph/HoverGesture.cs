using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;

namespace Microsoft.VisualStudio.PerformanceGraph
{
    class HoverGesture
    {
        DispatcherTimer hover;
        int lastMoveTime;
        MouseEventArgs lastMoveEvent;
        FrameworkElement target;

        public HoverGesture(FrameworkElement target)
        {
            this.target = target;
            this.target.MouseMove += new MouseEventHandler(OnMouseMove);
            this.target.MouseLeave += new MouseEventHandler(OnMouseLeave);
        }

        public event MouseEventHandler Hover;
        public event EventHandler Hidden;

        protected void OnMouseMove(object sender, MouseEventArgs e)
        {
            HidePopup();
            if (hover == null)
            {
                hover = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, OnHoverTick, target.Dispatcher);
            }
            lastMoveEvent = e;
            lastMoveTime = Environment.TickCount;
            hover.Start();
        }


        private void OnHoverTick(object sender, EventArgs e)
        {
            if (lastMoveTime != 0 && Environment.TickCount - lastMoveTime >= 300)
            {
                OnHover(lastMoveEvent);
                if (hover != null)
                {
                    hover.Stop();
                }
            }
        }

        protected void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!PopupContainsMouse(e))
            {
                HidePopup();
            }
            lastMoveTime = 0;
        }

        private bool PopupContainsMouse(MouseEventArgs e)
        {
            if (popup == null) return false;
            Rect bounds = new Rect(new Point(0, 0), popup.RenderSize);
            return bounds.Contains(e.GetPosition(popup));
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
            if (popup == null)
            {
                popup = new Popup();
                border = new Border();
                border.Background = Brushes.LemonChiffon;
                popup.Child = border;
            }

            border = (Border)popup.Child;
            border.Child = content;
            popup.Placement = PlacementMode.Mouse;
            return popup;
        }

        Popup popup;

        public void HidePopup()
        {
            if (popup != null)
            {
                popup.IsOpen = false;
            }
            if (Hidden != null)
            {
                Hidden(this, EventArgs.Empty);
            }
        }

    }
}
