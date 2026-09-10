using System.IO;
using System.Windows.Media;

namespace GlyphFrame.App.Services;

internal enum MediaPlaybackOpenOutcome
{
    Ready,
    Failed,
    FallbackRequired,
    Cancelled,
    Superseded,
    Closed
}

internal sealed record MediaPlaybackOpenResult(
    MediaPlaybackOpenOutcome Outcome,
    string UserMessage,
    string? TechnicalDetail)
{
    public bool IsReady => Outcome == MediaPlaybackOpenOutcome.Ready;
}

internal sealed record MediaPlaybackControllerOptions(
    bool RequireNaturalVideoDimensions,
    bool ReturnFallbackOnCancellation,
    string OpenedMessage,
    string FailureMessage,
    string TimeoutMessage,
    string ClosedMessage,
    string SupersededMessage,
    string FallbackMessage,
    string CancellationTechnicalDetail);

internal sealed class MediaPlaybackController : IDisposable
{
    private readonly IMediaPlaybackBackend _backend;
    private readonly IMediaPlaybackPositionTimer _positionTimer;
    private readonly IMediaPlaybackOpenDeadline _openDeadline;
    private readonly TimeSpan _openTimeout;
    private readonly MediaPlaybackControllerOptions _options;
    private TaskCompletionSource<MediaPlaybackOpenResult>? _openCompletion;
    private string? _sourcePath;
    private string? _failureMessage;
    private string? _technicalDetail;
    private TimeSpan _duration;
    private int _naturalVideoWidth;
    private int _naturalVideoHeight;
    private bool _disposed;
    private bool _isOpening;
    private bool _isOpen;
    private bool _isPlaying;
    private bool _hasEnded;
    private bool _fallbackRequired;
    private long _generation;

    public MediaPlaybackController(
        IMediaPlaybackBackend backend,
        IMediaPlaybackPositionTimer positionTimer,
        IMediaPlaybackOpenDeadline openDeadline,
        TimeSpan openTimeout,
        MediaPlaybackControllerOptions options)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _positionTimer = positionTimer ?? throw new ArgumentNullException(nameof(positionTimer));
        _openDeadline = openDeadline ?? throw new ArgumentNullException(nameof(openDeadline));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (openTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(openTimeout));
        }

        _openTimeout = openTimeout;
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

    public int NaturalVideoWidth => _naturalVideoWidth;

    public int NaturalVideoHeight => _naturalVideoHeight;

    public double Volume => Math.Clamp(_backend.Volume, 0d, 1d);

    public bool IsMuted => _backend.IsMuted;

    public bool IsOpening => _isOpening;

    public bool IsOpen => _isOpen;

    public bool IsPlaying => _isPlaying;

    public bool HasEnded => _hasEnded;

    public bool IsTimerRunning => _positionTimer.IsRunning;

    public bool FallbackRequired => _fallbackRequired;

    public string? FailureMessage => _failureMessage;

    public string? TechnicalDetail => _technicalDetail;

    public async Task<MediaPlaybackOpenResult> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!_options.ReturnFallbackOnCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        double preservedVolume = Volume;
        bool preservedMute = IsMuted;
        CloseCore(MediaPlaybackOpenOutcome.Superseded, _options.SupersededMessage, notify: false);
        long generation = ++_generation;
        _backend.Volume = preservedVolume;
        _backend.IsMuted = preservedMute;

        TaskCompletionSource<MediaPlaybackOpenResult> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _openCompletion = completion;
        _sourcePath = filePath;
        _failureMessage = null;
        _technicalDetail = null;
        _duration = TimeSpan.Zero;
        _naturalVideoWidth = 0;
        _naturalVideoHeight = 0;
        _isOpening = true;
        _isOpen = false;
        _isPlaying = false;
        _hasEnded = false;
        _fallbackRequired = false;
        OnStateChanged();

        if (cancellationToken.IsCancellationRequested)
        {
            return TransitionToFailure(
                MediaPlaybackOpenOutcome.Cancelled,
                _options.FallbackMessage,
                _options.CancellationTechnicalDetail);
        }

        try
        {
            _backend.Open(filePath, generation);
            using CancellationTokenRegistration registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            await _openDeadline.WaitAsync(completion.Task, _openTimeout, cancellationToken);
            MediaPlaybackOpenResult result = await completion.Task;
            return !_disposed && generation == _generation
                ? result
                : SupersededResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (_disposed || generation != _generation)
            {
                return SupersededResult();
            }

            if (!_options.ReturnFallbackOnCancellation)
            {
                CloseCore(MediaPlaybackOpenOutcome.Cancelled, _options.ClosedMessage);
                throw;
            }

            return TransitionToFailure(
                MediaPlaybackOpenOutcome.Cancelled,
                _options.FallbackMessage,
                _options.CancellationTechnicalDetail);
        }
        catch (TimeoutException exception)
        {
            if (_disposed || generation != _generation)
            {
                return SupersededResult();
            }

            return TransitionToFailure(
                FailureOutcome,
                _options.RequireNaturalVideoDimensions ? _options.FallbackMessage : _options.TimeoutMessage,
                exception.Message);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            if (_disposed || generation != _generation)
            {
                return SupersededResult();
            }

            return TransitionToFailure(FailureOutcome, FailureUserMessage, exception.Message);
        }
        finally
        {
            if (ReferenceEquals(_openCompletion, completion))
            {
                _openCompletion = null;
                _isOpening = false;
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

    public void Close()
    {
        if (!_disposed)
        {
            CloseCore(MediaPlaybackOpenOutcome.Closed, _options.ClosedMessage);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseCore(MediaPlaybackOpenOutcome.Closed, _options.ClosedMessage);
        _disposed = true;
        _backend.MediaOpened -= OnMediaOpened;
        _backend.MediaEnded -= OnMediaEnded;
        _backend.MediaFailed -= OnMediaFailed;
        _positionTimer.Tick -= OnPositionTimerTick;
        _positionTimer.Dispose();
        _backend.Dispose();
    }

    private MediaPlaybackOpenOutcome FailureOutcome => _options.RequireNaturalVideoDimensions
        ? MediaPlaybackOpenOutcome.FallbackRequired
        : MediaPlaybackOpenOutcome.Failed;

    private string FailureUserMessage => _options.RequireNaturalVideoDimensions
        ? _options.FallbackMessage
        : _options.FailureMessage;

    private void OnMediaOpened(object? sender, MediaPlaybackOpenedEventArgs e)
    {
        if (_disposed || e.Generation != _generation || _sourcePath is null || _openCompletion is null)
        {
            return;
        }

        int width = _backend.NaturalVideoWidth;
        int height = _backend.NaturalVideoHeight;
        if (_options.RequireNaturalVideoDimensions && (width <= 0 || height <= 0))
        {
            TransitionToFailure(
                MediaPlaybackOpenOutcome.FallbackRequired,
                _options.FallbackMessage,
                $"MediaOpened reported invalid natural video dimensions {width} x {height}.");
            return;
        }

        _duration = _backend.Duration is TimeSpan duration && duration > TimeSpan.Zero
            ? duration
            : TimeSpan.Zero;
        _naturalVideoWidth = Math.Max(0, width);
        _naturalVideoHeight = Math.Max(0, height);
        _backend.Pause();
        _backend.Position = TimeSpan.Zero;
        _isOpening = false;
        _isOpen = true;
        _isPlaying = false;
        _hasEnded = false;
        _fallbackRequired = false;
        _failureMessage = null;
        _technicalDetail = null;
        _positionTimer.Stop();
        _openCompletion.TrySetResult(new MediaPlaybackOpenResult(
            MediaPlaybackOpenOutcome.Ready,
            _options.OpenedMessage,
            null));
        OnStateChanged();
    }

    private void OnMediaEnded(object? sender, MediaPlaybackEndedEventArgs e)
    {
        if (_disposed || e.Generation != _generation || !_isOpen || !_isPlaying)
        {
            return;
        }

        _positionTimer.Stop();
        _backend.Pause();
        _isPlaying = false;
        _hasEnded = true;
        OnStateChanged();
    }

    private void OnMediaFailed(object? sender, MediaPlaybackFailedEventArgs e)
    {
        if (_disposed
            || e.Generation != _generation
            || _sourcePath is null
            || (!_isOpening && !_isOpen))
        {
            return;
        }

        TransitionToFailure(
            FailureOutcome,
            _options.RequireNaturalVideoDimensions
                ? _options.FallbackMessage
                : e.UserMessage ?? _options.FailureMessage,
            e.TechnicalDetail);
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!_disposed && _isOpen)
        {
            OnStateChanged();
        }
    }

    private MediaPlaybackOpenResult TransitionToFailure(
        MediaPlaybackOpenOutcome outcome,
        string message,
        string? technicalDetail)
    {
        _positionTimer.Stop();
        _backend.Close();
        _duration = TimeSpan.Zero;
        _naturalVideoWidth = 0;
        _naturalVideoHeight = 0;
        _isOpening = false;
        _isOpen = false;
        _isPlaying = false;
        _hasEnded = false;
        _fallbackRequired = outcome is MediaPlaybackOpenOutcome.FallbackRequired
            or MediaPlaybackOpenOutcome.Cancelled;
        _failureMessage = message;
        _technicalDetail = technicalDetail;
        MediaPlaybackOpenResult result = new(outcome, message, technicalDetail);
        _openCompletion?.TrySetResult(result);
        OnStateChanged();
        return result;
    }

    private void CloseCore(
        MediaPlaybackOpenOutcome outcome,
        string message,
        bool notify = true)
    {
        _generation++;
        _positionTimer.Stop();
        _backend.Stop();
        _backend.Close();
        _openCompletion?.TrySetResult(new MediaPlaybackOpenResult(outcome, message, null));
        _openCompletion = null;
        _sourcePath = null;
        _duration = TimeSpan.Zero;
        _naturalVideoWidth = 0;
        _naturalVideoHeight = 0;
        _failureMessage = null;
        _technicalDetail = null;
        _isOpening = false;
        _isOpen = false;
        _isPlaying = false;
        _hasEnded = false;
        _fallbackRequired = false;
        if (notify)
        {
            OnStateChanged();
        }
    }

    private TimeSpan ClampPosition(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return _duration > TimeSpan.Zero && position > _duration ? _duration : position;
    }

    private MediaPlaybackOpenResult SupersededResult() => new(
        MediaPlaybackOpenOutcome.Superseded,
        _options.SupersededMessage,
        null);

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
