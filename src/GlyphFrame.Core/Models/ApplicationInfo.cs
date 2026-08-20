namespace GlyphFrame.Core.Models;

public sealed record ApplicationInfo(string Name, string Description, string Status)
{
    public static ApplicationInfo Phase2 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 2 - Repository and Solution");

    public static ApplicationInfo Phase3 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 3 - UI Skeleton");

    public static ApplicationInfo Phase4 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 4 - Settings Infrastructure");

    public static ApplicationInfo Phase5 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 5 - Media Input");

    public static ApplicationInfo Phase6 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 6 - Image Pipeline");

    public static ApplicationInfo Phase7 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 7 - Bundled FFmpeg");

    public static ApplicationInfo Phase8 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 8 - Video Preview");

    public static ApplicationInfo Phase9 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 9 - Streaming Video Export");
}
