using System.Runtime.CompilerServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

public sealed class StreamingVideoExportService(
    ISequentialVideoFrameReader frameReader,
    IVideoEncoder encoder,
    IImagePipelineService imagePipeline,
    IOutputDimensionResolver dimensionResolver) : IVideoExportService
{
    private readonly ISequentialVideoFrameReader _frameReader = frameReader
        ?? throw new ArgumentNullException(nameof(frameReader));
    private readonly IVideoEncoder _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
    private readonly IImagePipelineService _imagePipeline = imagePipeline
        ?? throw new ArgumentNullException(nameof(imagePipeline));
    private readonly IOutputDimensionResolver _dimensionResolver = dimensionResolver
        ?? throw new ArgumentNullException(nameof(dimensionResolver));

    public async Task<VideoExportResult> ExportAsync(
        VideoExportJob job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        string? partialPath = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            (string inputPath, string outputPath) = ValidatePaths(job);
            PixelDimensions sourceDimensions = ResolveSourceDimensions(job.Metadata);
            PixelDimensions requestedDimensions = _dimensionResolver.Resolve(
                sourceDimensions,
                job.RenderSettings.OutputResolution);
            PixelDimensions outputDimensions = VideoExportPolicy.NormalizeForYuv420(requestedDimensions);
            double framesPerSecond = VideoExportPolicy.NormalizeFramesPerSecond(job.Metadata.Fps);
            partialPath = CreatePartialPath(outputPath);

            SequentialVideoDecodeJob decodeJob = new(inputPath, sourceDimensions);
            VideoEncodingJob encodingJob = new(partialPath, outputDimensions, framesPerSecond);
            IAsyncEnumerable<RenderedImage> frames = RenderFramesAsync(
                decodeJob,
                outputDimensions,
                job.RenderSettings,
                job.CharacterPreset,
                cancellationToken);
            VideoEncodingResult encoding = await _encoder.EncodeAsync(
                encodingJob,
                frames,
                cancellationToken).ConfigureAwait(false);
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
            FinalizeOutput(partialPath, outputPath, job.Overwrite);
            partialPath = null;
            return VideoExportResult.Completed(
                outputPath,
                encoding.FramesWritten,
                outputDimensions,
                framesPerSecond);
        }
        catch (OperationCanceledException)
        {
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
            DeletePartialFile(partialPath);
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

    private static string CreatePartialPath(string outputPath)
    {
        string directory = Path.GetDirectoryName(outputPath)!;
        string name = Path.GetFileName(outputPath);
        return Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.partial");
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
}
