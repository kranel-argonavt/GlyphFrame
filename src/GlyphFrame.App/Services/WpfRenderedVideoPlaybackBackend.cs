using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Services;

public sealed class WpfRenderedVideoPlaybackBackend : IRenderedVideoPlaybackBackend
{
    private MediaPlayer _player = new();
    private readonly VideoDrawing _videoDrawing;
    private readonly DrawingImage _visualSource;
    private Window? _fullscreenWindow;
    private Window? _owner;
    private bool _disposed;

    public WpfRenderedVideoPlaybackBackend()
    {
        _videoDrawing = new VideoDrawing
        {
            Player = _player,
            Rect = new Rect(0, 0, 16, 9)
        };
        _visualSource = new DrawingImage(_videoDrawing);
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
    }

    public event EventHandler? MediaOpened;

    public event EventHandler? MediaEnded;

    public event EventHandler<RenderedVideoPlaybackFailedEventArgs>? MediaFailed;

    public ImageSource VisualSource => _visualSource;

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Position = value;
    }

    public TimeSpan? Duration => _player.NaturalDuration.HasTimeSpan
        ? _player.NaturalDuration.TimeSpan
        : null;

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

    public void Open(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        double volume = _player.Volume;
        bool muted = _player.IsMuted;
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        _player.Close();
        // A fresh transport isolates queued events from a previous Open/Close generation.
        _player = new MediaPlayer { ScrubbingEnabled = true, Volume = volume, IsMuted = muted };
        _videoDrawing.Player = _player;
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
        _player.Open(new Uri(Path.GetFullPath(filePath), UriKind.Absolute));
        _player.ScrubbingEnabled = true;
    }

    public void Play() => _player.Play();

    public void Pause() => _player.Pause();

    public void Stop() => _player.Stop();

    public void Close()
    {
        CloseFullscreen();
        double volume = _player.Volume;
        bool muted = _player.IsMuted;
        _player.Close();
        _player.Volume = volume;
        _player.IsMuted = muted;
    }

    public void SetOwner(Window? owner)
    {
        if (ReferenceEquals(owner, _owner))
        {
            return;
        }

        CloseFullscreen();
        if (_owner is not null)
        {
            _owner.Closed -= OnOwnerClosed;
        }

        _owner = owner;
        if (_owner is not null)
        {
            _owner.Closed += OnOwnerClosed;
        }
    }

    public void UpdateFitMode(string fitMode)
    {
        if (_fullscreenWindow?.Content is VideoReviewSurface surface)
        {
            surface.Viewport.SetCurrentValue(ReviewViewport.ScaleModeProperty, fitMode);
        }
    }

    public void ShowFullscreen(string fitMode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owner is null || !_owner.IsLoaded)
        {
            return;
        }

        if (_fullscreenWindow is not null)
        {
            _fullscreenWindow.Activate();
            return;
        }

        VideoReviewSurface surface = new() { DataContext = _owner.DataContext, IsFullscreen = true };
        surface.Viewport.SetCurrentValue(ReviewViewport.SourceProperty, _visualSource);
        surface.Viewport.SetCurrentValue(ReviewViewport.ScaleModeProperty, fitMode);
        Window window = new()
        {
            Owner = _owner,
            Background = Brushes.Black,
            Content = surface,
            WindowStyle = WindowStyle.None,
            WindowState = WindowState.Maximized,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true,
            Title = "GlyphFrame rendered video review"
        };
        window.KeyDown += OnFullscreenKeyDown;
        window.Closed += OnFullscreenClosed;
        _fullscreenWindow = window;
        window.Show();
        window.Activate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CloseFullscreen();
        SetOwner(null);
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        _player.Close();
        _disposed = true;
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (_disposed || !ReferenceEquals(sender, _player))
        {
            return;
        }

        if (_player.NaturalVideoWidth > 0 && _player.NaturalVideoHeight > 0)
        {
            _videoDrawing.Rect = new Rect(0, 0, _player.NaturalVideoWidth, _player.NaturalVideoHeight);
        }

        MediaOpened?.Invoke(this, EventArgs.Empty);
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (!_disposed && ReferenceEquals(sender, _player))
        {
            MediaEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        if (!_disposed && ReferenceEquals(sender, _player))
        {
            MediaFailed?.Invoke(this, new RenderedVideoPlaybackFailedEventArgs(
                "GlyphFrame could not play the rendered video.", e.ErrorException?.ToString()));
        }
    }

    private void OnOwnerClosed(object? sender, EventArgs e)
    {
        Close();
        SetOwner(null);
    }

    private void OnFullscreenKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ((Window)sender).Close();
        }
    }

    private void OnFullscreenClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.KeyDown -= OnFullscreenKeyDown;
            window.Closed -= OnFullscreenClosed;
        }

        _fullscreenWindow = null;
    }

    private void CloseFullscreen()
    {
        Window? window = _fullscreenWindow;
        if (window is null)
        {
            return;
        }

        _fullscreenWindow = null;
        window.KeyDown -= OnFullscreenKeyDown;
        window.Closed -= OnFullscreenClosed;
        window.Close();
    }

}

public sealed class DispatcherRenderedVideoPositionTimer : IRenderedVideoPositionTimer
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };
    private bool _disposed;

    public DispatcherRenderedVideoPositionTimer()
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
