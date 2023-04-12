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
        private TextBox text;
        private double memory;

        public CalculatorControl()
        {
            this.InitializeComponent();
        }

        public void Attach(TextBox box)
        {
            if (this.text != null)
            {
                this.text.KeyDown -= new KeyEventHandler(this.OnTextKeyDown);
            }
            this.text = box;
            if (this.text != null)
            {
                this.text.KeyDown += new KeyEventHandler(this.OnTextKeyDown);
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

        private void AnimateClick(Button b)
        {
            b.Background = (Brush)this.Resources["SimulateDownBrush"];
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

        private void OnTextKeyDown(object sender, KeyEventArgs e)
        {
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            if (e.Key == Key.OemPlus)
            {
                if (!shift)
                {
                    // this is '=' key
                    this.AnimateClick(this.ButtonEquals);
                    this.Calculate();
                    e.Handled = true;
                    return;
                }
                else
                {
                    this.AnimateClick(this.ButtonPlus);
                }
            }

            switch (e.Key)
            {
                case Key.Enter:
                    this.AnimateClick(this.ButtonEquals);
                    this.Calculate();
                    break;
                case Key.D0:
                case Key.NumPad0:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button0);
                    }
                    break;
                case Key.D1:
                case Key.NumPad1:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button1);
                    }
                    break;
                case Key.D2:
                case Key.NumPad2:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button2);
                    }
                    break;
                case Key.D3:
                case Key.NumPad3:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button3);
                    }
                    break;
                case Key.D4:
                case Key.NumPad4:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button4);
                    }
                    break;
                case Key.D5:
                case Key.NumPad5:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button5);
                    }
                    else
                    {
                        this.AnimateClick(this.ButtonPercent);
                    }
                    break;
                case Key.D6:
                case Key.NumPad6:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button6);
                    }
                    break;
                case Key.D7:
                case Key.NumPad7:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button7);
                    }
                    break;
                case Key.D8:
                case Key.NumPad8:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button8);
                    }
                    else
                    {
                        this.AnimateClick(this.ButtonMultiply);
                    }
                    break;
                case Key.D9:
                case Key.NumPad9:
                    if (!shift)
                    {
                        this.AnimateClick(this.Button9);
                    }
                    break;
                case Key.Divide:
                    this.AnimateClick(this.ButtonDivide);
                    break;
                case Key.Multiply:
                    this.AnimateClick(this.ButtonMultiply);
                    break;
                case Key.Subtract:
                    this.AnimateClick(this.ButtonMinus);
                    break;
                case Key.OemPeriod:
                    this.AnimateClick(this.ButtonPeriod);
                    break;
            }
        }

        private void UpdateValue(double v)
        {
            this.text.Text = v.ToString("N2");
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (this.text == null)
            {
                return;
            }
            Button b = (Button)sender;
            string key = (string)b.Tag;
            int pos = this.text.SelectionStart;
            switch (key)
            {
                case "C":
                    this.text.Text = string.Empty;
                    break;
                case "SquareRoot":
                    this.UpdateValue(Math.Sqrt(this.Calculate()));
                    this.text.SelectionStart = this.text.Text.Length;
                    this.text.SelectionLength = 0;
                    break;
                case "MC":
                    this.memory = 0;
                    break;
                case "MR":
                    string ms = this.memory.ToString();
                    this.text.Text = this.text.Text.Substring(0, pos) + ms + this.text.Text.Substring(pos + this.text.SelectionLength);
                    this.text.SelectionStart = pos + ms.Length;
                    this.text.SelectionLength = 0;
                    break;
                case "MS":
                    this.memory = this.Calculate();
                    break;
                case "M+":
                    this.memory += this.Calculate();
                    break;
                case "Sign":
                    this.UpdateValue(-this.Calculate());
                    this.text.SelectionStart = this.text.Text.Length;
                    this.text.SelectionLength = 0;
                    break;
                case "=":
                    this.Calculate();
                    break;
                default:
                    this.text.Text += key;
                    this.text.SelectionStart = pos + 1;
                    this.text.SelectionLength = 0;
                    break;
            }
        }

        public double Calculate()
        {
            double result = 0;
            string test = this.text.Text;
            try
            {
                if (!string.IsNullOrEmpty(test.Trim()))
                {
                    Parser p = new Parser();
                    result = p.Parse(test);
                    this.text.Text = result.ToString();
                    this.text.SelectionStart = this.text.Text.Length;
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
