using System.Diagnostics;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record VideoEncodingProgress(
    VideoEncodingProgressStage Stage,
    long FramesAccepted);

public sealed record VideoExportProgress(
    VideoExportStage Stage,
    long ProcessedFrames,
    long? TotalFrames,
    double? Percentage,
    TimeSpan Elapsed,
    TimeSpan? EstimatedRemaining,
    bool IsIndeterminate,
    string StageDescription);

public sealed class VideoExportProgressTracker
{
    public const double MaximumIncompletePercentage = 99d;
    public const long MinimumFramesForEstimate = 2;
    public static readonly TimeSpan MinimumElapsedForEstimate = TimeSpan.FromSeconds(1);

    private readonly Func<TimeSpan> _elapsedProvider;
    private readonly object _gate = new();
    private readonly IProgress<VideoExportProgress>? _progress;
    private readonly long? _totalFrames;
    private VideoExportStage _stage = VideoExportStage.Preparing;
    private long _processedFrames;
    private double _lastPercentage;
    private bool _terminal;

    public VideoExportProgressTracker(
        IProgress<VideoExportProgress>? progress,
        long? totalFrames,
        Func<TimeSpan>? elapsedProvider = null)
    {
        _progress = progress;
        _totalFrames = NormalizeTotalFrames(totalFrames);
        _elapsedProvider = elapsedProvider ?? CreateStopwatchProvider();
    }

    public void ReportStage(VideoExportStage stage)
    {
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        VideoExportProgress? snapshot;
        lock (_gate)
        {
            if (_terminal || stage == VideoExportStage.Completed)
            {
                return;
            }

            if (stage == VideoExportStage.Cancelling)
            {
                _stage = stage;
                _terminal = true;
            }
            else if (stage < _stage)
            {
                return;
            }
            else
            {
                _stage = stage;
            }

            snapshot = CreateSnapshot();
        }

        _progress?.Report(snapshot);
    }

    public void ReportFramesAccepted(long framesAccepted)
    {
        VideoExportProgress? snapshot;
        lock (_gate)
        {
            if (_terminal || _stage > VideoExportStage.ProcessingFrames)
            {
                return;
            }

            _stage = VideoExportStage.ProcessingFrames;
            _processedFrames = Math.Max(_processedFrames, Math.Max(0, framesAccepted));
            snapshot = CreateSnapshot();
        }

        _progress?.Report(snapshot);
    }

    public void Complete()
    {
        VideoExportProgress? snapshot;
        lock (_gate)
        {
            if (_terminal)
            {
                return;
            }

            _stage = VideoExportStage.Completed;
            _lastPercentage = 100d;
            _terminal = true;
            snapshot = CreateSnapshot();
        }

        _progress?.Report(snapshot);
    }

    public static long? NormalizeTotalFrames(long? totalFrames) => totalFrames is > 0
        ? totalFrames
        : null;

    private VideoExportProgress CreateSnapshot()
    {
        TimeSpan elapsed = NormalizeElapsed(_elapsedProvider());
        double? percentage = CalculatePercentage();
        TimeSpan? estimatedRemaining = CalculateEstimatedRemaining(elapsed);
        return new VideoExportProgress(
            _stage,
            _processedFrames,
            _totalFrames,
            percentage,
            elapsed,
            estimatedRemaining,
            percentage is null,
            GetStageDescription(_stage));
    }

    private double? CalculatePercentage()
    {
        if (_stage == VideoExportStage.Completed)
        {
            return 100d;
        }

        if (_stage != VideoExportStage.ProcessingFrames || _totalFrames is not long totalFrames)
        {
            return null;
        }

        double candidate = Math.Clamp(
            _processedFrames / (double)totalFrames * MaximumIncompletePercentage,
            0d,
            MaximumIncompletePercentage);
        if (!double.IsFinite(candidate))
        {
            candidate = _lastPercentage;
        }

        _lastPercentage = Math.Max(_lastPercentage, candidate);
        return _lastPercentage;
    }

    private TimeSpan? CalculateEstimatedRemaining(TimeSpan elapsed)
    {
        if (_stage != VideoExportStage.ProcessingFrames
            || _totalFrames is not long totalFrames
            || _processedFrames < MinimumFramesForEstimate
            || _processedFrames >= totalFrames
            || elapsed < MinimumElapsedForEstimate)
        {
            return null;
        }

        double remainingTicks = elapsed.Ticks / (double)_processedFrames
            * (totalFrames - _processedFrames);
        if (!double.IsFinite(remainingTicks) || remainingTicks < 0d)
        {
            return null;
        }

        if (remainingTicks >= TimeSpan.MaxValue.Ticks)
        {
            return TimeSpan.MaxValue;
        }

        long ticks = (long)remainingTicks;
        return TimeSpan.FromTicks(ticks);
    }

    private static string GetStageDescription(VideoExportStage stage) => stage switch
    {
        VideoExportStage.Preparing => "Preparing export...",
        VideoExportStage.ProcessingFrames => "Decoding, rendering, and encoding frames...",
        VideoExportStage.FinishingEncoding => "Finishing video encoding...",
        VideoExportStage.InspectingAudio => "Checking source audio...",
        VideoExportStage.MergingAudio => "Merging audio...",
        VideoExportStage.CopyingArtifact => "Copying reviewed video...",
        VideoExportStage.Finalizing => "Finalizing output...",
        VideoExportStage.Cancelling => "Cancelling export and cleaning partial files...",
        VideoExportStage.Completed => "Export completed.",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    private static TimeSpan NormalizeElapsed(TimeSpan elapsed) => elapsed < TimeSpan.Zero
        ? TimeSpan.Zero
        : elapsed;

    private static Func<TimeSpan> CreateStopwatchProvider()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed;
    }
}
