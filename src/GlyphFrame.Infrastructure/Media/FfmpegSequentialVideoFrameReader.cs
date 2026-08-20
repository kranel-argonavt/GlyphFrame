using System.ComponentModel;
using System.Runtime.CompilerServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;

namespace GlyphFrame.Infrastructure.Media;

public sealed class FfmpegSequentialVideoFrameReader : ISequentialVideoFrameReader
{
    private readonly IStreamingProcessFactory _processFactory;
    private readonly IBundledFfmpegLocator _toolLocator;

    public FfmpegSequentialVideoFrameReader(IBundledFfmpegLocator toolLocator)
        : this(toolLocator, new SystemStreamingProcessFactory())
    {
    }

    internal FfmpegSequentialVideoFrameReader(
        IBundledFfmpegLocator toolLocator,
        IStreamingProcessFactory processFactory)
    {
        _toolLocator = toolLocator ?? throw new ArgumentNullException(nameof(toolLocator));
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
    }

    public async IAsyncEnumerable<DecodedVideoFrame> ReadFramesAsync(
        SequentialVideoDecodeJob job,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        string inputPath = ResolveInputPath(job.InputPath);
        BundledToolPath ffmpeg = _toolLocator.Locate().Ffmpeg;
        if (!ffmpeg.Exists || ffmpeg.IsDirectory)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.DecoderUnavailable,
                "Bundled FFmpeg is unavailable. Check its status in Settings.",
                $"FFmpeg path: {ffmpeg.ResolvedPath}");
        }

        int frameByteCount;
        try
        {
            frameByteCount = checked(job.DisplayDimensions.Width * job.DisplayDimensions.Height * 4);
        }
        catch (OverflowException exception)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidDimensions,
                "The selected video dimensions are too large to export.",
                exception.Message,
                exception);
        }

        IStreamingProcessSession process;
        try
        {
            process = _processFactory.Start(
                ffmpeg.ResolvedPath,
                CreateArguments(inputPath),
                redirectStandardInput: false,
                redirectStandardOutput: true);
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException
            or UnauthorizedAccessException
            or IOException)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.DecoderUnavailable,
                "GlyphFrame could not start the bundled video decoder.",
                exception.Message,
                exception);
        }

        await using (process.ConfigureAwait(false))
        {
            Task<string> errorTask = StreamingProcessSupport.ReadStandardErrorAsync(process.StandardError);
            long sequenceNumber = 0;
            try
            {
                while (true)
                {
                    byte[] pixels = new byte[frameByteCount];
                    int received = await ReadFrameAsync(
                        process.StandardOutput,
                        pixels,
                        cancellationToken).ConfigureAwait(false);
                    if (received == 0)
                    {
                        break;
                    }

                    if (received != frameByteCount)
                    {
                        throw new VideoExportException(
                            VideoExportErrorCategory.TruncatedFrame,
                            "GlyphFrame received an incomplete frame while decoding the video.",
                            $"Frame {sequenceNumber}: expected {frameByteCount} RGBA bytes, received {received}.");
                    }

                    yield return new DecodedVideoFrame(
                        sequenceNumber,
                        new DecodedImage(
                            job.DisplayDimensions.Width,
                            job.DisplayDimensions.Height,
                            pixels));
                    sequenceNumber = checked(sequenceNumber + 1);
                }

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                string standardError = await ReadErrorWithBoundAsync(errorTask).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new VideoExportException(
                        VideoExportErrorCategory.DecodeFailed,
                        "GlyphFrame could not decode the complete video.",
                        $"FFmpeg exit code: {process.ExitCode}. {standardError}".Trim());
                }

                if (sequenceNumber == 0)
                {
                    throw new VideoExportException(
                        VideoExportErrorCategory.DecodeFailed,
                        "The selected file does not contain a decodable video stream.",
                        standardError);
                }
            }
            finally
            {
                await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                Observe(errorTask);
            }
        }
    }

    internal static IReadOnlyList<string> CreateArguments(string inputPath) =>
    [
        "-hide_banner",
        "-nostdin",
        "-loglevel", "error",
        "-i", inputPath,
        "-map", "0:v:0",
        "-an",
        "-sn",
        "-dn",
        "-fps_mode", "passthrough",
        "-pix_fmt", "rgba",
        "-f", "rawvideo",
        "pipe:1"
    ];

    internal static async Task<int> ReadFrameAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static string ResolveInputPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidInput,
                "Select a video before exporting.");
        }

        string resolved;
        try
        {
            resolved = Path.GetFullPath(inputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidInput,
                "The selected video path is not valid.",
                exception.Message,
                exception);
        }

        if (!File.Exists(resolved))
        {
            throw new VideoExportException(
                VideoExportErrorCategory.InvalidInput,
                "The selected video no longer exists.");
        }

        return resolved;
    }

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
