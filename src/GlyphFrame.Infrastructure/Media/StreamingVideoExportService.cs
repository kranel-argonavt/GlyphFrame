using System.Runtime.CompilerServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

public sealed class StreamingVideoExportService(
    ISequentialVideoFrameReader frameReader,
    IVideoEncoder encoder,
    IImagePipelineService imagePipeline,
    IOutputDimensionResolver dimensionResolver,
    IAudioStreamProbe audioProbe,
    IAudioCompatibilityPolicy audioCompatibilityPolicy,
    IAudioMuxer audioMuxer) : IVideoExportService
{
    private readonly ISequentialVideoFrameReader _frameReader = frameReader
        ?? throw new ArgumentNullException(nameof(frameReader));
    private readonly IVideoEncoder _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
    private readonly IImagePipelineService _imagePipeline = imagePipeline
        ?? throw new ArgumentNullException(nameof(imagePipeline));
    private readonly IOutputDimensionResolver _dimensionResolver = dimensionResolver
        ?? throw new ArgumentNullException(nameof(dimensionResolver));
    private readonly IAudioCompatibilityPolicy _audioCompatibilityPolicy = audioCompatibilityPolicy
        ?? throw new ArgumentNullException(nameof(audioCompatibilityPolicy));
    private readonly IAudioMuxer _audioMuxer = audioMuxer ?? throw new ArgumentNullException(nameof(audioMuxer));
    private readonly IAudioStreamProbe _audioProbe = audioProbe ?? throw new ArgumentNullException(nameof(audioProbe));

    public async Task<VideoExportResult> ExportAsync(
        VideoExportJob job,
        CancellationToken cancellationToken = default,
        IProgress<VideoExportProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        VideoExportProgressTracker progressTracker = new(
            progress,
            job.Metadata.FrameCount);
        progressTracker.ReportStage(VideoExportStage.Preparing);
        string? videoPartialPath = null;
        string? muxPartialPath = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(job.AudioMode))
            {
                throw new VideoExportException(
                    VideoExportErrorCategory.InvalidInput,
                    "The selected audio export mode is not supported.");
            }

            (string inputPath, string outputPath) = ValidatePaths(job);
            PixelDimensions sourceDimensions = ResolveSourceDimensions(job.Metadata);
            PixelDimensions requestedDimensions = _dimensionResolver.Resolve(
                sourceDimensions,
                job.RenderSettings.OutputResolution);
            PixelDimensions outputDimensions = VideoExportPolicy.NormalizeForYuv420(requestedDimensions);
            double framesPerSecond = VideoExportPolicy.NormalizeFramesPerSecond(job.Metadata.Fps);
            videoPartialPath = CreatePartialPath(outputPath, "video");

            SequentialVideoDecodeJob decodeJob = new(inputPath, sourceDimensions);
            VideoEncodingJob encodingJob = new(videoPartialPath, outputDimensions, framesPerSecond);
            IAsyncEnumerable<RenderedImage> frames = RenderFramesAsync(
                decodeJob,
                outputDimensions,
                job.RenderSettings,
                job.CharacterPreset,
                cancellationToken);
            progressTracker.ReportStage(VideoExportStage.ProcessingFrames);
            CallbackProgress<VideoEncodingProgress> encodingProgress = new(update =>
            {
                if (update.Stage == VideoEncodingProgressStage.FrameAccepted)
                {
                    progressTracker.ReportFramesAccepted(update.FramesAccepted);
                }
                else if (update.Stage == VideoEncodingProgressStage.Finalizing)
                {
                    progressTracker.ReportStage(VideoExportStage.FinishingEncoding);
                }
            });
            VideoEncodingResult encoding = await _encoder.EncodeAsync(
                encodingJob,
                frames,
                cancellationToken,
                encodingProgress).ConfigureAwait(false);
            if (!encoding.Success)
            {
                VideoExportErrorCategory category = encoding.ErrorCategory == VideoExportErrorCategory.None
                    ? VideoExportErrorCategory.EncoderFailed
                    : encoding.ErrorCategory;
                return VideoExportResult.Failure(
                    category,
                    encoding.UserMessage ?? "GlyphFrame could not encode the symbolized MP4 video.",
                    encoding.TechnicalDetail);
            }

            cancellationToken.ThrowIfCancellationRequested();
            AudioMuxOutcome audioOutcome = AudioMuxOutcome.Removed;
            string publicationPath = videoPartialPath;
            if (job.AudioMode == AudioExportMode.KeepAudio)
            {
                progressTracker.ReportStage(VideoExportStage.InspectingAudio);
                AudioProbeResult probe = await _audioProbe.ProbeAsync(inputPath, cancellationToken)
                    .ConfigureAwait(false);
                if (!probe.Success)
                {
                    return VideoExportResult.Failure(
                        probe.ErrorCategory == VideoExportErrorCategory.None
                            ? VideoExportErrorCategory.AudioProbeFailed
                            : probe.ErrorCategory,
                        probe.UserMessage,
                        probe.TechnicalDetail);
                }

                if (!probe.HasAudio)
                {
                    audioOutcome = AudioMuxOutcome.NoSourceAudio;
                }
                else
                {
                    AudioMuxStrategy strategy = _audioCompatibilityPolicy.ResolveStrategy(probe.Stream?.CodecName);
                    TimeSpan videoDuration = ResolveEncodedDuration(encoding.FramesWritten, framesPerSecond);
                    muxPartialPath = CreatePartialPath(outputPath, "audio");
                    progressTracker.ReportStage(VideoExportStage.MergingAudio);
                    AudioMuxResult mux = await _audioMuxer.MuxAsync(
                        new AudioMuxJob(
                            videoPartialPath,
                            inputPath,
                            muxPartialPath,
                            strategy,
                            videoDuration),
                        cancellationToken).ConfigureAwait(false);
                    if (!mux.Success)
                    {
                        return VideoExportResult.Failure(
                            mux.ErrorCategory,
                            mux.UserMessage,
                            mux.TechnicalDetail);
                    }

                    audioOutcome = mux.Outcome ?? throw new VideoExportException(
                        VideoExportErrorCategory.AudioMuxFailed,
                        "GlyphFrame could not determine how source audio was preserved.");
                    publicationPath = muxPartialPath;
                    DeleteIntermediateOrThrow(videoPartialPath);
                    videoPartialPath = null;
                }
            }

            progressTracker.ReportStage(VideoExportStage.Finalizing);
            cancellationToken.ThrowIfCancellationRequested();
            FinalizeOutput(publicationPath, outputPath, job.Overwrite);
            if (string.Equals(publicationPath, videoPartialPath, StringComparison.OrdinalIgnoreCase))
            {
                videoPartialPath = null;
            }
            else
            {
                muxPartialPath = null;
            }

            progressTracker.Complete();
            return VideoExportResult.Completed(
                outputPath,
                encoding.FramesWritten,
                outputDimensions,
                framesPerSecond,
                audioOutcome);
        }
        catch (OperationCanceledException)
        {
            progressTracker.ReportStage(VideoExportStage.Cancelling);
            return VideoExportResult.Cancelled();
        }
        catch (VideoExportException exception)
        {
            return VideoExportResult.Failure(
                exception.Category,
                exception.UserMessage,
                exception.TechnicalDetail);
        }
        catch (ImageProcessingException exception)
        {
            return VideoExportResult.Failure(
                VideoExportErrorCategory.RenderingFailed,
                exception.UserMessage,
                exception.TechnicalDetail);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or OverflowException
            or TimeoutException)
        {
            return VideoExportResult.Failure(
                VideoExportErrorCategory.UnexpectedFailure,
                "GlyphFrame could not export the symbolized video.",
                exception.Message);
        }
        finally
        {
            DeletePartialFile(videoPartialPath);
            DeletePartialFile(muxPartialPath);
        }
    }

    private async IAsyncEnumerable<RenderedImage> RenderFramesAsync(
        SequentialVideoDecodeJob decodeJob,
        PixelDimensions outputDimensions,
        ImageRenderSettings renderSettings,
        CharacterPresetOption preset,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long expectedSequence = 0;
        await foreach (DecodedVideoFrame decoded in _frameReader
            .ReadFramesAsync(decodeJob, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (decoded.SequenceNumber != expectedSequence)
            {
                throw new VideoExportException(
                    VideoExportErrorCategory.DecodeFailed,
                    "Decoded video frames were not received in sequential order.",
                    $"Expected frame {expectedSequence}, received {decoded.SequenceNumber}.");
            }

            ImagePipelineResult rendered = await _imagePipeline.RenderAsync(
                decoded.Frame,
                renderSettings,
                preset,
                cancellationToken).ConfigureAwait(false);
            yield return CropToDimensions(rendered.RenderedImage, outputDimensions);
            expectedSequence = checked(expectedSequence + 1);
        }
    }

    internal static RenderedImage CropToDimensions(RenderedImage source, PixelDimensions dimensions)
    {
        if (source.Dimensions == dimensions)
        {
            return source;
        }

        if (source.Dimensions.Width < dimensions.Width || source.Dimensions.Height < dimensions.Height)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidDimensions,
                "The rendered video frame was smaller than the normalized MP4 dimensions.");
        }

        int destinationStride = checked(dimensions.Width * 4);
        int sourceStride = checked(source.Dimensions.Width * 4);
        byte[] cropped = new byte[checked(destinationStride * dimensions.Height)];
        ReadOnlySpan<byte> sourcePixels = source.RgbaPixels.Span;
        for (int row = 0; row < dimensions.Height; row++)
        {
            sourcePixels.Slice(row * sourceStride, destinationStride)
                .CopyTo(cropped.AsSpan(row * destinationStride, destinationStride));
        }

        return new RenderedImage(dimensions.Width, dimensions.Height, cropped);
    }

    private static (string InputPath, string OutputPath) ValidatePaths(VideoExportJob job)
    {
        if (string.IsNullOrWhiteSpace(job.InputPath) || !File.Exists(job.InputPath))
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidInput,
                "The selected video no longer exists.");
        }

        if (string.IsNullOrWhiteSpace(job.OutputPath))
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidOutput,
                "Choose an MP4 output destination before exporting.");
        }

        string inputPath = Path.GetFullPath(job.InputPath);
        string outputPath = Path.GetFullPath(job.OutputPath);
        if (!Path.GetExtension(outputPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidOutput,
                "The video output filename must use the MP4 extension.");
        }

        if (inputPath.Equals(outputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidOutput,
                "Choose a different output file so the source video is not overwritten.");
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidOutput,
                "The selected output folder does not exist.");
        }

        if (File.Exists(outputPath) && !job.Overwrite)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidOutput,
                "The output file already exists and was not replaced.");
        }

        return (inputPath, outputPath);
    }

    private static PixelDimensions ResolveSourceDimensions(VideoMetadata metadata)
    {
        if (metadata.DisplayWidth is not int width || metadata.DisplayHeight is not int height)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.MetadataUnavailable,
                "GlyphFrame could not determine the video's display dimensions.");
        }

        try
        {
            return new PixelDimensions(width, height);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidDimensions,
                "The video's display dimensions are not valid for export.",
                exception.Message,
                exception);
        }
    }

    private static string CreatePartialPath(string outputPath, string purpose)
    {
        string directory = Path.GetDirectoryName(outputPath)!;
        string name = Path.GetFileName(outputPath);
        return Path.Combine(directory, $".{name}.{purpose}.{Guid.NewGuid():N}.partial");
    }

    private static TimeSpan ResolveEncodedDuration(long framesWritten, double framesPerSecond)
    {
        if (framesWritten <= 0 || !double.IsFinite(framesPerSecond) || framesPerSecond <= 0d)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.AudioMuxFailed,
                "GlyphFrame could not determine the completed symbolized video's duration.");
        }

        return TimeSpan.FromSeconds(framesWritten / framesPerSecond);
    }

    private static void DeleteIntermediateOrThrow(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.OutputFinalizationFailed,
                "GlyphFrame could not clean the intermediate video before publishing the final output.",
                exception.Message,
                exception);
        }
    }

    private static void FinalizeOutput(string partialPath, string outputPath, bool overwrite)
    {
        if (!File.Exists(partialPath))
        {
            throw new VideoExportException(
                VideoExportErrorCategory.OutputFinalizationFailed,
                "The MP4 encoder did not create a completed output file.");
        }

        try
        {
            if (File.Exists(outputPath))
            {
                if (!overwrite)
                {
                    throw new VideoExportException(
                        VideoExportErrorCategory.InvalidOutput,
                        "The output file already exists and was not replaced.");
                }

                File.Replace(partialPath, outputPath, null);
            }
            else
            {
                File.Move(partialPath, outputPath);
            }
        }
        catch (VideoExportException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.OutputFinalizationFailed,
                "GlyphFrame could not finalize the MP4 output file.",
                exception.Message,
                exception);
        }
    }

    private static void DeletePartialFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not remove partial video export '{path}': {exception.Message}");
        }
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        private readonly Action<T> _callback = callback ?? throw new ArgumentNullException(nameof(callback));

        public void Report(T value) => _callback(value);
    }
}
