using GlyphFrame.Core.Interfaces;

namespace GlyphFrame.Rendering.Mapping;

public sealed class BrightnessToSymbolMapper : IBrightnessToSymbolMapper
{
    public char Map(byte luminance, string characters)
    {
        ArgumentException.ThrowIfNullOrEmpty(characters);

        int lastIndex = characters.Length - 1;
        int index = ((255 - luminance) * lastIndex + 127) / 255;
        return characters[Math.Clamp(index, 0, lastIndex)];
    }
}
