namespace GlyphFrame.Core.Models;

/// <summary>
/// An opaque rendered symbol-art image stored as tightly packed RGBA8888 pixels.
/// Every alpha byte is expected to be 255.
/// </summary>
public sealed class RenderedImage : IDisposable
{
    private byte[]? _rgbaPixels;

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

    public bool IsDisposed => _rgbaPixels is null;

    public ReadOnlyMemory<byte> RgbaPixels => _rgbaPixels
        ?? throw new ObjectDisposedException(nameof(RenderedImage));

    public void Dispose() => Interlocked.Exchange(ref _rgbaPixels, null);
}
