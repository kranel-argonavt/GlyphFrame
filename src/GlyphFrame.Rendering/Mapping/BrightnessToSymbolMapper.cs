using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;

namespace GlyphFrame.Rendering.Mapping;

public sealed class BrightnessToSymbolMapper : IBrightnessToSymbolMapper
{
    public char Map(byte luminance, string characters, SymbolPolarity polarity)
    {
        ArgumentException.ThrowIfNullOrEmpty(characters);
        if (!Enum.IsDefined(polarity))
        {
            throw new ArgumentOutOfRangeException(nameof(polarity), polarity, "Unsupported symbol polarity.");
        }

        int lastIndex = characters.Length - 1;
        int scaledLuminance = polarity == SymbolPolarity.LightOnDark
            ? luminance
            : 255 - luminance;
        int index = (scaledLuminance * lastIndex + 127) / 255;
        return characters[Math.Clamp(index, 0, lastIndex)];
    }
}
