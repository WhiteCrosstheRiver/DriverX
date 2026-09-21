using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriverX.Desktop;

// Interpret the approved monochrome artwork as coverage, so the window theme
// supplies the ink color without painting the source image's black rectangle.
public static class BrandMask
{
    public static ImageBrush Brush { get; } = Create();

    static ImageBrush Create()
    {
        var source = new BitmapImage(new Uri("pack://application:,,,/DriverX;component/Assets/driverx-logo.png"));
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = converted.PixelWidth, height = converted.PixelHeight;
        var pixels = new byte[width * height * 4];
        converted.CopyPixels(pixels, width * 4, 0);
        int left = width, top = height, right = 0, bottom = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = (y * width + x) * 4;
            int luminance = (pixels[i] * 19 + pixels[i + 1] * 183 + pixels[i + 2] * 54) / 256;
            byte alpha = (byte)(Math.Clamp((luminance - 16) * 255 / 223, 0, 255) * pixels[i + 3] / 255);
            pixels[i] = pixels[i + 1] = pixels[i + 2] = 255;
            pixels[i + 3] = alpha;
            if (alpha > 16) { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        BitmapSource mask = left <= right ? new CroppedBitmap(bitmap, new System.Windows.Int32Rect(left, top, right-left+1, bottom-top+1)) : bitmap;
        mask.Freeze();
        var brush = new ImageBrush(mask) { Stretch = Stretch.Uniform };
        brush.Freeze();
        return brush;
    }
}
