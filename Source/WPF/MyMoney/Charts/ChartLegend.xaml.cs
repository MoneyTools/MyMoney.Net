using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Walkabout.Utilities;

namespace Walkabout.Charts
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class ChartLegend : UserControl
    {
        DelayedActions actions = new DelayedActions();
        ControlTemplate swatchTemplate = null;

        class Row
        {
            public ToggleButton button;
            public TextBlock label;
            public TextBlock value;
            public ChartDataValue item;
        }

        List<Row> elements = new List<Row>();

        public ChartLegend()
        {
            this.InitializeComponent();
            IsVisibleChanged += this.OnVisibleChanged;
        }

        public event EventHandler<ChartDataValue> Toggled;

        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.OnDelayedUpdate();
        }

        public ChartDataSeries DataSeries
        {
            get { return (ChartDataSeries)this.GetValue(DataSeriesProperty); }
            set { this.SetValue(DataSeriesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DataSeries.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DataSeriesProperty =
            DependencyProperty.Register("DataSeries", typeof(ChartDataSeries), typeof(ChartLegend), new PropertyMetadata(null, new PropertyChangedCallback(OnDataSeriesChanged)));

        private static void OnDataSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChartLegend)d).OnDelayedUpdate();
        }

        private void OnDelayedUpdate()
        {
            this.actions.StartDelayedAction("update", this.UpdateLegend, TimeSpan.FromMilliseconds(10));
        }

        private void UpdateLegend()
        {
            if (double.IsNaN(this.ActualWidth))
            {
                return;
            }
            double w = this.ActualWidth - (this.Padding.Left + this.Padding.Right);
            double h = this.ActualHeight - (this.Padding.Top + this.Padding.Bottom);
            if (w < 0 || h < 0 || this.Visibility != Visibility.Visible)
            {
                return;
            }

            this.LegendGrid.Children.Clear();

            if (this.DataSeries == null || this.DataSeries.Values == null)
            {
                return;
            }

            if (this.swatchTemplate == null)
            {
                this.swatchTemplate = (ControlTemplate)this.FindResource("ChartLegendFilterControlTemplate");
                if (this.swatchTemplate == null)
                {
                    throw new Exception("Cannot find required resource ChartLegendFilterControlTemplate");
                }
            }

            int index = 0;
            var rows = this.DataSeries.Values.Count;
            for (index = 0; index < rows; index++)
            {
                var dv = this.DataSeries.Values[index];
                this.AddRow(index, dv);
            }

            while (this.elements.Count > index)
            {
                this.RemoveRow(index);
            }
        }

        private void RemoveRow(int index)
        {
            var ui = this.elements[index];
            this.LegendGrid.RowDefinitions.RemoveAt(index);
            if (ui.button != null)
            {
                this.LegendGrid.Children.Remove(ui.button);
            }
            this.LegendGrid.Children.Remove(ui.label);
            this.LegendGrid.Children.Remove(ui.value);
            this.elements.RemoveAt(index);
        }

        private void AddRow(int index, ChartDataValue dv)
        {
            Row ui = null;

            if (this.LegendGrid.RowDefinitions.Count <= index)
            {
                this.LegendGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            }

            if (this.elements.Count > index)
            {
                // reuse previous elements!
                ui = this.elements[index];
            }
            else
            {
                ui = new Row();
                if (dv.Color.HasValue)
                {
                    var c = dv.Color.Value;
                    ToggleButton colorSwatch = new ToggleButton();
                    colorSwatch.Template = this.swatchTemplate;
                    colorSwatch.Margin = new Thickness(2);
                    colorSwatch.Width = 16;
                    colorSwatch.Height = 16;
                    colorSwatch.DataContext = dv;
                    ui.button = colorSwatch;
                }

                TextBlock label = new TextBlock() { Text = dv.Label, Margin = new Thickness(5, 2, 5, 2) };
                ui.label = label;

                TextBlock total = new TextBlock() { Text = dv.Value.ToString("C0"), Margin = new Thickness(5, 2, 5, 2), HorizontalAlignment = HorizontalAlignment.Right };
                ui.value = total;

                this.elements.Add(ui);
            }

            ui.item = dv;

            if (ui.button != null)
            {
                var c = dv.Color.Value;
                HlsColor hls = new HlsColor(c);
                ui.button.Background = new SolidColorBrush(c);
                if (hls.Luminance < 0.5)
                {
                    ui.button.Foreground = Brushes.White;
                }
                else
                {
                    ui.button.Foreground = Brushes.Black;
                }

                ui.button.DataContext = dv;
                Grid.SetRow(ui.button, index);
                Grid.SetColumn(ui.button, 0);
                this.LegendGrid.Children.Add(ui.button);
                ui.button.Checked -= this.OnColorSwatchToggled;
                ui.button.Unchecked -= this.OnColorSwatchToggled;
                ui.button.IsChecked = dv.Hidden;
                ui.button.Checked += this.OnColorSwatchToggled;
                ui.button.Unchecked += this.OnColorSwatchToggled;
            }

            ui.label.Text = dv.Label;
            Grid.SetRow(ui.label, index);
            Grid.SetColumn(ui.label, 1);
            this.LegendGrid.Children.Add(ui.label);

            ui.value.Text = dv.Value.ToString("C0");
            Grid.SetRow(ui.value, index);
            Grid.SetColumn(ui.value, 2);
            this.LegendGrid.Children.Add(ui.value);
        }

        private void OnColorSwatchToggled(object sender, RoutedEventArgs e)
        {
            if (Toggled != null && sender is ToggleButton button)
            {
                var dv = (ChartDataValue)button.DataContext;
                dv.Hidden = button.IsChecked == true;
                Toggled(this, dv);
            }
        }
    }
}
