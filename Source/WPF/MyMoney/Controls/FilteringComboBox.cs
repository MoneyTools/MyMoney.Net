using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Walkabout.Controls
{

    public class FilteringComboBox : ComboBox
    {
        public static RoutedEvent FilterChangedEvent = EventManager.RegisterRoutedEvent("FilterChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilteringComboBox));

        public event RoutedEventHandler FilterChanged;

        ListCollectionView view;

        protected override void OnItemsSourceChanged(System.Collections.IEnumerable oldValue, System.Collections.IEnumerable newValue)
        {
            this.Filter = null;
            this.Items.Filter = null;
            this.view = newValue as ListCollectionView;
            base.OnItemsSourceChanged(oldValue, newValue);
        }

        public Predicate<object> FilterPredicate
        {
            get { return this.view.Filter; }
            set { this.view.Filter = value; }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            TextBox edit = this.Template.FindName("PART_EditableTextBox", this) as TextBox;
            if (edit != null)
            {
                edit.KeyUp -= new System.Windows.Input.KeyEventHandler(this.OnEditKeyUp);
                edit.KeyUp += new System.Windows.Input.KeyEventHandler(this.OnEditKeyUp);
            }
        }

        void OnEditKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            TextBox box = (TextBox)sender;
            string filter = box.Text;
            if (string.IsNullOrEmpty(filter))
            {
                this.Items.Filter = null;
            }
            else if (box.SelectionLength < filter.Length)
            {
                if (box.SelectionStart >= 0)
                {
                    filter = filter.Substring(0, box.SelectionStart);
                }
                this.SetFilter(filter);
            }
        }

        public string Filter
        {
            get; set;
        }

        void SetFilter(string text)
        {
            this.Filter = text;
            var e = new RoutedEventArgs(FilterChangedEvent);
            if (FilterChanged != null)
            {
                FilterChanged(this, e);
            }
            this.RaiseEvent(e);
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
        }

    }


    /// <summary>
    /// Fix the missing WPF ComboBox TextChanged event
    /// </summary>
    public class ComboBox2 : ComboBox
    {

        static ComboBox2()
        {
            TextProperty.OverrideMetadata(typeof(ComboBox2), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnTextChanged)));
        }


        public static RoutedEvent FilterChangedEvent = EventManager.RegisterRoutedEvent("TextChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ComboBox2));

        public event RoutedEventHandler TextChanged;



        private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ComboBox2 combo = sender as ComboBox2;

            if (combo != null)
            {
                if (combo.TextChanged != null)
                {
                    var a = new RoutedEventArgs(FilterChangedEvent);
                    combo.TextChanged(combo, a);
                }
            }
        }

    }

}
