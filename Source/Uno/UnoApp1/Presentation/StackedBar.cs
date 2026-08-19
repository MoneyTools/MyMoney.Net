using System.Diagnostics;
using Microsoft.UI.Xaml.Media.Animation;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.UI;

namespace UnoApp1.Presentation;

internal class StackedBar : SKXamlCanvas
{
    private readonly List<BarInfo> infos = new List<BarInfo>();
    private double mouseOverBlend;
    private Rect outline;

    public StackedBar()
    {
        this.AnimationColorMilliseconds = 120;        
    }

    /// <summary>
    /// Time to animate the column color.
    /// </summary>
    public int AnimationColorMilliseconds { get; set; }


    public PointCollection Points
    {
        get { return (PointCollection)this.GetValue(PointsProperty); }
        set { this.SetValue(PointsProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Points.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register("Points", typeof(PointCollection), typeof(StackedBar),
            new PropertyMetadata(new PointCollection(), OnPointsChanged));

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        StackedBar bar = (StackedBar)d;
        bar.OnPointsChanged();
    }

    private void OnPointsChanged()
    {
        this.outline = new Rect();
        bool first = true;
        foreach (var pt in this.Points)
        {
            if (first) {
                first = false;
                this.outline.X = pt.X; 
                this.outline.Width = 0;
                this.outline.Y = pt.Y;
                this.outline.Height = 0;
            } 
            else 
            {
                if (this.outline.X < pt.X)
                {
                    this.outline.Width = Math.Max(this.outline.Width, pt.X - this.outline.X);
                } 
                else
                {
                    // switch pt is on the left.
                    this.outline.Width += this.outline.X - pt.X;
                    this.outline.X = pt.X;
                }

                if (this.outline.Y < pt.Y)
                {
                    this.outline.Height = Math.Max(this.outline.Height, pt.Y - this.outline.Y);
                }
                else
                {
                    // pt is below current outline.
                    this.outline.Height += this.outline.Y - pt.Y;
                    this.outline.Y = pt.Y;
                }
            }
        }

        this.LayoutSegments();
        this.InvalidateArrange();
        this.Invalidate();
    }

    public void AnimateBar(PointCollection target, Duration duration, TimeSpan startTime)
    {
        var source = this.Points;
        if (source == null || source.Count == 0) {
            // Must have some visible start points otherwise we'll run into null ref
            // exceptions trying to animate something that has no SKCanvas.
            var botttomLeft = this.GetBottomLeft(target);
            var bottomRight = this.GetBottomRight(target);
            var topRight = new Point(bottomRight.X, bottomRight.Y + 1); // must be at least 1 pixel to have a bitmap backbuffer, otherwise Invalidate blows up.
            var startPoints = new PointCollection()
            {
                botttomLeft,
                bottomRight,
                botttomLeft,
                topRight,
            };
            this.Points = source = startPoints;
        }

        this.animation = new PointCollectionAnimation(this, "Points") { From = source, To = target };
        this.animation.BeginAnimation(duration, startTime);        
    }

    PointCollectionAnimation animation;

    Point GetBottomLeft(PointCollection target)
    {
        double x = double.NegativeInfinity;
        double y = double.NegativeInfinity;
        foreach (var pt in target)
        {
            x = (x == double.NegativeInfinity) ? pt.X : Math.Min(x, pt.X);
            y = (y == double.NegativeInfinity) ? pt.Y : Math.Max(y, pt.Y);
        }
        return new Point(x, y);
    }

    Point GetBottomRight(PointCollection target)
    {
        double x = double.NegativeInfinity;
        double y = double.NegativeInfinity;
        foreach (var pt in target)
        {
            x = (x == double.NegativeInfinity) ? pt.X : Math.Max(x, pt.X);
            y = (y == double.NegativeInfinity) ? pt.Y : Math.Max(y, pt.Y);
        }
        return new Point(x, y);
    }


    public Orientation Orientation
    {
        get { return (Orientation)this.GetValue(OrientationProperty); }
        set { this.SetValue(OrientationProperty, value); }
    }

    // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register("OrientationProperty", typeof(Orientation), typeof(StackedBar), new PropertyMetadata(Orientation.Vertical));


    public double MouseOverBlend
    {
        get => this.mouseOverBlend;
        set {
            if (this.mouseOverBlend != value)
            {
                this.mouseOverBlend = value;
                this.Invalidate();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (this.Width != 0 && this.Height != 0)
        {
            return new Size(this.Width, this.Height);
        }
        return new Size(Math.Max(1.0, this.outline.Width), Math.Max(1.0, this.outline.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (this.Width != 0 && this.Height != 0)
        {
            return new Size(this.Width, this.Height);
        }
        return new Size(Math.Max(1.0, this.outline.Width), Math.Max(1.0, this.outline.Height));
    }

    class BarInfo
    {
        public double Length;
        public Color Color;
        public Color MouseOverColor;
        public object UserData;
        public Rect Bounds;
    }

    public void AddSegment(double length, Color color, object userData)
    {
        var moc = this.GetMouseOverColor(color);
        this.infos.Add(new BarInfo
        {
            Length = length,
            Color = color,
            MouseOverColor = moc,
            UserData = userData
        });
    }

    internal void ClearSegments()
    {
        infos.Clear();
        this.Invalidate();
    }

    public object? HitBarSegment(Point pos)
    {
        foreach (var i in this.infos)
        {
            if (i.Bounds.Contains(pos))
            {
                return i.UserData;
            }
        }
        return null;
    }

    private Color GetMouseOverColor(Color c)
    {
        var hls = new HlsColor(c);
        hls.Lighten(0.25f);
        return hls.Color;
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        if (e.Surface == null)
        {
            return;
        }
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        // !this.IsVisible || 
        if (this.infos.Count == 0 || this.outline == Rect.Empty) return;

        double totalLength = (from i in this.infos select i.Length).Sum();
        if (totalLength == 0) return;
        foreach (var i in infos)
        {
            var seg = i.Bounds;
            Color fill = i.Color;
            if (this.MouseOverBlend > 0)
            {
                fill = this.BlendColors(i.Color, i.MouseOverColor, this.MouseOverBlend);
            }
            using var paint = new SKPaint
            {
                Color = new SKColor(fill.R, fill.G, fill.B, fill.A),
                Style = SKPaintStyle.Fill
            };

            canvas.DrawRect((float)seg.X, (float)seg.Y, (float)seg.Width, (float)seg.Height, paint);
        }
    }

    private Color BlendColors(Color color1, Color color2, double amount)
    {
        var blend = Extensions.FromRgb(
            (byte)(color1.R + (this.MouseOverBlend * (color2.R - color1.R))),
            (byte)(color1.G + (this.MouseOverBlend * (color2.G - color1.G))),
            (byte)(color1.B + (this.MouseOverBlend * (color2.B - color1.B))));
        return blend;
    }

    public double GetSegmentLength()
    {
        return (from i in this.infos select i.Length).Sum();
    }

    private void LayoutSegments()
    {
        double totalLength = this.GetSegmentLength();
        if (totalLength == 0) return;

        if (this.Orientation == Orientation.Vertical)
        {
            Rect bounds = this.outline;
            double width = bounds.Width;
            double x = bounds.Left;
            double y = bounds.Top;
            foreach (var i in infos)
            {
                double height = Math.Round((i.Length / totalLength) * bounds.Height);
                i.Bounds = new Rect(x, y, width, height);
                y += height;
            }
        }
        else
        {
            Rect bounds = this.outline;
            double height = bounds.Height;
            double x = bounds.Left;
            double y = bounds.Top;
            foreach (var i in infos)
            {
                double width = Math.Round((i.Length / totalLength) * bounds.Width);
                i.Bounds = new Rect(x, y, width, height);
                x += width;
            }
        }
    }


}
