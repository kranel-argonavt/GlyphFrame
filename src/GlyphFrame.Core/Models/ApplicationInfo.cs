namespace GlyphFrame.Core.Models;

public sealed record ApplicationInfo(string Name, string Description, string Status)
{
    public static ApplicationInfo Phase11B4 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 11B.4 - Publish the Reviewed Video Artifact");
}
