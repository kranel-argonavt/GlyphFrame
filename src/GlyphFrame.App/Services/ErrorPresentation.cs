using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Services;

public enum ErrorPresentationContext
{
    MediaInput,
    ImageRendering,
    ImageExport,
    VideoFramePreview,
    FrameSymbolization,
    VideoRendering,
    VideoPublication,
    ManagedArtifact,
    OriginalPlayback,
    SymbolizedPlayback,
    ToolValidation
}

public enum ApplicationErrorCode
{
    InputRequired,
    InvalidInputPath,
    InputNotFound,
    FolderSelected,
    MultipleInputs,
    UnsupportedInput,
    InputAccessDenied,
    InputLocked,
    InvalidImage,
    InvalidVideo,
    MetadataUnavailable,
    FrameOpenFailed,
    FrameDecodeFailed,
    FrameTimedOut,
    FrameIncomplete,
    FrameRenderFailed,
    InvalidDimensions,
    ToolMissing,
    ToolInvalid,
    ToolAccessDenied,
    ToolTimedOut,
    ImageSettingsInvalid,
    FontUnavailable,
    ImageRenderFailed,
    ImageExportFailed,
    VideoDecodeFailed,
    VideoRenderFailed,
    VideoEncodeFailed,
    AudioProbeFailed,
    AudioMuxFailed,
    VideoFinalizationFailed,
    DestinationUnavailable,
    DestinationAccessDenied,
    InsufficientDiskSpace,
    DestinationConflict,
    AtomicPublicationFailed,
    ArtifactCacheUnavailable,
    ArtifactValidationFailed,
    ArtifactAcceptanceFailed,
    ArtifactCleanupFailed,
    OriginalPlaybackUnavailable,
    SymbolizedPlaybackUnavailable,
    UnexpectedFailure
}

/// <summary>
/// Immutable UI-safe failure information. Primary messages come only from the fixed
/// application catalog; arbitrary diagnostics can enter only TechnicalDetail.
/// </summary>
public sealed record ErrorPresentation
{
    private ErrorPresentation(
        ApplicationErrorCode code,
        ErrorPresentationContext context,
        string userMessage,
        string? technicalDetail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        if (!Enum.IsDefined(context))
        {
            throw new ArgumentOutOfRangeException(nameof(context));
        }

        Code = code;
        Context = context;
        UserMessage = userMessage.Trim();
        TechnicalDetail = NormalizeOptional(technicalDetail);
    }

    public ApplicationErrorCode Code { get; }

    public ErrorPresentationContext Context { get; }

    public string UserMessage { get; }

    public string? TechnicalDetail { get; }

    public static ErrorPresentation Create(
        ApplicationErrorCode code,
        ErrorPresentationContext context,
        string? technicalDetail = null) => new(
        code,
        context,
        FriendlyMessage(code),
        technicalDetail);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FriendlyMessage(ApplicationErrorCode code) => code switch
    {
        ApplicationErrorCode.InputRequired => "Choose one supported image or video.",
        ApplicationErrorCode.InvalidInputPath => "GlyphFrame could not use that file path. Choose another file.",
        ApplicationErrorCode.InputNotFound => "The selected file is no longer available. Choose it again or select another file.",
        ApplicationErrorCode.FolderSelected => "Choose a media file instead of a folder.",
        ApplicationErrorCode.MultipleInputs => "Choose or drop one image or video at a time.",
        ApplicationErrorCode.UnsupportedInput => "This media format is not supported. Choose a supported image or video.",
        ApplicationErrorCode.InputAccessDenied => "GlyphFrame cannot read this file. Check its permissions or choose another file.",
        ApplicationErrorCode.InputLocked => "This file is being used by another application. Close it there and try again.",
        ApplicationErrorCode.InvalidImage => "GlyphFrame could not read this image. The file may be damaged or unsupported.",
        ApplicationErrorCode.InvalidVideo => "GlyphFrame could not read this video. The file may be damaged or unsupported.",
        ApplicationErrorCode.MetadataUnavailable => "GlyphFrame could not read the media information needed to continue.",
        ApplicationErrorCode.FrameOpenFailed => "GlyphFrame could not open this video frame. Choose another position or file.",
        ApplicationErrorCode.FrameDecodeFailed => "GlyphFrame could not decode this video frame. Try another position or video.",
        ApplicationErrorCode.FrameTimedOut => "Loading this video frame took too long. Try another position or file.",
        ApplicationErrorCode.FrameIncomplete => "The decoded video frame was incomplete. Try another position or video.",
        ApplicationErrorCode.FrameRenderFailed => "GlyphFrame could not generate the symbolized frame. Review the settings and try again.",
        ApplicationErrorCode.InvalidDimensions => "The media dimensions are not valid for this operation.",
        ApplicationErrorCode.ToolMissing => "Required bundled video tools are missing. Restore the GlyphFrame application files and try again.",
        ApplicationErrorCode.ToolInvalid => "The bundled video tools could not be validated. Reinstall or restore the GlyphFrame application files.",
        ApplicationErrorCode.ToolAccessDenied => "GlyphFrame cannot start its bundled video tools. Check application-file permissions and try again.",
        ApplicationErrorCode.ToolTimedOut => "The bundled video tools did not respond in time. Try again.",
        ApplicationErrorCode.ImageSettingsInvalid => "The current image render settings cannot be used. Review the settings and try again.",
        ApplicationErrorCode.FontUnavailable => "The selected font is unavailable. Choose another font and try again.",
        ApplicationErrorCode.ImageRenderFailed => "GlyphFrame could not render the image. Review the settings and try again.",
        ApplicationErrorCode.ImageExportFailed => "GlyphFrame could not export the image. Choose another destination and try again.",
        ApplicationErrorCode.VideoDecodeFailed => "GlyphFrame could not decode the video. Try another video or check the bundled video tools.",
        ApplicationErrorCode.VideoRenderFailed => "GlyphFrame could not render the complete video. You can try again.",
        ApplicationErrorCode.VideoEncodeFailed => "GlyphFrame could not encode the rendered video. Check the bundled video tools and try again.",
        ApplicationErrorCode.AudioProbeFailed => "GlyphFrame could not inspect the source audio reliably. Check the bundled video tools and try again.",
        ApplicationErrorCode.AudioMuxFailed => "GlyphFrame could not preserve the source audio. Try again or choose Remove Audio.",
        ApplicationErrorCode.VideoFinalizationFailed => "GlyphFrame could not finalize the rendered video. Check available storage and try again.",
        ApplicationErrorCode.DestinationUnavailable => "The selected output location is unavailable. Choose another destination.",
        ApplicationErrorCode.DestinationAccessDenied => "GlyphFrame cannot write to this location. Choose a destination you can modify.",
        ApplicationErrorCode.InsufficientDiskSpace => "There is not enough free disk space to complete this operation. Free space or choose another drive.",
        ApplicationErrorCode.DestinationConflict => "The destination changed or conflicts with a protected file. Choose another destination and try again.",
        ApplicationErrorCode.AtomicPublicationFailed => "GlyphFrame could not safely publish the completed file. The existing destination was preserved.",
        ApplicationErrorCode.ArtifactCacheUnavailable => "GlyphFrame could not use its local review cache. Check available storage and permissions, then try again.",
        ApplicationErrorCode.ArtifactValidationFailed => "The completed review video could not be validated. Render the video again.",
        ApplicationErrorCode.ArtifactAcceptanceFailed => "GlyphFrame could not retain the completed review video. Render the video again.",
        ApplicationErrorCode.ArtifactCleanupFailed => "GlyphFrame could not remove one of its temporary review files. It will retry cleanup later.",
        ApplicationErrorCode.OriginalPlaybackUnavailable => "Continuous playback is unavailable for this video. Frame preview and rendering remain available.",
        ApplicationErrorCode.SymbolizedPlaybackUnavailable => "GlyphFrame could not open the rendered video for review. The review file is retained.",
        ApplicationErrorCode.UnexpectedFailure => "GlyphFrame could not complete this operation. Try again.",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };
}

public interface IErrorPresentationService
{
    ErrorPresentation? From(MediaInputResult result);

    ErrorPresentation? From(VideoFrameReadResult result);

    ErrorPresentation? From(ImageProcessingException exception, ErrorPresentationContext context);

    ErrorPresentation? From(VideoExportResult result, ErrorPresentationContext context);

    ErrorPresentation? From(ManagedVideoRenderResult result);

    ErrorPresentation? From(FfmpegToolValidationResult result);

    ErrorPresentation PlaybackFailure(
        ErrorPresentationContext context,
        string? sourceMessage,
        string? technicalDetail);

    ErrorPresentation? FromException(ErrorPresentationContext context, Exception exception);

    bool IsRecoverable(ErrorPresentationContext context, Exception exception);

    bool IsFatal(Exception exception);
}

public sealed class ErrorPresentationService : IErrorPresentationService
{
    private const int ErrorDiskFull = 0x70;
    private const int ErrorHandleDiskFull = 0x27;
    private const int ErrorAccessDenied = 0x05;

    public ErrorPresentation? From(MediaInputResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Success || result.ErrorCategory is MediaInputErrorCategory.Cancelled or MediaInputErrorCategory.None)
        {
            return null;
        }

        ApplicationErrorCode code = result.ErrorCategory switch
        {
            MediaInputErrorCategory.EmptyPath => ApplicationErrorCode.InputRequired,
            MediaInputErrorCategory.InvalidPath => ApplicationErrorCode.InvalidInputPath,
            MediaInputErrorCategory.FileNotFound => ApplicationErrorCode.InputNotFound,
            MediaInputErrorCategory.DirectorySelected => ApplicationErrorCode.FolderSelected,
            MediaInputErrorCategory.MultipleFiles => ApplicationErrorCode.MultipleInputs,
            MediaInputErrorCategory.UnsupportedExtension => ApplicationErrorCode.UnsupportedInput,
            MediaInputErrorCategory.AccessDenied => ApplicationErrorCode.InputAccessDenied,
            MediaInputErrorCategory.FileInUse => ApplicationErrorCode.InputLocked,
            MediaInputErrorCategory.InvalidImage => ApplicationErrorCode.InvalidImage,
            MediaInputErrorCategory.InvalidVideo => ApplicationErrorCode.InvalidVideo,
            MediaInputErrorCategory.MetadataUnavailable => ApplicationErrorCode.MetadataUnavailable,
            _ => ApplicationErrorCode.UnexpectedFailure
        };
        return Create(code, ErrorPresentationContext.MediaInput, result.UserMessage, result.TechnicalDetail);
    }

    public ErrorPresentation? From(VideoFrameReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Success || result.Status == VideoFrameReadStatus.Cancelled
            || result.ErrorCategory is VideoFrameReadErrorCategory.Cancelled or VideoFrameReadErrorCategory.None)
        {
            return null;
        }

        ApplicationErrorCode code = result.ErrorCategory switch
        {
            VideoFrameReadErrorCategory.InvalidPath => ApplicationErrorCode.InvalidInputPath,
            VideoFrameReadErrorCategory.FileNotFound => ApplicationErrorCode.InputNotFound,
            VideoFrameReadErrorCategory.AccessDenied => ApplicationErrorCode.InputAccessDenied,
            VideoFrameReadErrorCategory.InvalidMedia => ApplicationErrorCode.InvalidVideo,
            VideoFrameReadErrorCategory.OpenFailed => ApplicationErrorCode.FrameOpenFailed,
            VideoFrameReadErrorCategory.NoVideoStream => ApplicationErrorCode.InvalidVideo,
            VideoFrameReadErrorCategory.DecodeFailed => ApplicationErrorCode.FrameDecodeFailed,
            VideoFrameReadErrorCategory.DecoderUnavailable => ApplicationErrorCode.ToolMissing,
            VideoFrameReadErrorCategory.ProcessStartFailed => ApplicationErrorCode.ToolInvalid,
            VideoFrameReadErrorCategory.TimedOut => ApplicationErrorCode.FrameTimedOut,
            VideoFrameReadErrorCategory.InvalidDimensions => ApplicationErrorCode.InvalidDimensions,
            VideoFrameReadErrorCategory.IncompleteOutput => ApplicationErrorCode.FrameIncomplete,
            VideoFrameReadErrorCategory.MetadataUnavailable => ApplicationErrorCode.MetadataUnavailable,
            _ => ApplicationErrorCode.UnexpectedFailure
        };
        return Create(code, ErrorPresentationContext.VideoFramePreview,
            result.UserMessage, result.TechnicalDetail);
    }

    public ErrorPresentation? From(ImageProcessingException exception, ErrorPresentationContext context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception.Category == ImageProcessingErrorCategory.Cancelled)
        {
            return null;
        }

        ApplicationErrorCode code = exception.Category switch
        {
            ImageProcessingErrorCategory.InvalidPath => context == ErrorPresentationContext.ImageExport
                ? ApplicationErrorCode.DestinationUnavailable : ApplicationErrorCode.InvalidInputPath,
            ImageProcessingErrorCategory.FileNotFound => ApplicationErrorCode.InputNotFound,
            ImageProcessingErrorCategory.AccessDenied => context == ErrorPresentationContext.ImageExport
                ? ApplicationErrorCode.DestinationAccessDenied : ApplicationErrorCode.InputAccessDenied,
            ImageProcessingErrorCategory.InvalidImage => ApplicationErrorCode.InvalidImage,
            ImageProcessingErrorCategory.UnsupportedDimensions => ApplicationErrorCode.InvalidDimensions,
            ImageProcessingErrorCategory.InvalidSettings => ApplicationErrorCode.ImageSettingsInvalid,
            ImageProcessingErrorCategory.FontUnavailable => ApplicationErrorCode.FontUnavailable,
            ImageProcessingErrorCategory.RenderingFailed => context == ErrorPresentationContext.FrameSymbolization
                ? ApplicationErrorCode.FrameRenderFailed : ApplicationErrorCode.ImageRenderFailed,
            ImageProcessingErrorCategory.OutputUnavailable => ApplicationErrorCode.DestinationUnavailable,
            ImageProcessingErrorCategory.EncodingFailed => ApplicationErrorCode.ImageExportFailed,
            _ => DefaultUnexpectedCode(context)
        };
        return Create(code, context, exception.UserMessage, exception.TechnicalDetail ?? exception.ToString());
    }

    public ErrorPresentation? From(VideoExportResult result, ErrorPresentationContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Success || result.Status == VideoExportStatus.Cancelled
            || result.ErrorCategory is VideoExportErrorCategory.Cancelled or VideoExportErrorCategory.None)
        {
            return null;
        }

        ApplicationErrorCode code = context == ErrorPresentationContext.VideoPublication
            ? MapVideoPublicationError(result.ErrorCategory)
            : MapVideoPipelineError(result.ErrorCategory);
        return Create(code, context, result.UserMessage, result.TechnicalDetail);
    }

    public ErrorPresentation? From(ManagedVideoRenderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Success || result.Status == ManagedVideoArtifactStatus.Cancelled
            || result.ErrorCategory is ManagedVideoArtifactErrorCategory.Cancelled or ManagedVideoArtifactErrorCategory.None)
        {
            return null;
        }

        ApplicationErrorCode code = result.PipelineErrorCategory is VideoExportErrorCategory pipelineCategory
            && pipelineCategory is not VideoExportErrorCategory.None and not VideoExportErrorCategory.Cancelled
            ? MapVideoPipelineError(pipelineCategory)
            : result.ErrorCategory switch
            {
                ManagedVideoArtifactErrorCategory.InvalidJob => ApplicationErrorCode.VideoRenderFailed,
                ManagedVideoArtifactErrorCategory.RenderFailed => ApplicationErrorCode.VideoRenderFailed,
                ManagedVideoArtifactErrorCategory.CacheUnavailable => ApplicationErrorCode.ArtifactCacheUnavailable,
                ManagedVideoArtifactErrorCategory.ValidationFailed => ApplicationErrorCode.ArtifactValidationFailed,
                ManagedVideoArtifactErrorCategory.FinalizationFailed => ApplicationErrorCode.ArtifactAcceptanceFailed,
                _ => ApplicationErrorCode.UnexpectedFailure
            };
        return Create(code, ErrorPresentationContext.VideoRendering,
            result.UserMessage, result.TechnicalDetail);
    }

    public ErrorPresentation? From(FfmpegToolValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsAvailable)
        {
            return null;
        }

        ApplicationErrorCode code = result.Status switch
        {
            FfmpegToolValidationStatus.Missing => ApplicationErrorCode.ToolMissing,
            FfmpegToolValidationStatus.AccessDenied => ApplicationErrorCode.ToolAccessDenied,
            FfmpegToolValidationStatus.TimedOut => ApplicationErrorCode.ToolTimedOut,
            _ => ApplicationErrorCode.ToolInvalid
        };
        return Create(code, ErrorPresentationContext.ToolValidation,
            result.UserMessage, result.TechnicalDetails);
    }

    public ErrorPresentation PlaybackFailure(
        ErrorPresentationContext context,
        string? sourceMessage,
        string? technicalDetail)
    {
        ApplicationErrorCode code = context switch
        {
            ErrorPresentationContext.OriginalPlayback => ApplicationErrorCode.OriginalPlaybackUnavailable,
            ErrorPresentationContext.SymbolizedPlayback => ApplicationErrorCode.SymbolizedPlaybackUnavailable,
            _ => ApplicationErrorCode.UnexpectedFailure
        };
        return Create(code, context, sourceMessage, technicalDetail);
    }

    public ErrorPresentation? FromException(ErrorPresentationContext context, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is OperationCanceledException)
        {
            return null;
        }

        if (!IsRecoverable(context, exception))
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        ApplicationErrorCode code = exception switch
        {
            UnauthorizedAccessException when context == ErrorPresentationContext.ManagedArtifact
                => ApplicationErrorCode.ArtifactCacheUnavailable,
            UnauthorizedAccessException => IsDestinationContext(context)
                ? ApplicationErrorCode.DestinationAccessDenied : ApplicationErrorCode.InputAccessDenied,
            DirectoryNotFoundException when context == ErrorPresentationContext.ManagedArtifact
                => ApplicationErrorCode.ArtifactCacheUnavailable,
            DirectoryNotFoundException => IsDestinationContext(context)
                ? ApplicationErrorCode.DestinationUnavailable : ApplicationErrorCode.InputNotFound,
            FileNotFoundException when context == ErrorPresentationContext.ManagedArtifact
                => ApplicationErrorCode.ArtifactValidationFailed,
            FileNotFoundException => ApplicationErrorCode.InputNotFound,
            TimeoutException => context switch
            {
                ErrorPresentationContext.VideoFramePreview => ApplicationErrorCode.FrameTimedOut,
                ErrorPresentationContext.ToolValidation => ApplicationErrorCode.ToolTimedOut,
                _ => DefaultUnexpectedCode(context)
            },
            IOException ioException when IsDiskFull(ioException) => ApplicationErrorCode.InsufficientDiskSpace,
            IOException when context == ErrorPresentationContext.ManagedArtifact
                => ApplicationErrorCode.ArtifactCacheUnavailable,
            IOException when IsDestinationContext(context) => ApplicationErrorCode.DestinationUnavailable,
            Win32Exception win32Exception when win32Exception.NativeErrorCode is ErrorDiskFull or ErrorHandleDiskFull
                => ApplicationErrorCode.InsufficientDiskSpace,
            Win32Exception win32Exception when win32Exception.NativeErrorCode == ErrorAccessDenied
                => IsDestinationContext(context)
                    ? ApplicationErrorCode.DestinationAccessDenied : ApplicationErrorCode.InputAccessDenied,
            InvalidOperationException when context == ErrorPresentationContext.ManagedArtifact
                => ApplicationErrorCode.ArtifactAcceptanceFailed,
            _ => DefaultUnexpectedCode(context)
        };
        return ErrorPresentation.Create(code, context, exception.ToString());
    }

    public bool IsFatal(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is OutOfMemoryException or StackOverflowException or AccessViolationException)
        {
            return true;
        }

        return exception is AggregateException aggregate
            ? aggregate.InnerExceptions.Any(IsFatal)
            : exception.InnerException is not null && IsFatal(exception.InnerException);
    }

    public bool IsRecoverable(ErrorPresentationContext context, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!Enum.IsDefined(context) || exception is OperationCanceledException || IsFatal(exception))
        {
            return false;
        }

        if (exception is AggregateException aggregate)
        {
            return aggregate.InnerExceptions.Count > 0
                && aggregate.InnerExceptions.All(inner => IsRecoverable(context, inner));
        }

        bool allowed = context switch
        {
            ErrorPresentationContext.MediaInput => IsExpectedBoundaryException(exception, allowOverflow: true),
            ErrorPresentationContext.ImageRendering => IsExpectedBoundaryException(exception, allowOverflow: true),
            ErrorPresentationContext.ImageExport => IsExpectedBoundaryException(exception, allowOverflow: true),
            ErrorPresentationContext.VideoFramePreview => IsExpectedBoundaryException(exception, allowOverflow: true),
            ErrorPresentationContext.FrameSymbolization => IsExpectedBoundaryException(exception, allowOverflow: true),
            ErrorPresentationContext.VideoRendering => IsExpectedBoundaryException(exception, allowOverflow: true),
            ErrorPresentationContext.VideoPublication => IsExpectedBoundaryException(exception, allowOverflow: false),
            ErrorPresentationContext.ManagedArtifact => IsExpectedBoundaryException(exception, allowOverflow: true),
            ErrorPresentationContext.OriginalPlayback => IsExpectedBoundaryException(exception, allowOverflow: false),
            ErrorPresentationContext.SymbolizedPlayback => IsExpectedBoundaryException(exception, allowOverflow: false),
            ErrorPresentationContext.ToolValidation => IsExpectedBoundaryException(exception, allowOverflow: false),
            _ => false
        };
        return allowed && (exception.InnerException is null
            || IsRecoverable(context, exception.InnerException));
    }

    private static bool IsExpectedBoundaryException(Exception exception, bool allowOverflow) => exception is
        IOException or UnauthorizedAccessException or SecurityException or Win32Exception
        or TimeoutException or NotSupportedException
        || exception is ArgumentException and not ArgumentNullException
        || exception is InvalidOperationException and not ObjectDisposedException
        || (allowOverflow && exception is OverflowException);

    private static ErrorPresentation Create(
        ApplicationErrorCode code,
        ErrorPresentationContext context,
        string? sourceMessage,
        string? technicalDetail) => ErrorPresentation.Create(
        code,
        context,
        CombineTechnical(sourceMessage, technicalDetail));

    private static string? CombineTechnical(string? sourceMessage, string? technicalDetail)
    {
        string? source = string.IsNullOrWhiteSpace(sourceMessage) ? null : sourceMessage.Trim();
        string? detail = string.IsNullOrWhiteSpace(technicalDetail) ? null : technicalDetail.Trim();
        if (source is null)
        {
            return detail;
        }

        if (detail is null)
        {
            return $"Source message: {source}";
        }

        return string.Equals(source, detail, StringComparison.Ordinal)
            ? detail
            : $"Source message: {source}{Environment.NewLine}{Environment.NewLine}Technical detail:{Environment.NewLine}{detail}";
    }

    private static bool IsDestinationContext(ErrorPresentationContext context) => context is
        ErrorPresentationContext.ImageExport or ErrorPresentationContext.VideoPublication;

    private static ApplicationErrorCode MapVideoPipelineError(VideoExportErrorCategory category) => category switch
    {
        VideoExportErrorCategory.InvalidInput => ApplicationErrorCode.InvalidVideo,
        VideoExportErrorCategory.InvalidOutput => ApplicationErrorCode.DestinationUnavailable,
        VideoExportErrorCategory.DecoderUnavailable => ApplicationErrorCode.ToolMissing,
        VideoExportErrorCategory.MetadataUnavailable => ApplicationErrorCode.MetadataUnavailable,
        VideoExportErrorCategory.InvalidDimensions => ApplicationErrorCode.InvalidDimensions,
        VideoExportErrorCategory.DecodeFailed or VideoExportErrorCategory.TruncatedFrame
            => ApplicationErrorCode.VideoDecodeFailed,
        VideoExportErrorCategory.RenderingFailed => ApplicationErrorCode.VideoRenderFailed,
        VideoExportErrorCategory.EncoderStartFailed or VideoExportErrorCategory.EncoderWriteFailed
            or VideoExportErrorCategory.EncoderFailed => ApplicationErrorCode.VideoEncodeFailed,
        VideoExportErrorCategory.AudioMetadataUnavailable or VideoExportErrorCategory.AudioProbeFailed
            => ApplicationErrorCode.AudioProbeFailed,
        VideoExportErrorCategory.AudioMuxStartFailed or VideoExportErrorCategory.AudioStreamCopyFailed
            or VideoExportErrorCategory.AudioTranscodeFailed or VideoExportErrorCategory.AudioMuxFailed
            => ApplicationErrorCode.AudioMuxFailed,
        VideoExportErrorCategory.OutputFinalizationFailed => ApplicationErrorCode.VideoFinalizationFailed,
        _ => ApplicationErrorCode.UnexpectedFailure
    };

    private static ApplicationErrorCode MapVideoPublicationError(VideoExportErrorCategory category) => category switch
    {
        VideoExportErrorCategory.InvalidInput => ApplicationErrorCode.ArtifactValidationFailed,
        VideoExportErrorCategory.InvalidOutput => ApplicationErrorCode.DestinationUnavailable,
        VideoExportErrorCategory.DestinationAccessDenied => ApplicationErrorCode.DestinationAccessDenied,
        VideoExportErrorCategory.InsufficientDiskSpace => ApplicationErrorCode.InsufficientDiskSpace,
        VideoExportErrorCategory.DestinationConflict => ApplicationErrorCode.DestinationConflict,
        _ => ApplicationErrorCode.AtomicPublicationFailed
    };

    private static ApplicationErrorCode DefaultUnexpectedCode(ErrorPresentationContext context) => context switch
    {
        ErrorPresentationContext.MediaInput => ApplicationErrorCode.UnexpectedFailure,
        ErrorPresentationContext.ImageRendering => ApplicationErrorCode.ImageRenderFailed,
        ErrorPresentationContext.ImageExport => ApplicationErrorCode.ImageExportFailed,
        ErrorPresentationContext.VideoFramePreview => ApplicationErrorCode.FrameDecodeFailed,
        ErrorPresentationContext.FrameSymbolization => ApplicationErrorCode.FrameRenderFailed,
        ErrorPresentationContext.VideoRendering => ApplicationErrorCode.VideoRenderFailed,
        ErrorPresentationContext.VideoPublication => ApplicationErrorCode.AtomicPublicationFailed,
        ErrorPresentationContext.ManagedArtifact => ApplicationErrorCode.ArtifactAcceptanceFailed,
        ErrorPresentationContext.OriginalPlayback => ApplicationErrorCode.OriginalPlaybackUnavailable,
        ErrorPresentationContext.SymbolizedPlayback => ApplicationErrorCode.SymbolizedPlaybackUnavailable,
        ErrorPresentationContext.ToolValidation => ApplicationErrorCode.ToolInvalid,
        _ => ApplicationErrorCode.UnexpectedFailure
    };

    private static bool IsDiskFull(IOException exception)
    {
        int code = exception.HResult & 0xFFFF;
        return code is ErrorDiskFull or ErrorHandleDiskFull;
    }
}
