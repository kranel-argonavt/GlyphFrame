using System.Globalization;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record SequentialVideoDecodeJob(
    string InputPath,
    PixelDimensions DisplayDimensions);

public sealed record DecodedVideoFrame(long SequenceNumber, DecodedImage Frame);

public sealed record VideoEncodingJob(
    string OutputPath,
    PixelDimensions FrameDimensions,
    double FramesPerSecond);

public sealed record VideoEncodingResult(
    bool Success,
    long FramesWritten,
    string? UserMessage = null,
    string? TechnicalDetail = null,
    VideoExportErrorCategory ErrorCategory = VideoExportErrorCategory.None);

public sealed record VideoExportJob(
    string InputPath,
    string OutputPath,
    VideoMetadata Metadata,
    ImageRenderSettings RenderSettings,
    CharacterPresetOption CharacterPreset,
    bool Overwrite);

public sealed record VideoExportResult(
    VideoExportStatus Status,
    VideoExportErrorCategory ErrorCategory,
    string UserMessage,
    string? TechnicalDetail,
    string? OutputPath,
    long FramesWritten,
    PixelDimensions? OutputDimensions,
    double? FramesPerSecond)
{
    public bool Success => Status == VideoExportStatus.Completed;

    public static VideoExportResult Completed(
        string outputPath,
        long framesWritten,
        PixelDimensions dimensions,
        double framesPerSecond) => new(
            VideoExportStatus.Completed,
            VideoExportErrorCategory.None,
            "The symbolized video was exported successfully.",
            null,
            outputPath,
            framesWritten,
            dimensions,
            framesPerSecond);

    public static VideoExportResult Failure(
        VideoExportErrorCategory category,
        string userMessage,
        string? technicalDetail = null) => new(
            VideoExportStatus.Failed,
            category,
            userMessage,
            technicalDetail,
            null,
            0,
            null,
            null);

    public static VideoExportResult Cancelled() => new(
        VideoExportStatus.Cancelled,
        VideoExportErrorCategory.Cancelled,
        "Video export was cancelled. No partial output was kept.",
        null,
        null,
        0,
        null,
        null);
}

public sealed class VideoExportException : Exception
{
    public VideoExportException(
        VideoExportErrorCategory category,
        string userMessage,
        string? technicalDetail = null,
        Exception? innerException = null)
        : base(technicalDetail ?? userMessage, innerException)
    {
        Category = category;
        UserMessage = userMessage;
        TechnicalDetail = technicalDetail;
    }

    public VideoExportErrorCategory Category { get; }

    public string UserMessage { get; }

    public string? TechnicalDetail { get; }
}

public static class VideoExportPolicy
{
    public const double FallbackFramesPerSecond = 30d;
    public const double MaximumFramesPerSecond = 240d;

    public static double NormalizeFramesPerSecond(double? sourceFramesPerSecond) =>
        sourceFramesPerSecond is double value
        && double.IsFinite(value)
        && value > 0d
        && value <= MaximumFramesPerSecond
            ? value
            : FallbackFramesPerSecond;

    public static string FormatFramesPerSecond(double framesPerSecond) =>
        NormalizeFramesPerSecond(framesPerSecond).ToString("0.######", CultureInfo.InvariantCulture);

    public static PixelDimensions NormalizeForYuv420(PixelDimensions dimensions)
    {
        if (dimensions.Width < 2 || dimensions.Height < 2)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidDimensions,
                "The selected output dimensions are too small for MP4 export.");
        }

        int width = dimensions.Width & ~1;
        int height = dimensions.Height & ~1;
        return new PixelDimensions(width, height);
    }
}
