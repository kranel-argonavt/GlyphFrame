using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlyphFrame.Core.Imaging;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Services;

public sealed class WpfImageSourceFactory : IImageSourceFactory
{
    public ImageSource Create(DecodedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Create(image.Width, image.Height, image.RgbaPixels);
    }

    public ImageSource Create(RenderedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Create(image.Dimensions.Width, image.Dimensions.Height, image.RgbaPixels);
    }

    private static BitmapSource Create(int width, int height, ReadOnlyMemory<byte> pixels)
    {
        byte[] bgraPixels = StraightAlphaPixelConverter.RgbaToBgra(pixels.Span);

        BitmapSource source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            bgraPixels,
            checked(width * 4));
        source.Freeze();
        return source;
    }
}
