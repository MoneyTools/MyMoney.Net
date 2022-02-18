using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for ProgressDots.xaml
    /// </summary>
    public partial class ProgressDots : UserControl
    {
        public ProgressDots()
        {
            InitializeComponent();
            this.SizeChanged += OnRenderSizeChanged;
        }

        private void OnRenderSizeChanged(object sender, SizeChangedEventArgs sizeInfo)
        {
            double width = sizeInfo.NewSize.Width;
            double position1 = -10;
            double position2 = width * 0.45;
            double position3 = width * 0.55;
            double position4 = width + 10;

            int index = 1;

            // {Binding DotBrush} is not working for some unknown reason.
            foreach (UIElement child in IndeterminateRoot.Children)
            {
                if (child is Ellipse)
                {
                    Ellipse e = (Ellipse)child;
                    e.Fill = this.DotBrush;
                    TranslateTransform tt = new TranslateTransform();
                    string name = string.Format("RTT{0}", index++);
                    tt.SetValue(NameProperty, name);
                    e.RenderTransform = tt;
                    if (IndeterminateRoot.FindName(name) != null)
                    {
                        IndeterminateRoot.UnregisterName(name);
                    }
                    IndeterminateRoot.RegisterName(name, tt);
                }
            }

            Storyboard storyboard = new Storyboard() { RepeatBehavior = RepeatBehavior.Forever, Duration = new Duration(TimeSpan.FromSeconds(4.4)) };

            TimeSpan start = TimeSpan.FromSeconds(0.2);

            for (int i = 1; i < index; i++)
            {
                DoubleAnimationUsingKeyFrames animation = new DoubleAnimationUsingKeyFrames() { BeginTime = start };
                Storyboard.SetTargetName(animation, string.Format("RTT{0}", i));
                Storyboard.SetTargetProperty(animation, new PropertyPath("X"));

                animation.KeyFrames.Add(new LinearDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)), Value = position1 });
                animation.KeyFrames.Add(new EasingDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8)), Value = position2, EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseOut, Exponent = 1 } });
                animation.KeyFrames.Add(new LinearDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.0)), Value = position3 });
                animation.KeyFrames.Add(new EasingDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.8)), Value = position4, EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseIn, Exponent = 1 } });
                storyboard.Children.Add(animation);

                /*
                    <DoubleAnimationUsingKeyFrames BeginTime="00:00:00.0" Storyboard.TargetProperty="Opacity" Storyboard.TargetName="R1">
                        <DiscreteDoubleKeyFrame KeyTime="0" Value="1"/>
                        <DiscreteDoubleKeyFrame KeyTime="00:00:02.8" Value="0"/>
                    </DoubleAnimationUsingKeyFrames>                  
                */
                animation = new DoubleAnimationUsingKeyFrames() { BeginTime = start };
                Storyboard.SetTargetName(animation, string.Format("R{0}", i));
                Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
                animation.KeyFrames.Add(new DiscreteDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 1 });
                animation.KeyFrames.Add(new DiscreteDoubleKeyFrame() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.8)), Value = 0 });
                storyboard.Children.Add(animation);

                start += TimeSpan.FromSeconds(0.2);
            }
            IndeterminateRoot.BeginStoryboard(storyboard);
        }



        public Brush DotBrush
        {
            get { return (Brush)GetValue(DotBrushProperty); }
            set { SetValue(DotBrushProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DotBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DotBrushProperty =
            DependencyProperty.Register("DotBrush", typeof(Brush), typeof(ProgressDots), new PropertyMetadata(null));

    }
}
