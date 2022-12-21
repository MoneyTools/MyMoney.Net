using System;
using System.Windows;

namespace Walkabout.Utilities
{
    public delegate void SetMessageHandler(string value);

    internal class AnimatedMessage
    {
        private SetMessageHandler handler;
        private int startPos;
        private string initialValue;
        private string finalValue;
        private readonly DelayedActions actions = new DelayedActions();
        private TimeSpan delay;

        public AnimatedMessage(SetMessageHandler handler)
        {
            this.handler = handler;
            Application.Current.MainWindow.Closing += this.OnMainWindowClosing;
        }

        private void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.actions.CancelDelayedAction("animate");
            this.handler = null;
            Application.Current.MainWindow.Closing -= this.OnMainWindowClosing;
        }

        public void Start(string initialValue, string finalValue, TimeSpan delay)
        {
            this.actions.CancelDelayedAction("animate");
            this.handler(initialValue);
            this.initialValue = initialValue;
            this.finalValue = finalValue;
            this.startPos = 0;
            this.delay = delay;

            for (int n = initialValue.Length; this.startPos < n && this.startPos < finalValue.Length; this.startPos++)
            {
                if (initialValue[this.startPos] != finalValue[this.startPos])
                {
                    break;
                }
            }
            this.actions.StartDelayedAction("animate", new Action(this.OnTick), delay);
        }

        private void OnTick()
        {
            var safeHandler = this.handler;
            if (safeHandler != null)
            {
                if (this.startPos > this.finalValue.Length)
                {
                    this.startPos--;
                    safeHandler(this.initialValue.Substring(0, this.startPos));
                    this.actions.StartDelayedAction("animate", new Action(this.OnTick), this.delay);
                }
                else if (this.startPos < this.finalValue.Length)
                {
                    this.startPos++;
                    safeHandler(this.finalValue.Substring(0, this.startPos));
                    this.actions.StartDelayedAction("animate", new Action(this.OnTick), this.delay);
                }
            }
        }
    }
}
