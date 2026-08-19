using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Core;

namespace UnoApp1.Presentation;

internal class UiDispatcher
{
    private static CoreDispatcher dispatcher;
    private static int uiThreadId;

    public static CoreDispatcher CurrentDispatcher
    {
        get
        {
            return dispatcher;
        }
        set
        {
            uiThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            dispatcher = value;
        }
    }

    /// <summary>
    /// Invoke something on the UI thread.
    /// </summary>
    /// <param name="d">The delegate to invoke</param>
    /// <param name="args">Optional parameters</param>
    public static async Task InvokeAsync(System.Delegate d, params object[] args)
    {
        await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
            d.DynamicInvoke(args);
        });
    }
}
