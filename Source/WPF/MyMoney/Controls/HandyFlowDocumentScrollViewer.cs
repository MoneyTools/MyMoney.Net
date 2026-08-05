using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace Walkabout.Controls
{
    internal class HandyFlowDocumentScrollViewer : FlowDocumentScrollViewer
    {
        private ScrollViewer? scrollViewer;
        private const double ScrollSpeedMultiplier = 1.0;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.scrollViewer = this.GetTemplateChild("PART_ContentHost") as ScrollViewer;
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (this.scrollViewer == null)
            {
                base.OnPreviewMouseWheel(e);
            }
            else
            {
                this.scrollViewer.ScrollToVerticalOffset(this.scrollViewer.VerticalOffset - (e.Delta * ScrollSpeedMultiplier));
                e.Handled = true;
            }
        }
    }
}
