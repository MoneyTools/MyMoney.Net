using System;
using System.Collections.Generic;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.PlatformConfiguration;
using Xamarin.Forms.PlatformConfiguration.iOSSpecific;

namespace XMoney.Views
{
    public class Page : ContentPage
    {
        protected bool seenOnce = false;

        public Page()
        {
            this.BackgroundColor = Color.White;
            
            Xamarin.Forms.NavigationPage.SetHasNavigationBar(this, true);
            
            //NavigationPage.SetTitleFont(this, Font.SystemFontOfSize(NamedSize.Micro));

        }


        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {

                if (App.IsInLandscapeMode())
                {
                    var safeInsets = On<iOS>().SafeAreaInsets();
                    safeInsets.Top = 0;
                    safeInsets.Bottom = 0;
                    Padding = safeInsets;
                }
                else
                {
                    Padding = new Thickness(0);
                }
            }
        }

        public ToolbarItem AddToolBarButtonSetting()
        {
            var toolbarItem = new ToolbarItem
            {
                Text = "⚙️"
            };

            toolbarItem.Clicked += async (object sender, EventArgs e) =>
            {
                await Navigation.PushAsync(new PageSettings());
            };

            this.ToolbarItems.Add(toolbarItem);
            return toolbarItem;
        }

        public ToolbarItem AddToolBarButtonInfo()
        {
            var toolbarItem = new ToolbarItem
            {
                Text = "Info"
            };

            this.ToolbarItems.Add(toolbarItem);
            return toolbarItem;
        }

        public Xamarin.Forms.SearchBar FormSearchBar;
        public Xamarin.Forms.SearchBar AddSearchBar(Layout<View> view, int row = 0)
        {
            this.FormSearchBar = new Xamarin.Forms.SearchBar { Placeholder = "Filter...", HeightRequest = 24, BackgroundColor = Color.DimGray, TextColor = Color.Black, PlaceholderColor = Color.DarkGray };
            this.FormSearchBar.TextChanged += SearchBarTextChanged;
            Grid.SetRow(this.FormSearchBar, row);
            view.Children.Add(this.FormSearchBar);
            return this.FormSearchBar;
        }

        public Label FormFilterBar;
        public Label AddFilterBar(Layout<View> view, int row = 0)
        {
            this.FormFilterBar = new Label() { HorizontalOptions = LayoutOptions.FillAndExpand, BackgroundColor = Color.DimGray, TextColor = Color.Black, HorizontalTextAlignment = TextAlignment.Center };
            view.Children.Add(this.FormFilterBar);
            Grid.SetRow(this.FormFilterBar, row);
            return this.FormFilterBar;
        }

        public string filterText = "";
        private System.Timers.Timer timerForLoadingList = null;

        private void SearchBarTextChanged(object sender, TextChangedEventArgs e)
        {
            this.filterText = e.NewTextValue;
            if (this.filterText == null)
            {
                this.filterText = "";
            }

            if (this.timerForLoadingList == null)
            {
                this.timerForLoadingList = new System.Timers.Timer
                {
                    Interval = 1500,
                    Enabled = true
                };

                this.timerForLoadingList.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
                {
                    this.timerForLoadingList.Stop();
                    MainThread.BeginInvokeOnMainThread(() =>
                   {
                       this.OnSearchBarTextChanged();
                   });
                };
            }

            // reset the timer
            this.timerForLoadingList.Stop();
            this.timerForLoadingList.Start();
        }

        public virtual void OnSearchBarTextChanged()
        {
            // Derive Pages should now react to the user text change
        }


        public XButton AddButton(Layout<View> view, string value, Action onClicked = null)
        {
            var element = new XButton
            {
                Text = value,
                HeightRequest = 30,
                VerticalOptions = LayoutOptions.Center,
            };

            if (onClicked != null)
            {
                element.Clicked += (sender, e) =>
                {
                    onClicked();
                };
            }
            view.Children.Add(element);
            return element;
        }

        public XButtonCurrency AddButton(StackLayout stack, decimal value, Action onClicked = null)
        {
            var element = new XButtonCurrency
            {
                Value = value,
                HeightRequest = 30,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 100,
                FontSize = 12,
            };

            if (onClicked != null)
            {
                element.Clicked += (sender, e) =>
                {
                    onClicked();
                };
            }
            stack.Children.Add(element);
            return element;
        }

        public XButtonFlex AddButton(Grid grid, string value, int row, int column, int columnSpan = 1, Action onClicked = null)
        {
            var element = new XButtonFlex
            {
                Text = value,
                HeightRequest = 30,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 100,
                BackgroundColor = Color.Pink,
                //BorderColor = Color.DarkGray,                
                TextColor = Color.Black,
                FontSize = 10
            };

            if (onClicked != null)
            {
                element.Clicked += (sender, e) =>
                {
                    onClicked();
                };
            }

            this.AddElement(grid, element, row, column, columnSpan);
            return element;

        }

        public XButtonCurrency AddButton(Grid grid, decimal value, int row, int column, int columnSpan = 1, Action onClicked = null)
        {
            var element = new XButtonCurrency
            {
                Value = value,
                HeightRequest = 30,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 100,

            };

            if (onClicked != null)
            {
                element.Clicked += (sender, e) =>
                {
                    onClicked();
                };
            }

            this.AddElement(grid, element, row, column, columnSpan);
            return element;
        }

        public XButtonCurrency AddButtonCurrency(StackLayout stack, decimal value, Action onClicked = null)
        {
            var button = this.AddButton(stack, value, onClicked);

            button.HorizontalOptions = LayoutOptions.EndAndExpand;
            return button;
        }

        public void AddElement(Layout<View> layoutView, View element, int row = 0, int column = 0, int columnSpan = 1)
        {
            layoutView.Children.Add(element);
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            Grid.SetColumnSpan(element, columnSpan);
        }

        public Label AddElementText(StackLayout stack, string text)
        {
            var element = new Label { Text = text };
            element.TextColor = Color.Black;
            stack.Children.Add(element);
            return element;
        }

        public Label AddElementText(Grid grid, string text, int row, int column, int columnSpan = 1)
        {
            var element = new Label { Text = text };
            element.TextColor = Color.Black;
            this.AddElement(grid, element, row, column, columnSpan);
            return element;
        }

        public XButtonCurrency AddButtonCurrency(Grid grid, decimal value, int row, int column, int columnSpan = 1, Action onClicked = null)
        {
            var button = this.AddButton(grid, value, row, column, columnSpan, onClicked);
            button.HorizontalOptions = LayoutOptions.EndAndExpand;
            return button;
        }

        public View CreateViewCaptionValue(string caption = "", string centerText = null, object value = null, Action onClicked = null)
        {
            var bigCaption = caption.StartsWith("#");

            var view = new FlexLayout()
            {
                HorizontalOptions = LayoutOptions.FillAndExpand,

                AlignContent = FlexAlignContent.Stretch,
            };

            bool isDeviceNarrow = App.IsSmallDevice();
            if (isDeviceNarrow)
            {
                view.Direction = FlexDirection.Column;
                view.AlignItems = FlexAlignItems.Stretch;
            }
            else
            {
                view.JustifyContent = FlexJustify.SpaceBetween;
                view.AlignItems = FlexAlignItems.Center;
                view.Wrap = FlexWrap.Wrap;
                view.Direction = FlexDirection.Row;
            }

            // Caption
            {
                bool centerTextHorizontally = false;
                if (caption.StartsWith("^"))
                {
                    centerTextHorizontally = true;
                    caption = caption.TrimStart('^');
                }
                var lableCaption = new Label()
                {
                    Text = caption.TrimStart('#'),
                    TextColor = Color.Black,
                    HorizontalOptions = LayoutOptions.StartAndExpand,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = centerTextHorizontally ? TextAlignment.Center : TextAlignment.Start,
                    VerticalTextAlignment = TextAlignment.Center,
                    FontAttributes = bigCaption ? FontAttributes.Bold : FontAttributes.None,
                    HeightRequest = 50,
                    LineBreakMode = LineBreakMode.NoWrap,
                };
                if (bigCaption)
                {
                    lableCaption.FontSize *= 1.20;
                }
                FlexLayout.SetGrow(lableCaption, 1);
                view.Children.Add(lableCaption);
            }

            // Center text
            if (centerText != null)
            {
                var lableCaption = new Label()
                {
                    Text = centerText,
                    TextColor = Color.Black,
                    HorizontalOptions = LayoutOptions.StartAndExpand,
                    VerticalOptions = LayoutOptions.StartAndExpand,
                    FontSize = 12
                };
                FlexLayout.SetGrow(lableCaption, 3);
                view.Children.Add(lableCaption);
            }

            // Value
            {
                var buttonValue = new XButton()
                {
                    HorizontalOptions = LayoutOptions.EndAndExpand,
                    //HeightRequest = 50,
                };
                FlexLayout.SetGrow(buttonValue, 2);
                if (bigCaption)
                {
                    buttonValue.FontSize *= 1.30;
                }

                if (value is decimal or double or float)
                {
                    buttonValue.Text = ((decimal)value).ToString("C");
                    buttonValue.TextColor = MyColors.GetCurrencyColor((decimal)value);
                    buttonValue.SetAsCurrenty();
                }
                else
                {
                    buttonValue.Text = value.ToString();
                }

                if (onClicked == null)
                {
                    buttonValue.BackgroundColor = Color.Transparent;
                    buttonValue.BorderColor = Color.Transparent;

                }
                else
                {
                    buttonValue.Clicked += (sender, e) =>
                    {
                        onClicked();
                    };
                }
                view.Children.Add(buttonValue);
            }

            return view;
        }

        public View CreateViewHeaderCaptionAndValue(string caption = "", decimal value = 0)
        {
            return CreateViewHeaderCaptionAndValue(caption, value.ToString("C"), MyColors.GetCurrencyColor(value));
        }

        public View CreateViewHeaderCaptionAndValue(string caption, string value, Color? colorForValue = null)
        {

            var stack = new FlexLayout()
            {
                Direction = FlexDirection.Row,
                Wrap = FlexWrap.Wrap,
                JustifyContent = FlexJustify.SpaceBetween,
                AlignContent = FlexAlignContent.Center,
                AlignItems = FlexAlignItems.Center,
                //HeightRequest = 80,
                Padding = new Thickness(5),
            };

            // Caption
            {
                var lableCaption = new Label()
                {
                    Text = caption,
                    TextColor = Color.Black,
                    HorizontalOptions = LayoutOptions.StartAndExpand,
                    VerticalOptions = LayoutOptions.Center,
                    FontAttributes = FontAttributes.Bold,
                };
                lableCaption.FontSize *= 1.10;
                stack.Children.Add(lableCaption);
            }

            // Value
            {
                var labelForValue = new Label()
                {
                    Text = value,
                    TextColor = colorForValue == null ? Color.Black : (Color)colorForValue,
                    FontAttributes = FontAttributes.Bold,
                };
                labelForValue.FontSize *= 1.10;
                stack.Children.Add(labelForValue);
            }

            return stack;
        }

        public View CreateSeparatorHorizontal(int marginVertical = 2, int marginHorizontal = 0)
        {
            var box = new BoxView()
            {
                HeightRequest = 2,
                BackgroundColor = Color.DarkGray,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                Margin = new Thickness(marginHorizontal, marginVertical, marginHorizontal, marginVertical),
            };
            return box;
        }

        private readonly ActivityIndicator spinner = new()
        {
            VerticalOptions = LayoutOptions.CenterAndExpand,
            HorizontalOptions = LayoutOptions.CenterAndExpand,
            HeightRequest = 60,
            WidthRequest = 60,
            Color = Color.Blue,
            IsRunning = true,
            
        };

        public void ShowPageAsBusy(string optionalText="")
        {
            var view = new StackLayout() { Margin = new Thickness(20) };
            view.Children.Add(new Label() { Text = optionalText });
            view.Children.Add(this.spinner);
            this.Content = view;
        }

        public async void JumpPopup(List<string> listButtonText, List<Action> listOfButtonAction)
        {
            string[] textForButtons = listButtonText.ToArray();

            //Deselect Item
            string buttonTextSelected = await DisplayActionSheet("Transaction", "Done", null, textForButtons);

            var indexFound = listButtonText.IndexOf(buttonTextSelected);
            if (indexFound != -1)
            {
                listOfButtonAction[indexFound]();
            }
        }
    }
}
