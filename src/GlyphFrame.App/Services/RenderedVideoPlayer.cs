using System.Windows;
using System.Windows.Media;

namespace GlyphFrame.App.Services;

public sealed record RenderedVideoOpenResult(
    bool Success,
    string UserMessage,
    string? TechnicalDetail)
{
    public static RenderedVideoOpenResult Opened() => new(
        true,
        "The rendered video is ready for review.",
        null);

    public static RenderedVideoOpenResult Failed(string message, string? technicalDetail = null) => new(
        false,
        message,
        technicalDetail);
}

public sealed class RenderedVideoPlaybackFailedEventArgs(string message, string? technicalDetail = null)
    : EventArgs
{
    public string Message { get; } = message;

    public string? TechnicalDetail { get; } = technicalDetail;
}

public interface IRenderedVideoPlaybackBackend : IDisposable
{
    event EventHandler? MediaOpened;

    event EventHandler? MediaEnded;

    event EventHandler<RenderedVideoPlaybackFailedEventArgs>? MediaFailed;

    ImageSource VisualSource { get; }

    TimeSpan Position { get; set; }

    TimeSpan? Duration { get; }

    double Volume { get; set; }

    bool IsMuted { get; set; }

    void Open(string filePath);

    void Play();

    void Pause();

    void Stop();

    void Close();

    void ShowFullscreen(string fitMode);

    void SetOwner(Window? owner) { }

    void UpdateFitMode(string fitMode) { }
}

public interface IRenderedVideoPositionTimer : IMediaPlaybackPositionTimer;

public interface IRenderedVideoPlayer : IDisposable
{
    event EventHandler? StateChanged;

    ImageSource PlaybackSource { get; }

    string? SourcePath { get; }

    TimeSpan Position { get; }

    TimeSpan Duration { get; }

    double Volume { get; }

    bool IsMuted { get; }

    bool IsOpen { get; }

    bool IsPlaying { get; }

    bool HasEnded { get; }

    bool IsTimerRunning { get; }

    string? FailureMessage { get; }

    string? TechnicalDetail { get; }

    Task<RenderedVideoOpenResult> OpenAsync(string filePath, CancellationToken cancellationToken = default);

    void Play();

    void Pause();

    void Restart();

    void Seek(TimeSpan position);

    void SetVolume(double volume);

    void ToggleMute();

    void ShowFullscreen(string fitMode);

    void SetOwner(Window? owner) { }

    void UpdateFitMode(string fitMode) { }

    void Close();
}

public sealed class RenderedVideoPlayer : IRenderedVideoPlayer
{
    private static readonly TimeSpan DefaultOpenTimeout = TimeSpan.FromSeconds(15);
    private static readonly MediaPlaybackControllerOptions ControllerOptions = new(
        RequireNaturalVideoDimensions: false,
        ReturnFallbackOnCancellation: false,
        OpenedMessage: "The rendered video is ready for review.",
        FailureMessage: "GlyphFrame could not open the rendered video for review.",
        TimeoutMessage: "GlyphFrame could not open the rendered video for review in time.",
        ClosedMessage: "Rendered-video review was closed.",
        SupersededMessage: "Rendered-video review was superseded.",
        FallbackMessage: "GlyphFrame could not open the rendered video for review.",
        CancellationTechnicalDetail: "Rendered-video review opening was cancelled.");

    private readonly IRenderedVideoPlaybackBackend _backend;
    private readonly MediaPlaybackController _controller;
    private Window? _owner;
    private bool _disposed;

    public RenderedVideoPlayer()
        : this(new WpfRenderedVideoPlaybackBackend(), new DispatcherRenderedVideoPositionTimer(), DefaultOpenTimeout)
    {
    }

    public RenderedVideoPlayer(
        IRenderedVideoPlaybackBackend backend,
        IRenderedVideoPositionTimer positionTimer,
        TimeSpan? openTimeout = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        ArgumentNullException.ThrowIfNull(positionTimer);
        _controller = new MediaPlaybackController(
            new RenderedPlaybackBackendAdapter(backend),
            positionTimer,
            new SystemMediaPlaybackOpenDeadline(),
            openTimeout ?? DefaultOpenTimeout,
            ControllerOptions);
        _controller.StateChanged += OnControllerStateChanged;
    }

    public event EventHandler? StateChanged;

    public ImageSource PlaybackSource => _controller.PlaybackSource;

    public string? SourcePath => _controller.SourcePath;

    public TimeSpan Position => _controller.Position;

    public TimeSpan Duration => _controller.Duration;

    public double Volume => _controller.Volume;

    public bool IsMuted => _controller.IsMuted;

    public bool IsOpen => _controller.IsOpen;

    public bool IsPlaying => _controller.IsPlaying;

    public bool HasEnded => _controller.HasEnded;

    public bool IsTimerRunning => _controller.IsTimerRunning;

    public string? FailureMessage => _controller.FailureMessage;

    public string? TechnicalDetail => _controller.TechnicalDetail;

    public async Task<RenderedVideoOpenResult> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MediaPlaybackOpenResult result = await _controller.OpenAsync(filePath, cancellationToken);
        return result.IsReady
            ? RenderedVideoOpenResult.Opened()
            : RenderedVideoOpenResult.Failed(result.UserMessage, result.TechnicalDetail);
    }

    public void Play() => _controller.Play();

    public void Pause() => _controller.Pause();

    public void Restart() => _controller.Restart();

    public void Seek(TimeSpan position) => _controller.Seek(position);

    public void SetVolume(double volume) => _controller.SetVolume(volume);

    public void ToggleMute() => _controller.ToggleMute();

    public void ShowFullscreen(string fitMode)
    {
        if (!_disposed && IsOpen)
        {
            _backend.ShowFullscreen(fitMode);
        }
    }

    public void Close() => _controller.Close();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SetOwner(null);
        _controller.StateChanged -= OnControllerStateChanged;
        _controller.Dispose();
        _disposed = true;
    }

    public void SetOwner(Window? owner)
    {
        if (_owner is not null)
        {
            _owner.Closed -= OnOwnerClosed;
        }

        _owner = owner;
        if (_owner is not null)
        {
            _owner.Closed += OnOwnerClosed;
        }

        _backend.SetOwner(owner);
    }

    public void UpdateFitMode(string fitMode) => _backend.UpdateFitMode(fitMode);

    private void OnOwnerClosed(object? sender, EventArgs e) => Close();

    private void OnControllerStateChanged(object? sender, EventArgs e) =>
        StateChanged?.Invoke(this, EventArgs.Empty);

    private sealed class RenderedPlaybackBackendAdapter : IMediaPlaybackBackend
    {
        private readonly IRenderedVideoPlaybackBackend _backend;
        private long _generation;
        private bool _active;
        private bool _disposed;

        public RenderedPlaybackBackendAdapter(IRenderedVideoPlaybackBackend backend)
        {
            _backend = backend;
            _backend.MediaOpened += OnMediaOpened;
            _backend.MediaEnded += OnMediaEnded;
            _backend.MediaFailed += OnMediaFailed;
        }

        public event EventHandler<MediaPlaybackOpenedEventArgs>? MediaOpened;

        public event EventHandler<MediaPlaybackEndedEventArgs>? MediaEnded;

        public event EventHandler<MediaPlaybackFailedEventArgs>? MediaFailed;

        public ImageSource VisualSource => _backend.VisualSource;

        public TimeSpan Position
        {
            get => _backend.Position;
            set => _backend.Position = value;
        }

        public TimeSpan? Duration => _backend.Duration;

        public int NaturalVideoWidth => 0;

        public int NaturalVideoHeight => 0;

        public double Volume
        {
            get => _backend.Volume;
            set => _backend.Volume = value;
        }

        public bool IsMuted
        {
            get => _backend.IsMuted;
            set => _backend.IsMuted = value;
        }

        public void Open(string filePath, long generation)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _generation = generation;
            _active = true;
            try
            {
                _backend.Open(filePath);
            }
            catch
            {
                _active = false;
                throw;
            }
        }

        public void Play() => _backend.Play();

        public void Pause() => _backend.Pause();

        public void Stop() => _backend.Stop();

        public void Close()
        {
            _active = false;
            _backend.Close();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _active = false;
            _backend.MediaOpened -= OnMediaOpened;
            _backend.MediaEnded -= OnMediaEnded;
            _backend.MediaFailed -= OnMediaFailed;
            _backend.Dispose();
            _disposed = true;
        }

        private void OnMediaOpened(object? sender, EventArgs e)
        {
            if (_active && !_disposed)
            {
                MediaOpened?.Invoke(this, new MediaPlaybackOpenedEventArgs(_generation));
            }
        }

        private void OnMediaEnded(object? sender, EventArgs e)
        {
            if (_active && !_disposed)
            {
                MediaEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(_generation));
            }
        }

        private void OnMediaFailed(object? sender, RenderedVideoPlaybackFailedEventArgs e)
        {
            if (_active && !_disposed)
            {
                MediaFailed?.Invoke(this, new MediaPlaybackFailedEventArgs(
                    _generation,
                    e.Message,
                    e.TechnicalDetail));
            }
        }
    }
}
