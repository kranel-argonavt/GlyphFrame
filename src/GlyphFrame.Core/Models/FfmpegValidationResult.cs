using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record FfmpegToolValidationResult(
    FfmpegToolKind Tool,
    FfmpegToolValidationStatus Status,
    string ResolvedPath,
    string? Version,
    string UserMessage,
    string? TechnicalDetails)
{
    public bool IsAvailable => Status == FfmpegToolValidationStatus.Available;

    public string DisplayName => Tool == FfmpegToolKind.Ffmpeg ? "FFmpeg" : "ffprobe";
}

public sealed record FfmpegValidationResult(
    string ToolsDirectory,
    FfmpegToolValidationResult Ffmpeg,
    FfmpegToolValidationResult Ffprobe,
    DateTimeOffset ValidatedAt)
{
    public bool IsAvailable => Ffmpeg.IsAvailable && Ffprobe.IsAvailable;

    public string StatusLabel => IsAvailable ? "Available" : "Unavailable";

    public string UserMessage => IsAvailable
        ? $"Bundled FFmpeg {Ffmpeg.Version} and ffprobe {Ffprobe.Version} are available."
        : string.Join(" ", new[] { Ffmpeg, Ffprobe }.Where(result => !result.IsAvailable).Select(result => result.UserMessage));

    public string TechnicalDetails => string.Join(
        Environment.NewLine,
        new[] { Ffmpeg, Ffprobe }
            .Select(result => $"{result.DisplayName}: {result.ResolvedPath}{Environment.NewLine}{result.TechnicalDetails ?? "No additional diagnostic details."}"));
}
