using System.Windows;
using System.Windows.Media;

namespace GlyphFrame.App.Services;

public sealed class WpfRenderedVideoPlaybackBackend : IRenderedVideoPlaybackBackend
{
    private readonly WpfMediaPlaybackBackend _transport = new();
    private readonly IVideoPlaybackFullscreenHost _fullscreenHost =
        new WpfVideoPlaybackFullscreenHost(VideoPlaybackSurfaceKind.Rendered);
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
        _fullscreenHost.Close();
        _transport.Close();
    }

    public void SetOwner(Window? owner)
    {
        _fullscreenHost.SetOwner(owner);
    }

    public void UpdateFitMode(string fitMode)
    {
        _fullscreenHost.UpdateFitMode(fitMode);
    }

    public void ShowFullscreen(string fitMode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _fullscreenHost.Show(fitMode);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _fullscreenHost.Close();
        SetOwner(null);
        _transport.MediaOpened -= OnMediaOpened;
        _transport.MediaEnded -= OnMediaEnded;
        _transport.MediaFailed -= OnMediaFailed;
        _transport.Dispose();
        _fullscreenHost.Dispose();
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
