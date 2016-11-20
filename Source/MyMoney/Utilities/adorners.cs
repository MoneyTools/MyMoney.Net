using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Diagnostics;
using System;
using System.IO;

namespace Walkabout.Utilities
{
    public class AdornerDropTarget : Adorner
    {
        // Be sure to call the base class constructor.
        public AdornerDropTarget(UIElement adornedElement)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        // A common way to implement an adorner's rendering behavior is to override the OnRender
        // method, which is called by the layout system as part of a rendering pass.
        protected override void OnRender(DrawingContext drawingContext)
        {
            Rect adornedElementRect = new Rect(this.AdornedElement.DesiredSize);

            // Some arbitrary drawing implements.
            SolidColorBrush renderBrush = new SolidColorBrush(Color.FromArgb(30, 100,20,100));
            renderBrush.Opacity = 0.2;
            Pen renderPen = new Pen(Brushes.Orange, 1.5);

            // Draw a circle at each corner.
            drawingContext.DrawRectangle(renderBrush, renderPen, adornedElementRect);
        }
    }


   

}
