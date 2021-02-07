using System.Collections.Generic;
using System.Linq;
using Xamarin.Essentials;
using Xamarin.Forms;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class PageRentalsByYears : Page
    {
        private readonly List<RentPayment> list = new();
        private readonly RentBuildings building;
        public string Name = "";
        public int Year = -1;

        public PageRentalsByYears(RentBuildings building)
        {
            this.building = building;
            this.AddToolBarButtonSetting();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (!seenOnce)
            {
                this.Title = building.Name;
                this.ShowPageAsBusy();
                LoadList();
                seenOnce = true;
            }
        }

        private void LoadList()
        {
            this.list.Clear();

            var TheList = new FlexLayout
            {
                Wrap = FlexWrap.Wrap,
                Direction = FlexDirection.Row,
                JustifyContent = FlexJustify.Center,
                AlignItems = FlexAlignItems.Center,
                AlignContent = FlexAlignContent.Start,
                Padding = new Thickness(8)
            };

            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            var isSmallDevice = App.IsSmallDevice();


            var allYearsTotalForThisBuilding = this.building.Years.OrderByDescending(kvp => kvp.Key);

            foreach (var yearDataForThisBuilding in allYearsTotalForThisBuilding)
            {
                var card = new Frame
                {
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.FillAndExpand,
                    BackgroundColor = Color.White,
                    HasShadow = true,
                    Margin = new Thickness(5, 5, 5, 0),
                    Padding = new Thickness(20),
                };

                if (isSmallDevice)
                {
                    int widthOfCards = (int)(mainDisplayInfo.Width / mainDisplayInfo.Density);
                    int totalCardsThatCanFitAtMinWidth = widthOfCards / 240;
                    if (totalCardsThatCanFitAtMinWidth > 2)
                    {
                        widthOfCards /= totalCardsThatCanFitAtMinWidth;
                    }
                    card.WidthRequest = widthOfCards - 10;
                }
                else
                {
                    card.WidthRequest = 300;
                }

                // Year Numbers
                {
                    var rows = new StackLayout()
                    {
                        Orientation = StackOrientation.Vertical,
                        HorizontalOptions = LayoutOptions.FillAndExpand,
                    };

                    var lastRow = CreateViewCaptionValue("#" + yearDataForThisBuilding.Value.Year.ToString(), null, yearDataForThisBuilding.Value.TotalProfit, null);
                    rows.Children.Add(lastRow);

                    foreach (var department in yearDataForThisBuilding.Value.Departments)
                    {
                        rows.Children.Add(CreateViewCaptionValue(
                            department.Name,
                            null,
                            department.Total,
                            async () =>
                            {
                                var filter = new Filter
                                {
                                    Year = department.Year
                                };
                                department.DepartmentCategory.GetDecendentIds(filter.CategoryIds);
                                await Navigation.PushAsync(new PageTransactions(filter));
                            }
                            ));
                    }

                    card.Content = rows;
                }

                TheList.Children.Add(card);
            }

            this.Content = new ScrollView
            {
                Content = TheList
            };

        }
    }
}