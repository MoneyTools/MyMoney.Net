using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Walkabout.Utilities
{
    public class AdornerDropTarget : Adorner
    {
        Brush brush;
        Pen pen;

        // Be sure to call the base class constructor.
        public AdornerDropTarget(FrameworkElement adornedElement)
            : base(adornedElement)
        {
            IsHitTestVisible = false;

            brush = adornedElement.FindResource("DragDropFeedbackBrush") as Brush;
            if (brush == null)
            {
                brush = Brushes.Orange;
            }
            pen = new Pen(brush, 1.5);
        }

        // A common way to implement an adorner's rendering behavior is to override the OnRender
        // method, which is called by the layout system as part of a rendering pass.
        protected override void OnRender(DrawingContext drawingContext)
        {
            Rect adornedElementRect = new Rect(this.AdornedElement.DesiredSize);
            drawingContext.DrawRectangle(null, pen, adornedElementRect);
        }
    }




}
