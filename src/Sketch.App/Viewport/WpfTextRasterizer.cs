using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sketch.Rendering;
using Sketch.Tools;

namespace Sketch.App.Viewport;

/// <summary>
/// Rasterises label text with WPF into premultiplied BGRA pixels. Boxed labels (tool feedback) sit on a small
/// white card; model labels (texts, dimensions, DEAP opening labels) are drawn without a background, with a thin
/// light halo so they stay readable over geometry without hiding it.
/// </summary>
internal sealed class WpfTextRasterizer : ITextRasterizer
{
    private static readonly Typeface Regular = new("Segoe UI");
    private static readonly Typeface Semibold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private readonly double _dpiScale;

    public WpfTextRasterizer(double dpiScale) => _dpiScale = dpiScale;

    public (byte[] Bgra, int Width, int Height) Rasterize(string text, DrawColor color, bool boxed)
    {
        var brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, boxed ? Regular : Semibold,
            boxed ? 12 : 11.5, brush, _dpiScale) { TextAlignment = TextAlignment.Center };
        var pad = boxed ? 3.0 : 2.5;
        var w = formatted.WidthIncludingTrailingWhitespace + pad * 2;
        var h = formatted.Height + pad * 2;
        var origin = new Point(w / 2, pad); // centred text: x is the centre line

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (boxed)
            {
                var background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
                var border = new Pen(new SolidColorBrush(Color.FromArgb(120, 120, 120, 120)), 1);
                dc.DrawRoundedRectangle(background, border, new Rect(0.5, 0.5, w - 1, h - 1), 2, 2);
                dc.DrawText(formatted, origin);
            }
            else
            {
                // Halo: the glyph outlines stroked in a soft light colour behind the text.
                var geometry = formatted.BuildGeometry(origin);
                var halo = new Pen(new SolidColorBrush(Color.FromArgb(215, 250, 250, 246)), 3.2) { LineJoin = PenLineJoin.Round };
                halo.Freeze();
                dc.DrawGeometry(null, halo, geometry);
                dc.DrawGeometry(brush, null, geometry);
            }
        }

        var pw = (int)Math.Ceiling(w * _dpiScale);
        var ph = (int)Math.Ceiling(h * _dpiScale);
        var bitmap = new RenderTargetBitmap(pw, ph, 96 * _dpiScale, 96 * _dpiScale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[pw * ph * 4];
        bitmap.CopyPixels(pixels, pw * 4, 0);
        return (pixels, pw, ph);
    }
}
