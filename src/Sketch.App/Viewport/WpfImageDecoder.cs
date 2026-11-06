using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sketch.Rendering;

namespace Sketch.App.Viewport;

/// <summary>Decodes PNG/JPG/BMP/TIFF/GIF with WPF into BGRA pixels (large images are reduced to 4096 px).</summary>
internal sealed class WpfImageDecoder : IImageDecoder
{
    public const int MaxSize = 4096;

    public (byte[] Bgra, int Width, int Height) Decode(byte[] fileBytes)
    {
        var source = Load(fileBytes);
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return (pixels, converted.PixelWidth, converted.PixelHeight);
    }

    /// <summary>Pixel size of an image file (for placing it with the right proportions).</summary>
    public static (int Width, int Height) Size(byte[] fileBytes)
    {
        var frame = BitmapDecoder.Create(new MemoryStream(fileBytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    private static BitmapSource Load(byte[] bytes)
    {
        var frame = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var largest = Math.Max(frame.PixelWidth, frame.PixelHeight);
        if (largest <= MaxSize) return frame;
        var scale = (double)MaxSize / largest;
        return new TransformedBitmap(frame, new ScaleTransform(scale, scale));
    }
}
