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

    public static ApplicationInfo Phase10 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 10 - Audio");

    public static ApplicationInfo Phase11 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 11 - Progress and Cancellation");

    public static ApplicationInfo Phase11B2 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 11B.2 - Managed Video Artifact Foundation");

    public static ApplicationInfo Phase11B3 { get; } = new(
        "GlyphFrame",
        "Video & Image to Symbol Converter",
        "Phase 11B.3 - Full Video Render and Review UI");
}
