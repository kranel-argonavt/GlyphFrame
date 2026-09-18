namespace GlyphFrame.App.Services;

public interface IVideoPlaybackCoordinator
{
    void PauseAll();

    void PlayOriginal();

    void RestartOriginal();

    void PlayRendered();

    void RestartRendered();

    PlaybackTimestampTransfer PrepareOriginalSwitch(TimeSpan? targetDuration, double? targetFramesPerSecond);

    PlaybackTimestampTransfer PrepareRenderedSwitch(
        TimeSpan? targetDuration,
        double? targetFramesPerSecond,
        TimeSpan? outgoingTimestamp = null);

    void ApplyOriginalTransfer(PlaybackTimestampTransfer transfer);

    void ApplyRenderedTransfer(PlaybackTimestampTransfer transfer);
}

public sealed record PlaybackTimestampTransfer(
    TimeSpan RequestedTimestamp,
    TimeSpan TargetTimestamp,
    TimeSpan? TargetDuration,
    TimeSpan? TargetFrameInterval,
    TimeSpan? ValidationTolerance)
{
    public static PlaybackTimestampTransfer Create(
        TimeSpan requestedTimestamp,
        TimeSpan? targetDuration,
        double? targetFramesPerSecond)
    {
        TimeSpan requested = requestedTimestamp < TimeSpan.Zero ? TimeSpan.Zero : requestedTimestamp;
        TimeSpan? duration = IsUsable(targetDuration) ? targetDuration : null;
        TimeSpan? frameInterval = ResolveFrameInterval(targetFramesPerSecond);
        TimeSpan target = TimeSpan.Zero;
        if (duration is TimeSpan usableDuration)
        {
            target = requested > usableDuration ? usableDuration : requested;
            if (requested >= usableDuration && frameInterval is TimeSpan interval)
            {
                target = usableDuration > interval ? usableDuration - interval : TimeSpan.Zero;
            }
        }

        TimeSpan? tolerance = frameInterval is TimeSpan trustworthyInterval
            ? Max(TimeSpan.FromMilliseconds(250), trustworthyInterval + trustworthyInterval)
            : null;
        return new(requested, target, duration, frameInterval, tolerance);
    }

    private static bool IsUsable(TimeSpan? value) => value is TimeSpan duration && duration > TimeSpan.Zero;

    private static TimeSpan? ResolveFrameInterval(double? framesPerSecond)
    {
        if (framesPerSecond is not double fps || !double.IsFinite(fps) || fps <= 0d)
        {
            return null;
        }

        double ticks = TimeSpan.TicksPerSecond / fps;
        return double.IsFinite(ticks) && ticks >= 1d && ticks <= TimeSpan.MaxValue.Ticks
            ? TimeSpan.FromTicks((long)Math.Round(ticks, MidpointRounding.AwayFromZero))
            : null;
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second) => first >= second ? first : second;
}

/// <summary>Owns only the single-audio transport rule. Each player retains its
/// independent source, position, volume, mute, timer, and native lifetime.</summary>
public sealed class VideoPlaybackCoordinator(
    IOriginalVideoPlayer original,
    IRenderedVideoPlayer rendered) : IVideoPlaybackCoordinator
{
    private readonly IOriginalVideoPlayer _original = original ?? throw new ArgumentNullException(nameof(original));
    private readonly IRenderedVideoPlayer _rendered = rendered ?? throw new ArgumentNullException(nameof(rendered));

    public void PauseAll()
    {
        _original.Pause();
        _rendered.Pause();
    }

    public void PlayOriginal()
    {
        _rendered.Pause();
        _original.Play();
    }

    public void RestartOriginal()
    {
        _rendered.Pause();
        _original.Restart();
    }

    public void PlayRendered()
    {
        _original.Pause();
        _rendered.Play();
    }

    public void RestartRendered()
    {
        _original.Pause();
        _rendered.Restart();
    }

    public PlaybackTimestampTransfer PrepareOriginalSwitch(
        TimeSpan? targetDuration,
        double? targetFramesPerSecond)
    {
        _rendered.Pause();
        TimeSpan outgoingTimestamp = _rendered.Position;
        _original.Pause();
        return PlaybackTimestampTransfer.Create(
            outgoingTimestamp,
            targetDuration,
            targetFramesPerSecond);
    }

    public PlaybackTimestampTransfer PrepareRenderedSwitch(
        TimeSpan? targetDuration,
        double? targetFramesPerSecond,
        TimeSpan? outgoingTimestamp = null)
    {
        _original.Pause();
        TimeSpan timestamp = outgoingTimestamp ?? _original.Position;
        _rendered.Pause();
        return PlaybackTimestampTransfer.Create(timestamp, targetDuration, targetFramesPerSecond);
    }

    public void ApplyOriginalTransfer(PlaybackTimestampTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        _original.Pause();
        _original.Seek(transfer.TargetTimestamp);
        _original.Pause();
    }

    public void ApplyRenderedTransfer(PlaybackTimestampTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        _rendered.Pause();
        _rendered.Seek(transfer.TargetTimestamp);
        _rendered.Pause();
    }
}
