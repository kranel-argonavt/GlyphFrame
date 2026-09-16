using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GlyphFrame.App.Tests;

public sealed class WpfRenderedVideoLifetimeTests
{
    [Fact]
    public void RenderedAndOriginalWpfTransports_AreDistinctAndDisposeIndependently()
    {
        RunSta(() =>
        {
            WpfRenderedVideoPlaybackBackend rendered = new();
            WpfMediaPlaybackBackend original = new();
            try
            {
                VideoDrawing renderedDrawing = Assert.IsType<VideoDrawing>(
                    Assert.IsType<DrawingImage>(rendered.VisualSource).Drawing);
                VideoDrawing originalDrawing = Assert.IsType<VideoDrawing>(
                    Assert.IsType<DrawingImage>(original.VisualSource).Drawing);
                Assert.NotSame(rendered.VisualSource, original.VisualSource);
                Assert.NotSame(renderedDrawing.Player, originalDrawing.Player);

                string renderedPath = Path.Combine(Path.GetTempPath(), $"renderedFill-{Guid.NewGuid():N}.mp4");
                string originalPath = Path.Combine(Path.GetTempPath(), $"original-{Guid.NewGuid():N}.mp4");
                rendered.Open(renderedPath);
                original.Open(originalPath, 42);
                Assert.NotSame(renderedDrawing.Player, originalDrawing.Player);

                original.Volume = 0.2d;
                original.IsMuted = true;
                Assert.NotEqual(original.Volume, rendered.Volume);
                Assert.NotEqual(original.IsMuted, rendered.IsMuted);

                original.Dispose();
                rendered.Volume = 0.6d;
                Assert.Equal(0.6d, rendered.Volume, 3);
                rendered.Close();
            }
            finally
            {
                original.Dispose();
                rendered.Dispose();
            }
        });
    }

    [Fact]
    public void Fullscreen_IsOwned_EscapePreservesBackend_AndOwnerCloseClosesChild()
    {
        RunSta(() =>
        {
            using WpfRenderedVideoPlaybackBackend backend = new();
            SharedReviewModel sharedContext = new(backend.VisualSource);
            Window owner = new() { Width = 100, Height = 100, ShowInTaskbar = false, DataContext = sharedContext };
            try
            {
                owner.Show();
                owner.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                backend.SetOwner(owner);
                backend.ShowFullscreen("Fit");
                Window fullscreen = Assert.Single(owner.OwnedWindows.Cast<Window>());
                Assert.Same(owner, fullscreen.Owner);
                VideoReviewSurface surface = Assert.IsType<VideoReviewSurface>(fullscreen.Content);
                ReviewViewport viewport = Assert.IsType<ReviewViewport>(surface.FindName("Viewport"));
                Assert.Same(sharedContext, surface.DataContext);
                Assert.True(surface.IsFullscreen);
                Assert.Equal("Exit fullscreen", surface.FullscreenActionLabel);
                Assert.Same(backend.VisualSource, viewport.Source);
                Button play = Assert.IsType<Button>(surface.FindName("PlayButton"));
                Assert.Same(sharedContext.ToggleRenderedVideoPlaybackCommand, play.Command);
                play.Command.Execute(null);
                Assert.True(sharedContext.IsRenderedVideoPlaying);
                sharedContext.ShowRenderedVideoReplay = true;
                Assert.True(surface.ShowReplay);
                backend.UpdateFitMode("Fill");
                Assert.Equal("Fill", viewport.ScaleMode);
                backend.Position = TimeSpan.FromSeconds(3);
                fullscreen.RaiseEvent(new KeyEventArgs(
                    Keyboard.PrimaryDevice, PresentationSource.FromVisual(fullscreen), 0, Key.Escape)
                { RoutedEvent = Keyboard.KeyDownEvent });
                Assert.Empty(owner.OwnedWindows.Cast<Window>());
                Assert.Equal(TimeSpan.FromSeconds(3), backend.Position);
                backend.ShowFullscreen("100%");
                Window second = Assert.Single(owner.OwnedWindows.Cast<Window>());
                VideoReviewSurface secondSurface = Assert.IsType<VideoReviewSurface>(second.Content);
                Assert.True(secondSurface.ShowReplay);
                // Explicit exit uses the same view button, not a second transport's Stop.
                Button exit = Assert.IsType<Button>(secondSurface.FindName("FullscreenButton"));
                exit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Empty(owner.OwnedWindows.Cast<Window>());
                Assert.Equal(TimeSpan.FromSeconds(3), backend.Position);
                backend.ShowFullscreen("Fit");
                second = Assert.Single(owner.OwnedWindows.Cast<Window>());
                owner.Close();
                Assert.False(second.IsVisible);
                backend.Close();
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Fact]
    public void OriginalAndRenderedFullscreenHosts_AreIndependentAndUseTheirOwnCommands()
    {
        RunSta(() =>
        {
            using WpfMediaPlaybackBackend originalBackend = new();
            using WpfRenderedVideoPlaybackBackend renderedBackend = new();
            using WpfVideoPlaybackFullscreenHost originalHost = new(VideoPlaybackSurfaceKind.Original);
            using WpfVideoPlaybackFullscreenHost renderedHost = new(VideoPlaybackSurfaceKind.Rendered);
            SharedReviewModel model = new(renderedBackend.VisualSource, originalBackend.VisualSource);
            Window owner = new() { Width = 100, Height = 100, ShowInTaskbar = false, DataContext = model };
            try
            {
                owner.Show();
                owner.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                originalHost.SetOwner(owner);
                renderedHost.SetOwner(owner);
                originalHost.Show("Fill");
                renderedHost.Show("100%");

                Window[] windows = owner.OwnedWindows.Cast<Window>().ToArray();
                Assert.Equal(2, windows.Length);
                Window originalWindow = Assert.Single(windows, window => window.Title.Contains("original", StringComparison.OrdinalIgnoreCase));
                Window renderedWindow = Assert.Single(windows, window => window.Title.Contains("rendered", StringComparison.OrdinalIgnoreCase));
                VideoReviewSurface originalSurface = Assert.IsType<VideoReviewSurface>(originalWindow.Content);
                VideoReviewSurface renderedSurface = Assert.IsType<VideoReviewSurface>(renderedWindow.Content);
                Assert.Equal(VideoPlaybackSurfaceKind.Original, originalSurface.PlaybackKind);
                Assert.Equal(VideoPlaybackSurfaceKind.Rendered, renderedSurface.PlaybackKind);
                Assert.Same(model.ToggleOriginalVideoPlaybackCommand, Assert.IsType<Button>(originalSurface.FindName("PlayButton")).Command);
                Assert.Same(model.ToggleRenderedVideoPlaybackCommand, Assert.IsType<Button>(renderedSurface.FindName("PlayButton")).Command);

                originalWindow.RaiseEvent(new KeyEventArgs(
                    Keyboard.PrimaryDevice, PresentationSource.FromVisual(originalWindow), 0, Key.Escape)
                { RoutedEvent = Keyboard.KeyDownEvent });

                Assert.False(originalWindow.IsVisible);
                Assert.True(renderedWindow.IsVisible);
                owner.Close();
                Assert.False(renderedWindow.IsVisible);
            }
            finally
            {
                owner.Close();
            }
        });
    }

    private sealed class SharedReviewModel : ObservableObject
    {
        private bool _playing;
        private bool _replay;
        public SharedReviewModel(ImageSource renderedSource, ImageSource? originalSource = null)
        {
            RenderedVideoPlaybackSource = renderedSource;
            OriginalVideoPlaybackSource = originalSource ?? renderedSource;
            ToggleRenderedVideoPlaybackCommand = new RelayCommand(() => IsRenderedVideoPlaying = !IsRenderedVideoPlaying);
            ToggleOriginalVideoPlaybackCommand = new RelayCommand(() => IsOriginalVideoPlaying = !IsOriginalVideoPlaying);
        }
        public ImageSource RenderedVideoPlaybackSource { get; }
        public ImageSource OriginalVideoPlaybackSource { get; }
        public string SelectedFitMode { get; set; } = "Fit";
        public double RenderedVideoPixelWidth => 1920;
        public double RenderedVideoPixelHeight => 1080;
        public double OriginalVideoPixelWidth => 1080;
        public double OriginalVideoPixelHeight => 1920;
        public double OriginalVideoRotationDegrees => 90;
        public RelayCommand ToggleRenderedVideoPlaybackCommand { get; }
        public RelayCommand ToggleOriginalVideoPlaybackCommand { get; }
        public bool IsRenderedVideoPlaying { get => _playing; set => SetProperty(ref _playing, value); }
        public bool IsOriginalVideoPlaying { get; set; }
        public bool ShowRenderedVideoReplay { get => _replay; set => SetProperty(ref _replay, value); }
    }

    [Fact]
    public void EveryOpen_RecreatesTransportWithScrubbingAndPreservedAudioPreferences()
    {
        RunSta(() =>
        {
            using WpfRenderedVideoPlaybackBackend backend = new();
            backend.Volume = 0.3;
            backend.IsMuted = true;
            // Opening an absent local URI is sufficient to inspect transport configuration;
            // no codec or real frame assertion is claimed by this lifecycle test.
            string path = Path.Combine(Path.GetTempPath(), $"glyphframe-{Guid.NewGuid():N}.mp4");
            backend.Open(path);
            VideoDrawing drawing = Assert.IsType<VideoDrawing>(((DrawingImage)backend.VisualSource).Drawing);
            MediaPlayer first = drawing.Player;
            Assert.True(first.ScrubbingEnabled);
            backend.Close();
            backend.Open(path);
            MediaPlayer second = drawing.Player;
            Assert.NotSame(first, second);
            Assert.True(second.ScrubbingEnabled);
            Assert.Equal(0.3, second.Volume, 3);
            Assert.True(second.IsMuted);
            backend.Close();
        });
    }

    private static void RunSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "WPF lifecycle test did not finish.");
        failure?.Throw();
    }
}
