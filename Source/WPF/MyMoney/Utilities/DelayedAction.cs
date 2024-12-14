﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace Walkabout.Utilities
{
    /// <summary>
    /// This class provides a delayed action that has a name.  If the same named action is
    /// started multiple times before the delay if fires the action only once.
    /// </summary>
    public class DelayedActions
    {
        private readonly Dictionary<string, DelayedAction> pending = new Dictionary<string, DelayedAction>();

        public void StartDelayedAction(string name, Action action, TimeSpan delay)
        {
            DelayedAction da;
            if (!this.pending.TryGetValue(name, out da))
            {
                da = new DelayedAction(name);
                da.Complete += this.OnActionComplete;
                this.pending[name] = da;
            }
            da.StartDelayTimer(action, delay);
        }

        private void OnActionComplete(object sender, EventArgs e)
        {
            DelayedAction da = (DelayedAction)sender;
            this.pending.Remove(da.Name);
        }

        public void CancelDelayedAction(string name)
        {
            DelayedAction action;
            if (this.pending.TryGetValue(name, out action))
            {
                action.StopDelayTimer();
                this.pending.Remove(name);
            }
        }

        public void CancelAll()
        {
            var snapshot = this.pending.ToArray();
            this.pending.Clear();
            foreach (var pair in snapshot)
            {
                pair.Value.StopDelayTimer();
            }
        }

        internal bool HasDelayedAction(string name)
        {
            return this.pending.ContainsKey(name);
        }

        private class DelayedAction
        {
            private System.Threading.Timer delayTimer;
            private Action delayedAction;
            private uint startTime;
            private string name;

            public DelayedAction(string name)
            {
                this.name = name;
            }

            public string Name => name;

            public event EventHandler Complete;

            /// <summary>
            /// Start a count down with the given delay, and fire the given action when it reaches zero.
            /// But if this method is called again before the timeout it resets the timeout and starts again.
            /// </summary>
            /// <param name="action">The action to perform when the delay is reached</param>
            /// <param name="delay">The timeout before calling the action</param>
            public void StartDelayTimer(Action action, TimeSpan delay)
            {
                this.startTime = NativeMethods.TickCount;

                // stop any previous timer and start over.
                this.StopDelayTimer();

                this.delayedAction = action;

                this.delayTimer = new System.Threading.Timer(this.OnDelayTimerTick, null, (int)delay.TotalMilliseconds, System.Threading.Timeout.Infinite);
            }

            public void StopDelayTimer()
            {
                System.Threading.Timer timer = this.delayTimer;
                System.Threading.Interlocked.CompareExchange(ref this.delayTimer, null, timer);
                if (timer != null)
                {
                    // give up on this old one and start over.
                    timer.Dispose();
                    timer = null;
                }
                this.delayedAction = null;
            }

            private void OnDelayTimerTick(object state)
            {
                uint endTime = NativeMethods.TickCount;
                uint diff = this.startTime - endTime;
                Action a = this.delayedAction;

                this.StopDelayTimer();

                if (a != null)
                {
                    UiDispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            a();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("OnDelayTimerTick caught unhandled exception: " + ex.ToString());
                        }
                        if (this.Complete != null)
                        {
                            this.Complete(this, EventArgs.Empty);
                        }
                    }));
                }
            }
        }

    }
}
