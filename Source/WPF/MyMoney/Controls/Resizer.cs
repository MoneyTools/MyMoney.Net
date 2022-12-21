using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Walkabout.Controls
{
    public class Resizer : FrameworkElement
    {
        private Rect limit;
        private Rect bounds;
        private Rect initialBounds;
        private DispatcherTimer timer;

        public Resizer()
        {
        }

        public event EventHandler Resized;
        public event EventHandler Resizing;

        public Rect Bounds
        {
            get { return this.bounds; }
            set { this.bounds = value; this.InvalidateVisual(); }
        }

        public Rect LimitBounds
        {
            get { return this.limit; }
            set
            {
                this.limit = value;
                this.PinToLimits();
                this.SizeToBounds();
                this.InvalidateVisual();
            }
        }

        private void PinToLimits()
        {
            double left = this.bounds.Left;
            double top = this.bounds.Top;
            double right = this.bounds.Right;
            double bottom = this.bounds.Bottom;
            bool changed = false;
            if (left < this.limit.Left)
            {
                left = this.limit.Left;
                changed = true;
            }
            if (top < this.limit.Top)
            {
                top = this.limit.Top;
                changed = true;
            }
            if (right > this.limit.Right)
            {
                right = this.limit.Right;
                changed = true;
            }
            if (bottom > this.limit.Bottom)
            {
                bottom = this.limit.Bottom;
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
                this.Bounds = new Rect(left, top, w, h);
            }
        }

        private void SizeToBounds()
        {
            Canvas.SetLeft(this, this.limit.Left);
            Canvas.SetTop(this, this.limit.Top);
            Size size = this.limit.Size;
            this.Width = size.Width;
            this.Height = size.Height;
            this.InvalidateVisual();
        }


        public double ThumbSize
        {
            get { return this.thumbSize; }
            set { this.thumbSize = value; }
        }

        private Brush thumbBrush = Brushes.Navy;

        public Brush ThumbBrush
        {
            get { return this.thumbBrush; }
            set { this.thumbBrush = value; }
        }

        private Brush borderBrush = Brushes.Navy;

        public Brush BorderBrush
        {
            get { return this.borderBrush; }
            set { this.borderBrush = value; }
        }

        private static readonly Brush SmokyGlassBrush = new SolidColorBrush(Color.FromArgb(0xA0, 0xe0, 0xe0, 0xff));
        private double thumbSize = 8;
        private readonly double[] dashes = new double[] { 3, 3 };
        private double offset = 0;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            Rect imageBounds = this.limit;
            imageBounds.Offset(-this.limit.Left, -this.limit.Top);
            Rect resizerBounds = this.bounds;
            resizerBounds.Offset(-this.limit.Left, -this.limit.Top);

            CombinedGeometry mask = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(imageBounds), new RectangleGeometry(resizerBounds));
            drawingContext.DrawGeometry(SmokyGlassBrush, null, mask);

            Pen pen = new Pen(this.borderBrush, 1);
            pen.DashStyle = new DashStyle(this.dashes, this.offset);
            this.offset++;
            if (this.offset == 6)
            {
                this.offset = 0;
            }

            Rect box = resizerBounds;
            drawingContext.DrawRectangle(null, pen, box);

            drawingContext.DrawRectangle(this.thumbBrush, null, this.TopLeftThumb);
            drawingContext.DrawRectangle(this.thumbBrush, null, this.TopMiddleThumb);
            drawingContext.DrawRectangle(this.thumbBrush, null, this.TopRightThumb);

            drawingContext.DrawRectangle(this.thumbBrush, null, this.MiddleLeftThumb);
            drawingContext.DrawRectangle(this.thumbBrush, null, this.MiddleRightThumb);

            drawingContext.DrawRectangle(this.thumbBrush, null, this.BottomLeftThumb);
            drawingContext.DrawRectangle(this.thumbBrush, null, this.BottomMiddleThumb);
            drawingContext.DrawRectangle(this.thumbBrush, null, this.BottomRightThumb);

        }

        private enum Corner { None, Middle, TopLeft, TopMiddle, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomMiddle, BottomRight };

        private Corner dragging;
        private Point mouseDownPosition;

        protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            if (!this.IsEnabled)
            {
                // resizing is not enabled.
                this.dragging = Corner.None;
                return;
            }

            this.Cursor = System.Windows.Input.Cursors.Arrow;

            Point pos = e.GetPosition(this);
            if (this.TopLeftThumb.Contains(pos))
            {
                this.dragging = Corner.TopLeft;
            }
            else if (this.TopMiddleThumb.Contains(pos))
            {
                this.dragging = Corner.TopMiddle;
            }
            else if (this.TopRightThumb.Contains(pos))
            {
                this.dragging = Corner.TopRight;
            }
            else if (this.MiddleLeftThumb.Contains(pos))
            {
                this.dragging = Corner.MiddleLeft;
            }
            else if (this.MiddleRightThumb.Contains(pos))
            {
                this.dragging = Corner.MiddleRight;
            }
            else if (this.BottomLeftThumb.Contains(pos))
            {
                this.dragging = Corner.BottomLeft;
            }
            else if (this.BottomMiddleThumb.Contains(pos))
            {
                this.dragging = Corner.BottomMiddle;
            }
            else if (this.BottomRightThumb.Contains(pos))
            {
                this.dragging = Corner.BottomRight;
            }
            else
            {
                this.dragging = Corner.Middle;
            }

            this.initialBounds = this.bounds;

            e.Handled = true;
            this.mouseDownPosition = pos;
            System.Windows.Input.Mouse.Capture(this);

            if (this.timer == null)
            {
                this.timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Normal, this.OnTimerTick, this.Dispatcher);
                this.timer.Start();
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            this.InvalidateVisual(); // draw marching ants.
        }

        protected override void OnPreviewMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            if (this.IsEnabled)
            {
                System.Windows.Input.Mouse.Capture(null);
                this.dragging = Corner.None;
            }
            if (this.timer != null)
            {
                this.timer.Stop();
                this.timer.Tick -= this.OnTimerTick;
                this.timer = null;
            }
        }

        protected override void OnLostMouseCapture(System.Windows.Input.MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            this.dragging = Corner.None;
            if (this.IsEnabled && Resized != null)
            {
                Resized(this, EventArgs.Empty);
            }
        }

        protected override void OnPreviewMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (!this.IsEnabled)
            {
                return;
            }

            Rect newBounds = this.initialBounds;

            Point pos = e.GetPosition(this);
            double dx = pos.X - this.mouseDownPosition.X;
            double dy = pos.Y - this.mouseDownPosition.Y;

            if (this.dragging == Corner.Middle)
            {
                newBounds.X += dx;
                newBounds.Y += dy;
                this.bounds = newBounds;
                this.PinToLimits();
                this.InvalidateVisual();
            }
            else if (this.dragging != Corner.None)
            {
                // actually move the shape!
                switch (this.dragging)
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
                this.PinToLimits();
                this.InvalidateVisual();
                if (Resizing != null)
                {
                    Resizing(this, EventArgs.Empty);
                }
            }
            else
            {
                if (this.TopLeftThumb.Contains(pos))
                {
                    this.Cursor = System.Windows.Input.Cursors.SizeNWSE;
                }
                else if (this.TopMiddleThumb.Contains(pos))
                {
                    this.Cursor = System.Windows.Input.Cursors.SizeNS;
                }
                else if (this.TopRightThumb.Contains(pos))
                {
                    this.Cursor = System.Windows.Input.Cursors.SizeNESW;
                }
                else if (this.MiddleLeftThumb.Contains(pos))
                {
                    this.Cursor = System.Windows.Input.Cursors.SizeWE;
                }
                else if (this.MiddleRightThumb.Contains(pos))
                {
                    this.Cursor = System.Windows.Input.Cursors.SizeWE;
                }
                else if (this.BottomLeftThumb.Contains(pos))
                {
                    this.Cursor = System.Windows.Input.Cursors.SizeNESW;
                }
                else if (this.BottomMiddleThumb.Contains(pos))
                {
                    this.Cursor = System.Windows.Input.Cursors.SizeNS;
                }
                else if (this.BottomRightThumb.Contains(pos))
                {
                    this.Cursor = System.Windows.Input.Cursors.SizeNWSE;
                }
                else
                {
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                }
            }
        }

        public Rect TopLeftThumb
        {
            get
            {
                Rect result = new Rect(this.bounds.Left - this.thumbSize, this.bounds.Top - this.thumbSize, this.thumbSize, this.thumbSize);
                result.Offset(-this.limit.Left, -this.limit.Top);
                return result;
            }
        }

        public Rect TopMiddleThumb
        {
            get
            {
                Rect result = new Rect(this.bounds.Left + (this.bounds.Width / 2) - (this.thumbSize / 2), this.bounds.Top - this.thumbSize, this.thumbSize, this.thumbSize);
                result.Offset(-this.limit.Left, -this.limit.Top);
                return result;
            }
        }
        public Rect TopRightThumb
        {
            get
            {
                Rect result = new Rect(this.bounds.Right, this.bounds.Top - this.thumbSize, this.thumbSize, this.thumbSize);
                result.Offset(-this.limit.Left, -this.limit.Top);
                return result;
            }
        }
        public Rect MiddleLeftThumb
        {
            get
            {
                Rect result = new Rect(this.bounds.Left - this.thumbSize, this.bounds.Top + (this.bounds.Height / 2) - (this.thumbSize / 2), this.thumbSize, this.thumbSize);
                result.Offset(-this.limit.Left, -this.limit.Top);
                return result;
            }
        }

        public Rect MiddleRightThumb
        {
            get
            {
                Rect result = new Rect(this.bounds.Right, this.bounds.Top + (this.bounds.Height / 2) - (this.thumbSize / 2), this.thumbSize, this.thumbSize);
                result.Offset(-this.limit.Left, -this.limit.Top);
                return result;
            }
        }

        public Rect BottomLeftThumb
        {
            get
            {
                Rect result = new Rect(this.bounds.Left - this.thumbSize, this.bounds.Bottom, this.thumbSize, this.thumbSize);
                result.Offset(-this.limit.Left, -this.limit.Top);
                return result;
            }
        }

        public Rect BottomMiddleThumb
        {
            get
            {
                Rect result = new Rect(this.bounds.Left + (this.bounds.Width / 2) - (this.thumbSize / 2), this.bounds.Bottom, this.thumbSize, this.thumbSize);
                result.Offset(-this.limit.Left, -this.limit.Top);
                return result;
            }
        }

        public Rect BottomRightThumb
        {
            get
            {
                Rect result = new Rect(this.bounds.Right, this.bounds.Bottom, this.thumbSize, this.thumbSize);
                result.Offset(-this.limit.Left, -this.limit.Top);
                return result;
            }
        }
    }
}
