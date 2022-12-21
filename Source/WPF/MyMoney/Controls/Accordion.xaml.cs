using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Walkabout.Utilities;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for Accordion.xaml
    /// </summary>
    public partial class Accordion : UserControl
    {
        const int ExpandAnimationMilliseconds = 250;
        const string nameIdOfQuickFilter = "QuickFilter";
        const string nameIdOfStatusControl = "Status";

        Expander currentlyExpandedExpander;

        public event RoutedEventHandler Expanded;


        public delegate void FilterEventHandler(object sender, string filter);

        public event FilterEventHandler FilterUpdated;

        public Accordion()
        {
            this.InitializeComponent();
        }


        public static Brush GetGlyphBrush(DependencyObject obj)
        {
            return (Brush)obj.GetValue(GlyphBrushProperty);
        }

        public static void SetGlyphBrush(DependencyObject obj, Brush value)
        {
            obj.SetValue(GlyphBrushProperty, value);
        }

        // Using a DependencyProperty as the backing store for GlyphBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GlyphBrushProperty =
            DependencyProperty.RegisterAttached("GlyphBrush", typeof(Brush), typeof(Accordion), new UIPropertyMetadata(Brushes.Black));



        public object Selected
        {
            get { return this.currentlyExpandedExpander.Content; }
            set
            {
                foreach (object item in this.MainGrid.Children)
                {
                    Expander asExpander = item as Expander;
                    if (asExpander != null)
                    {
                        if (asExpander.Content == value)
                        {
                            asExpander.IsExpanded = true;
                            return;
                        }
                    }
                }
                throw new InvalidOperationException("The selected item has to be added first");
            }
        }

        public void Add(string header, string id, object content)
        {
            this.Add(header, id, content, false);
        }

        Dictionary<string, Expander> tabs = new Dictionary<string, Expander>();

        public bool ContainsTab(string name)
        {
            return this.tabs.ContainsKey(name);
        }

        public void RemoveTab(string name)
        {
            if (this.tabs.ContainsKey(name))
            {
                Expander expander = this.tabs[name];
                this.tabs.Remove(name);
                this.MainGrid.Children.Remove(expander);
            }
        }

        public void Add(string header, string id, object content, bool searchBox)
        {

            //-----------------------------------------------------------------
            // The new accordion section is build using an Expander
            //
            Expander expanderToAdd = new Expander();
            this.tabs[header] = expanderToAdd;


            expanderToAdd.Name = id;
            expanderToAdd.IsExpanded = false;
            expanderToAdd.Margin = new Thickness(0, 0, 0, 3);


            // The expander header is a Grid with 2 columns
            Grid expanderHeader = new Grid();
            expanderHeader.Name = "MyHeader";
            ColumnDefinition col1 = new ColumnDefinition();

            expanderHeader.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            expanderHeader.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

            //-----------------------------------------------------------------
            // First column contains the Title of the accordion section
            //
            TextBlock headerText = new TextBlock();
            headerText.Text = header;
            headerText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(headerText, 0);
            expanderHeader.Children.Add(headerText);

            //-----------------------------------------------------------------
            // Second column contains the "Quick-Filter" control
            //
            if (searchBox)
            {
                QuickFilterControl qf = new QuickFilterControl();
                qf.FilterValueChanged += new QuickFilterControl.QuickFilterValueChanged(this.OnFilterValueChanged);
                qf.Name = nameIdOfQuickFilter;
                qf.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                qf.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                Grid.SetColumn(qf, 1);
                expanderHeader.Children.Add(qf);
                qf.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                IContainerStatus iCS = content as IContainerStatus;
                if (iCS != null)
                {
                    TextBlock tb = new TextBlock();
                    tb.Name = nameIdOfStatusControl;
                    tb.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    tb.TextAlignment = TextAlignment.Right;
                    tb.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                    tb.Margin = new Thickness(5, 0, 0, 0);
                    tb.Visibility = System.Windows.Visibility.Visible;
                    Grid.SetColumn(tb, 1);
                    expanderHeader.Children.Add(tb);

                    iCS.SetTextBlock(tb);
                }
            }



            //-----------------------------------------------------------------
            // Set the expander header to be this new Grid we just created
            expanderToAdd.Header = expanderHeader;


            //-----------------------------------------------------------------
            // Now set the content of the expander
            //
            expanderToAdd.Content = content as UIElement;

            expanderToAdd.Expanded += new RoutedEventHandler(this.OnExpanderExpanded);
            expanderToAdd.Collapsed += new RoutedEventHandler(this.OnExpanderCollapsed);
            expanderToAdd.SizeChanged += new SizeChangedEventHandler(this.OnExpanderToAdd_SizeChanged);


            //-----------------------------------------------------------------
            // Add a new row to contain the new Accordion section
            //
            this.MainGrid.RowDefinitions.Count();
            RowDefinition rd = new RowDefinition();
            rd.Height = new GridLength(1, GridUnitType.Auto);
            this.MainGrid.RowDefinitions.Add(rd);

            // and the new Expender to the Accordion Section
            int lastRow = this.MainGrid.Children.Add(expanderToAdd);
            Grid.SetRow(expanderToAdd, lastRow);


        }







        /// <summary>
        /// Accordion top section title was resized
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnExpanderToAdd_SizeChanged(object sender, SizeChangedEventArgs e)
        {

            Expander expander = sender as Expander;

            if (expander != null)
            {

                Grid customHeader = WpfHelper.FindDescendantElement(expander, "MyHeader") as Grid;

                if (customHeader != null)
                {
                    customHeader.Width = expander.ActualWidth - 30; // rough size of the DropDown adornment

                    double width = customHeader.Width - customHeader.ColumnDefinitions[0].ActualWidth - 8;
                    width = Math.Min(width, 144);
                    width = Math.Max(width, 0); ;
                    if (width < 34)
                    {
                        // if it's to small don't show it at all
                        width = 0;
                    }

                    FrameworkElement control = WpfHelper.FindDescendantElement(customHeader, nameIdOfQuickFilter);
                    if (control == null)
                    {
                        control = WpfHelper.FindDescendantElement(customHeader, nameIdOfStatusControl);
                    }

                    if (control != null)
                    {
                        control.Width = width;
                    }
                }
            }
        }






        /// <summary>
        /// Accordion section was Expanded (aka selected)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnExpanderExpanded(object sender, RoutedEventArgs e)
        {
            if (this.currentlyExpandedExpander != null)
            {
                Expander expanderLoosing = this.currentlyExpandedExpander;
                expanderLoosing.IsExpanded = false;
                this.SetRowHeight(this.currentlyExpandedExpander, GridUnitType.Auto);
            }

            this.currentlyExpandedExpander = sender as Expander;

            this.SetRowHeight(sender, GridUnitType.Star);

            if (Expanded != null)
            {
                Expanded.Invoke(this.currentlyExpandedExpander.Content, e);
            }

            QuickFilterControl qf = WpfHelper.FindDescendantElement(this.currentlyExpandedExpander, nameIdOfQuickFilter) as QuickFilterControl;

            if (qf != null)
            {
                qf.Visibility = System.Windows.Visibility.Visible;
            }
        }


        /// <summary>
        /// Accordion section was collapsed, in order words is not longer the selected section
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnExpanderCollapsed(object sender, RoutedEventArgs e)
        {
            if (this.currentlyExpandedExpander != null)
            {
                QuickFilterControl qf = WpfHelper.FindDescendantElement(this.currentlyExpandedExpander, nameIdOfQuickFilter) as QuickFilterControl;

                if (qf != null)
                {
                    qf.Visibility = System.Windows.Visibility.Collapsed;
                }
            }

            this.currentlyExpandedExpander = null;

            this.SetRowHeight(sender, GridUnitType.Auto);


        }

        void OnFilterValueChanged(object sender, string filter)
        {
            if (FilterUpdated != null)
            {
                FilterUpdated.Invoke(this.currentlyExpandedExpander.Content, filter);
            }

        }
        void SetRowHeight(object expander, GridUnitType gu)
        {
            if (expander != null)
            {
                int row = Grid.GetRow(expander as UIElement);
                this.MainGrid.RowDefinitions[row].Height = new GridLength(100, gu);
            }
        }

        void RemoveRow(object expanderToRemove)
        {
            int row = Grid.GetRow(expanderToRemove as UIElement);
            this.MainGrid.RowDefinitions[row].Height = new GridLength(0);
        }

        public void Remove(object content)
        {
            foreach (object item in this.MainGrid.Children)
            {
                Expander asExpander = item as Expander;
                if (asExpander != null)
                {
                    if (asExpander.Content == content)
                    {
                        this.RemoveRow(asExpander);
                        this.MainGrid.Children.Remove(asExpander);
                        return;
                    }
                }
            }

            throw new InvalidOperationException("The item was not removed");
        }

    }

    public interface IContainerStatus
    {
        void SetTextBlock(TextBlock statusControl);
    }
}
