using Xamarin.Forms;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class PageAccountDetails : Page
    {
        private readonly Accounts account;

        public PageAccountDetails(Accounts account)
        {
            this.account = account;
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


                stack.Children.Add(new Label { Text = this.account.Name, HorizontalTextAlignment = TextAlignment.Center, TextColor = Color.DarkBlue, FontSize = 20, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 5, 0, 5) });
                stack.Children.Add(CreateViewHeaderCaptionAndValue("Account Id", this.account.AccountId));
                stack.Children.Add(new Label { Text = this.account.TypeAsText, TextColor = Color.Black, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
                stack.Children.Add(new Label { Text = this.account.Description, TextColor = Color.Black, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
                stack.Children.Add(new Label { Text = this.account.IsClosed ? "Is Closed" : "Is Active", TextColor = Color.Black, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
                stack.Children.Add(CreateViewHeaderCaptionAndValue("Balance Open", this.account.OpeningBalance));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("Balance Last", this.account.Balance));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("Currency", this.account.Currency));
            }

            this.Content = scroll;
        }
    }
}
