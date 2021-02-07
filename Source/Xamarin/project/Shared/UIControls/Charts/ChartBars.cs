using System;
using Xamarin.Forms;

namespace xMoney.UIControls
{
    public class ChartBars : Chart
    {
        private int HeightRowPositveAndNegative { get; set; }
        private const int columnWidth = 40;

        private readonly StackLayout stackMainVertical;

        private readonly StackLayout stackRowLabelTop;
        private readonly StackLayout stackRowPositive;
        private readonly StackLayout stackRowNegative;
        private readonly StackLayout stackRowLabelBottom;

        public ChartBars()
        {
            this.stackMainVertical = new StackLayout()
            {
                Orientation = StackOrientation.Vertical,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Spacing = 0
            };

            Content = this.stackMainVertical;

            // Top Row
            {
                this.stackRowLabelTop = new StackLayout()
                {
                    Orientation = StackOrientation.Horizontal
                };
                this.stackMainVertical.Children.Add(this.stackRowLabelTop);
            }

            // Center Row
            {
                this.stackRowPositive = new StackLayout()
                {
                    Orientation = StackOrientation.Horizontal,
                    //BackgroundColor = Color.LightGray,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.FillAndExpand,
                };
                this.stackMainVertical.Children.Add(this.stackRowPositive);

                this.stackRowNegative = new StackLayout()
                {
                    Orientation = StackOrientation.Horizontal,
                    //BackgroundColor = Color.LightGray,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.FillAndExpand,
                };
                this.stackMainVertical.Children.Add(this.stackRowNegative);
            }

            // Bottom Row
            {
                this.stackRowLabelBottom = new StackLayout()
                {
                    Orientation = StackOrientation.Horizontal,
                };
                this.stackMainVertical.Children.Add(this.stackRowLabelBottom);
            }
        }

        public void SetHeight(int height)
        {
            this.HeightRequest = height;
            height -= 1;
            this.HeightRowPositveAndNegative = height / 2;
        }

        public Action<ChartEntry> ActionWhenBarClicked;

        public override void Render()
        {
            this.stackRowPositive.Children.Clear();
            this.stackRowNegative.Children.Clear();

            this.EvalMinMaxTotal();

            foreach (var entry in this.chartData)
            {
                // On Clicked
                var tapGestureRecognizer = new TapGestureRecognizer();
                tapGestureRecognizer.Tapped += (s, e) =>
                {
                    if (this.ActionWhenBarClicked != null)
                    {
                        ActionWhenBarClicked(entry);
                    }
                };

                // Top Text
                {
                    var label = new Label
                    {
                        Text = entry.TextTop,
                        FontSize = 9,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = entry.IsGapColumn ? Color.Gray : Color.Black,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalOptions = LayoutOptions.Center,
                        LineBreakMode = LineBreakMode.NoWrap,
                        Margin = 0,
                        WidthRequest = columnWidth,
                    };
                    this.stackRowLabelTop.Children.Add(label);
                    label.GestureRecognizers.Add(tapGestureRecognizer);
                }

                // Vertical column Bars
                {
                    // Column Stack Positve Valiue
                    var columnP = new StackLayout()
                    {
                        WidthRequest = columnWidth,
                        VerticalOptions = LayoutOptions.FillAndExpand,
                        Orientation = StackOrientation.Vertical,
                        //BackgroundColor = Color.LightGreen
                    };
                    columnP.GestureRecognizers.Add(tapGestureRecognizer);
                    this.stackRowPositive.Children.Add(columnP);

                    // Column Stack Positve Valiue
                    var columnN = new StackLayout()
                    {
                        WidthRequest = columnWidth,
                        VerticalOptions = LayoutOptions.FillAndExpand,
                        Orientation = StackOrientation.Vertical,
                    };
                    columnN.GestureRecognizers.Add(tapGestureRecognizer);
                    this.stackRowNegative.Children.Add(columnN);


                    var bar = new BoxView()
                    {
                        WidthRequest = columnWidth,
                    };

                    if (entry.IsGapColumn)
                    {
                        // empty column
                    }
                    else
                    {
                        if (entry.Value >= 0)
                        {
                            // POSITIVE GREEN
                            bar.VerticalOptions = LayoutOptions.EndAndExpand;
                            bar.BackgroundColor = Color.DarkGreen;
                            bar.HeightRequest = Math.Max(1, (int)Math.Abs(HeightRowPositveAndNegative * entry.RelativeValueInPercentage));
                            bar.CornerRadius = new CornerRadius(2, 2, 0, 0);

                            columnP.Children.Add(bar);
                        }
                        else
                        {
                            // NEGATIVE RED
                            bar.VerticalOptions = LayoutOptions.StartAndExpand;
                            bar.BackgroundColor = Color.DarkRed;
                            bar.HeightRequest = Math.Max(1, (int)Math.Abs(HeightRowPositveAndNegative * entry.RelativeValueInPercentage));
                            bar.CornerRadius = new CornerRadius(0, 0, 2, 2);

                            columnN.Children.Add(bar);
                        }
                    }
                }

                // Bottom Text
                {
                    var label = new Label
                    {
                        Text = entry.TextBottom,
                        FontSize = 9,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = entry.IsGapColumn ? Color.Gray : Color.Black,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalOptions = LayoutOptions.Center,
                        LineBreakMode = LineBreakMode.NoWrap,
                        Margin = 0,
                        WidthRequest = columnWidth
                    };
                    this.stackRowLabelBottom.Children.Add(label);
                    label.GestureRecognizers.Add(tapGestureRecognizer);
                }
            }
        }
    }
}