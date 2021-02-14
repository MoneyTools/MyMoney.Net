using Xamarin.Forms;

namespace XMoney
{
    public class Dock : ContentView
    {
        private readonly StackLayout _stack = new();
        private readonly StackLayout _stackFilter = new();
        public Button BackButton = new();
        public Label Title = new();
        public Label FilterText = new();

        public Dock()
        {

            this._stack.Orientation = StackOrientation.Vertical;

            this.Content = _stack;
            this.HorizontalOptions = LayoutOptions.FillAndExpand;
            this.VerticalOptions = LayoutOptions.FillAndExpand;
            this.Init();
        }


        //          0                1
        //    +------------------------------------------------+
        //  0 + < Back    |       Title
        //    +------------------------------------------------+
        //  1 +           |
        //    +------------------------------------------------+
        private void Init()
        {
            var stackBackAndTitle = new StackLayout
            {
                HorizontalOptions = LayoutOptions.FillAndExpand,
                BackgroundColor = Color.Red,
                Orientation = StackOrientation.Horizontal
            };
            stackBackAndTitle.Children.Add(this.BackButton);
            stackBackAndTitle.Children.Add(this.Title);
            stackBackAndTitle.Orientation = StackOrientation.Horizontal;


            Title.HorizontalTextAlignment = TextAlignment.Center;
            Title.HorizontalOptions = LayoutOptions.CenterAndExpand;

            _stack.Children.Add(stackBackAndTitle);

            this.FilterText.Text = "Filter";
            _stackFilter.Children.Add(this.FilterText);
        }

        public void ShowFilter(bool value)
        {
            if (value)
            {
                _stack.Children.Add(_stackFilter);
            }
            else
            {
                _ = _stack.Children.Remove(_stackFilter);
            }
        }
    }
}
