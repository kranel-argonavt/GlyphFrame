namespace GlyphFrame.Core.Models;

public readonly record struct PixelColor(byte Red, byte Green, byte Blue, byte Alpha = 255)
{
    public static PixelColor SymbolBackground { get; } = new(13, 15, 20);

    public static PixelColor SymbolLightBackground { get; } = new(245, 245, 242);
}
