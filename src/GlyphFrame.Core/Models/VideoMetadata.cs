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
    int? AudioSampleRate);
