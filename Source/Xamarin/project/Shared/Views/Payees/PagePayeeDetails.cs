using Xamarin.Forms;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class PagePayeeDetails : Page
    {
        private readonly Payees payee;

        public PagePayeeDetails(Payees payee)
        {
            this.payee = payee;
            this.Title = "Info";
            this.AddToolBarButtonSetting();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!seenOnce)
            {
                this.ShowView();
                seenOnce = true;
            }
        }

        private void ShowView()
        {

            var scroll = new ScrollView()
            {
                BackgroundColor = Color.White
            };

            {
                int horizontalMargins = App.IsSmallDevice() ? 20 : 40;

                var stack = new StackLayout()
                {
                    Padding = new Thickness(horizontalMargins, 0, horizontalMargins, 0),
                    Orientation = StackOrientation.Vertical,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                };
                scroll.Content = stack;

                stack.Children.Add(new Label { Text = this.payee.Name, HorizontalTextAlignment = TextAlignment.Center, TextColor = Color.DarkBlue, FontSize = 20, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 5, 0, 5) });
                stack.Children.Add(CreateViewHeaderCaptionAndValue(this.payee.Quantity.ToString(), this.payee.AmountAsText));
            }

            this.Content = scroll;
        }
    }
}
