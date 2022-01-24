using System;
using Walkabout.Help;
using System.Windows;

namespace Walkabout.Dialogs
{
    public class BaseDialog : Window
    {
        public BaseDialog()
        {
            this.SetResourceReference(Window.BackgroundProperty, "SystemControlPageBackgroundChromeMediumLowBrush");
            this.SetResourceReference(Window.ForegroundProperty, "SystemControlPageTextBaseHighBrush");
        }
    }
}
