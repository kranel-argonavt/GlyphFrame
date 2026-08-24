using System.Globalization;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record AudioStreamInfo(
    string? CodecName,
    int? SampleRate,
    int? Channels,
    TimeSpan? Duration,
    TimeSpan? StartTime);

public sealed record AudioProbeResult(
    bool Success,
    bool HasAudio,
    AudioStreamInfo? Stream,
    VideoExportErrorCategory ErrorCategory,
    string UserMessage,
    string? TechnicalDetail = null)
{
    public static AudioProbeResult Present(AudioStreamInfo stream) => new(
        true,
        true,
        stream ?? throw new ArgumentNullException(nameof(stream)),
        VideoExportErrorCategory.None,
        "Source audio is available.");

    public static AudioProbeResult Absent() => new(
        true,
        false,
        null,
        VideoExportErrorCategory.None,
        "The source video does not contain an audio stream.");

    public static AudioProbeResult Failure(
        string userMessage,
        string? technicalDetail = null,
        VideoExportErrorCategory category = VideoExportErrorCategory.AudioProbeFailed) => new(
        false,
        false,
        null,
        category,
        userMessage,
        technicalDetail);
}

public sealed record AudioMuxJob(
    string VideoOnlyInputPath,
    string SourceMediaPath,
    string OutputPath,
    AudioMuxStrategy Strategy,
    TimeSpan VideoDuration);

public sealed record AudioMuxResult(
    bool Success,
    AudioMuxOutcome? Outcome,
    VideoExportErrorCategory ErrorCategory,
    string UserMessage,
    string? TechnicalDetail = null)
{
    public static AudioMuxResult Completed(AudioMuxStrategy strategy) => new(
        true,
        strategy == AudioMuxStrategy.StreamCopy
            ? AudioMuxOutcome.StreamCopied
            : AudioMuxOutcome.TranscodedToAac,
        VideoExportErrorCategory.None,
        strategy == AudioMuxStrategy.StreamCopy
            ? "Source audio was preserved without re-encoding."
            : "Source audio was converted to AAC and preserved.");

    public static AudioMuxResult Failure(
        VideoExportErrorCategory category,
        string userMessage,
        string? technicalDetail = null) => new(
            false,
            null,
            category,
            userMessage,
            technicalDetail);
}

public static class AudioMuxPolicy
{
    public const int AacBitrateKbps = 192;
    public const int AacSampleRate = 48_000;
    public const int AacChannels = 2;
    public static readonly TimeSpan DurationTolerance = TimeSpan.FromMilliseconds(125);

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        return duration.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
