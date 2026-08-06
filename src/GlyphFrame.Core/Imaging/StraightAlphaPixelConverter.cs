namespace GlyphFrame.Core.Imaging;

/// <summary>
/// Adapts tightly packed straight-alpha RGBA8888 pixels to straight-alpha BGRA8888.
/// </summary>
public static class StraightAlphaPixelConverter
{
    public static byte[] RgbaToBgra(ReadOnlySpan<byte> rgbaPixels)
    {
        if (rgbaPixels.Length % 4 != 0)
        {
            throw new ArgumentException("The pixel buffer must contain complete RGBA pixels.", nameof(rgbaPixels));
        }

        byte[] bgraPixels = rgbaPixels.ToArray();
        for (int offset = 0; offset < bgraPixels.Length; offset += 4)
        {
            (bgraPixels[offset], bgraPixels[offset + 2]) = (bgraPixels[offset + 2], bgraPixels[offset]);
        }

        return bgraPixels;
    }
}
