using System.Collections.Generic;
using System.Diagnostics;
using Xamarin.Forms;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class PageCategoryDetails : Page
    {
        private readonly Categories category;

        public PageCategoryDetails(Categories category)
        {
            this.category = category;
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
                int horizontalMargins = IsNarrow() ? 20 : 40;

                var stack = new StackLayout()
                {
                    Padding = new Thickness(horizontalMargins, 0, horizontalMargins, 0),
                    Orientation = StackOrientation.Vertical,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                };
                scroll.Content = stack;

                this.AddSubCategory(stack, this.category);

                stack.Children.Add(new Label { Text = this.category.TypeAsText, TextColor = Color.Black, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
                stack.Children.Add(new Label { Text = this.category.Description, TextColor = Color.Black, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
                stack.Children.Add(new BoxView { BackgroundColor = Color.FromHex(this.category.Color), HeightRequest = 10, WidthRequest = 20, Margin = new Thickness(0, 10, 0, 0) });
                stack.Children.Add(CreateViewHeaderCaptionAndValue("Balance", this.category.AmountAsText));
            }

            this.Content = scroll;
        }

        private void AddSubCategory(StackLayout stack, Categories category)
        {
            var listOfDecendentIds = new List<int>();
            category.GetDecendentIds(listOfDecendentIds);

            foreach (var id in listOfDecendentIds)
            {
                var c = Categories.Get(id);
                if (c != null)
                {
                    string name = c.Name;
                    if (Debugger.IsAttached)
                    {
                        name += "                #" + c.Id;
                    }
                    stack.Children.Add(new Label { Text = name, HorizontalTextAlignment = TextAlignment.Start, TextColor = Color.DarkBlue, FontSize = 20, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 5, 0, 5) });
                }
            }
        }
    }
}
