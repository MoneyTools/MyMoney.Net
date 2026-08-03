using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Walkabout.Controls
{
    /// <summary>
    /// The events on TextBox suck, it is much nicer to have an idea of a 
    /// text change that is more of a "commit" rather than every little change.
    /// </summary>
    internal class HandyTextBox : TextBox
    {
        bool changed;

        public HandyTextBox()
        {

        }

        public event EventHandler<string> Committed;

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            changed = true;
            base.OnTextChanged(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter && changed)
            {
                changed = false;
                if (Committed != null)
                {
                    Committed(this, this.Text);
                }
            }
            base.OnPreviewKeyDown(e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            if (changed)
            {
                changed = false;
                if (Committed != null)
                {
                    Committed(this, this.Text);
                }
            }
            base.OnLostFocus(e);
        }
    }
}
