using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public static class SymbolBackgroundResolver
{
    public static PixelColor Resolve(SymbolPolarity polarity) => polarity switch
    {
        SymbolPolarity.LightOnDark => PixelColor.SymbolBackground,
        SymbolPolarity.DarkOnLight => PixelColor.SymbolLightBackground,
        _ => throw new ArgumentOutOfRangeException(nameof(polarity), polarity, "Unsupported symbol polarity.")
    };
}
