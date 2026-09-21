using System.ComponentModel;
using System.IO;
using GlyphFrame.App.Services;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Tests;

public sealed class ErrorPresentationTests
{
    private readonly ErrorPresentationService _service = new();

    [Fact]
    public void Model_UsesCatalogMessageAndNormalizesOptionalTechnicalDetail()
    {
        ErrorPresentation presentation = ErrorPresentation.Create(
            ApplicationErrorCode.InvalidVideo,
            ErrorPresentationContext.MediaInput,
            "  diagnostic detail  ");
        ErrorPresentation noDetail = ErrorPresentation.Create(
            ApplicationErrorCode.InvalidImage,
            ErrorPresentationContext.MediaInput,
            "   ");

        Assert.False(string.IsNullOrWhiteSpace(presentation.UserMessage));
        Assert.Equal("diagnostic detail", presentation.TechnicalDetail);
        Assert.Null(noDetail.TechnicalDetail);
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorPresentation.Create(
            (ApplicationErrorCode)int.MaxValue,
            ErrorPresentationContext.MediaInput));
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorPresentation.Create(
            ApplicationErrorCode.InvalidVideo,
            (ErrorPresentationContext)int.MaxValue));
    }

    [Fact]
    public void Catalog_CoversEveryStableCodeWithANonEmptySafeMessage()
    {
        const string raw = @"C:\private\source.mkv HRESULT=0x80004005 ffmpeg stderr";

        foreach (ApplicationErrorCode code in Enum.GetValues<ApplicationErrorCode>())
        {
            ErrorPresentation presentation = ErrorPresentation.Create(
                code,
                ErrorPresentationContext.MediaInput,
                raw);

            Assert.False(string.IsNullOrWhiteSpace(presentation.UserMessage));
            Assert.DoesNotContain(raw, presentation.UserMessage, StringComparison.Ordinal);
            Assert.Equal(raw, presentation.TechnicalDetail);
        }
    }

    [Fact]
    public void MissingSourceMessages_UseCatalogFallbackWithoutFabricatingTechnicalDetail()
    {
        ErrorPresentation presentation = _service.PlaybackFailure(
            ErrorPresentationContext.OriginalPlayback,
            "   ",
            "   ");

        Assert.Equal(ApplicationErrorCode.OriginalPlaybackUnavailable, presentation.Code);
        Assert.False(string.IsNullOrWhiteSpace(presentation.UserMessage));
        Assert.Null(presentation.TechnicalDetail);
    }

    [Fact]
    public void SourceMessageAndTechnicalDetail_AreBothRetainedOutsidePrimaryMessage()
    {
        ErrorPresentation presentation = _service.PlaybackFailure(
            ErrorPresentationContext.SymbolizedPlayback,
            "lower-layer user message",
            "native diagnostic detail");

        Assert.Equal(ApplicationErrorCode.SymbolizedPlaybackUnavailable, presentation.Code);
        Assert.DoesNotContain("lower-layer", presentation.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("native diagnostic", presentation.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lower-layer user message", presentation.TechnicalDetail);
        Assert.Contains("native diagnostic detail", presentation.TechnicalDetail);
    }

    [Theory]
    [InlineData(MediaInputErrorCategory.EmptyPath)]
    [InlineData(MediaInputErrorCategory.InvalidPath)]
    [InlineData(MediaInputErrorCategory.FileNotFound)]
    [InlineData(MediaInputErrorCategory.DirectorySelected)]
    [InlineData(MediaInputErrorCategory.MultipleFiles)]
    [InlineData(MediaInputErrorCategory.UnsupportedExtension)]
    [InlineData(MediaInputErrorCategory.AccessDenied)]
    [InlineData(MediaInputErrorCategory.FileInUse)]
    [InlineData(MediaInputErrorCategory.InvalidImage)]
    [InlineData(MediaInputErrorCategory.InvalidVideo)]
    [InlineData(MediaInputErrorCategory.MetadataUnavailable)]
    [InlineData(MediaInputErrorCategory.UnexpectedFailure)]
    public void MediaInputCategories_MapToSafeFriendlyPresentation(MediaInputErrorCategory category)
    {
        const string raw = @"C:\private\source.mkv HRESULT=0x80004005 ffmpeg stderr";
        ErrorPresentation? presentation = _service.From(MediaInputResult.Failure(category, raw, raw));

        AssertPresentation(presentation, ErrorPresentationContext.MediaInput, raw);
    }

    [Theory]
    [InlineData(VideoFrameReadErrorCategory.InvalidPath)]
    [InlineData(VideoFrameReadErrorCategory.FileNotFound)]
    [InlineData(VideoFrameReadErrorCategory.AccessDenied)]
    [InlineData(VideoFrameReadErrorCategory.InvalidMedia)]
    [InlineData(VideoFrameReadErrorCategory.OpenFailed)]
    [InlineData(VideoFrameReadErrorCategory.NoVideoStream)]
    [InlineData(VideoFrameReadErrorCategory.DecodeFailed)]
    [InlineData(VideoFrameReadErrorCategory.DecoderUnavailable)]
    [InlineData(VideoFrameReadErrorCategory.ProcessStartFailed)]
    [InlineData(VideoFrameReadErrorCategory.TimedOut)]
    [InlineData(VideoFrameReadErrorCategory.InvalidDimensions)]
    [InlineData(VideoFrameReadErrorCategory.IncompleteOutput)]
    [InlineData(VideoFrameReadErrorCategory.MetadataUnavailable)]
    [InlineData(VideoFrameReadErrorCategory.UnexpectedFailure)]
    public void FrameCategories_MapToSafeFriendlyPresentation(VideoFrameReadErrorCategory category)
    {
        const string raw = "System.InvalidOperationException: raw decoder failure\n   at Decoder.Read()";
        VideoFrameReadResult result = VideoFrameReadResult.Failure(
            VideoFrameReadStatus.Failed,
            category,
            0.5,
            raw,
            raw);

        ErrorPresentation? presentation = _service.From(result);

        AssertPresentation(presentation, ErrorPresentationContext.VideoFramePreview, raw);
    }

    [Theory]
    [InlineData(ImageProcessingErrorCategory.InvalidPath, ErrorPresentationContext.ImageRendering)]
    [InlineData(ImageProcessingErrorCategory.FileNotFound, ErrorPresentationContext.ImageRendering)]
    [InlineData(ImageProcessingErrorCategory.AccessDenied, ErrorPresentationContext.ImageExport)]
    [InlineData(ImageProcessingErrorCategory.InvalidImage, ErrorPresentationContext.ImageRendering)]
    [InlineData(ImageProcessingErrorCategory.UnsupportedDimensions, ErrorPresentationContext.ImageRendering)]
    [InlineData(ImageProcessingErrorCategory.InvalidSettings, ErrorPresentationContext.ImageRendering)]
    [InlineData(ImageProcessingErrorCategory.FontUnavailable, ErrorPresentationContext.ImageRendering)]
    [InlineData(ImageProcessingErrorCategory.RenderingFailed, ErrorPresentationContext.ImageRendering)]
    [InlineData(ImageProcessingErrorCategory.RenderingFailed, ErrorPresentationContext.FrameSymbolization)]
    [InlineData(ImageProcessingErrorCategory.OutputUnavailable, ErrorPresentationContext.ImageExport)]
    [InlineData(ImageProcessingErrorCategory.EncodingFailed, ErrorPresentationContext.ImageExport)]
    [InlineData(ImageProcessingErrorCategory.UnexpectedFailure, ErrorPresentationContext.ImageRendering)]
    public void ImageCategories_MapToSafeFriendlyPresentation(
        ImageProcessingErrorCategory category,
        ErrorPresentationContext context)
    {
        const string raw = "Skia internal failure at C:\\private\\image.png";
        ImageProcessingException exception = new(category, raw, raw);

        ErrorPresentation? presentation = _service.From(exception, context);

        AssertPresentation(presentation, context, raw);
    }

    [Theory]
    [InlineData(VideoExportErrorCategory.InvalidInput)]
    [InlineData(VideoExportErrorCategory.InvalidOutput)]
    [InlineData(VideoExportErrorCategory.DestinationAccessDenied)]
    [InlineData(VideoExportErrorCategory.InsufficientDiskSpace)]
    [InlineData(VideoExportErrorCategory.DestinationConflict)]
    [InlineData(VideoExportErrorCategory.DecoderUnavailable)]
    [InlineData(VideoExportErrorCategory.MetadataUnavailable)]
    [InlineData(VideoExportErrorCategory.InvalidDimensions)]
    [InlineData(VideoExportErrorCategory.DecodeFailed)]
    [InlineData(VideoExportErrorCategory.TruncatedFrame)]
    [InlineData(VideoExportErrorCategory.RenderingFailed)]
    [InlineData(VideoExportErrorCategory.EncoderStartFailed)]
    [InlineData(VideoExportErrorCategory.EncoderWriteFailed)]
    [InlineData(VideoExportErrorCategory.EncoderFailed)]
    [InlineData(VideoExportErrorCategory.AudioMetadataUnavailable)]
    [InlineData(VideoExportErrorCategory.AudioProbeFailed)]
    [InlineData(VideoExportErrorCategory.AudioMuxStartFailed)]
    [InlineData(VideoExportErrorCategory.AudioStreamCopyFailed)]
    [InlineData(VideoExportErrorCategory.AudioTranscodeFailed)]
    [InlineData(VideoExportErrorCategory.AudioMuxFailed)]
    [InlineData(VideoExportErrorCategory.OutputFinalizationFailed)]
    [InlineData(VideoExportErrorCategory.UnexpectedFailure)]
    public void VideoFailureCategories_MapToSafeFriendlyPresentation(VideoExportErrorCategory category)
    {
        const string raw = "ffmpeg exited -22\nC:\\private\\partial.mp4";
        VideoExportResult result = VideoExportResult.Failure(category, raw, raw);

        ErrorPresentation? presentation = _service.From(result, ErrorPresentationContext.VideoPublication);

        AssertPresentation(presentation, ErrorPresentationContext.VideoPublication, raw);
    }

    [Theory]
    [InlineData(VideoExportErrorCategory.DecodeFailed, ApplicationErrorCode.VideoDecodeFailed)]
    [InlineData(VideoExportErrorCategory.RenderingFailed, ApplicationErrorCode.VideoRenderFailed)]
    [InlineData(VideoExportErrorCategory.EncoderFailed, ApplicationErrorCode.VideoEncodeFailed)]
    [InlineData(VideoExportErrorCategory.AudioProbeFailed, ApplicationErrorCode.AudioProbeFailed)]
    [InlineData(VideoExportErrorCategory.AudioMuxFailed, ApplicationErrorCode.AudioMuxFailed)]
    [InlineData(VideoExportErrorCategory.OutputFinalizationFailed, ApplicationErrorCode.VideoFinalizationFailed)]
    public void VideoPipelineFailures_KeepSpecificRenderingFamily(
        VideoExportErrorCategory category,
        ApplicationErrorCode expectedCode)
    {
        VideoExportResult result = VideoExportResult.Failure(category, "raw failure", "technical detail");

        ErrorPresentation presentation = Assert.IsType<ErrorPresentation>(
            _service.From(result, ErrorPresentationContext.VideoRendering));

        Assert.Equal(expectedCode, presentation.Code);
        Assert.Equal(ErrorPresentationContext.VideoRendering, presentation.Context);
    }

    [Theory]
    [InlineData(ManagedVideoArtifactErrorCategory.InvalidJob)]
    [InlineData(ManagedVideoArtifactErrorCategory.RenderFailed)]
    [InlineData(ManagedVideoArtifactErrorCategory.CacheUnavailable)]
    [InlineData(ManagedVideoArtifactErrorCategory.ValidationFailed)]
    [InlineData(ManagedVideoArtifactErrorCategory.FinalizationFailed)]
    [InlineData(ManagedVideoArtifactErrorCategory.UnexpectedFailure)]
    public void ManagedArtifactCategories_MapToSafeFriendlyPresentation(ManagedVideoArtifactErrorCategory category)
    {
        const string raw = @"C:\cache\artifact.pending.mp4 failed validation";
        ManagedVideoRenderResult result = ManagedVideoRenderResult.Failure(category, raw, raw);

        ErrorPresentation? presentation = _service.From(result);

        AssertPresentation(presentation, ErrorPresentationContext.VideoRendering, raw);
    }

    [Theory]
    [InlineData(VideoExportErrorCategory.DecodeFailed, ApplicationErrorCode.VideoDecodeFailed)]
    [InlineData(VideoExportErrorCategory.RenderingFailed, ApplicationErrorCode.VideoRenderFailed)]
    [InlineData(VideoExportErrorCategory.EncoderFailed, ApplicationErrorCode.VideoEncodeFailed)]
    [InlineData(VideoExportErrorCategory.AudioProbeFailed, ApplicationErrorCode.AudioProbeFailed)]
    [InlineData(VideoExportErrorCategory.AudioMuxFailed, ApplicationErrorCode.AudioMuxFailed)]
    [InlineData(VideoExportErrorCategory.OutputFinalizationFailed, ApplicationErrorCode.VideoFinalizationFailed)]
    public void ManagedRender_PrefersPreservedPipelineError(
        VideoExportErrorCategory pipelineCategory,
        ApplicationErrorCode expectedCode)
    {
        ManagedVideoRenderResult result = ManagedVideoRenderResult.Failure(
            ManagedVideoArtifactErrorCategory.RenderFailed,
            "raw pipeline message",
            "raw pipeline diagnostics",
            pipelineCategory);

        ErrorPresentation presentation = Assert.IsType<ErrorPresentation>(_service.From(result));

        Assert.Equal(expectedCode, presentation.Code);
        Assert.Equal(ErrorPresentationContext.VideoRendering, presentation.Context);
        Assert.Contains("raw pipeline message", presentation.TechnicalDetail);
        Assert.Contains("raw pipeline diagnostics", presentation.TechnicalDetail);
        Assert.DoesNotContain("raw pipeline", presentation.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(FfmpegToolValidationStatus.Missing)]
    [InlineData(FfmpegToolValidationStatus.InvalidPath)]
    [InlineData(FfmpegToolValidationStatus.AccessDenied)]
    [InlineData(FfmpegToolValidationStatus.StartFailed)]
    [InlineData(FfmpegToolValidationStatus.NonZeroExitCode)]
    [InlineData(FfmpegToolValidationStatus.TimedOut)]
    [InlineData(FfmpegToolValidationStatus.InvalidVersionOutput)]
    public void ToolValidationCategories_MapToSafeFriendlyPresentation(FfmpegToolValidationStatus status)
    {
        const string raw = @"C:\GlyphFrame\Tools\ffmpeg.exe exited with diagnostics";
        FfmpegToolValidationResult result = new(
            FfmpegToolKind.Ffmpeg,
            status,
            @"C:\GlyphFrame\Tools\ffmpeg.exe",
            null,
            raw,
            raw);

        ErrorPresentation? presentation = _service.From(result);

        AssertPresentation(presentation, ErrorPresentationContext.ToolValidation, raw);
    }

    [Fact]
    public void CancellationResults_AreNotErrors()
    {
        Assert.Null(_service.From(MediaInputResult.Failure(
            MediaInputErrorCategory.Cancelled,
            "Media loading was cancelled.")));
        Assert.Null(_service.From(VideoFrameReadResult.Cancelled(0.5)));
        Assert.Null(_service.From(new ImageProcessingException(
            ImageProcessingErrorCategory.Cancelled,
            "Cancelled."),
            ErrorPresentationContext.ImageRendering));
        Assert.Null(_service.From(VideoExportResult.Cancelled(), ErrorPresentationContext.VideoPublication));
        Assert.Null(_service.From(ManagedVideoRenderResult.Cancelled()));
        Assert.Null(_service.FromException(
            ErrorPresentationContext.VideoRendering,
            new OperationCanceledException()));
    }

    [Fact]
    public void UnexpectedRecoverableException_PreservesCompleteTechnicalDetailOnly()
    {
        Exception exception = CaptureNestedException();

        ErrorPresentation presentation = Assert.IsType<ErrorPresentation>(
            _service.FromException(ErrorPresentationContext.ImageRendering, exception));

        Assert.Equal(ApplicationErrorCode.ImageRenderFailed, presentation.Code);
        Assert.DoesNotContain("controlled inner", presentation.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(typeof(InvalidOperationException).FullName!, presentation.TechnicalDetail);
        Assert.Contains("controlled outer", presentation.TechnicalDetail);
        Assert.Contains("controlled inner", presentation.TechnicalDetail);
        Assert.Contains("at ", presentation.TechnicalDetail);
    }

    [Fact]
    public void FatalExceptions_AreClassifiedAndNeverNormalized()
    {
        OutOfMemoryException fatal = new("controlled fatal");

        Assert.True(_service.IsFatal(fatal));
        Assert.True(_service.IsFatal(new StackOverflowException("controlled fatal")));
        Assert.True(_service.IsFatal(new AccessViolationException("controlled fatal")));
        Assert.Throws<OutOfMemoryException>(() =>
            _service.FromException(ErrorPresentationContext.VideoRendering, fatal));
        Assert.True(_service.IsFatal(new InvalidOperationException("wrapper", fatal)));
        Assert.True(_service.IsFatal(new AggregateException(new IOException("recoverable"), fatal)));
    }

    [Fact]
    public void RecoverabilityPolicy_AllowsExpectedBoundaryFailuresOnly()
    {
        Assert.True(_service.IsRecoverable(
            ErrorPresentationContext.VideoPublication,
            new IOException("filesystem")));
        Assert.True(_service.IsRecoverable(
            ErrorPresentationContext.ToolValidation,
            new Win32Exception(5, "process")));
        Assert.True(_service.IsRecoverable(
            ErrorPresentationContext.VideoFramePreview,
            new TimeoutException("timeout")));
        Assert.True(_service.IsRecoverable(
            ErrorPresentationContext.ImageRendering,
            new InvalidOperationException("known operation failure")));
        Assert.True(_service.IsRecoverable(
            ErrorPresentationContext.ImageRendering,
            new InvalidOperationException("wrapper", new IOException("filesystem"))));
        Assert.True(_service.IsRecoverable(
            ErrorPresentationContext.VideoPublication,
            new AggregateException(new IOException("one"), new UnauthorizedAccessException("two"))));

        Assert.False(_service.IsRecoverable(
            ErrorPresentationContext.VideoRendering,
            new OperationCanceledException()));
        Assert.False(_service.IsRecoverable(
            ErrorPresentationContext.VideoRendering,
            new NullReferenceException("programmer defect")));
        Assert.False(_service.IsRecoverable(
            ErrorPresentationContext.VideoRendering,
            new IndexOutOfRangeException("programmer defect")));
        Assert.False(_service.IsRecoverable(
            ErrorPresentationContext.VideoRendering,
            new ArgumentNullException("programmer defect")));
        Assert.False(_service.IsRecoverable(
            ErrorPresentationContext.VideoRendering,
            new InvalidOperationException("wrapper", new NullReferenceException("programmer defect"))));
        Assert.False(_service.IsRecoverable(
            ErrorPresentationContext.VideoPublication,
            new AggregateException(new IOException("recoverable"), new IndexOutOfRangeException("defect"))));

        Assert.Throws<NullReferenceException>(() => _service.FromException(
            ErrorPresentationContext.VideoRendering,
            new NullReferenceException("programmer defect")));
        Assert.Throws<IndexOutOfRangeException>(() => _service.FromException(
            ErrorPresentationContext.VideoRendering,
            new IndexOutOfRangeException("programmer defect")));
    }

    [Fact]
    public void DiskFullHResult_MapsOnlyWhenReliablyIdentified()
    {
        ErrorPresentation presentation = Assert.IsType<ErrorPresentation>(
            _service.FromException(
                ErrorPresentationContext.VideoPublication,
                new ControlledIOException(unchecked((int)0x80070070))));

        Assert.Equal(ApplicationErrorCode.InsufficientDiskSpace, presentation.Code);
        Assert.Contains("disk space", presentation.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecoverableSystemFailures_MapToOperationSpecificFamilies()
    {
        Assert.Equal(ApplicationErrorCode.DestinationAccessDenied,
            Present(ErrorPresentationContext.ImageExport, new UnauthorizedAccessException()).Code);
        Assert.Equal(ApplicationErrorCode.DestinationAccessDenied,
            Present(ErrorPresentationContext.VideoPublication, new Win32Exception(5)).Code);
        Assert.Equal(ApplicationErrorCode.DestinationUnavailable,
            Present(ErrorPresentationContext.VideoPublication, new DirectoryNotFoundException()).Code);
        Assert.Equal(ApplicationErrorCode.InputNotFound,
            Present(ErrorPresentationContext.MediaInput, new FileNotFoundException()).Code);
        Assert.Equal(ApplicationErrorCode.ArtifactValidationFailed,
            Present(ErrorPresentationContext.ManagedArtifact, new FileNotFoundException()).Code);
        Assert.Equal(ApplicationErrorCode.FrameTimedOut,
            Present(ErrorPresentationContext.VideoFramePreview, new TimeoutException()).Code);
        Assert.Equal(ApplicationErrorCode.OriginalPlaybackUnavailable,
            Present(ErrorPresentationContext.OriginalPlayback, new TimeoutException()).Code);

        ErrorPresentation Present(ErrorPresentationContext context, Exception exception) =>
            Assert.IsType<ErrorPresentation>(_service.FromException(context, exception));
    }

    [Fact]
    public void PlaybackContextsRemainIndependent()
    {
        ErrorPresentation original = _service.PlaybackFailure(
            ErrorPresentationContext.OriginalPlayback,
            "raw original codec failure",
            "original technical");
        ErrorPresentation symbolized = _service.PlaybackFailure(
            ErrorPresentationContext.SymbolizedPlayback,
            "raw rendered codec failure",
            "rendered technical");

        Assert.Equal(ApplicationErrorCode.OriginalPlaybackUnavailable, original.Code);
        Assert.Equal(ApplicationErrorCode.SymbolizedPlaybackUnavailable, symbolized.Code);
        Assert.NotEqual(original.Context, symbolized.Context);
        Assert.Contains("original technical", original.TechnicalDetail);
        Assert.Contains("rendered technical", symbolized.TechnicalDetail);
    }

    private static void AssertPresentation(
        ErrorPresentation? presentation,
        ErrorPresentationContext context,
        string raw)
    {
        ErrorPresentation actual = Assert.IsType<ErrorPresentation>(presentation);
        Assert.Equal(context, actual.Context);
        Assert.False(string.IsNullOrWhiteSpace(actual.UserMessage));
        Assert.DoesNotContain(raw, actual.UserMessage, StringComparison.Ordinal);
        Assert.Contains(raw, actual.TechnicalDetail, StringComparison.Ordinal);
    }

    private static Exception CaptureNestedException()
    {
        try
        {
            try
            {
                throw new IOException("controlled inner");
            }
            catch (Exception inner)
            {
                throw new InvalidOperationException("controlled outer", inner);
            }
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed class ControlledIOException : IOException
    {
        public ControlledIOException(int hResult)
            : base("controlled disk failure")
        {
            HResult = hResult;
        }
    }
}
