using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Markup;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for DropDownButton.xaml
    /// </summary>
    public partial class DropDownButton : ToggleButton
    {
        public readonly static RoutedEvent DropDownEvent = EventManager.RegisterRoutedEvent("DropDown", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(DropDownButton));
        public readonly static DependencyProperty DropDownProperty = DependencyProperty.Register("DropDown", typeof(ContextMenu), typeof(DropDownButton), new FrameworkPropertyMetadata(null, OnDropDownChanged));
        public readonly static DependencyProperty PopupProperty = DependencyProperty.Register("Popup", typeof(Popup), typeof(DropDownButton), new FrameworkPropertyMetadata(null, OnPopupChanged));
        public readonly static DependencyProperty ButtonProperty = DependencyProperty.Register("Button", typeof(DropDownButton), typeof(DropDownButton));

        NameScope scope = new NameScope();

        public DropDownButton()
        {
            InitializeComponent();
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);
        }

        protected override void OnPreviewLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnPreviewLostKeyboardFocus(e);
            if (!IsKeyboardFocusWithin)
            {
                IsChecked = false;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape)
            {
                IsChecked = false;
            }
        }

        private static void OnPopupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DropDownButton button = (DropDownButton)d;
            Popup popup = (Popup)e.OldValue;
            if (popup != null)
            {
                popup.PreviewKeyDown -= new KeyEventHandler(button.OnPopupKeyDown);
                popup.LostFocus -= new RoutedEventHandler(button.OnPopupLostFocus);
            }
            popup = (Popup)e.NewValue;
            if (popup != null)
            {
                popup.SetValue(ButtonProperty, button);
                popup.DataContext = button.DataContext;
                popup.PreviewKeyDown += new KeyEventHandler(button.OnPopupKeyDown);
                popup.LostFocus += new RoutedEventHandler(button.OnPopupLostFocus);
            }
        }

        private static void OnDropDownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DropDownButton button = (DropDownButton)d; 
            ContextMenu menu = (ContextMenu)e.NewValue;
            if (menu != null)
            {
                menu.DataContext = button.DataContext;
            }
            if (menu != null)
            {
                menu.SetValue(ButtonProperty, button);
            }
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            object data = e.NewValue;
            if (data != null && this.Popup != null)
            {
                this.Popup.DataContext = data;
            }
            if (data != null && this.DropDownMenu != null)
            {
                this.DropDownMenu.DataContext = data;
            }
        }

        /// <summary>
        /// Associate a drop down menu with the button so this menu is shown when the button is clicked.
        /// Popup and ContextMenu are mutually exclusive.
        /// </summary>
        public ContextMenu DropDownMenu
        {
            get { return (ContextMenu)GetValue(DropDownProperty); }
            set { SetValue(DropDownProperty, value); }
        }

        /// <summary>
        /// Get/Set the popup associated with this button.
        /// Popup and ContextMenu are mutually exclusive.
        /// </summary>
        public Popup Popup
        {
            get { return (Popup)GetValue(PopupProperty); }
            set { SetValue(PopupProperty, value); }
        }        

        /// <summary>
        /// Handle the pressed event to open the drop down menu (or popup)
        /// </summary>
        protected override void OnChecked(RoutedEventArgs e)
        {
            base.OnChecked(e);
            if (DropDownMenu != null)
            {
                // If there is a drop-down assigned to this button, then position and display it 
                DropDownMenu.PlacementTarget = this;
                DropDownMenu.Placement = PlacementMode.Bottom;
                DropDownMenu.IsOpen = true;
                RaiseEvent(new RoutedEventArgs(DropDownEvent, this));
            }
            else if (Popup != null)
            {
                Popup.Placement = PlacementMode.Bottom;
                Popup.PlacementTarget = this;
                Popup.IsOpen = true;
                RaiseEvent(new RoutedEventArgs(DropDownEvent, this));
            }
        }

        protected override void OnUnchecked(RoutedEventArgs e)
        {
            base.OnUnchecked(e);
            ClosePopup();
        }

        public void ClosePopup()
        {
            if (DropDownMenu != null)
            {
                DropDownMenu.IsOpen = false;
            }
            else if (Popup != null)
            {
                Popup.IsOpen = false;
            }
        }

        private void OnPopupLostFocus(object sender, RoutedEventArgs e)
        {
            if (!Popup.IsKeyboardFocusWithin)
            {
                IsChecked = false;
            }
        }

        void OnPopupKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                IsChecked = false;
            }
        }

    }


}
