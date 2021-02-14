using Xamarin.Forms;

namespace XMoney
{
    public class ViewButtonList : ContentView
    {
        private readonly Grid _grid = new();

        public ViewButtonList()
        {
            _grid.Margin = 0;
            _grid.HorizontalOptions = LayoutOptions.FillAndExpand;
            _grid.VerticalOptions = LayoutOptions.FillAndExpand;
            _grid.ColumnSpacing = 0;

            this.Content = _grid;
            this.HorizontalOptions = LayoutOptions.FillAndExpand;
            this.VerticalOptions = LayoutOptions.FillAndExpand;
        }

        private void ResetGrid()
        {
            // add a new Row
            {
                _grid.RowDefinitions = new RowDefinitionCollection();
                var rowDef = new RowDefinition
                {
                    Height = new GridLength(1.0, GridUnitType.Star)
                };
                _grid.RowDefinitions.Add(rowDef);
            }

            // add column
            {
                _grid.ColumnDefinitions = new ColumnDefinitionCollection();
                _grid.RowSpacing = 0;
                _grid.ColumnSpacing = 0;
            }
        }

        public void Clear()
        {
            _grid.Children.Clear();
            ResetGrid();
        }

        public XButtonFlex AddButton(string title, int percentage, int id = -1)
        {
            return AddButton(title, "", percentage, id);
        }

        public XButtonFlex AddButton(string title, string titleShort, int percentage, int id = -1)
        {
            // add another Column Definition to the Grid
            {
                var colDef = new ColumnDefinition
                {
                    Width = new GridLength(percentage, GridUnitType.Star)
                };

                _grid.ColumnDefinitions.Add(colDef);
            }

            // Create a new Button
            XButtonFlex button = XButtonFlex.AddButtonStyle1(id, title, titleShort);
            button.Selected = false;

            // Add the button to the grid
            {
                _grid.Children.Add(button);
                Grid.SetRow(button, 0);
                Grid.SetColumn(button, _grid.Children.Count - 1);
            }

            return button;
        }

        public void Selected(object objToSelected)
        {
            foreach (View form in _grid.Children)
            {
                if (form is XButtonFlex button)
                {
                    button.Selected = button == objToSelected;
                }
            }
        }

        public void SelectedByAutomationId(int id)
        {
            foreach (View form in _grid.Children)
            {
                if (form is XButtonFlex button)
                {
                    button.Selected = button.AutomationId == id.ToString();
                }
            }
        }
    }
}