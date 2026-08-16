namespace GlyphFrame.Core.Models;

public static class VideoFrameExtractionDefaults
{
    public static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(15);

    public const int MaximumDimension = 16_384;

    public const long MaximumPixelCount = 100_000_000;
}
