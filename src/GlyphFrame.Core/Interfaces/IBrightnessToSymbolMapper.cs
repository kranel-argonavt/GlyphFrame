using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Interfaces;

public interface IBrightnessToSymbolMapper
{
    char Map(byte luminance, string characters, SymbolPolarity polarity);
}
