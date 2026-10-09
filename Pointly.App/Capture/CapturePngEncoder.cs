using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pointly.App.Capture;

public static class CapturePngEncoder
{
    public static byte[] Encode(WindowCaptureResult result)
    {
        var bitmap = BitmapSource.Create(
            result.PixelWidth,
            result.PixelHeight,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            result.Bgra32,
            result.PixelWidth * 4);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
