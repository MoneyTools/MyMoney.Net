using System;
using System.Windows;

namespace Walkabout.Utilities
{
    public delegate void SetMessageHandler(string value);

    class AnimatedMessage
    {
        SetMessageHandler handler;
        int startPos;
        string initialValue;
        string finalValue;
        DelayedActions actions = new DelayedActions();
        TimeSpan delay;

        public AnimatedMessage(SetMessageHandler handler)
        {
            this.handler = handler;
            Application.Current.MainWindow.Closing += OnMainWindowClosing;
        }

        private void OnMainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            actions.CancelDelayedAction("animate");
            this.handler = null;
            Application.Current.MainWindow.Closing -= OnMainWindowClosing;
        }

        public void Start(string initialValue, string finalValue, TimeSpan delay)
        {
            actions.CancelDelayedAction("animate");
            handler(initialValue);
            this.initialValue = initialValue;
            this.finalValue = finalValue;
            this.startPos = 0;
            this.delay = delay;

            for (int n = initialValue.Length; this.startPos < n && this.startPos < finalValue.Length; this.startPos++)
            {
                if(initialValue[this.startPos] != finalValue[this.startPos])
                {
                    break;
                }
            }
            actions.StartDelayedAction("animate", new Action(OnTick), delay);
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
                    actions.StartDelayedAction("animate", new Action(OnTick), delay);
                }
                else if (this.startPos < this.finalValue.Length)
                {
                    this.startPos++;
                    safeHandler(this.finalValue.Substring(0, this.startPos));
                    actions.StartDelayedAction("animate", new Action(OnTick), delay);
                }
            }
        }
    }
}
