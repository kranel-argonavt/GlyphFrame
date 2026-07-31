namespace GlyphFrame.Core.Models;

public sealed record ApplicationInfo(string Name, string Description, string Status)
{
    public static ApplicationInfo Phase2 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 2 — Repository and Solution");
}
