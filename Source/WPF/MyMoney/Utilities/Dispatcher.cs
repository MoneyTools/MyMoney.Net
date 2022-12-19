using System.Windows.Threading;

namespace Walkabout.Utilities
{
    /// <summary>
    /// During shutdown when window handles are not available, we cannot use ISynchronizeInvoke
    /// </summary>
    public static class UiDispatcher
    {
        static Dispatcher dispatcher;
        static int uiThreadId;

        public static Dispatcher CurrentDispatcher
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
        /// Only use the Dispatcher if we are not already on the UI Thread.
        /// This avoid WPF exceptions saying the dispatcher is unavailable.
        /// </summary>
        /// <param name="d">The delegate to invoke</param>
        /// <param name="args">Optional parameters</param>
        public static object BeginInvoke(System.Delegate d, params object[] args)
        {
            if (dispatcher != null && System.Threading.Thread.CurrentThread.ManagedThreadId != uiThreadId)
            {
                // Note: we cannot use dispatcher.Invoke because that can lead to deadlocks.
                // For example, if this is a background thread with a lock() on a money data object
                // (like account.rebalance does) then UI thread might be blocked on trying to get
                // that lock and this dispatcher invoke would therefore create a deadlock.
                return dispatcher.BeginInvoke(d, args);
            }
            else
            {
                // we are already on the UI thread so call the delegate directly.
                return d.DynamicInvoke(args);
            }
        }


        /// <summary>
        /// Only use the Dispatcher if we are not already on the UI Thread.
        /// This avoid WPF exceptions saying the dispatcher is unavailable.
        /// </summary>
        /// <param name="d">The delegate to invoke</param>
        /// <param name="args">Optional parameters</param>
        public static object Invoke(System.Delegate d, params object[] args)
        {
            if (dispatcher != null && System.Threading.Thread.CurrentThread.ManagedThreadId != uiThreadId)
            {
                // Note: must be careful using this method, it could deadlock the UI, so make sure
                // it cannot be interleaved with similar calls.
                return dispatcher.Invoke(d, args);
            }
            else
            {
                // we are already on the UI thread so call the delegate directly.
                return d.DynamicInvoke(args);
            }
        }
    }
}
