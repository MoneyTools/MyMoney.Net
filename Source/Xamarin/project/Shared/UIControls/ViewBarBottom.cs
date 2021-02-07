using Xamarin.Forms;

namespace XMoney
{
    public class ViewBarBottom : ContentView
    {
        private readonly StackLayout _stackHorizontal;

        public ViewBarBottom()
        {
            BackgroundColor = Color.FromRgb(33, 33, 33); // Almost pure Black

            _stackHorizontal = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand,
                BackgroundColor = Color.Transparent,
                Padding = new Thickness(4)
            };

            Content = _stackHorizontal;
            switch (Device.RuntimePlatform)
            {
                case Device.Android:
                case Device.iOS:
                    HeightRequest = 40;
                    break;

                case Device.macOS:
                case Device.UWP:
                default:
                    HeightRequest = 300;
                    break;
            }
        }

        public XButtonFlex AddButton(string text, string shortText = "")
        {
            var button = new XButtonFlex
            {
                Text = text,
                TextLong = text,
                TextShort = shortText,
                Margin = 0,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                WidthRequest = 40,
                Opacity = 0.8f
            };

            _stackHorizontal.Children.Add(button);
            return button;
        }

        public void SelectButton(XButtonFlex buttonToSelect)
        {
            foreach (var form in _stackHorizontal.Children)
            {
                if (form is XButtonFlex b)
                {
                    b.Selected = b == buttonToSelect;
                }
            }
        }
    }
}