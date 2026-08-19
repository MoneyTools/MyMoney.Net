using System;

using Microsoft.UI.Xaml.Media.Animation;
using System.Reflection;
using System.Reflection.Emit;
using Windows.Foundation;

namespace UnoApp1.Presentation;

public partial class PointCollectionAnimation : DependencyObject
{
    public PointCollection From { get; set; }

    public PointCollection To { get; set; }

    public DependencyObject Target { get; set; }

    private Action<object, PointCollection> setter;

    private Func<object, PointCollection> getter;

    public PointCollectionAnimation(DependencyObject target, string propertyPath)
    {
        this.Target = target;

        var propInfo = target.GetType().GetProperty(propertyPath);
        if (propInfo == null)
        {
            throw new Exception($"Target does not have property {propertyPath}");
        }
        if (propInfo.PropertyType != typeof(PointCollection))
        {
            throw new Exception($"Target property {propertyPath} is not of type PointCollection");
        }

        this.setter = CreatePointCollectionSetter(target.GetType(), propertyPath);
        this.getter = CreatePointCollectionGetter(target.GetType(), propertyPath);
        this.From = this.To = this.getter(target);
    }

    public static Func<object, PointCollection> CreatePointCollectionGetter(Type targetType, string propertyName)
    {
        // 1. Get the PropertyInfo and its Getter MethodInfo
        PropertyInfo propertyInfo = targetType.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new ArgumentException($"Property '{propertyName}' not found on {targetType}.");

        MethodInfo getMethod = propertyInfo.GetGetMethod()
            ?? throw new InvalidOperationException($"Property '{propertyName}' has no public getter.");

        // 2. Create a DynamicMethod that takes object target and returns PointCollection
        var dynamicMethod = new DynamicMethod(
            name: $"Get_{propertyName}",
            returnType: typeof(PointCollection),
            parameterTypes: new[] { typeof(object) },
            m: targetType.Module,
            skipVisibility: true);

        ILGenerator il = dynamicMethod.GetILGenerator();

        // 3. Emit IL Code

        // Arg 0: Load target object onto the stack
        il.Emit(OpCodes.Ldarg_0);

        // Cast/Unbox the generic object parameter to targetType
        if (targetType.IsValueType)
        {
            // For structs, unbox to address or unbox value
            il.Emit(OpCodes.Unbox_Any, targetType);
        }
        else
        {
            il.Emit(OpCodes.Castclass, targetType);
        }

        // Call the property getter method (get_Points)
        il.Emit(targetType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, getMethod);

        // Return the PointCollection instance sitting on top of the stack
        il.Emit(OpCodes.Ret);

        // 4. Create and return the strongly-typed Func delegate
        return (Func<object, PointCollection>)dynamicMethod.CreateDelegate(typeof(Func<object, PointCollection>));
    }

    public static Action<object, PointCollection> CreatePointCollectionSetter(Type targetType, string propertyName)
    {
        // 1. Get the PropertyInfo and its Setter MethodInfo
        PropertyInfo propertyInfo = targetType.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new ArgumentException($"Property '{propertyName}' not found on {targetType}.");

        MethodInfo setMethod = propertyInfo.GetSetMethod()
            ?? throw new InvalidOperationException($"Property '{propertyName}' has no public setter.");

        // 2. Create a DynamicMethod that matches Action<object, PointCollection>
        var dynamicMethod = new DynamicMethod(
            name: $"Set_{propertyName}",
            returnType: null, // void
            parameterTypes: new[] { typeof(object), typeof(PointCollection) },
            m: targetType.Module,
            skipVisibility: true);

        ILGenerator il = dynamicMethod.GetILGenerator();

        // 3. Emit IL Code

        // Arg 0: Load target object onto the stack, casting/unboxing if targetType is a class
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, targetType);

        // Arg 1: Load PointCollection value onto the stack
        il.Emit(OpCodes.Ldarg_1);

        // Call the property setter
        // Use Callvirt for instance methods unless targetType is a non-sealed class where non-virtual call is required
        il.Emit(targetType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, setMethod);

        // Return from method
        il.Emit(OpCodes.Ret);

        // 4. Create and return the strongly-typed Action delegate
        return (Action<object, PointCollection>)dynamicMethod.CreateDelegate(typeof(Action<object, PointCollection>));
    }

    public void BeginAnimation(Duration duration, TimeSpan startTime)
    {
        this.BeginAnimation(new DoubleAnimation()
        {
            From = 0.0,
            To = 1.0,
            Duration = duration,
            BeginTime = startTime,
            EnableDependentAnimation = true,
            FillBehavior = FillBehavior.HoldEnd
        }, "Progress");

    }


    public double Progress
    {
        get { return (double)GetValue(ProgressProperty); }
        set { SetValue(ProgressProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Progress.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(PointCollectionAnimation), new PropertyMetadata(0.0, OnProgressChanged));

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (d is PointCollectionAnimation s)
        {
            s.OnProgressChanged();
        }
    }

    // cached collections we are changing during animation.
    PointCollection ptsDst1 = new PointCollection();
    PointCollection ptsDst2 = new PointCollection();
    bool flip;


    private void OnProgressChanged()
    {
        // Set ptsFrom from From or defaultOriginValue
        PointCollection ptsFrom = this.From;
        PointCollection ptsTo = this.To;
        double progress = this.Progress;

        // Choose which destination collection to use
        PointCollection ptsDst = this.flip ? this.ptsDst1 : this.ptsDst2;
        this.flip = !this.flip;
        ptsDst.Clear();

        // Interpolate the points, but in a left to right sweeping motion
        // where column growth happens in 1/10th of the allocated duration (0.1 on our 0-1 clock scale).
        double end = ptsTo.Count;
        Point first = (end > 0) ? ptsTo[0] : new Point(0, 0);

        for (int i = 0; i < ptsTo.Count; i++)
        {
            double fromX = (i < ptsFrom.Count) ? ptsFrom[i].X : first.X;
            double fromY = (i < ptsFrom.Count) ? ptsFrom[i].Y : first.Y;
            ptsDst.Add(new Point(((1 - progress) * fromX) + (progress * ptsTo[i].X),
                                 ((1 - progress) * fromY) + (progress * ptsTo[i].Y)));
        }

        // Set the new PointCollection
        this.setter(this.Target, ptsDst);
    }

}

