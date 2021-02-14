using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;

namespace XMoney
{
    public class GradientView : ContentView
    {
        public Color StartColor { get; set; } = Color.Transparent;
        public Color EndColor { get; set; } = Color.Transparent;
        public bool Horizontal { get; set; } = false;

        public GradientView()
        {
            var canvasView = new SKCanvasView();
            canvasView.PaintSurface += OnCanvasViewPaintSurface;
            Content = canvasView;
        }

        private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            SKImageInfo info = args.Info;
            SKSurface surface = args.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear();

            SKColor[] colors = new SKColor[] { StartColor.ToSKColor(), EndColor.ToSKColor() };
            var startPoint = new SKPoint(0, 0);
            SKPoint endPoint = Horizontal ? new SKPoint(info.Width, 0) : new SKPoint(0, info.Height);

            SKShader shader = SKShader.CreateLinearGradient(startPoint, endPoint, colors, null, SKShaderTileMode.Clamp);

            var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Shader = shader
            };

            canvas.DrawRect(new SKRect(0, 0, info.Width, info.Height), paint);
        }
    }
}
