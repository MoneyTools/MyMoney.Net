using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Walkabout.Utilities
{
    public class AdornerDropTarget : Adorner
    {
        private Brush brush;
        private Pen pen;

        // Be sure to call the base class constructor.
        public AdornerDropTarget(FrameworkElement adornedElement)
            : base(adornedElement)
        {
            this.IsHitTestVisible = false;

            this.brush = adornedElement.FindResource("DragDropFeedbackBrush") as Brush;
            if (this.brush == null)
            {
                this.brush = Brushes.Orange;
            }
            this.pen = new Pen(this.brush, 1.5);
        }

        // A common way to implement an adorner's rendering behavior is to override the OnRender
        // method, which is called by the layout system as part of a rendering pass.
        protected override void OnRender(DrawingContext drawingContext)
        {
            Rect adornedElementRect = new Rect(this.AdornedElement.DesiredSize);
            drawingContext.DrawRectangle(null, this.pen, adornedElementRect);
        }
    }




}
