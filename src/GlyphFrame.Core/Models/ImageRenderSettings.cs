using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record ImageRenderSettings(
    string FontFamily,
    int FontSize,
    OutputResolution OutputResolution,
    SymbolPolarity SymbolPolarity = SymbolPolarity.LightOnDark)
{
    public static ImageRenderSettings Default { get; } = new(
        "Cascadia Mono",
        10,
        OutputResolution.SameAsSource,
        SymbolPolarity.LightOnDark);
}
