using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Walkabout.Controls
{
    public class Resizer : FrameworkElement
    {
        Rect limit;
        Rect bounds;
        Rect initialBounds;
        DispatcherTimer timer;

        public Resizer()
        {
        }

        public event EventHandler Resized;
        public event EventHandler Resizing;

        public Rect Bounds { 
            get { return bounds; }
            set { bounds = value; InvalidateVisual(); }
        }

        public Rect LimitBounds
        {
            get { return limit; }
            set
            {
                limit = value;
                PinToLimits();
                SizeToBounds();
                InvalidateVisual();
            }
        }

        private void PinToLimits()
        {
            double left = bounds.Left;
            double top = bounds.Top;
            double right = bounds.Right;
            double bottom = bounds.Bottom;
            bool changed = false;
            if (left < limit.Left)
            {
                left = limit.Left;
                changed = true;
            }
            if (top < limit.Top)
            {
                top = limit.Top;
                changed = true;
            }
            if (right > limit.Right)
            {
                right = limit.Right;
                changed = true;
            }
            if (bottom > limit.Bottom)
            {
                bottom = limit.Bottom;
                changed = true;
            }
            if (changed)
            {
                double w = right - left;
                if (w < 0)
                {
                    w = 0;
                }
                double h = bottom - top;
                if (h < 0)
                {
                    h = 0;
                }
                Bounds = new Rect(left, top, w, h);
            }
        }

        private void SizeToBounds()
        {
            Canvas.SetLeft(this, limit.Left );
            Canvas.SetTop(this, limit.Top);
            Size size = limit.Size;
            this.Width = size.Width;
            this.Height = size.Height;
            InvalidateVisual();
        }


        public double ThumbSize
        {
            get { return thumbSize; }
            set { thumbSize = value; }
        }

        Brush thumbBrush = Brushes.Navy;

        public Brush ThumbBrush
        {
            get { return thumbBrush; }
            set { thumbBrush = value; }
        }

        Brush borderBrush = Brushes.Navy;

        public Brush BorderBrush
        {
            get { return borderBrush; }
            set { borderBrush = value; }
        }

        static Brush SmokyGlassBrush = new SolidColorBrush(Color.FromArgb(0xA0, 0xe0, 0xe0, 0xff));
        double thumbSize = 8;

        double[] dashes = new double[] { 3, 3 };
        double offset = 0;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            Rect imageBounds = limit;
            imageBounds.Offset(-limit.Left, -limit.Top);
            Rect resizerBounds = bounds;
            resizerBounds.Offset(-limit.Left, -limit.Top);

            CombinedGeometry mask = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(imageBounds), new RectangleGeometry(resizerBounds));            
            drawingContext.DrawGeometry(SmokyGlassBrush, null, mask);

            Pen pen = new Pen(borderBrush, 1);
            pen.DashStyle = new DashStyle(dashes, offset);
            offset++;
            if (offset == 6)
            {
                offset = 0;
            }

            Rect box = resizerBounds;
            drawingContext.DrawRectangle(null, pen, box);
            
            drawingContext.DrawRectangle(thumbBrush, null, TopLeftThumb);
            drawingContext.DrawRectangle(thumbBrush, null, TopMiddleThumb);
            drawingContext.DrawRectangle(thumbBrush, null, TopRightThumb);

            drawingContext.DrawRectangle(thumbBrush, null, MiddleLeftThumb);
            drawingContext.DrawRectangle(thumbBrush, null, MiddleRightThumb);

            drawingContext.DrawRectangle(thumbBrush, null, BottomLeftThumb);
            drawingContext.DrawRectangle(thumbBrush, null, BottomMiddleThumb);
            drawingContext.DrawRectangle(thumbBrush, null, BottomRightThumb);

        }

        enum Corner { None, Middle, TopLeft, TopMiddle, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomMiddle, BottomRight };

        Corner dragging;
        Point mouseDownPosition;

        protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            if (!IsEnabled)
            {
                // resizing is not enabled.
                dragging = Corner.None;
                return;
            }

            Cursor = System.Windows.Input.Cursors.Arrow;

            Point pos = e.GetPosition(this);
            if (TopLeftThumb.Contains(pos))
            {
                dragging = Corner.TopLeft;
            }
            else if (TopMiddleThumb.Contains(pos))
            {
                dragging = Corner.TopMiddle;
            }
            else if (TopRightThumb.Contains(pos))
            {
                dragging = Corner.TopRight;
            }
            else if (MiddleLeftThumb.Contains(pos))
            {
                dragging = Corner.MiddleLeft;
            }
            else if (MiddleRightThumb.Contains(pos))
            {
                dragging = Corner.MiddleRight;
            }
            else if (BottomLeftThumb.Contains(pos))
            {
                dragging = Corner.BottomLeft;
            }
            else if (BottomMiddleThumb.Contains(pos))
            {
                dragging = Corner.BottomMiddle;
            }
            else if (BottomRightThumb.Contains(pos))
            {
                dragging = Corner.BottomRight;
            }
            else
            {
                dragging = Corner.Middle;
            }

            initialBounds = bounds;
            
            e.Handled = true;
            mouseDownPosition = pos;
            System.Windows.Input.Mouse.Capture(this);

            if (timer == null)
            {
                timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Normal, OnTimerTick, this.Dispatcher);
                timer.Start();
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            InvalidateVisual(); // draw marching ants.
        }

        protected override void OnPreviewMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            if (IsEnabled)
            {
                System.Windows.Input.Mouse.Capture(null);
                dragging = Corner.None;
            }
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= OnTimerTick;
                timer = null;
            }
        }

        protected override void OnLostMouseCapture(System.Windows.Input.MouseEventArgs e)
        {
            base.OnLostMouseCapture(e); 
            dragging = Corner.None;
            if (IsEnabled && Resized != null)
            {
                Resized(this, EventArgs.Empty);
            }
        }

        protected override void OnPreviewMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (!IsEnabled)
            {
                return;
            }

            Rect newBounds = this.initialBounds;

            Point pos = e.GetPosition(this);
            double dx = pos.X - mouseDownPosition.X;
            double dy = pos.Y - mouseDownPosition.Y;

            if (dragging == Corner.Middle)
            {
                newBounds.X += dx;
                newBounds.Y += dy;
                this.bounds = newBounds;
                PinToLimits();
                InvalidateVisual();
            }
            else if (dragging != Corner.None)
            {
                // actually move the shape!
                switch (dragging)
                {
                    case Corner.TopLeft:
                        newBounds.X += dx;
                        newBounds.Y += dy;
                        newBounds.Width = Math.Max(0, newBounds.Width - dx);
                        newBounds.Height = Math.Max(0, newBounds.Height - dy);
                        break;
                    case Corner.TopMiddle:
                        newBounds.Y += dy;
                        newBounds.Height = Math.Max(0, newBounds.Height - dy);
                        break;
                    case Corner.TopRight:
                        newBounds.Width = Math.Max(0, newBounds.Width + dx);
                        newBounds.Height = Math.Max(0, newBounds.Height - dy);
                        newBounds.Y += dy;
                        break;
                    case Corner.MiddleLeft:
                        newBounds.Width = Math.Max(0, newBounds.Width - dx);
                        newBounds.X += dx;
                        break;
                    case Corner.MiddleRight:
                        newBounds.Width = Math.Max(0, newBounds.Width + dx);
                        break;
                    case Corner.BottomLeft:
                        newBounds.X += dx;
                        newBounds.Height = Math.Max(0, newBounds.Height + dy);
                        newBounds.Width = Math.Max(0, newBounds.Width - dx);
                        break;
                    case Corner.BottomMiddle:
                        newBounds.Height = Math.Max(0, newBounds.Height + dy);
                        break;
                    case Corner.BottomRight:
                        newBounds.Width = Math.Max(0, newBounds.Width + dx);
                        newBounds.Height = Math.Max(0, newBounds.Height + dy);
                        break;
                }

                this.bounds = newBounds;
                PinToLimits();
                InvalidateVisual();
                if (Resizing != null)
                {
                    Resizing(this, EventArgs.Empty);
                }
            }
            else
            {
                if (TopLeftThumb.Contains(pos))
                {
                    Cursor = System.Windows.Input.Cursors.SizeNWSE;
                }
                else if (TopMiddleThumb.Contains(pos))
                {
                    Cursor = System.Windows.Input.Cursors.SizeNS;
                }
                else if (TopRightThumb.Contains(pos))
                {
                    Cursor = System.Windows.Input.Cursors.SizeNESW;
                }
                else if (MiddleLeftThumb.Contains(pos))
                {
                    Cursor = System.Windows.Input.Cursors.SizeWE;
                }
                else if (MiddleRightThumb.Contains(pos))
                {
                    Cursor = System.Windows.Input.Cursors.SizeWE;
                }
                else if (BottomLeftThumb.Contains(pos))
                {
                    Cursor = System.Windows.Input.Cursors.SizeNESW;
                }
                else if (BottomMiddleThumb.Contains(pos))
                {
                    Cursor = System.Windows.Input.Cursors.SizeNS;
                }
                else if (BottomRightThumb.Contains(pos))
                {
                    Cursor = System.Windows.Input.Cursors.SizeNWSE;
                }
                else
                {
                    Cursor = System.Windows.Input.Cursors.Arrow;
                }
            }
        }

        public Rect TopLeftThumb
        {
            get { 
                Rect result = new Rect(bounds.Left - thumbSize, bounds.Top - thumbSize, thumbSize, thumbSize);
                result.Offset(-limit.Left, -limit.Top);
                return result;
            }
        }

        public Rect TopMiddleThumb
        {
            get
            {
                Rect result = new Rect(bounds.Left + bounds.Width / 2 - thumbSize / 2, bounds.Top - thumbSize, thumbSize, thumbSize);
                result.Offset(-limit.Left, -limit.Top);
                return result;
            }
        }
        public Rect TopRightThumb
        {
            get
            {
                Rect result = new Rect(bounds.Right, bounds.Top - thumbSize, thumbSize, thumbSize);
                result.Offset(-limit.Left, -limit.Top);
                return result;
            }
        }
        public Rect MiddleLeftThumb
        {
            get
            {
                Rect result = new Rect(bounds.Left - thumbSize, bounds.Top + bounds.Height / 2 - thumbSize / 2, thumbSize, thumbSize);
                result.Offset(-limit.Left, -limit.Top);
                return result;
            }
        }

        public Rect MiddleRightThumb
        {
            get
            {
                Rect result = new Rect(bounds.Right, bounds.Top + bounds.Height / 2 - thumbSize / 2, thumbSize, thumbSize);
                result.Offset(-limit.Left, -limit.Top);
                return result;
            }
        }

        public Rect BottomLeftThumb
        {
            get
            {
                Rect result = new Rect(bounds.Left - thumbSize, bounds.Bottom, thumbSize, thumbSize);
                result.Offset(-limit.Left, -limit.Top);
                return result;
            }
        }

        public Rect BottomMiddleThumb
        {
            get
            {
                Rect result = new Rect(bounds.Left + bounds.Width / 2 - thumbSize / 2, bounds.Bottom, thumbSize, thumbSize);
                result.Offset(-limit.Left, -limit.Top);
                return result;
            }
        }

        public Rect BottomRightThumb
        {
            get
            {
                Rect result = new Rect(bounds.Right, bounds.Bottom, thumbSize, thumbSize);
                result.Offset(-limit.Left, -limit.Top);
                return result;
            }
        }
    }
}
