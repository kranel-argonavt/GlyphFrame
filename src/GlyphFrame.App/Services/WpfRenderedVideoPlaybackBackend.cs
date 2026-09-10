using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Services;

public sealed class WpfRenderedVideoPlaybackBackend : IRenderedVideoPlaybackBackend
{
    private readonly WpfMediaPlaybackBackend _transport = new();
    private Window? _fullscreenWindow;
    private Window? _owner;
    private bool _disposed;
    private long _generation;

    public WpfRenderedVideoPlaybackBackend()
    {
        _transport.MediaOpened += OnMediaOpened;
        _transport.MediaEnded += OnMediaEnded;
        _transport.MediaFailed += OnMediaFailed;
    }

    public event EventHandler? MediaOpened;

    public event EventHandler? MediaEnded;

    public event EventHandler<RenderedVideoPlaybackFailedEventArgs>? MediaFailed;

    public ImageSource VisualSource => _transport.VisualSource;

    public TimeSpan Position
    {
        get => _transport.Position;
        set => _transport.Position = value;
    }

    public TimeSpan? Duration => _transport.Duration;

    public double Volume
    {
        get => _transport.Volume;
        set => _transport.Volume = value;
    }

    public bool IsMuted
    {
        get => _transport.IsMuted;
        set => _transport.IsMuted = value;
    }

    public void Open(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transport.Open(filePath, ++_generation);
    }

    public void Play() => _transport.Play();

    public void Pause() => _transport.Pause();

    public void Stop() => _transport.Stop();

    public void Close()
    {
        CloseFullscreen();
        _transport.Close();
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
        surface.Viewport.SetCurrentValue(ReviewViewport.SourceProperty, _transport.VisualSource);
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
        _transport.MediaOpened -= OnMediaOpened;
        _transport.MediaEnded -= OnMediaEnded;
        _transport.MediaFailed -= OnMediaFailed;
        _transport.Dispose();
        _disposed = true;
    }

    private void OnMediaOpened(object? sender, MediaPlaybackOpenedEventArgs e)
    {
        if (!_disposed)
        {
            MediaOpened?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMediaEnded(object? sender, MediaPlaybackEndedEventArgs e)
    {
        if (!_disposed)
        {
            MediaEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMediaFailed(object? sender, MediaPlaybackFailedEventArgs e)
    {
        if (!_disposed)
        {
            MediaFailed?.Invoke(this, new RenderedVideoPlaybackFailedEventArgs(
                "GlyphFrame could not play the rendered video.",
                e.TechnicalDetail));
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
    private readonly DispatcherMediaPlaybackPositionTimer _timer = new();

    public event EventHandler? Tick
    {
        add => _timer.Tick += value;
        remove => _timer.Tick -= value;
    }

    public bool IsRunning => _timer.IsRunning;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Dispose() => _timer.Dispose();
}
