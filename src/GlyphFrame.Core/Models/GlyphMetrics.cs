namespace GlyphFrame.Core.Models;

public sealed record GlyphMetrics(
    string RequestedFontFamily,
    string ResolvedFontFamily,
    float FontSizePixels,
    float AdvanceWidth,
    float LineHeight,
    float BaselineOffset,
    bool UsedFallback,
    string? DiagnosticMessage = null)
{
    public bool IsValid => AdvanceWidth > 0 && LineHeight > 0 && BaselineOffset >= 0;
}
