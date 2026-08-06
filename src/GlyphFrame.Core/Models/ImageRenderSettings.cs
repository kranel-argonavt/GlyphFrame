using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record ImageRenderSettings(
    string FontFamily,
    int FontSize,
    OutputResolution OutputResolution)
{
    public static ImageRenderSettings Default { get; } = new(
        "Cascadia Mono",
        10,
        OutputResolution.SameAsSource);
}
