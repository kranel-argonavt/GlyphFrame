namespace GlyphFrame.Core.Models;

public sealed record VideoMetadata(
    string FilePath,
    string FileName,
    int? Width,
    int? Height,
    TimeSpan? Duration,
    double? Fps,
    long? FrameCount,
    string Container,
    long FileSizeBytes,
    string? VideoCodec,
    bool? HasAudio,
    string? AudioCodec,
    int? AudioSampleRate,
    int? RotationDegrees = null)
{
    public int NormalizedRotationDegrees => RotationDegrees is int rotation
        ? ((rotation % 360) + 360) % 360
        : 0;

    public bool SwapsDisplayDimensions => NormalizedRotationDegrees is 90 or 270;

    public int? DisplayWidth => SwapsDisplayDimensions ? Height : Width;

    public int? DisplayHeight => SwapsDisplayDimensions ? Width : Height;
}
