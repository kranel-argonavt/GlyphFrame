using System.Globalization;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Media;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

/// <summary>
/// Extracts one Unicode-safe video frame through GlyphFrame's bundled FFmpeg process.
/// </summary>
public sealed class FfmpegVideoFrameReader : IVideoFrameReader
{
    private readonly IBundledFfmpegLocator _toolLocator;
    private readonly IBinaryProcessRunner _processRunner;
    private readonly IVideoMetadataReader _metadataReader;

    public FfmpegVideoFrameReader(
        IBundledFfmpegLocator toolLocator,
        IBinaryProcessRunner processRunner,
        IVideoMetadataReader metadataReader)
    {
        _toolLocator = toolLocator ?? throw new ArgumentNullException(nameof(toolLocator));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _metadataReader = metadataReader ?? throw new ArgumentNullException(nameof(metadataReader));
    }

    public async Task<VideoFrameReadResult> ReadAsync(
        string inputPath,
        double normalizedPosition,
        VideoMetadata? metadataContext = null,
        CancellationToken cancellationToken = default)
    {
        double requestedPosition = NormalizePosition(normalizedPosition);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? path = NormalizePath(inputPath);
            if (path is null)
            {
                return Failure(
                    VideoFrameReadStatus.InvalidMedia,
                    VideoFrameReadErrorCategory.InvalidPath,
                    requestedPosition,
                    "The selected video path is not valid.");
            }

            if (!File.Exists(path))
            {
                return Failure(
                    VideoFrameReadStatus.InvalidMedia,
                    VideoFrameReadErrorCategory.FileNotFound,
                    requestedPosition,
                    "The selected video no longer exists.");
            }

            if (SupportedMediaFormats.DetectFromExtension(path) != MediaSelectionKind.Video)
            {
                return Failure(
                    VideoFrameReadStatus.InvalidMedia,
                    VideoFrameReadErrorCategory.InvalidMedia,
                    requestedPosition,
                    "This video format is not supported.");
            }

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, true))
            {
            }

            BundledFfmpegLocation location = _toolLocator.Locate();
            if (!location.Ffmpeg.Exists || location.Ffmpeg.IsDirectory)
            {
                return Failure(
                    VideoFrameReadStatus.Failed,
                    VideoFrameReadErrorCategory.DecoderUnavailable,
                    requestedPosition,
                    "GlyphFrame's bundled video decoder is unavailable.",
                    $"Bundled FFmpeg was not found at the resolved application tool location: {location.ToolsDirectory}");
            }

            VideoMetadata metadata = await ResolveMetadataAsync(path, metadataContext, cancellationToken)
                .ConfigureAwait(false);
            if (!TryGetFrameLayout(metadata, out int width, out int height, out int expectedBytes, out string? layoutError))
            {
                return Failure(
                    VideoFrameReadStatus.Failed,
                    VideoFrameReadErrorCategory.InvalidDimensions,
                    requestedPosition,
                    "GlyphFrame could not determine safe video-frame dimensions.",
                    layoutError);
            }

            TimeSpan? requestedTimestamp = ResolveRequestedTimestamp(metadata, requestedPosition);
            if (requestedPosition > 0d && requestedTimestamp is null)
            {
                return Failure(
                    VideoFrameReadStatus.Unavailable,
                    VideoFrameReadErrorCategory.MetadataUnavailable,
                    requestedPosition,
                    "GlyphFrame could not determine this video position.",
                    "A non-zero normalized position requires a reliable positive duration.");
            }

            BinaryProcessExecutionResult execution = await _processRunner.RunAsync(
                new BinaryProcessExecutionRequest(
                    location.Ffmpeg.ResolvedPath,
                    BuildArguments(path, requestedTimestamp),
                    VideoFrameExtractionDefaults.Timeout,
                    expectedBytes),
                cancellationToken).ConfigureAwait(false);

            if (execution.Status == ProcessExecutionStatus.TimedOut)
            {
                return Failure(
                    VideoFrameReadStatus.Unavailable,
                    VideoFrameReadErrorCategory.TimedOut,
                    requestedPosition,
                    "Video-frame loading took too long. Try another position.",
                    BuildTechnicalDetail(execution, requestedTimestamp, expectedBytes));
            }

            if (execution.Status == ProcessExecutionStatus.AccessDenied)
            {
                return Failure(
                    VideoFrameReadStatus.Failed,
                    VideoFrameReadErrorCategory.AccessDenied,
                    requestedPosition,
                    "GlyphFrame does not have permission to start its bundled video decoder.",
                    execution.TechnicalDetail);
            }

            if (execution.Status == ProcessExecutionStatus.StartFailed)
            {
                return Failure(
                    VideoFrameReadStatus.Failed,
                    VideoFrameReadErrorCategory.ProcessStartFailed,
                    requestedPosition,
                    "GlyphFrame could not start its bundled video decoder.",
                    execution.TechnicalDetail);
            }

            if (!execution.Succeeded)
            {
                bool noVideo = ContainsNoVideoStreamDiagnostic(execution.StandardError);
                return Failure(
                    VideoFrameReadStatus.InvalidMedia,
                    noVideo ? VideoFrameReadErrorCategory.NoVideoStream : VideoFrameReadErrorCategory.DecodeFailed,
                    requestedPosition,
                    noVideo
                        ? "The selected file does not contain a decodable video stream."
                        : "GlyphFrame could not decode a frame at this position.",
                    BuildTechnicalDetail(execution, requestedTimestamp, expectedBytes));
            }

            if (execution.StandardOutputExceededLimit || execution.StandardOutput.Length != expectedBytes)
            {
                return Failure(
                    VideoFrameReadStatus.Unavailable,
                    VideoFrameReadErrorCategory.IncompleteOutput,
                    requestedPosition,
                    "GlyphFrame received an incomplete video frame.",
                    BuildTechnicalDetail(execution, requestedTimestamp, expectedBytes));
            }

            DecodedImage frame = new(width, height, execution.StandardOutput);
            return VideoFrameReadResult.ForFrame(
                frame,
                requestedPosition,
                actualNormalizedPosition: null,
                requestedTimestamp,
                actualTimestamp: null,
                actualFrameIndex: null,
                metadata.Duration,
                metadata.FrameCount);
        }
        catch (OperationCanceledException)
        {
            return VideoFrameReadResult.Cancelled(requestedPosition);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(
                VideoFrameReadStatus.Failed,
                VideoFrameReadErrorCategory.AccessDenied,
                requestedPosition,
                "GlyphFrame does not have permission to read this video.",
                exception.Message);
        }
        catch (IOException exception)
        {
            return Failure(
                VideoFrameReadStatus.Failed,
                VideoFrameReadErrorCategory.OpenFailed,
                requestedPosition,
                "GlyphFrame could not read this video.",
                exception.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return Failure(
                VideoFrameReadStatus.InvalidMedia,
                VideoFrameReadErrorCategory.InvalidMedia,
                requestedPosition,
                "GlyphFrame could not decode this video.",
                exception.Message);
        }
        catch (Exception exception)
        {
            return Failure(
                VideoFrameReadStatus.Failed,
                VideoFrameReadErrorCategory.UnexpectedFailure,
                requestedPosition,
                "GlyphFrame could not load this video frame.",
                exception.ToString());
        }
    }

    private async Task<VideoMetadata> ResolveMetadataAsync(
        string path,
        VideoMetadata? metadata,
        CancellationToken cancellationToken)
    {
        if (metadata is not null)
        {
            return metadata;
        }

        return await _metadataReader.ReadAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> BuildArguments(string inputPath, TimeSpan? timestamp)
    {
        List<string> arguments =
        [
            "-hide_banner",
            "-nostdin",
            "-loglevel", "error"
        ];

        if (timestamp is TimeSpan target && target > TimeSpan.Zero)
        {
            arguments.Add("-ss");
            arguments.Add(target.TotalSeconds.ToString("0.#########", CultureInfo.InvariantCulture));
        }

        arguments.AddRange(
        [
            "-i", inputPath,
            "-map", "0:v:0",
            "-frames:v", "1",
            "-an",
            "-sn",
            "-dn",
            "-pix_fmt", "rgba",
            "-f", "rawvideo",
            "pipe:1"
        ]);
        return arguments;
    }

    private static TimeSpan? ResolveRequestedTimestamp(VideoMetadata metadata, double position)
    {
        if (position <= 0d)
        {
            return TimeSpan.Zero;
        }

        if (metadata.Duration is not TimeSpan duration || duration <= TimeSpan.Zero)
        {
            return null;
        }

        double targetSeconds = duration.TotalSeconds * position;
        if (position >= 1d)
        {
            double frameSeconds = metadata.Fps is > 0 and double fps && double.IsFinite(fps)
                ? 1d / fps
                : Math.Min(0.04d, duration.TotalSeconds / 2d);
            targetSeconds = Math.Max(0d, duration.TotalSeconds - Math.Max(0.001d, frameSeconds));
        }

        return TimeSpan.FromSeconds(targetSeconds);
    }

    private static bool TryGetFrameLayout(
        VideoMetadata metadata,
        out int width,
        out int height,
        out int expectedBytes,
        out string? error)
    {
        width = metadata.DisplayWidth ?? 0;
        height = metadata.DisplayHeight ?? 0;
        expectedBytes = 0;
        if (width <= 0 || height <= 0)
        {
            error = $"Display dimensions were {width} x {height}.";
            return false;
        }

        if (width > VideoFrameExtractionDefaults.MaximumDimension
            || height > VideoFrameExtractionDefaults.MaximumDimension)
        {
            error = $"Display dimensions {width} x {height} exceed the supported safety bound.";
            return false;
        }

        try
        {
            long pixels = checked((long)width * height);
            if (pixels > VideoFrameExtractionDefaults.MaximumPixelCount)
            {
                error = $"Display dimensions {width} x {height} exceed the supported pixel-count bound.";
                return false;
            }

            expectedBytes = checked(width * height * 4);
            error = null;
            return true;
        }
        catch (OverflowException)
        {
            error = $"Display dimensions {width} x {height} overflow the RGBA frame size.";
            return false;
        }
    }

    private static string BuildTechnicalDetail(
        BinaryProcessExecutionResult execution,
        TimeSpan? requestedTimestamp,
        int expectedBytes)
    {
        string stderr = string.IsNullOrWhiteSpace(execution.StandardError)
            ? "No FFmpeg error output."
            : execution.StandardError.Trim();
        return string.Join(
            " ",
            "Backend: bundled FFmpeg.",
            $"Exit code: {execution.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}.",
            $"Requested timestamp: {requestedTimestamp?.TotalSeconds.ToString("0.#########", CultureInfo.InvariantCulture) ?? "unavailable"} seconds.",
            $"Expected RGBA bytes: {expectedBytes}; received: {execution.StandardOutput.Length}.",
            execution.StandardOutputExceededLimit ? "Standard output exceeded its capture bound." : string.Empty,
            execution.StandardErrorWasTruncated ? "Standard error was truncated." : string.Empty,
            stderr,
            execution.TechnicalDetail ?? string.Empty).Trim();
    }

    private static bool ContainsNoVideoStreamDiagnostic(string stderr) =>
        stderr.Contains("matches no streams", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("does not contain any stream", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("stream map", StringComparison.OrdinalIgnoreCase)
            && stderr.Contains("0:v:0", StringComparison.OrdinalIgnoreCase);

    private static double NormalizePosition(double position) => double.IsFinite(position)
        ? Math.Clamp(position, 0d, 1d)
        : 0d;

    private static string? NormalizePath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(inputPath.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static VideoFrameReadResult Failure(
        VideoFrameReadStatus status,
        VideoFrameReadErrorCategory category,
        double requestedPosition,
        string userMessage,
        string? technicalDetail = null) =>
        VideoFrameReadResult.Failure(status, category, requestedPosition, userMessage, technicalDetail);
}
