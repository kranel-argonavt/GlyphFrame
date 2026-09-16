using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Services;

public enum VideoPlaybackSurfaceKind
{
    Rendered,
    Original
}

public interface IVideoPlaybackFullscreenHost : IDisposable
{
    void SetOwner(Window? owner);

    void Show(string fitMode);

    void UpdateFitMode(string fitMode);

    void Close();
}

public sealed class WpfVideoPlaybackFullscreenHost(VideoPlaybackSurfaceKind surfaceKind)
    : IVideoPlaybackFullscreenHost
{
    private readonly VideoPlaybackSurfaceKind _surfaceKind = surfaceKind;
    private Window? _owner;
    private Window? _window;
    private bool _disposed;

    public void SetOwner(Window? owner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(owner, _owner))
        {
            return;
        }

        Close();
        _owner = owner;
    }

    public void Show(string fitMode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owner is null || !_owner.IsLoaded)
        {
            return;
        }

        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        VideoReviewSurface surface = new()
        {
            DataContext = _owner.DataContext,
            IsFullscreen = true,
            PlaybackKind = _surfaceKind
        };
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
            Title = _surfaceKind == VideoPlaybackSurfaceKind.Original
                ? "GlyphFrame original video review"
                : "GlyphFrame rendered video review"
        };
        window.KeyDown += OnKeyDown;
        window.Closed += OnClosed;
        _window = window;
        window.Show();
        window.Activate();
    }

    public void UpdateFitMode(string fitMode)
    {
        if (_window?.Content is VideoReviewSurface surface)
        {
            surface.Viewport.SetCurrentValue(ReviewViewport.ScaleModeProperty, fitMode);
        }
    }

    public void Close()
    {
        Window? window = _window;
        if (window is null)
        {
            return;
        }

        _window = null;
        window.KeyDown -= OnKeyDown;
        window.Closed -= OnClosed;
        window.Close();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        _owner = null;
        _disposed = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ((Window)sender).Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.KeyDown -= OnKeyDown;
            window.Closed -= OnClosed;
        }

        _window = null;
    }
}

internal sealed class NullVideoPlaybackFullscreenHost : IVideoPlaybackFullscreenHost
{
    public void SetOwner(Window? owner) { }

    public void Show(string fitMode) { }

    public void UpdateFitMode(string fitMode) { }

    public void Close() { }

    public void Dispose() { }
}
