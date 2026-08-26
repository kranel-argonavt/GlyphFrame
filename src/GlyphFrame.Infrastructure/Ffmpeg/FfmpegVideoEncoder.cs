using System.ComponentModel;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Ffmpeg;

public sealed class FfmpegVideoEncoder : IVideoEncoder
{
    private static readonly TimeSpan DefaultFinalizationTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _finalizationTimeout;
    private readonly IStreamingProcessFactory _processFactory;
    private readonly IBundledFfmpegLocator _toolLocator;

    public FfmpegVideoEncoder(IBundledFfmpegLocator toolLocator)
        : this(toolLocator, new SystemStreamingProcessFactory(), DefaultFinalizationTimeout)
    {
    }

    internal FfmpegVideoEncoder(
        IBundledFfmpegLocator toolLocator,
        IStreamingProcessFactory processFactory,
        TimeSpan finalizationTimeout)
    {
        _toolLocator = toolLocator ?? throw new ArgumentNullException(nameof(toolLocator));
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        _finalizationTimeout = finalizationTimeout > TimeSpan.Zero
            ? finalizationTimeout
            : throw new ArgumentOutOfRangeException(nameof(finalizationTimeout));
    }

    public async Task<VideoEncodingResult> EncodeAsync(
        VideoEncodingJob job,
        IAsyncEnumerable<RenderedImage> frames,
        CancellationToken cancellationToken = default,
        IProgress<VideoEncodingProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(frames);
        cancellationToken.ThrowIfCancellationRequested();
        BundledToolPath ffmpeg = _toolLocator.Locate().Ffmpeg;
        if (!ffmpeg.Exists || ffmpeg.IsDirectory)
        {
            return Failure(
                VideoExportErrorCategory.EncoderStartFailed,
                0,
                "Bundled FFmpeg is unavailable. Check its status in Settings.",
                $"FFmpeg path: {ffmpeg.ResolvedPath}");
        }

        IStreamingProcessSession process;
        try
        {
            process = _processFactory.Start(
                ffmpeg.ResolvedPath,
                CreateArguments(job),
                redirectStandardInput: true,
                redirectStandardOutput: false);
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException
            or UnauthorizedAccessException
            or IOException)
        {
            return Failure(
                VideoExportErrorCategory.EncoderStartFailed,
                0,
                "GlyphFrame could not start the bundled MP4 encoder.",
                exception.Message);
        }

        await using (process.ConfigureAwait(false))
        {
            Task<string> errorTask = StreamingProcessSupport.ReadStandardErrorAsync(process.StandardError);
            long framesWritten = 0;
            try
            {
                await foreach (RenderedImage frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    ValidateFrame(frame, job.FrameDimensions, framesWritten);
                    await process.StandardInput.WriteAsync(frame.RgbaPixels, cancellationToken)
                        .ConfigureAwait(false);
                    framesWritten = checked(framesWritten + 1);
                    progress?.Report(new VideoEncodingProgress(
                        VideoEncodingProgressStage.FrameAccepted,
                        framesWritten));
                }

                await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                process.CompleteStandardInput();
                progress?.Report(new VideoEncodingProgress(
                    VideoEncodingProgressStage.Finalizing,
                    framesWritten));
                using CancellationTokenSource finalizationTimeout = new(_finalizationTimeout);
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    finalizationTimeout.Token);
                try
                {
                    await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    finalizationTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                    return Failure(
                        VideoExportErrorCategory.EncoderFailed,
                        framesWritten,
                        "The MP4 encoder did not finish in time.",
                        $"Encoder finalization exceeded {_finalizationTimeout}.");
                }

                string standardError = await ReadErrorWithBoundAsync(errorTask).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    return Failure(
                        VideoExportErrorCategory.EncoderFailed,
                        framesWritten,
                        "GlyphFrame could not finish encoding the MP4 video.",
                        $"FFmpeg exit code: {process.ExitCode}. {standardError}".Trim());
                }

                if (framesWritten == 0)
                {
                    return Failure(
                        VideoExportErrorCategory.EncoderFailed,
                        0,
                        "The selected video did not produce any frames to export.",
                        standardError);
                }

                return new VideoEncodingResult(true, framesWritten);
            }
            catch (OperationCanceledException)
            {
                await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                throw;
            }
            catch (VideoExportException)
            {
                await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                throw;
            }
            catch (IOException exception)
            {
                await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                return Failure(
                    VideoExportErrorCategory.EncoderWriteFailed,
                    framesWritten,
                    "GlyphFrame could not stream a rendered frame to the MP4 encoder.",
                    exception.Message);
            }
            finally
            {
                await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                Observe(errorTask);
            }
        }
    }

    internal static IReadOnlyList<string> CreateArguments(VideoEncodingJob job)
    {
        string size = $"{job.FrameDimensions.Width}x{job.FrameDimensions.Height}";
        string fps = VideoExportPolicy.FormatFramesPerSecond(job.FramesPerSecond);
        return
        [
            "-hide_banner",
            "-nostdin",
            "-loglevel", "error",
            "-f", "rawvideo",
            "-pix_fmt", "rgba",
            "-video_size", size,
            "-framerate", fps,
            "-i", "pipe:0",
            "-map", "0:v:0",
            "-an",
            "-c:v", "libx264",
            "-crf", "20",
            "-preset", "medium",
            "-pix_fmt", "yuv420p",
            "-movflags", "+faststart",
            "-f", "mp4",
            "-y",
            job.OutputPath
        ];
    }

    internal static void ValidateFramePayload(
        PixelDimensions actualDimensions,
        ReadOnlyMemory<byte> pixels,
        PixelDimensions expectedDimensions,
        long sequenceNumber)
    {
        if (actualDimensions != expectedDimensions)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.EncoderWriteFailed,
                "A rendered frame did not match the MP4 output dimensions.",
                $"Frame {sequenceNumber}: expected {expectedDimensions.Width}x{expectedDimensions.Height}, received {actualDimensions.Width}x{actualDimensions.Height}.");
        }

        int expectedBytes = checked(expectedDimensions.Width * expectedDimensions.Height * 4);
        if (pixels.Length != expectedBytes)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.EncoderWriteFailed,
                "A rendered frame did not contain the expected RGBA data.",
                $"Frame {sequenceNumber}: expected {expectedBytes} bytes, received {pixels.Length}.");
        }
    }

    private static void ValidateFrame(RenderedImage frame, PixelDimensions expected, long sequenceNumber) =>
        ValidateFramePayload(frame.Dimensions, frame.RgbaPixels, expected, sequenceNumber);

    private static VideoEncodingResult Failure(
        VideoExportErrorCategory category,
        long framesWritten,
        string userMessage,
        string? technicalDetail) => new(
            false,
            framesWritten,
            userMessage,
            technicalDetail,
            category);

    private static async Task<string> ReadErrorWithBoundAsync(Task<string> errorTask)
    {
        try
        {
            return await errorTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return "FFmpeg diagnostic output did not close within the drain timeout.";
        }
    }

    private static void Observe(Task task)
    {
        if (task.IsFaulted)
        {
            _ = task.Exception;
        }
        else if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
