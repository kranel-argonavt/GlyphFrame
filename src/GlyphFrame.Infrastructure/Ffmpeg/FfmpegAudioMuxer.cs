using System.ComponentModel;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Ffmpeg;

public sealed class FfmpegAudioMuxer : IAudioMuxer
{
    private static readonly TimeSpan DefaultMuxTimeout = TimeSpan.FromMinutes(2);
    private readonly TimeSpan _muxTimeout;
    private readonly IStreamingProcessFactory _processFactory;
    private readonly IBundledFfmpegLocator _toolLocator;

    public FfmpegAudioMuxer(IBundledFfmpegLocator toolLocator)
        : this(toolLocator, new SystemStreamingProcessFactory(), DefaultMuxTimeout)
    {
    }

    internal FfmpegAudioMuxer(
        IBundledFfmpegLocator toolLocator,
        IStreamingProcessFactory processFactory,
        TimeSpan muxTimeout)
    {
        _toolLocator = toolLocator ?? throw new ArgumentNullException(nameof(toolLocator));
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        _muxTimeout = muxTimeout > TimeSpan.Zero
            ? muxTimeout
            : throw new ArgumentOutOfRangeException(nameof(muxTimeout));
    }

    public async Task<AudioMuxResult> MuxAsync(
        AudioMuxJob job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        AudioMuxResult? validationFailure = ValidateJob(job);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        BundledToolPath ffmpeg = _toolLocator.Locate().Ffmpeg;
        if (!ffmpeg.Exists || ffmpeg.IsDirectory)
        {
            return Failure(
                VideoExportErrorCategory.AudioMuxStartFailed,
                "Bundled FFmpeg is unavailable, so GlyphFrame cannot preserve audio.",
                $"FFmpeg path: {ffmpeg.ResolvedPath}");
        }

        IStreamingProcessSession process;
        try
        {
            process = _processFactory.Start(
                ffmpeg.ResolvedPath,
                CreateArguments(job),
                redirectStandardInput: false,
                redirectStandardOutput: false);
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException
            or UnauthorizedAccessException
            or IOException)
        {
            return Failure(
                VideoExportErrorCategory.AudioMuxStartFailed,
                "GlyphFrame could not start the bundled audio muxer.",
                exception.Message);
        }

        await using (process.ConfigureAwait(false))
        {
            Task<string> errorTask = StreamingProcessSupport.ReadStandardErrorAsync(process.StandardError);
            try
            {
                using CancellationTokenSource timeout = new(_muxTimeout);
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeout.Token);
                try
                {
                    await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                    return Failure(
                        VideoExportErrorCategory.AudioMuxFailed,
                        "Audio muxing did not finish in time.",
                        $"Audio muxing exceeded {_muxTimeout}.");
                }

                string standardError = await ReadErrorWithBoundAsync(errorTask).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    return Failure(
                        job.Strategy == AudioMuxStrategy.StreamCopy
                            ? VideoExportErrorCategory.AudioStreamCopyFailed
                            : VideoExportErrorCategory.AudioTranscodeFailed,
                        job.Strategy == AudioMuxStrategy.StreamCopy
                            ? "GlyphFrame could not preserve the source audio without re-encoding."
                            : "GlyphFrame could not convert the source audio to AAC.",
                        $"FFmpeg exit code: {process.ExitCode}. {standardError}".Trim());
                }

                if (!File.Exists(job.OutputPath) || new FileInfo(job.OutputPath).Length == 0)
                {
                    return Failure(
                        VideoExportErrorCategory.AudioMuxFailed,
                        "FFmpeg did not create a completed audio-muxed MP4 file.",
                        standardError);
                }

                return AudioMuxResult.Completed(job.Strategy);
            }
            catch (OperationCanceledException)
            {
                await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                throw;
            }
            finally
            {
                await StreamingProcessSupport.TerminateAsync(process).ConfigureAwait(false);
                Observe(errorTask);
            }
        }
    }

    internal static IReadOnlyList<string> CreateArguments(AudioMuxJob job)
    {
        List<string> arguments =
        [
            "-hide_banner",
            "-nostdin",
            "-loglevel", "error",
            "-i", job.VideoOnlyInputPath,
            "-i", job.SourceMediaPath,
            "-map", "0:v:0",
            "-map", "1:a:0",
            "-c:v", "copy",
            "-c:a", job.Strategy == AudioMuxStrategy.StreamCopy ? "copy" : "aac"
        ];

        if (job.Strategy == AudioMuxStrategy.TranscodeToAac)
        {
            arguments.AddRange(
            [
                "-b:a", $"{AudioMuxPolicy.AacBitrateKbps}k",
                "-ar", AudioMuxPolicy.AacSampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-ac", AudioMuxPolicy.AacChannels.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ]);
        }

        arguments.AddRange(
        [
            "-t", AudioMuxPolicy.FormatDuration(job.VideoDuration),
            "-movflags", "+faststart",
            "-f", "mp4",
            "-y",
            job.OutputPath
        ]);
        return arguments;
    }

    private static AudioMuxResult? ValidateJob(AudioMuxJob job)
    {
        if (!Enum.IsDefined(job.Strategy))
        {
            return Failure(
                VideoExportErrorCategory.AudioMuxFailed,
                "The selected audio muxing strategy is not supported.");
        }

        if (!File.Exists(job.VideoOnlyInputPath) || !File.Exists(job.SourceMediaPath))
        {
            return Failure(
                VideoExportErrorCategory.AudioMuxFailed,
                "GlyphFrame could not find the media required for audio muxing.");
        }

        if (string.IsNullOrWhiteSpace(job.OutputPath) || job.VideoDuration <= TimeSpan.Zero)
        {
            return Failure(
                VideoExportErrorCategory.AudioMuxFailed,
                "The audio muxing request was not valid.");
        }

        return null;
    }

    private static AudioMuxResult Failure(
        VideoExportErrorCategory category,
        string userMessage,
        string? technicalDetail = null) => AudioMuxResult.Failure(category, userMessage, technicalDetail);

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
