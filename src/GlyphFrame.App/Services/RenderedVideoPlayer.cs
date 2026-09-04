using System.IO;
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

public interface IRenderedVideoPositionTimer : IDisposable
{
    event EventHandler? Tick;

    bool IsRunning { get; }

    void Start();

    void Stop();
}

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
    private readonly IRenderedVideoPlaybackBackend _backend;
    private readonly TimeSpan _openTimeout;
    private readonly IRenderedVideoPositionTimer _positionTimer;
    private TaskCompletionSource<RenderedVideoOpenResult>? _openCompletion;
    private string? _sourcePath;
    private string? _failureMessage;
    private string? _technicalDetail;
    private TimeSpan _duration;
    private bool _disposed;
    private bool _isOpen;
    private bool _isPlaying;
    private bool _hasEnded;
    private long _openGeneration;
    private Window? _owner;

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
        _positionTimer = positionTimer ?? throw new ArgumentNullException(nameof(positionTimer));
        _openTimeout = openTimeout ?? DefaultOpenTimeout;
        if (_openTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(openTimeout));
        }

        _backend.MediaOpened += OnMediaOpened;
        _backend.MediaEnded += OnMediaEnded;
        _backend.MediaFailed += OnMediaFailed;
        _positionTimer.Tick += OnPositionTimerTick;
    }

    public event EventHandler? StateChanged;

    public ImageSource PlaybackSource => _backend.VisualSource;

    public string? SourcePath => _sourcePath;

    public TimeSpan Position => ClampPosition(_backend.Position);

    public TimeSpan Duration => _duration;

    public double Volume => Math.Clamp(_backend.Volume, 0d, 1d);

    public bool IsMuted => _backend.IsMuted;

    public bool IsOpen => _isOpen;

    public bool IsPlaying => _isPlaying;

    public bool HasEnded => _hasEnded;

    public bool IsTimerRunning => _positionTimer.IsRunning;

    public string? FailureMessage => _failureMessage;

    public string? TechnicalDetail => _technicalDetail;

    public async Task<RenderedVideoOpenResult> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        double preservedVolume = Volume;
        bool preservedMute = IsMuted;
        Close();
        long generation = _openGeneration;
        _backend.Volume = preservedVolume;
        _backend.IsMuted = preservedMute;

        TaskCompletionSource<RenderedVideoOpenResult> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _openCompletion = completion;
        _sourcePath = filePath;
        _failureMessage = null;
        _technicalDetail = null;
        OnStateChanged();

        try
        {
            _backend.Open(filePath);
            using CancellationTokenRegistration registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            RenderedVideoOpenResult result = await completion.Task.WaitAsync(_openTimeout, cancellationToken);
            return !_disposed && generation == _openGeneration
                ? result
                : RenderedVideoOpenResult.Failed("Rendered-video review was superseded.");
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && generation == _openGeneration)
            {
                Close();
            }
            throw;
        }
        catch (TimeoutException exception)
        {
            if (_disposed || generation != _openGeneration)
            {
                return RenderedVideoOpenResult.Failed("Rendered-video review was superseded.");
            }
            SetFailure(
                "GlyphFrame could not open the rendered video for review in time.",
                exception.Message);
            _backend.Close();
            return RenderedVideoOpenResult.Failed(
                "GlyphFrame could not open the rendered video for review in time.",
                _technicalDetail);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            if (_disposed || generation != _openGeneration)
            {
                return RenderedVideoOpenResult.Failed("Rendered-video review was superseded.");
            }
            SetFailure("GlyphFrame could not open the rendered video for review.", exception.Message);
            _backend.Close();
            return RenderedVideoOpenResult.Failed(
                "GlyphFrame could not open the rendered video for review.",
                _technicalDetail);
        }
        finally
        {
            if (ReferenceEquals(_openCompletion, completion))
            {
                _openCompletion = null;
            }
        }
    }

    public void Play()
    {
        if (_disposed || !_isOpen)
        {
            return;
        }

        if (_hasEnded)
        {
            _backend.Position = TimeSpan.Zero;
            _hasEnded = false;
        }

        _backend.Play();
        _isPlaying = true;
        _positionTimer.Start();
        OnStateChanged();
    }

    public void Pause()
    {
        if (_disposed || !_isOpen)
        {
            return;
        }

        _backend.Pause();
        _isPlaying = false;
        _positionTimer.Stop();
        OnStateChanged();
    }

    public void Restart()
    {
        if (_disposed || !_isOpen)
        {
            return;
        }

        _backend.Position = TimeSpan.Zero;
        _hasEnded = false;
        Play();
    }

    public void Seek(TimeSpan position)
    {
        if (_disposed || !_isOpen)
        {
            return;
        }

        _backend.Position = ClampPosition(position);
        // Only MediaEnded represents natural completion. Scrubbing to the end is
        // ordinary paused seeking, not a request for the replay overlay.
        _hasEnded = false;
        OnStateChanged();
    }

    public void SetVolume(double volume)
    {
        if (_disposed)
        {
            return;
        }

        _backend.Volume = double.IsFinite(volume) ? Math.Clamp(volume, 0d, 1d) : 0.75d;
        OnStateChanged();
    }

    public void ToggleMute()
    {
        if (_disposed)
        {
            return;
        }

        _backend.IsMuted = !_backend.IsMuted;
        OnStateChanged();
    }

    public void ShowFullscreen(string fitMode)
    {
        if (!_disposed && _isOpen)
        {
            _backend.ShowFullscreen(fitMode);
        }
    }

    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        _openGeneration++;
        _positionTimer.Stop();
        _backend.Stop();
        _backend.Close();
        _openCompletion?.TrySetResult(RenderedVideoOpenResult.Failed(
            "Rendered-video review was closed."));
        _openCompletion = null;
        _sourcePath = null;
        _duration = TimeSpan.Zero;
        _failureMessage = null;
        _technicalDetail = null;
        _isOpen = false;
        _isPlaying = false;
        _hasEnded = false;
        OnStateChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        SetOwner(null);
        _disposed = true;
        _backend.MediaOpened -= OnMediaOpened;
        _backend.MediaEnded -= OnMediaEnded;
        _backend.MediaFailed -= OnMediaFailed;
        _positionTimer.Tick -= OnPositionTimerTick;
        _positionTimer.Dispose();
        _backend.Dispose();
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (_disposed || _sourcePath is null || _openCompletion is null)
        {
            return;
        }

        _duration = _backend.Duration is TimeSpan duration && duration > TimeSpan.Zero
            ? duration
            : TimeSpan.Zero;
        _backend.Pause();
        _backend.Position = TimeSpan.Zero;
        _isOpen = true;
        _isPlaying = false;
        _hasEnded = false;
        _failureMessage = null;
        _technicalDetail = null;
        _positionTimer.Stop();
        _openCompletion?.TrySetResult(RenderedVideoOpenResult.Opened());
        OnStateChanged();
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (_disposed || !_isOpen || !_isPlaying)
        {
            return;
        }

        _positionTimer.Stop();
        _backend.Pause();
        _isPlaying = false;
        _hasEnded = true;
        // Keep the actual final decoded frame; seeking to duration can blank it.
        OnStateChanged();
    }

    private void OnMediaFailed(object? sender, RenderedVideoPlaybackFailedEventArgs e)
    {
        if (_disposed || _sourcePath is null)
        {
            return;
        }

        _positionTimer.Stop();
        _backend.Close();
        SetFailure(e.Message, e.TechnicalDetail);
        _openCompletion?.TrySetResult(RenderedVideoOpenResult.Failed(e.Message, e.TechnicalDetail));
    }

    private void OnPositionTimerTick(object? sender, EventArgs e) => OnStateChanged();

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

    private TimeSpan ClampPosition(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return _duration > TimeSpan.Zero && position > _duration ? _duration : position;
    }

    private void SetFailure(string message, string? technicalDetail)
    {
        _isOpen = false;
        _isPlaying = false;
        _hasEnded = false;
        _failureMessage = message;
        _technicalDetail = technicalDetail;
        OnStateChanged();
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
