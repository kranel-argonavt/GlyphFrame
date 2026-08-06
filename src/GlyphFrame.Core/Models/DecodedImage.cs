namespace GlyphFrame.Core.Models;

/// <summary>
/// A decoded raster image stored as tightly packed RGBA8888 pixels in row-major order.
/// RGB channels are unpremultiplied color values and the alpha channel is straight alpha.
/// Each pixel is therefore represented by four bytes in R, G, B, A order.
/// </summary>
public sealed class DecodedImage
{
    private readonly byte[] _rgbaPixels;

    public DecodedImage(int width, int height, byte[] rgbaPixels)
    {
        Dimensions = new PixelDimensions(width, height);
        ArgumentNullException.ThrowIfNull(rgbaPixels);

        int expectedLength = checked(width * height * 4);
        if (rgbaPixels.Length != expectedLength)
        {
            throw new ArgumentException($"Expected {expectedLength} RGBA bytes.", nameof(rgbaPixels));
        }

        _rgbaPixels = rgbaPixels;
    }

    public PixelDimensions Dimensions { get; }

    public int Width => Dimensions.Width;

    public int Height => Dimensions.Height;

    /// <summary>
    /// Gets tightly packed, unpremultiplied RGBA8888 pixels with straight alpha.
    /// </summary>
    public ReadOnlyMemory<byte> RgbaPixels => _rgbaPixels;
}
