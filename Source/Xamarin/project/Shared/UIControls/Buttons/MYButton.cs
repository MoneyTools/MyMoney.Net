using System;
using Xamarin.Forms;

namespace XMoney
{
    public class XButton : ContentView
    {
        private readonly Grid _grid;
        private readonly Frame _frame;
        private readonly Label _label;
        public TapGestureRecognizer Gesture;

        public XButton()
        {
            _grid = new Grid();

            // Button Border
            {
                _frame = new Frame
                {
                    CornerRadius = 4,
                    BorderColor = Color.FromRgba(0.5, 0.5, 0.5, 0.5),
                    BackgroundColor = Color.Transparent,
                    HasShadow = false,
                };

                _grid.Children.Add(_frame);
            }

            // Button Text
            {
                _label = new Label
                {
                    Text = "",
                    LineBreakMode = LineBreakMode.NoWrap,
                    TextColor = Color.Black,
                    FontAttributes = FontAttributes.Bold,
                    VerticalOptions = LayoutOptions.CenterAndExpand,
                    HorizontalOptions = LayoutOptions.CenterAndExpand,
                    HorizontalTextAlignment = TextAlignment.Center
                };

                _grid.Children.Add(_label);
            }

            this.Content = _grid;

            Gesture = new TapGestureRecognizer();
            Gesture.Tapped += new EventHandler(InternalClickEvent);
            this.GestureRecognizers.Add(Gesture);
        }

        public virtual string Text
        {
            get { return _label.Text; }
            set { _label.Text = value; }
        }

        public virtual Color TextColor
        {
            get { return _label.TextColor; }
            set
            {
                _label.TextColor = value;
            }
        }

        public double FontSize { get { return _label.FontSize; } set { _label.FontSize = value; } }

        public FontAttributes FontAttributes { get { return _label.FontAttributes; } set { _label.FontAttributes = value; } }

        private void InternalClickEvent(object sender, EventArgs e)
        {
            XButton button = (XButton)sender;
            button.Clicked?.Invoke(sender, e);
        }

        public float Radius
        {
            get
            {
                return _frame.CornerRadius;
            }

            set
            {
                _frame.CornerRadius = value;
            }
        }

        public Color BorderColor
        {
            get
            {
                return _frame.BorderColor;
            }

            set
            {
                _frame.BorderColor = value;
            }
        }

        public new Color BackgroundColor
        {
            get
            {
                return _frame.BackgroundColor;
            }

            set
            {
                _frame.BackgroundColor = value;
            }
        }

        public EventHandler Clicked;
    }
}