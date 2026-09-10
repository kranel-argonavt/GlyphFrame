using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace GlyphFrame.App.Services;

public sealed class WpfMediaPlaybackBackend : IMediaPlaybackBackend
{
    private MediaPlayer _player = new();
    private readonly VideoDrawing _videoDrawing;
    private readonly DrawingImage _visualSource;
    private long _generation;
    private bool _sourceActive;
    private bool _disposed;

    public WpfMediaPlaybackBackend()
    {
        _videoDrawing = new VideoDrawing
        {
            Player = _player,
            Rect = new Rect(0, 0, 16, 9)
        };
        _visualSource = new DrawingImage(_videoDrawing);
        AttachPlayerEvents(_player);
    }

    public event EventHandler<MediaPlaybackOpenedEventArgs>? MediaOpened;

    public event EventHandler<MediaPlaybackEndedEventArgs>? MediaEnded;

    public event EventHandler<MediaPlaybackFailedEventArgs>? MediaFailed;

    public ImageSource VisualSource => _visualSource;

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Position = value;
    }

    public TimeSpan? Duration => _player.NaturalDuration.HasTimeSpan
        ? _player.NaturalDuration.TimeSpan
        : null;

    public int NaturalVideoWidth => _player.NaturalVideoWidth;

    public int NaturalVideoHeight => _player.NaturalVideoHeight;

    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = value;
    }

    public bool IsMuted
    {
        get => _player.IsMuted;
        set => _player.IsMuted = value;
    }

    public void Open(string filePath, long generation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        double volume = _player.Volume;
        bool muted = _player.IsMuted;
        DetachPlayerEvents(_player);
        _sourceActive = false;
        _player.Close();
        _player = new MediaPlayer { ScrubbingEnabled = true, Volume = volume, IsMuted = muted };
        _videoDrawing.Player = _player;
        _videoDrawing.Rect = new Rect(0, 0, 16, 9);
        _generation = generation;
        _sourceActive = true;
        AttachPlayerEvents(_player);
        try
        {
            _player.Open(new Uri(Path.GetFullPath(filePath), UriKind.Absolute));
            _player.ScrubbingEnabled = true;
        }
        catch
        {
            _sourceActive = false;
            throw;
        }
    }

    public void Play() => _player.Play();

    public void Pause() => _player.Pause();

    public void Stop() => _player.Stop();

    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        _sourceActive = false;
        double volume = _player.Volume;
        bool muted = _player.IsMuted;
        _player.Close();
        _player.Volume = volume;
        _player.IsMuted = muted;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _sourceActive = false;
        DetachPlayerEvents(_player);
        _player.Close();
        _disposed = true;
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (!IsCurrentActivePlayer(sender))
        {
            return;
        }

        if (_player.NaturalVideoWidth > 0 && _player.NaturalVideoHeight > 0)
        {
            _videoDrawing.Rect = new Rect(0, 0, _player.NaturalVideoWidth, _player.NaturalVideoHeight);
        }

        MediaOpened?.Invoke(this, new MediaPlaybackOpenedEventArgs(_generation));
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (IsCurrentActivePlayer(sender))
        {
            MediaEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(_generation));
        }
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        if (IsCurrentActivePlayer(sender))
        {
            MediaFailed?.Invoke(this, new MediaPlaybackFailedEventArgs(
                _generation,
                technicalDetail: e.ErrorException?.ToString()));
        }
    }

    private bool IsCurrentActivePlayer(object? sender) =>
        !_disposed && _sourceActive && ReferenceEquals(sender, _player);

    private void AttachPlayerEvents(MediaPlayer player)
    {
        player.MediaOpened += OnMediaOpened;
        player.MediaEnded += OnMediaEnded;
        player.MediaFailed += OnMediaFailed;
    }

    private void DetachPlayerEvents(MediaPlayer player)
    {
        player.MediaOpened -= OnMediaOpened;
        player.MediaEnded -= OnMediaEnded;
        player.MediaFailed -= OnMediaFailed;
    }
}

public sealed class DispatcherMediaPlaybackPositionTimer : IMediaPlaybackPositionTimer
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };
    private bool _disposed;

    public DispatcherMediaPlaybackPositionTimer()
    {
        _timer.Tick += OnTick;
    }

    public event EventHandler? Tick;

    public bool IsRunning => _timer.IsEnabled;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _disposed = true;
    }

    private void OnTick(object? sender, EventArgs e) => Tick?.Invoke(this, EventArgs.Empty);
}
