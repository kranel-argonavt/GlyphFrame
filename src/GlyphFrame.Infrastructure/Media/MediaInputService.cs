using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

public sealed class MediaInputService(
    IMediaTypeDetector mediaTypeDetector,
    IImageMetadataReader imageMetadataReader,
    IVideoMetadataReader videoMetadataReader) : IMediaInputService
{
    private readonly IMediaTypeDetector _mediaTypeDetector =
        mediaTypeDetector ?? throw new ArgumentNullException(nameof(mediaTypeDetector));
    private readonly IImageMetadataReader _imageMetadataReader =
        imageMetadataReader ?? throw new ArgumentNullException(nameof(imageMetadataReader));
    private readonly IVideoMetadataReader _videoMetadataReader =
        videoMetadataReader ?? throw new ArgumentNullException(nameof(videoMetadataReader));

    public async Task<MediaInputResult> LoadAsync(
        string? inputPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return Failure(MediaInputErrorCategory.EmptyPath, "Choose one supported image or video.");
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(inputPath.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure(MediaInputErrorCategory.InvalidPath, "GlyphFrame could not use this file path.", exception);
        }

        if (Directory.Exists(normalizedPath))
        {
            return Failure(MediaInputErrorCategory.DirectorySelected, "Choose a media file, not a folder.");
        }

        if (!File.Exists(normalizedPath))
        {
            return Failure(MediaInputErrorCategory.FileNotFound, "The selected file no longer exists.");
        }

        MediaSelectionKind mediaType = _mediaTypeDetector.Detect(normalizedPath);
        if (mediaType == MediaSelectionKind.None)
        {
            return Failure(MediaInputErrorCategory.UnsupportedExtension, "This file format is not supported.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (FileStream stream = new(
                normalizedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.Asynchronous))
            {
                byte[] probe = new byte[1];
                _ = await stream.ReadAsync(probe, cancellationToken).ConfigureAwait(false);
            }

            if (mediaType == MediaSelectionKind.Image)
            {
                ImageMetadata image = await _imageMetadataReader
                    .ReadAsync(normalizedPath, cancellationToken)
                    .ConfigureAwait(false);
                return MediaInputResult.ForImage(image);
            }

            VideoMetadata video = await _videoMetadataReader
                .ReadAsync(normalizedPath, cancellationToken)
                .ConfigureAwait(false);
            return MediaInputResult.ForVideo(video);
        }
        catch (OperationCanceledException exception)
        {
            return Failure(MediaInputErrorCategory.Cancelled, "Media loading was cancelled.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(
                MediaInputErrorCategory.AccessDenied,
                "GlyphFrame does not have permission to read this file.",
                exception);
        }
        catch (IOException exception) when (IsSharingViolation(exception))
        {
            return Failure(
                MediaInputErrorCategory.FileInUse,
                "This file is in use and cannot be read right now.",
                exception);
        }
        catch (InvalidDataException exception)
        {
            return InvalidMediaFailure(mediaType, exception);
        }
        catch (IOException exception)
        {
            return InvalidMediaFailure(mediaType, exception);
        }
        catch (Exception exception) when (exception.GetType().Namespace?.StartsWith("MetadataExtractor", StringComparison.Ordinal) == true)
        {
            return InvalidMediaFailure(mediaType, exception);
        }
        catch (Exception exception)
        {
            return Failure(
                MediaInputErrorCategory.UnexpectedFailure,
                "GlyphFrame could not read metadata from this file.",
                exception);
        }
    }

    private static MediaInputResult InvalidMediaFailure(MediaSelectionKind mediaType, Exception exception)
    {
        return mediaType == MediaSelectionKind.Image
            ? Failure(MediaInputErrorCategory.InvalidImage, "GlyphFrame could not read this image.", exception)
            : Failure(MediaInputErrorCategory.InvalidVideo, "GlyphFrame could not read this video.", exception);
    }

    private static MediaInputResult Failure(
        MediaInputErrorCategory category,
        string userMessage,
        Exception? exception = null) =>
        MediaInputResult.Failure(category, userMessage, exception?.ToString());

    private static bool IsSharingViolation(IOException exception)
    {
        int errorCode = exception.HResult & 0xFFFF;
        return errorCode is 32 or 33;
    }
}
