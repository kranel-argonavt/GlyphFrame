namespace GlyphFrame.Core.Models;

public sealed record FfmpegStatusPresentation(
    string Availability,
    string Message,
    string FfmpegVersion,
    string FfprobeVersion,
    string ToolsDirectory,
    string TechnicalDetails)
{
    public static FfmpegStatusPresentation FromValidation(FfmpegValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new FfmpegStatusPresentation(
            result.StatusLabel,
            result.UserMessage,
            result.Ffmpeg.Version ?? "Unavailable",
            result.Ffprobe.Version ?? "Unavailable",
            result.ToolsDirectory,
            result.TechnicalDetails);
    }
}
