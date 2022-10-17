using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Walkabout.Controls
{
    /// <summary>
    /// This is an attempt at an optimized TextBlock that has no concept of
    /// TextWrapping for multiline text formatting.  But alas, it does not seem
    /// to be any faster.  The Application TimeLine Profile might be showing
    /// it to be about 7% faster, but in practice with a stop watch the 
    /// scrolling to the top of a large brokerage account is not any faster.
    /// But perhaps more work could be done on this class to further optimize
    /// it. For example, it would be awesome if we could switch the Text
    /// without having to spin off the "new FormattedText" as garbage because
    /// we would then limit garbage collection while scrolling, but this would
    /// take diving into the implementation of that thing because currently
    /// the textToFormat is not settable on FormattedText which sucks.
    /// </summary>
    public class SingleLineTextBlock : Control
    {
        string text;

        class TextProperties
        {
            public FlowDirection FlowDirection;
            public TextAlignment TextAlignment;
            public VerticalAlignment VerticalAlignment;
            public double FontSize;
            public FontFamily FontFamily;
            public FontStretch FontStretch;
            public FontWeight FontWeight;
            public FontStyle FontStyle;
            public Brush ForegroundBrush;
            public CultureInfo CultureInfo;
            public double PixelsPerDip;
            public Typeface Typeface;
        }
        TextProperties _properties;
        FormattedText _formatted;

        public SingleLineTextBlock()
        {
        }

        public TextAlignment TextAlignment
        {
            get { return (TextAlignment)GetValue(TextAlignmentProperty); }
            set { SetValue(TextAlignmentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TextAlignment.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(SingleLineTextBlock), new PropertyMetadata(TextAlignment.Left, OnTextAlignmentChanged));

        private static void OnTextAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SingleLineTextBlock)d).OnTextAlignmentChanged();
        }

        private void OnTextAlignmentChanged()
        {
            if (this._properties != null)
            {
                this._properties.TextAlignment = this.TextAlignment;
            }
            InvalidateVisual();
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SingleLineTextBlock), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SingleLineTextBlock)d).OnTextChanged();
        }

        void OnTextChanged()
        {
            this.text = this.Text;
            _formatted = null;
            InvalidateMeasure();
            InvalidateArrange();
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            InvalidateAll();
            base.OnVisualParentChanged(oldParent);
        }

        void InvalidateAll()
        {
            _properties = null;
            _formatted = null;
            InvalidateMeasure();
            InvalidateArrange();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var ft = GetFormattedText();
            if (ft != null)
            {
                switch (this._properties.TextAlignment)
                {
                    case TextAlignment.Left:
                    case TextAlignment.Justify:
                        drawingContext.DrawText(ft, new Point(0, 0));
                        break;
                    case TextAlignment.Right:
                        var x = this.RenderSize.Width - ft.Width;
                        drawingContext.DrawText(ft, new Point(x, 0));
                        break;
                    case TextAlignment.Center:
                        var cx = (this.RenderSize.Width - ft.Width) / 2;
                        drawingContext.DrawText(ft, new Point(cx, 0));
                        break;
                    default:
                        break;
                }
            }
        }

        protected override int VisualChildrenCount
        {
            get { return 0; }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            CheckProperties();
            var ft = GetFormattedText();
            if (ft != null)
            {
                return new Size(ft.Width, ft.Height);
            }
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            InvalidateVisual();
            return finalSize;
        }

        private void CheckProperties()
        {
            // FontSize could have changed, but there is no event we can get for that.
            if (this._properties != null)
            {
                if (this._properties.FontSize != this.FontSize ||
                    this._properties.FontFamily != this.FontFamily ||
                    this._properties.FontWeight != this.FontWeight ||
                    this._properties.FontStretch != this.FontStretch)
                {
                    this._properties = null;
                    InvalidateArrange();
                }
                else if (this._properties.FlowDirection != this.FlowDirection ||
                    this._properties.ForegroundBrush != this.Foreground ||
                    this._properties.CultureInfo != CultureInfo.CurrentCulture ||
                    this._properties.TextAlignment != this.TextAlignment ||
                    this._properties.VerticalAlignment != this.VerticalAlignment)
                {
                    this._properties = null;
                    InvalidateVisual();
                }
            }
        }

        private FormattedText GetFormattedText()
        {
            if (!string.IsNullOrEmpty(this.text))
            {
                if (this._properties == null)
                {
                    this._properties = new TextProperties()
                    {
                        CultureInfo = CultureInfo.CurrentCulture,
                        FontFamily = this.FontFamily,
                        FontSize = this.FontSize,
                        FontStyle = this.FontStyle,
                        FontWeight = this.FontWeight,
                        FontStretch = this.FontStretch,
                        ForegroundBrush = this.Foreground,
                        PixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip,
                        FlowDirection = this.FlowDirection,
                        TextAlignment = this.TextAlignment,
                        VerticalAlignment = this.VerticalAlignment
                    };
                    this._properties.Typeface = new Typeface(this._properties.FontFamily, this._properties.FontStyle, this._properties.FontWeight, this._properties.FontStretch);
                    this._formatted = null;
                }
                if (this._formatted == null)
                {
                    this._formatted = new FormattedText(this.text, this._properties.CultureInfo, this._properties.FlowDirection,
                                             this._properties.Typeface, this._properties.FontSize, this._properties.ForegroundBrush,
                                             this._properties.PixelsPerDip);
                }
                return this._formatted;
            }
            return null;
        }

    }
}
