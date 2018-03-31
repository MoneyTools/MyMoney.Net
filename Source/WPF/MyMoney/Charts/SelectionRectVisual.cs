namespace Walkabout.Charts
{

    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Animation;
    using System.Windows.Shapes;

    /// <summary>
    /// Create a host visual derived from the FrameworkElement class for drawing the
    /// selection rubber band.
    /// </summary>
    internal class SelectionRectVisual : Canvas
    {

        Point _firstPoint;
        Point _secondPoint;
        double _zoom;
        Rectangle _white;
        Rectangle _dashed;

        /// <summary>
        /// Construct new SelectionRectVisual object for the given rectangle
        /// </summary>
        public SelectionRectVisual(Point firstPointP, Point secondPointP, double zoomP)
        {
            _white = new Rectangle()
            {
                Style = StyleResources.GetResource("SelectionRectWhite") as Style
            };
            _dashed = new Rectangle()
            {
                Style = StyleResources.GetResource("SelectionRectBlack") as Style
            };

            this._firstPoint = firstPointP;
            this._secondPoint = secondPointP;
            this._zoom = zoomP;

            SetRectBounds();
            this.Children.Add(_white);
            this.Children.Add(_dashed);

            DoubleAnimation animation = new DoubleAnimation(4, new Duration(TimeSpan.FromSeconds(1.5)))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            _dashed.BeginAnimation(Shape.StrokeDashOffsetProperty, animation);

        }

        /// <summary>
        /// Get/Set the first point in the rectangle (could be before or after second point).
        /// </summary>
        public Point FirstPoint
        {
            get { return _firstPoint; }
            set { _firstPoint = value; }
        }

        /// <summary>
        /// Get/Set the second point in the rectangle (could be before or after first point).
        /// </summary>
        public Point SecondPoint
        {
            get { return _secondPoint; }
            set
            {
                _secondPoint = value;
                SetRectBounds();
            }
        }

        void SetRectBounds()
        {
            Rect bounds = SelectedRect;
            double left = bounds.Left;
            double top = bounds.Top;
            double width = bounds.Width;
            double height = bounds.Height;
            Canvas.SetLeft(_white, left);
            Canvas.SetTop(_white, top);
            _white.Width = width;
            _white.Height = height;
            Canvas.SetLeft(_dashed, left);
            Canvas.SetTop(_dashed, top);
            _dashed.Width = width;
            _dashed.Height = height;
        }

        /// <summary>
        /// Get/Set the current Zoom level.
        /// </summary>
        public double Zoom
        {
            get { return _zoom; }
            set { _zoom = value; }
        }

        /// <summary>
        /// Get the actual Rectangle of the rubber band.
        /// </summary>
        internal Rect SelectedRect
        {
            get { return new Rect(FirstPoint, SecondPoint); }
        }
    }
}

