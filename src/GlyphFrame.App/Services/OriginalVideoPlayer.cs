using System.Windows;
using System.Windows.Media;

namespace GlyphFrame.App.Services;

public enum OriginalVideoOpenOutcome
{
    Ready,
    FallbackRequired,
    Cancelled,
    Superseded
}

public sealed record OriginalVideoOpenResult(
    OriginalVideoOpenOutcome Outcome,
    string UserMessage,
    string? TechnicalDetail)
{
    public bool IsReady => Outcome == OriginalVideoOpenOutcome.Ready;

    public bool FallbackRequired => !IsReady;
}

public interface IOriginalVideoPlayer : IDisposable
{
    event EventHandler? StateChanged;

    ImageSource PlaybackSource { get; }

    string? SourcePath { get; }

    TimeSpan Position { get; }

    TimeSpan Duration { get; }

    int NaturalVideoWidth { get; }

    int NaturalVideoHeight { get; }

    double Volume { get; }

    bool IsMuted { get; }

    bool IsOpening { get; }

    bool IsPlaybackReady { get; }

    bool IsPlaying { get; }

    bool HasEnded { get; }

    bool IsTimerRunning { get; }

    bool FallbackRequired { get; }

    string? FailureMessage { get; }

    string? TechnicalDetail { get; }

    Task<OriginalVideoOpenResult> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    void Play();

    void Pause();

    void Restart();

    void Seek(TimeSpan position);

    void SetVolume(double volume);

    void ToggleMute();

    void ShowFullscreen(string fitMode);

    void SetOwner(Window? owner);

    void UpdateFitMode(string fitMode);

    void Close();
}

public sealed class OriginalVideoPlayer : IOriginalVideoPlayer
{
    public const string PlaybackUnavailableMessage =
        "Continuous playback is unavailable for this video. Frame preview and rendering remain available.";

    private static readonly TimeSpan DefaultOpenTimeout = TimeSpan.FromSeconds(15);
    private static readonly MediaPlaybackControllerOptions ControllerOptions = new(
        RequireNaturalVideoDimensions: true,
        ReturnFallbackOnCancellation: true,
        OpenedMessage: "The original video is ready for playback.",
        FailureMessage: PlaybackUnavailableMessage,
        TimeoutMessage: PlaybackUnavailableMessage,
        ClosedMessage: "Original-video playback was closed.",
        SupersededMessage: "Original-video playback was superseded.",
        FallbackMessage: PlaybackUnavailableMessage,
        CancellationTechnicalDetail: "Original-video playback opening was cancelled.");

    private readonly MediaPlaybackController _controller;
    private readonly IVideoPlaybackFullscreenHost _fullscreenHost;
    private Window? _owner;
    private bool _disposed;

    public OriginalVideoPlayer()
        : this(
            new WpfMediaPlaybackBackend(),
            new DispatcherMediaPlaybackPositionTimer(),
            new SystemMediaPlaybackOpenDeadline(),
            DefaultOpenTimeout,
            new WpfVideoPlaybackFullscreenHost(VideoPlaybackSurfaceKind.Original))
    {
    }

    public OriginalVideoPlayer(
        IMediaPlaybackBackend backend,
        IMediaPlaybackPositionTimer positionTimer,
        IMediaPlaybackOpenDeadline? openDeadline = null,
        TimeSpan? openTimeout = null,
        IVideoPlaybackFullscreenHost? fullscreenHost = null)
    {
        _controller = new MediaPlaybackController(
            backend,
            positionTimer,
            openDeadline ?? new SystemMediaPlaybackOpenDeadline(),
            openTimeout ?? DefaultOpenTimeout,
            ControllerOptions);
        _fullscreenHost = fullscreenHost ?? new NullVideoPlaybackFullscreenHost();
        _controller.StateChanged += OnControllerStateChanged;
    }

    public event EventHandler? StateChanged;

    public ImageSource PlaybackSource => _controller.PlaybackSource;

    public string? SourcePath => _controller.SourcePath;

    public TimeSpan Position => _controller.Position;

    public TimeSpan Duration => _controller.Duration;

    public int NaturalVideoWidth => _controller.NaturalVideoWidth;

    public int NaturalVideoHeight => _controller.NaturalVideoHeight;

    public double Volume => _controller.Volume;

    public bool IsMuted => _controller.IsMuted;

    public bool IsOpening => _controller.IsOpening;

    public bool IsPlaybackReady => _controller.IsOpen && !_controller.FallbackRequired;

    public bool IsPlaying => _controller.IsPlaying;

    public bool HasEnded => _controller.HasEnded;

    public bool IsTimerRunning => _controller.IsTimerRunning;

    public bool FallbackRequired => _controller.FallbackRequired;

    public string? FailureMessage => _controller.FailureMessage;

    public string? TechnicalDetail => _controller.TechnicalDetail;

    public async Task<OriginalVideoOpenResult> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MediaPlaybackOpenResult result = await _controller.OpenAsync(filePath, cancellationToken);
        return new OriginalVideoOpenResult(
            MapOutcome(result.Outcome),
            result.UserMessage,
            result.TechnicalDetail);
    }

    public void Play() => _controller.Play();

    public void Pause() => _controller.Pause();

    public void Restart() => _controller.Restart();

    public void Seek(TimeSpan position) => _controller.Seek(position);

    public void SetVolume(double volume) => _controller.SetVolume(volume);

    public void ToggleMute() => _controller.ToggleMute();

    public void ShowFullscreen(string fitMode)
    {
        if (!_disposed && IsPlaybackReady)
        {
            _fullscreenHost.Show(fitMode);
        }
    }

    public void SetOwner(Window? owner)
    {
        if (ReferenceEquals(owner, _owner))
        {
            return;
        }

        if (_owner is not null)
        {
            _owner.Closed -= OnOwnerClosed;
        }

        _owner = owner;
        if (_owner is not null)
        {
            _owner.Closed += OnOwnerClosed;
        }

        _fullscreenHost.SetOwner(owner);
    }

    public void UpdateFitMode(string fitMode) => _fullscreenHost.UpdateFitMode(fitMode);

    public void Close()
    {
        _fullscreenHost.Close();
        _controller.Close();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SetOwner(null);
        _controller.StateChanged -= OnControllerStateChanged;
        _controller.Dispose();
        _fullscreenHost.Dispose();
        _disposed = true;
    }

    private static OriginalVideoOpenOutcome MapOutcome(MediaPlaybackOpenOutcome outcome) => outcome switch
    {
        MediaPlaybackOpenOutcome.Ready => OriginalVideoOpenOutcome.Ready,
        MediaPlaybackOpenOutcome.Cancelled => OriginalVideoOpenOutcome.Cancelled,
        MediaPlaybackOpenOutcome.Superseded or MediaPlaybackOpenOutcome.Closed =>
            OriginalVideoOpenOutcome.Superseded,
        MediaPlaybackOpenOutcome.Failed or MediaPlaybackOpenOutcome.FallbackRequired =>
            OriginalVideoOpenOutcome.FallbackRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };

    private void OnControllerStateChanged(object? sender, EventArgs e)
    {
        if (FallbackRequired)
        {
            _fullscreenHost.Close();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnOwnerClosed(object? sender, EventArgs e)
    {
        Close();
        SetOwner(null);
    }
}
