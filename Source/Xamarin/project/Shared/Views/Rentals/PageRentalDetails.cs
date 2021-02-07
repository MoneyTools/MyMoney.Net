using Xamarin.Forms;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class PageRentalDetails : Page
    {
        private readonly RentBuildings building;

        public PageRentalDetails(RentBuildings building)
        {
            this.building = building;
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


                stack.Children.Add(new Label { Text = this.building.Name, HorizontalTextAlignment = TextAlignment.Center, TextColor = Color.DarkBlue, FontSize = 20, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 5, 0, 5) });
                stack.Children.Add(new Label { Text = this.building.Address, TextColor = Color.Black, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
                stack.Children.Add(new Label { Text = this.building.Note, TextColor = Color.Black, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });

                stack.Children.Add(CreateViewHeaderCaptionAndValue(this.building.OwnershipName1, this.building.OwnershipPercentage1 + "%"));
                stack.Children.Add(CreateViewHeaderCaptionAndValue(this.building.OwnershipName2, this.building.OwnershipPercentage2 + "%"));

                stack.Children.Add(CreateSeparatorHorizontal(10));

                stack.Children.Add(CreateViewHeaderCaptionAndValue("Estimated Value", this.building.EstimatedValue));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("Land Value", this.building.LandValue));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("Purchase price", this.building.PurchasedPrice));

                stack.Children.Add(CreateSeparatorHorizontal(10));

                stack.Children.Add(CreateViewHeaderCaptionAndValue("Incomes", this.building.TotalIncomes));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("   Category", Categories.GetAsString(this.building.CategoryForIncome)));

                stack.Children.Add(CreateViewHeaderCaptionAndValue("Expenses", this.building.TotalExpenses));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("   Interest", Categories.GetAsString(this.building.CategoryForInterest)));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("   Maintenance", Categories.GetAsString(this.building.CategoryForMaintenance)));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("   Management", Categories.GetAsString(this.building.CategoryForManagement)));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("   Repairs", Categories.GetAsString(this.building.CategoryForRepairs)));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("   Taxes", Categories.GetAsString(this.building.CategoryForTaxes)));
                stack.Children.Add(CreateViewHeaderCaptionAndValue("Profit", this.building.TotalProfits));
            }

            this.Content = scroll;
        }
    }
}
