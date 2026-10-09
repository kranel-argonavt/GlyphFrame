using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

public sealed class PlaybackPositionSynchronizationTests
{
    [Theory]
    [InlineData(VideoPlaybackSurfaceKind.Original)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered)]
    public void CompletedSeek_RecreatedFullscreenUsesTransportSnapshot_NotDelayedNativeReadback(VideoPlaybackSurfaceKind role)
    {
        RunSta(() =>
        {
            using Players players = new();
            PlayerModel model = new(players);
            Window owner = WindowFor(model);
            VideoReviewSurface embedded = new() { DataContext = model, PlaybackKind = role };
            owner.Content = embedded;
            using WpfVideoPlaybackFullscreenHost host = new(role);
            try
            {
                owner.Show();
                host.SetOwner(owner);
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    players.Play(role);
                    players.Backend(role).NativePosition = TimeSpan.FromSeconds(20);
                    players.Backend(role).End();
                    Assert.True(players.Ended(role));
                    players.Seek(role, TimeSpan.FromSeconds(9));
                    // The native frame may have sought while Position readback still reports end/zero.
                    players.Backend(role).NativePosition = TimeSpan.Zero;
                    Settle(owner);
                    int seeks = players.Backend(role).Seeks.Count;
                    host.Show("Fit");
                    Window fullscreen = Assert.Single(owner.OwnedWindows.Cast<Window>());
                    Settle(fullscreen);
                    AssertSurface(embedded, 9, false);
                    AssertSurface(Assert.IsType<VideoReviewSurface>(fullscreen.Content), 9, false);
                    Assert.Equal(seeks, players.Backend(role).Seeks.Count);
                    Assert.False(players.Playing(role));
                    Assert.False(players.Timer(role).IsRunning);
                    host.Close();
                    Settle(owner);
                    AssertSurface(embedded, 9, false);
                    Assert.Equal(seeks, players.Backend(role).Seeks.Count);
                }
                players.Seek(role, TimeSpan.FromSeconds(20));
                AssertSurface(embedded, 20, false); // Paused endpoint seek is not natural completion.
                players.Restart(role);
                Assert.Equal(TimeSpan.Zero, players.Position(role));
                Assert.True(players.Playing(role));
            }
            finally { host.Close(); owner.Close(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TabComparison_PausesBeforeCapture_AndRetainsRequestedSeekDespiteStaleReadback(bool playing)
    {
        RunSta(() =>
        {
            using Players players = new();
            VideoPlaybackCoordinator coordinator = new(players.Original, players.Rendered);
            players.Original.SetVolume(0.2);
            players.Rendered.SetVolume(0.8);
            players.Original.ToggleMute();
            players.Original.Seek(TimeSpan.FromSeconds(7));
            if (playing)
            {
                coordinator.PlayOriginal();
                players.OriginalBackend.NativePosition = TimeSpan.FromSeconds(15.246);
                players.OriginalTimer.TickNow();
            }
            double expected = playing ? 15.246 : 7;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                PlaybackTimestampTransfer toRendered = coordinator.PrepareRenderedSwitch(TimeSpan.FromSeconds(20), 30);
                Assert.False(players.Original.IsPlaying);
                Assert.False(players.Original.IsTimerRunning);
                coordinator.ApplyRenderedTransfer(toRendered);
                Assert.Equal(expected, players.Rendered.Position.TotalSeconds, 3);
                // Native target still reads zero. An idempotent Pause must not overwrite the seek.
                PlaybackTimestampTransfer toOriginal = coordinator.PrepareOriginalSwitch(TimeSpan.FromSeconds(20), 30);
                Assert.Equal(expected, toOriginal.RequestedTimestamp.TotalSeconds, 3);
                coordinator.ApplyOriginalTransfer(toOriginal);
                Assert.Equal(expected, players.Original.Position.TotalSeconds, 3);
                Assert.False(players.Original.IsPlaying);
                Assert.False(players.Rendered.IsPlaying);
                Assert.False(players.Rendered.IsTimerRunning);
            }
            Assert.Equal(0.2, players.Original.Volume);
            Assert.Equal(0.8, players.Rendered.Volume);
            Assert.True(players.Original.IsMuted);
            Assert.False(players.Rendered.IsMuted);
            Assert.All(players.RenderedBackend.Seeks.Skip(1), seek => Assert.Equal(expected, seek.TotalSeconds, 3));
        });
    }

    [Theory]
    [InlineData(VideoPlaybackSurfaceKind.Original)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered)]
    public void SeekReadback_MultipleSurfacesRoleChangeAndDrag_DoNotPublishInitializationSeeks(VideoPlaybackSurfaceKind role)
    {
        RunSta(() =>
        {
            using Players players = new();
            players.Original.Seek(TimeSpan.FromSeconds(9));
            players.Rendered.Seek(TimeSpan.FromSeconds(12));
            PlayerModel model = new(players);
            VideoReviewSurface first = new() { DataContext = model, PlaybackKind = role };
            VideoReviewSurface second = new() { DataContext = model, PlaybackKind = role };
            Window window = WindowFor(model);
            window.Content = new StackPanel { Children = { first, second } };
            try
            {
                window.Show();
                Settle(window);
                double initial = role == VideoPlaybackSurfaceKind.Original ? 9 : 12;
                AssertSurface(first, initial, false);
                AssertSurface(second, initial, false);
                int originalSeeks = players.OriginalBackend.Seeks.Count;
                int renderedSeeks = players.RenderedBackend.Seeks.Count;
                first.PlaybackKind = role == VideoPlaybackSurfaceKind.Original
                    ? VideoPlaybackSurfaceKind.Rendered : VideoPlaybackSurfaceKind.Original;
                Settle(window);
                Assert.Equal(originalSeeks, players.OriginalBackend.Seeks.Count);
                Assert.Equal(renderedSeeks, players.RenderedBackend.Seeks.Count);
                AssertSurface(first, role == VideoPlaybackSurfaceKind.Original ? 12 : 9, false);
                ReviewSeekSlider slider = Slider(second);
                slider.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                slider.Value = 14;
                players.Seek(role, TimeSpan.FromSeconds(10));
                Assert.Equal(14, slider.Value);
                slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                Settle(window);
                Assert.Equal(14, players.Position(role).TotalSeconds);
                AssertSurface(second, 14, false);
                Assert.NotNull(BindingOperations.GetBindingExpression(slider, ReviewSeekSlider.PositionProperty));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PlayingClockAndPause_CaptureNativeProgress_WhilePausedReadsCannotResetSeek()
    {
        RunSta(() =>
        {
            using Players players = new();
            players.Original.Play();
            players.OriginalBackend.NativePosition = TimeSpan.FromSeconds(7);
            players.OriginalTimer.TickNow();
            Assert.Equal(7, players.Original.Position.TotalSeconds);
            players.OriginalBackend.NativePosition = TimeSpan.FromSeconds(8);
            players.Original.Pause();
            Assert.Equal(8, players.Original.Position.TotalSeconds);
            players.Original.Seek(TimeSpan.FromSeconds(10));
            players.OriginalBackend.NativePosition = TimeSpan.Zero;
            players.OriginalTimer.TickNow(); // Queued tick after Stop must not read stale position.
            players.Original.Pause();
            players.Original.SetVolume(0.3);
            Assert.Equal(10, players.Original.Position.TotalSeconds);
            Assert.False(players.Original.IsTimerRunning);
            players.Original.Close();
            Assert.Equal(TimeSpan.Zero, players.Original.Position);
        });
    }

    private static Window WindowFor(object model) => new()
    {
        DataContext = model,
        Width = 750,
        Height = 450,
        Left = -10000,
        Top = -10000,
        WindowStartupLocation = WindowStartupLocation.Manual,
        ShowActivated = false,
        ShowInTaskbar = false
    };

    private static ReviewSeekSlider Slider(VideoReviewSurface surface) =>
        Assert.Single(Assert.Single(((Grid)surface.Content).Children.OfType<Grid>(), row => Grid.GetRow(row) == 1)
            .Children.OfType<ReviewSeekSlider>());

    private static void AssertSurface(VideoReviewSurface surface, double seconds, bool replay)
    {
        surface.UpdateLayout();
        Assert.Equal(seconds, surface.PositionSeconds);
        Assert.Equal(seconds, Slider(surface).Value);
        Assert.Equal(replay, surface.ShowReplay);
        Assert.False(surface.IsPlaying);
        Assert.False(BindingOperations.GetBindingExpression(Slider(surface), ReviewSeekSlider.PositionProperty)!.HasError);
    }

    private static void Settle(Window window)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Playback binding test did not finish.");
        failure?.Throw();
    }

    private sealed class Players : IDisposable
    {
        public DelayedBackend OriginalBackend { get; } = new();
        public DelayedBackend RenderedBackend { get; } = new();
        public ManualTimer OriginalTimer { get; } = new();
        public ManualTimer RenderedTimer { get; } = new();
        public OriginalVideoPlayer Original { get; }
        public RenderedVideoPlayer Rendered { get; }
        public Players()
        {
            Original = new(OriginalBackend, OriginalTimer);
            Rendered = new(RenderedBackend, RenderedTimer);
            Task<OriginalVideoOpenResult> original = Original.OpenAsync("C:\\media\\original.mp4");
            OriginalBackend.Opened();
            Assert.True(original.GetAwaiter().GetResult().IsReady);
            Task<RenderedVideoOpenResult> rendered = Rendered.OpenAsync("C:\\cache\\rendered.mp4");
            RenderedBackend.Opened();
            Assert.True(rendered.GetAwaiter().GetResult().Success);
        }
        public DelayedBackend Backend(VideoPlaybackSurfaceKind role) => role == VideoPlaybackSurfaceKind.Original ? OriginalBackend : RenderedBackend;
        public ManualTimer Timer(VideoPlaybackSurfaceKind role) => role == VideoPlaybackSurfaceKind.Original ? OriginalTimer : RenderedTimer;
        public TimeSpan Position(VideoPlaybackSurfaceKind role) => role == VideoPlaybackSurfaceKind.Original ? Original.Position : Rendered.Position;
        public bool Playing(VideoPlaybackSurfaceKind role) => role == VideoPlaybackSurfaceKind.Original ? Original.IsPlaying : Rendered.IsPlaying;
        public bool Ended(VideoPlaybackSurfaceKind role) => role == VideoPlaybackSurfaceKind.Original ? Original.HasEnded : Rendered.HasEnded;
        public void Seek(VideoPlaybackSurfaceKind role, TimeSpan position) { if (role == VideoPlaybackSurfaceKind.Original) Original.Seek(position); else Rendered.Seek(position); }
        public void Play(VideoPlaybackSurfaceKind role) { if (role == VideoPlaybackSurfaceKind.Original) Original.Play(); else Rendered.Play(); }
        public void Restart(VideoPlaybackSurfaceKind role) { if (role == VideoPlaybackSurfaceKind.Original) Original.Restart(); else Rendered.Restart(); }
        public void Dispose() { Original.Dispose(); Rendered.Dispose(); }
    }

    private sealed class DelayedBackend : IMediaPlaybackBackend, IRenderedVideoPlaybackBackend
    {
        public event EventHandler<MediaPlaybackOpenedEventArgs>? MediaOpened;
        public event EventHandler<MediaPlaybackEndedEventArgs>? MediaEnded;
        public event EventHandler<MediaPlaybackFailedEventArgs>? MediaFailed;
        private event EventHandler? RenderedOpened;
        private event EventHandler? RenderedEnded;
        private event EventHandler<RenderedVideoPlaybackFailedEventArgs>? RenderedFailed;
        event EventHandler? IRenderedVideoPlaybackBackend.MediaOpened { add => RenderedOpened += value; remove => RenderedOpened -= value; }
        event EventHandler? IRenderedVideoPlaybackBackend.MediaEnded { add => RenderedEnded += value; remove => RenderedEnded -= value; }
        event EventHandler<RenderedVideoPlaybackFailedEventArgs>? IRenderedVideoPlaybackBackend.MediaFailed { add => RenderedFailed += value; remove => RenderedFailed -= value; }
        private long _generation;
        public ImageSource VisualSource { get; } = new DrawingImage();
        public TimeSpan NativePosition { get; set; }
        public List<TimeSpan> Seeks { get; } = [];
        public TimeSpan Position { get => NativePosition; set => Seeks.Add(value); }
        public TimeSpan? Duration => TimeSpan.FromSeconds(20);
        public int NaturalVideoWidth => 320;
        public int NaturalVideoHeight => 180;
        public double Volume { get; set; } = 0.75;
        public bool IsMuted { get; set; }
        public void Open(string path, long generation) => _generation = generation;
        public void Open(string path) { }
        public void Opened() { MediaOpened?.Invoke(this, new(_generation)); RenderedOpened?.Invoke(this, EventArgs.Empty); }
        public void End() { MediaEnded?.Invoke(this, new(_generation)); RenderedEnded?.Invoke(this, EventArgs.Empty); }
        public void Play() { }
        public void Pause() { }
        public void Stop() { }
        public void Close() { }
        public void ShowFullscreen(string fitMode) { }
        public void Dispose() { _ = MediaFailed; _ = RenderedFailed; MediaFailed = null; RenderedFailed = null; }
    }

    private sealed class ManualTimer : IRenderedVideoPositionTimer
    {
        public event EventHandler? Tick;
        public bool IsRunning { get; private set; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void TickNow() => Tick?.Invoke(this, EventArgs.Empty);
        public void Dispose() => Stop();
    }

    private sealed class PlayerModel : INotifyPropertyChanged
    {
        private readonly Players _players;
        public PlayerModel(Players players)
        {
            _players = players;
            players.Original.StateChanged += (_, _) => PropertyChanged?.Invoke(this, new(null));
            players.Rendered.StateChanged += (_, _) => PropertyChanged?.Invoke(this, new(null));
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public double OriginalVideoPositionSeconds { get => _players.Original.Position.TotalSeconds; set => _players.Original.Seek(TimeSpan.FromSeconds(value)); }
        public double RenderedVideoPositionSeconds { get => _players.Rendered.Position.TotalSeconds; set => _players.Rendered.Seek(TimeSpan.FromSeconds(value)); }
        public double OriginalVideoDurationSeconds => 20;
        public double RenderedVideoDurationSeconds => 20;
        public bool ShowOriginalVideoReplay => _players.Original.HasEnded;
        public bool ShowRenderedVideoReplay => _players.Rendered.HasEnded;
        public bool IsOriginalVideoPlaying => _players.Original.IsPlaying;
        public bool IsRenderedVideoPlaying => _players.Rendered.IsPlaying;
        public bool AreExportAnimationsEnabled => false;
    }
}
