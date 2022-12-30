using Xamarin.Forms;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class PageTransacationDetails : Page
    {
        private readonly Transactions transaction;

        public PageTransacationDetails(Transactions transaction)
        {
            this.transaction = transaction;
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
                BackgroundColor = Color.White,
            };

            {
                int horizontalMargins = IsNarrow() ? 5 : 40;

                var stack = new StackLayout()
                {
                    Padding = new Thickness(horizontalMargins, 10, horizontalMargins, 0),
                    Orientation = StackOrientation.Vertical,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                };

                scroll.Content = stack;

                // Account
                stack.Children.Add(
                    this.CreateViewCaptionValue(
                        "#Account",
                        null,
                        this.transaction.AccountAsText,
                        async () =>
                        {
                            var filter = new Filter { AccountId = this.transaction.Account };
                            await Navigation.PushAsync(new PageTransactions(filter));
                        })
                    );

                // Date                
                stack.Children.Add(
                    this.CreateViewCaptionValue(
                        "#Date",
                        null,
                        this.transaction.DateAsText,
                        async () =>
                        {
                            var filter = new Filter { DateText = this.transaction.DateAsText };
                            await Navigation.PushAsync(new PageTransactions(filter));
                        })
                    );

                // Amount

                stack.Children.Add(
                   this.CreateViewCaptionValue(
                          "#Amount",
                          null,
                          this.transaction.Amount,
                          async () =>
                          {
                              var filter = new Filter { Amount = this.transaction.Amount };
                              await Navigation.PushAsync(new PageTransactions(filter));
                          })
                   );


                // Payee
                stack.Children.Add(
                    this.CreateViewCaptionValue(
                        "#Payee",
                        null,
                        this.transaction.PayeeAsText,
                        async () =>
                        {
                            var filter = new Filter { PayeeId = this.transaction.Payee };
                            await Navigation.PushAsync(new PageTransactions(filter));
                        })
                    );

                // Category
                stack.Children.Add(
                    this.CreateViewCaptionValue(
                        "#Category",
                        null,
                        this.transaction.CategoryAsText,
                        async () =>
                        {
                            var filter = new Filter();
                            filter.CategoryIds.Add(this.transaction.Category);
                            await Navigation.PushAsync(new PageTransactions(filter));
                        })
                );

                // Memo
                stack.Children.Add(new Label { Text = this.transaction.Memo, TextColor = Color.Gray, FontAttributes = FontAttributes.Italic });


                // Splits
                if (this.transaction.IsSplit)
                {
                    stack.Children.Add(CreateSeparatorHorizontal(10));

                    var splits = Splits.GetSplitsForTransaction(this.transaction.Id);

                    foreach (var split in splits)
                    {
                        stack.Children.Add(CreateViewHeaderCaptionAndValue(split.CategoryAsText, split.Amount));
                        stack.Children.Add(new Label { Text = split.Memo, TextColor = Color.Gray, FontAttributes = FontAttributes.Italic, Margin = new Thickness(0, 0, 0, 5) });
                    }
                }
            }

            this.Content = scroll;
        }
    }
}
