using Xamarin.Forms;

namespace XMoney
{
    public class XButtonFlex : XButton
    {
        private bool _selected = false;
        private string _textLong = "";

        public XButtonFlex()
        {
        }

        protected override void OnSizeAllocated(double w, double h)
        {
            base.OnSizeAllocated(w, h);
            if (w < 150 && TextShort.Length > 0)
            {
                FontSize = 24;
                Text = TextShort;
            }
            else
            {
                FontSize = 12;
                Text = _textLong;
            }
            //base.OnSizeAllocated(w, h);
            this.Selected = this._selected;
        }

        public override string Text
        {
            get
            {
                return base.Text;
            }
            set
            {
                base.Text = value;
                this._textLong = value;
            }
        }

        public string TextShort { get; set; } = "";

        public string TextLong
        {
            get { return _textLong; }
            set
            {
                _textLong = value;
                Text = _textLong;
            }
        }

        public override Color TextColor
        {
            get { return base.TextColor; }
            set
            {
                base.TextColor = value;
            }
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                if (_selected)
                {
                    base.TextColor = TextColor_Selected;
                    BackgroundColor = BackgoundColor_Selected;
                }
                else
                {
                    base.TextColor = TextColor_Unselected;
                    BackgroundColor = BackgroundColor_Unselected;
                }
            }
        }

        public Color TextColor_Unselected { get; set; } = Color.White;

        public Color TextColor_Selected { get; set; } = Color.White;

        public Color BackgroundColor_Unselected { get; set; } = Color.FromRgba(120, 120, 120, 100);

        public Color BackgoundColor_Selected { get; set; } = Color.FromHex("#3874D6");


        public static XButtonFlex AddButtonStyle1(int id, string title, string shortTitle = "")
        {
            var button = new XButtonFlex();
            {
                button.Text = title;
                button._textLong = title;
                button.TextShort = shortTitle;
                button.VerticalOptions = LayoutOptions.FillAndExpand;
                button.AutomationId = id.ToString();
                button.Radius = 4;
                button.BorderColor = Color.FromHex("#AEBCD0");

                button.TextColor_Unselected = Color.FromHex("#4C4D4C");
                button.BackgroundColor_Unselected = Color.White;
            }

            return button;
        }

        public static XButtonFlex AddButtonStyle2(int id, string title, string shortTitle = "")
        {
            var button = new XButtonFlex();
            {
                button.Text = title;
                button._textLong = title;
                button.TextShort = shortTitle;
                button.VerticalOptions = LayoutOptions.FillAndExpand;
                button.AutomationId = id.ToString();
                button.Radius = 4;
                button.BorderColor = Color.FromHex("#AEBCD0");
                button.TextColor = Color.Black;
                button.TextColor_Unselected = Color.FromHex("#4C4D4C");
                button.BackgroundColor_Unselected = Color.White;
            }

            return button;
        }

    }
}