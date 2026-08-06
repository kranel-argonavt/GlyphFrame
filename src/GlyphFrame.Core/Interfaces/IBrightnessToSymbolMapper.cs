namespace GlyphFrame.Core.Interfaces;

public interface IBrightnessToSymbolMapper
{
    char Map(byte luminance, string characters);
}
