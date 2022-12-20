using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Microsoft.VisualStudio.PerformanceGraph
{
    /// <summary>
    /// Interaction logic for Legend.xaml
    /// </summary>
    public partial class Legend : UserControl
    {
        public Legend()
        {
            this.InitializeComponent();
        }

        public void AddItem(Color color, string name)
        {
            int row = this.LegendGrid.RowDefinitions.Count;
            this.LegendGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });

            Rectangle r = new Rectangle();
            r.Margin = new Thickness(1);
            r.Fill = new SolidColorBrush(color);
            r.Width = 16;
            r.Height = 16;
            Grid.SetColumn(r, 0);
            Grid.SetRow(r, row);

            this.LegendGrid.Children.Add(r);

            TextBlock block = new TextBlock() { Text = name };
            block.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            Grid.SetColumn(block, 1);
            Grid.SetRow(block, row);

            this.LegendGrid.Children.Add(block);
        }

        public void Clear()
        {
            this.LegendGrid.Children.Clear();
            this.LegendGrid.RowDefinitions.Clear();
        }

        private void OnCloseBoxClick(object sender, RoutedEventArgs e)
        {
            this.Visibility = System.Windows.Visibility.Collapsed;
        }
    }
}
