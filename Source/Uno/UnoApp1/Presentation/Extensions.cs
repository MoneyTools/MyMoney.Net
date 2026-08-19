using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.Foundation;
using Uno.UI.Xaml;

namespace UnoApp1.Presentation;

internal static class Extensions
{
    public static Color FromRgb(byte r, byte g, byte b)
    {
        return Color.FromArgb(255, r, g, b);
    }

    public static Rect Inflate(this Rect rect, double x, double y)
    {
        return new Rect(rect.X - x, rect.Y - y, rect.Width + (2 * x), rect.Height + (2 * y));
    }

    public static bool IntersectsWith(this Rect rect1, Rect rect2)
    {
        // return true if rect1 overlaps anywhere with rect2.
        double num = Math.Max(rect1.Left, rect2.Left);
        double num2 = Math.Min(rect1.Right, rect2.Right);
        double num3 = Math.Max(rect1.Top, rect2.Top);
        double num4 = Math.Min(rect1.Bottom, rect2.Bottom);
        return (num2 >= num && num4 >= num3);
    }

    public static void BeginAnimation(this DependencyObject target, Timeline animation, string propertyPath, Action? completedAction = null)
    {
        target.ClearAnimation(propertyPath);
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, propertyPath);
        var storyboard = new Storyboard();
        AddStoryboard(target, storyboard, propertyPath);
        storyboard.Children.Add(animation);
        storyboard.Completed += (s, e) =>
        {
            if (completedAction != null)
            {
                completedAction();
            }
        };

        storyboard.Begin();
    }

    public static void ClearAnimation(this DependencyObject target, string propertyPath)
    {
        ClearStoryboard(target, propertyPath);
    }

    private static DependencyProperty StoryboardMapProperty
    {
        get;
    } = DependencyProperty.RegisterAttached("StoryboardMapProperty", typeof(Dictionary<string, Storyboard>), typeof(Extensions), new FrameworkPropertyMetadata(null));


    private static void AddStoryboard(DependencyObject element, Storyboard storyboard, string path)
    {
        var map = (Dictionary<string, Storyboard>)element.GetValue(StoryboardMapProperty);
        if (map == null)
        {
            map = new Dictionary<string, Storyboard>();
            element.SetValue(StoryboardMapProperty, map);
        }
        map[path] = storyboard;
    }

    private static void ClearStoryboard(DependencyObject element, string path)
    {
        var map = (Dictionary<string, Storyboard>)element.GetValue(StoryboardMapProperty);
        if (map != null && map.TryGetValue(path, out Storyboard? value) && value != null)
        {
            value.Stop();
            map.Remove(path);
        }
    }

}
