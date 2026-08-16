using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record VideoFrameReadResult
{
    private VideoFrameReadResult(
        VideoFrameReadStatus status,
        VideoFrameReadErrorCategory errorCategory,
        DecodedImage? frame,
        double requestedNormalizedPosition,
        double? actualNormalizedPosition,
        TimeSpan? requestedTimestamp,
        TimeSpan? actualTimestamp,
        long? actualFrameIndex,
        TimeSpan? duration,
        long? frameCount,
        string userMessage,
        string? technicalDetail)
    {
        Status = status;
        ErrorCategory = errorCategory;
        Frame = frame;
        RequestedNormalizedPosition = requestedNormalizedPosition;
        ActualNormalizedPosition = actualNormalizedPosition;
        RequestedTimestamp = requestedTimestamp;
        ActualTimestamp = actualTimestamp;
        ActualFrameIndex = actualFrameIndex;
        Duration = duration;
        FrameCount = frameCount;
        UserMessage = userMessage;
        TechnicalDetail = technicalDetail;
    }

    public VideoFrameReadStatus Status { get; }

    public VideoFrameReadErrorCategory ErrorCategory { get; }

    public bool Success => Status == VideoFrameReadStatus.Success && Frame is not null;

    public DecodedImage? Frame { get; }

    public double RequestedNormalizedPosition { get; }

    public double? ActualNormalizedPosition { get; }

    public TimeSpan? RequestedTimestamp { get; }

    public TimeSpan? ActualTimestamp { get; }

    public long? ActualFrameIndex { get; }

    public TimeSpan? Duration { get; }

    public long? FrameCount { get; }

    public string UserMessage { get; }

    public string? TechnicalDetail { get; }

    public static VideoFrameReadResult ForFrame(
        DecodedImage frame,
        double requestedNormalizedPosition,
        double? actualNormalizedPosition,
        TimeSpan? requestedTimestamp,
        TimeSpan? actualTimestamp,
        long? actualFrameIndex,
        TimeSpan? duration,
        long? frameCount)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return new(
            VideoFrameReadStatus.Success,
            VideoFrameReadErrorCategory.None,
            frame,
            NormalizePosition(requestedNormalizedPosition),
            actualNormalizedPosition is double actual ? NormalizePosition(actual) : null,
            requestedTimestamp,
            actualTimestamp,
            actualFrameIndex,
            duration,
            frameCount,
            "Video frame decoded locally.",
            null);
    }

    public static VideoFrameReadResult Failure(
        VideoFrameReadStatus status,
        VideoFrameReadErrorCategory category,
        double requestedNormalizedPosition,
        string userMessage,
        string? technicalDetail = null)
    {
        if (status is VideoFrameReadStatus.Success or VideoFrameReadStatus.Cancelled)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (category is VideoFrameReadErrorCategory.None or VideoFrameReadErrorCategory.Cancelled)
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        return new(
            status,
            category,
            null,
            NormalizePosition(requestedNormalizedPosition),
            null,
            null,
            null,
            null,
            null,
            null,
            userMessage,
            technicalDetail);
    }

    public static VideoFrameReadResult Cancelled(double requestedNormalizedPosition) => new(
        VideoFrameReadStatus.Cancelled,
        VideoFrameReadErrorCategory.Cancelled,
        null,
        NormalizePosition(requestedNormalizedPosition),
        null,
        null,
        null,
        null,
        null,
        null,
        "Video frame loading was cancelled.",
        null);

    private static double NormalizePosition(double position) => double.IsFinite(position)
        ? Math.Clamp(position, 0d, 1d)
        : 0d;
}
