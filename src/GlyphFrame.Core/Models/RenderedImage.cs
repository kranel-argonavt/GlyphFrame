namespace GlyphFrame.Core.Models;

/// <summary>
/// An opaque rendered symbol-art image stored as tightly packed RGBA8888 pixels.
/// Every alpha byte is expected to be 255.
/// </summary>
public sealed class RenderedImage
{
    private readonly byte[] _rgbaPixels;

    public RenderedImage(int width, int height, byte[] rgbaPixels)
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

    public ReadOnlyMemory<byte> RgbaPixels => _rgbaPixels;
}
