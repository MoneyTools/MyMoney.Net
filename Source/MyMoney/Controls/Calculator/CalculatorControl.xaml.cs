using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Walkabout.Utilities;

namespace Walkabout.Controls
{
    /// <summary>
    /// This is a calculator control which is supposed to be attached to a TextBox and displayed by the
    /// CalculatorPopup.  
    /// </summary>
    public partial class CalculatorControl : UserControl
    {
        TextBox text;
        double memory;

        public CalculatorControl()
        {
            InitializeComponent();
        }

        public void Attach(TextBox box)
        {
            if (text != null)
            {
                text.KeyDown -= new KeyEventHandler(OnTextKeyDown);
            }
            text = box;
            if (text != null)
            {
                text.KeyDown += new KeyEventHandler(OnTextKeyDown);
            }
        }



        public static bool GetSimulateDown(DependencyObject obj)
        {
            return (bool)obj.GetValue(SimulateDownProperty);
        }

        public static void SetSimulateDown(DependencyObject obj, bool value)
        {
            obj.SetValue(SimulateDownProperty, value);
        }

        // Using a DependencyProperty as the backing store for SimulateDown.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SimulateDownProperty =
            DependencyProperty.RegisterAttached("SimulateDown", typeof(bool), typeof(CalculatorControl), new UIPropertyMetadata(false));


        void AnimateClick(Button b)
        {
            b.Background = (Brush)Resources["SimulateDownBrush"];           
            BooleanAnimationUsingKeyFrames a = new BooleanAnimationUsingKeyFrames();
            a.Duration = TimeSpan.FromSeconds(0.1);
            a.KeyFrames.Add(new DiscreteBooleanKeyFrame(true, KeyTime.FromPercent(0)));
            a.Completed += new EventHandler((s, e) =>
            {
                b.BeginAnimation(SimulateDownProperty, null);
                b.ClearValue(Button.BackgroundProperty);
            });
            b.BeginAnimation(SimulateDownProperty, a);
        }

        void OnTextKeyDown(object sender, KeyEventArgs e)
        {
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            if (e.Key == Key.OemPlus)
            {
                if (!shift)
                {
                    // this is '=' key
                    AnimateClick(ButtonEquals);
                    Calculate();
                    e.Handled = true;
                    return;
                }
                else
                {
                    AnimateClick(ButtonPlus);
                }
            }

            switch (e.Key)
            {
                case Key.Enter:
                    AnimateClick(ButtonEquals);
                    Calculate();
                    break;
                case Key.D0:
                case Key.NumPad0:
                    if (!shift)
                    {
                        AnimateClick(Button0);
                    }
                    break;
                case Key.D1:
                case Key.NumPad1:
                    if (!shift)
                    {
                        AnimateClick(Button1);
                    }
                    break;
                case Key.D2:
                case Key.NumPad2:
                    if (!shift)
                    {
                        AnimateClick(Button2);
                    }
                    break;
                case Key.D3:
                case Key.NumPad3:
                    if (!shift)
                    {
                        AnimateClick(Button3);
                    }
                    break;
                case Key.D4:
                case Key.NumPad4:
                    if (!shift)
                    {
                        AnimateClick(Button4);
                    }
                    break;
                case Key.D5:
                case Key.NumPad5:
                    if (!shift)
                    {
                        AnimateClick(Button5);
                    }
                    else
                    {
                        AnimateClick(ButtonPercent);
                    }
                    break;
                case Key.D6:
                case Key.NumPad6:
                    if (!shift)
                    {
                        AnimateClick(Button6);
                    }
                    break;
                case Key.D7:
                case Key.NumPad7:
                    if (!shift)
                    {
                        AnimateClick(Button7);
                    }
                    break;
                case Key.D8:
                case Key.NumPad8:
                    if (!shift)
                    {
                        AnimateClick(Button8);
                    }
                    else
                    {
                        AnimateClick(ButtonMultiply);
                    }
                    break;
                case Key.D9:
                case Key.NumPad9:
                    if (!shift)
                    {
                        AnimateClick(Button9);
                    }
                    break;
                case Key.Divide:
                    AnimateClick(ButtonDivide);
                    break;
                case Key.Multiply:
                    AnimateClick(ButtonMultiply);
                    break;
                case Key.Subtract:
                    AnimateClick(ButtonMinus);
                    break;
                case Key.OemPeriod:
                    AnimateClick(ButtonPeriod);
                    break;
            }
        }

        void UpdateValue(double v)
        {
            text.Text = v.ToString("N2");
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (text == null)
            {
                return;
            }
            Button b = (Button)sender;
            string key = (string)b.Tag;
            int pos = text.SelectionStart;
            switch (key)
            {
                case "C":
                    text.Text = string.Empty;
                    break;
                case "SquareRoot":
                    UpdateValue(Math.Sqrt(Calculate()));
                    text.SelectionStart = text.Text.Length;
                    text.SelectionLength = 0;
                    break;
                case "MC":
                    memory = 0;
                    break;
                case "MR":
                    string ms = memory.ToString();
                    text.Text = text.Text.Substring(0, pos) + ms + text.Text.Substring(pos + text.SelectionLength);
                    text.SelectionStart = pos + ms.Length;
                    text.SelectionLength = 0;
                    break;
                case "MS":
                    memory = Calculate();
                    break;
                case "M+":
                    memory += Calculate();
                    break;
                case "Sign":
                    UpdateValue(-Calculate());
                    text.SelectionStart = text.Text.Length;
                    text.SelectionLength = 0;
                    break;
                case "=":
                    Calculate();
                    break;
                default:
                    text.Text += key;
                    text.SelectionStart = pos + 1;
                    text.SelectionLength = 0;
                    break;
            }
        }

        public double Calculate()
        {
            double result = 0;
            string test = text.Text;
            try
            {
                if (!string.IsNullOrEmpty(test.Trim()))
                {
                    Parser p = new Parser();
                    result = p.Parse(test);
                    text.Text = result.ToString("N2");
                    text.SelectionStart = text.Text.Length;
                }
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return result;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key) // equals?
            {
                case Key.D0:
                    break;
                case Key.D1:
                    break;
                case Key.D2:
                    break;
                case Key.D3:
                    break;
                case Key.D4:
                    break;
                case Key.D5:
                    break;
                case Key.D6:
                    break;
                case Key.D7:
                    break;
                case Key.D8:
                    break;
                case Key.D9:
                    break;
                case Key.OemPlus:
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    break;
                case Key.Multiply:
                    break;
                case Key.Decimal:
                case Key.OemPeriod:
                    break;
                case Key.C:
                case Key.Clear:
                case Key.OemClear:
                    break;
                case Key.Divide:
                    break;
                case Key.Enter:
                    break;
            } 
            base.OnKeyDown(e);
        }

    }
}
