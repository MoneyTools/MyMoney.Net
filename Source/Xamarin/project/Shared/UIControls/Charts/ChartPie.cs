using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using xMoney.UIControls;

namespace XMoney
{

    public class ChartPie : Chart
    {
        private readonly SKCanvasView _canvasView;

        public ChartPie()
        {
            _canvasView = new SKCanvasView();
            _canvasView.PaintSurface += OnCanvasViewPaintSurface;
            this.Content = _canvasView;
        }

        public void AddData(decimal value, SKColor color)
        {
            this.chartData.Add(new ChartEntry(value, color));

            _canvasView.InvalidateSurface();
        }

        private SKPaintSurfaceEventArgs _lastArgs = null;

        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            _lastArgs = args;

            DrawChart(_lastArgs);
        }

        private void DrawChart(SKPaintSurfaceEventArgs args)
        {
            if (args == null)
            {
                return;
            }

            SKImageInfo info = args.Info;
            SKSurface surface = args.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear();

            decimal totalValues = 0;

            foreach (ChartEntry item in chartData)
            {
                totalValues += item.Value;
            }

            var center = new SKPoint(info.Width / 2, info.Height / 2);
            float explodeOffset = 0;
            float radius = Math.Min(info.Width / 2, info.Height / 2) - (2 * explodeOffset);
            var rect = new SKRect(center.X - radius, center.Y - radius,
                                  center.X + radius, center.Y + radius);
            rect.Inflate(-8, -8);

            float startAngle = 0;

            foreach (ChartEntry item in chartData)
            {
                double sweepAngle = (double)(360 * item.Value / totalValues);

                using (var path = new SKPath())
                using (var fillPaint = new SKPaint())
                using (var outlinePaint = new SKPaint())
                {
                    fillPaint.IsAntialias = true;
                    outlinePaint.IsAntialias = true;
                    path.MoveTo(center);
                    path.ArcTo(rect, startAngle, (float)sweepAngle, false);
                    path.Close();

                    fillPaint.Style = SKPaintStyle.Fill;
                    fillPaint.Color = item.Color;

                    outlinePaint.Style = SKPaintStyle.Stroke;
                    outlinePaint.StrokeWidth = 4;
                    outlinePaint.Color = MyColors.Darker(item.Color, 0.7f);

                    // Calculate "explode" transform
                    float angle = startAngle + (0.5f * (float)sweepAngle);
                    float x = explodeOffset * (float)Math.Cos(Math.PI * angle / 180);
                    float y = explodeOffset * (float)Math.Sin(Math.PI * angle / 180);

                    _ = canvas.Save();
                    canvas.Translate(x, y);

                    // Fill and stroke the path
                    canvas.DrawPath(path, fillPaint);
                    //canvas.DrawPath(path, outlinePaint);
                    canvas.Restore();
                }

                startAngle += (float)sweepAngle;
            }
        }
    }
}