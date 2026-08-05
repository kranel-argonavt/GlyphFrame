using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record MediaInputResult
{
    private MediaInputResult(
        bool success,
        MediaSelectionKind mediaType,
        ImageMetadata? imageMetadata,
        VideoMetadata? videoMetadata,
        MediaInputErrorCategory errorCategory,
        string userMessage,
        string? technicalDetail)
    {
        Success = success;
        MediaType = mediaType;
        ImageMetadata = imageMetadata;
        VideoMetadata = videoMetadata;
        ErrorCategory = errorCategory;
        UserMessage = userMessage;
        TechnicalDetail = technicalDetail;
    }

    public bool Success { get; }

    public MediaSelectionKind MediaType { get; }

    public ImageMetadata? ImageMetadata { get; }

    public VideoMetadata? VideoMetadata { get; }

    public MediaInputErrorCategory ErrorCategory { get; }

    public string UserMessage { get; }

    public string? TechnicalDetail { get; }

    public static MediaInputResult ForImage(ImageMetadata metadata, string userMessage = "Image metadata loaded.")
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new(true, MediaSelectionKind.Image, metadata, null, MediaInputErrorCategory.None, userMessage, null);
    }

    public static MediaInputResult ForVideo(VideoMetadata metadata, string userMessage = "Video metadata loaded.")
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new(true, MediaSelectionKind.Video, null, metadata, MediaInputErrorCategory.None, userMessage, null);
    }

    public static MediaInputResult Failure(
        MediaInputErrorCategory category,
        string userMessage,
        string? technicalDetail = null)
    {
        if (category == MediaInputErrorCategory.None)
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        return new(false, MediaSelectionKind.None, null, null, category, userMessage, technicalDetail);
    }
}
