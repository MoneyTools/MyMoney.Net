using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Walkabout.Controls
{
    /// <summary>
    /// This calculator popup defines an attached property that you can add to a TextBox to get a popup calculator attached
    /// to that text box so you can write simple expressions like "3 + (4*10)" and hit enter to get the computed result.
    /// To enable it simply do this to your TextBoxes:
    /// <code>
    ///     &lt;TextBox w:CalculatorPopup.CalculatorEnabled="True" /&gt;
    /// </code>
    /// Where the w prefix is the clr-Namespace:Walkabout.Controls.
    /// </summary>
    public class CalculatorPopup : Popup
    {
        CalculatorControl calculator;
        TextBox attached;

        public CalculatorPopup()
        {
            this.Child = calculator = new CalculatorControl();            
        }

        static CalculatorPopup sharedPopup;

        public static CalculatorPopup StaticCalculatorPopup
        {
            get
            {
                if (sharedPopup == null)
                {
                    sharedPopup = new CalculatorPopup();
                }
                return sharedPopup;
            }
        }

        public static bool GetCalculatorEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(CalculatorEnabledProperty);
        }

        public static void SetCalculatorEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(CalculatorEnabledProperty, value);
        }

        // Using a DependencyProperty as the backing store for CalculatorEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CalculatorEnabledProperty =
            DependencyProperty.RegisterAttached("CalculatorEnabled", typeof(bool), typeof(CalculatorPopup), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnCalculatorEnabledChanged)));

        static void OnCalculatorEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TextBox editor = d as TextBox;
            if (editor != null)
            {
                if ((bool)e.NewValue)
                {
                    editor.PreviewGotKeyboardFocus += new KeyboardFocusChangedEventHandler(StaticCalculatorPopup.OnKeyboardFocusChanged);
                    editor.PreviewLostKeyboardFocus += new KeyboardFocusChangedEventHandler(StaticCalculatorPopup.OnKeyboardFocusChanged);
                }
                else
                {
                    editor.PreviewGotKeyboardFocus -= new KeyboardFocusChangedEventHandler(StaticCalculatorPopup.OnKeyboardFocusChanged);
                    editor.PreviewLostKeyboardFocus -= new KeyboardFocusChangedEventHandler(StaticCalculatorPopup.OnKeyboardFocusChanged);
                }
            }
        }

        void OnKeyboardFocusChanged(object sender, KeyboardFocusChangedEventArgs e) 
        {
            if (e.OldFocus == attached)
            {
                Attach(null);
            }
            TextBox box = e.NewFocus as TextBox;
            if (box != null)
            {
                if (GetCalculatorEnabled(box))
                {
                    attached = box;
                    Attach(box);
                }
                else
                {
                    Attach(null);
                }
            }
        }

        void Attach(TextBox box)
        {
            this.IsOpen = false;
            if (attached != null)
            {
                attached.PreviewKeyDown -= new KeyEventHandler(TextBoxPreviewKeyDown);
            }
            attached = box;
            calculator.Attach(box);
            this.PlacementTarget = box;
            if (box != null)
            {
                box.PreviewKeyDown += new KeyEventHandler(TextBoxPreviewKeyDown);
            }
        }

        void TextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            switch (e.Key)
            {
                case Key.Add:
                case Key.Subtract:
                case Key.Multiply:
                case Key.Divide:
                    this.IsOpen = true;
                    break;
                case Key.D5:
                case Key.D8:
                case Key.D9:
                case Key.D0:
                    if (shift)
                    {
                        this.IsOpen = true;
                    }
                    break;
                case Key.Enter:
                    IsOpen = false;
                    break;
                case Key.OemPlus:
                    this.IsOpen = true;
                    break;
                case Key.Escape:
                    this.IsOpen = false;
                    break;
            }
        }
    }
}
