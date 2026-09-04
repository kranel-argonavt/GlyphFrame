using System.Windows.Media;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class RenderedVideoPlayerTests
{
    [Fact]
    public async Task OpenPlayPauseAndTimer_FollowExplicitPlaybackLifecycle()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = new(backend, timer, TimeSpan.FromSeconds(1));

        Task<RenderedVideoOpenResult> opening = player.OpenAsync("C:\\cache\\review.mp4");
        backend.Duration = TimeSpan.FromSeconds(12);
        backend.RaiseOpened();
        RenderedVideoOpenResult result = await opening;

        Assert.True(result.Success);
        Assert.True(player.IsOpen);
        Assert.Equal(TimeSpan.FromSeconds(12), player.Duration);
        Assert.False(timer.IsRunning);

        player.Play();
        Assert.True(player.IsPlaying);
        Assert.True(timer.IsRunning);
        Assert.Equal(1, backend.PlayCalls);

        player.Pause();
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);
        Assert.Equal(1, backend.PauseCallsAfterOpen);
    }

    [Fact]
    public async Task EndedPlayback_RestartsFromBeginningWhenPlayIsRequested()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = await OpenPlayerAsync(backend, timer, TimeSpan.FromSeconds(5));
        player.Play();

        backend.Position = TimeSpan.FromSeconds(4.98); // Final decoded frame, not a seek past it.
        backend.RaiseEnded();

        Assert.True(player.HasEnded);
        Assert.False(player.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(4.98), player.Position);
        Assert.False(timer.IsRunning);

        player.Play();

        Assert.False(player.HasEnded);
        Assert.True(player.IsPlaying);
        Assert.Equal(TimeSpan.Zero, backend.Position);
    }

    [Fact]
    public async Task PausedSeeking_DoesNotShowReplay_EvenIfBackendReportsEnd()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = await OpenPlayerAsync(backend, timer, TimeSpan.FromSeconds(8));
        player.Play();
        player.Pause();
        Assert.False(player.HasEnded);
        player.Seek(TimeSpan.FromSeconds(8));
        backend.RaiseEnded();
        Assert.False(player.HasEnded);
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public async Task SeekingBackwardAfterNaturalEnd_ClearsReplayAndKeepsTransportPaused()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = await OpenPlayerAsync(backend, timer, TimeSpan.FromSeconds(8));
        player.Play();
        backend.Position = TimeSpan.FromSeconds(8);
        backend.RaiseEnded();
        Assert.True(player.HasEnded);
        Assert.Equal(1, backend.PauseCallsAfterOpen);
        player.Seek(TimeSpan.FromSeconds(3));
        Assert.False(player.HasEnded);
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(3), player.Position);
        player.Play();
        Assert.Equal(TimeSpan.FromSeconds(3), player.Position); // Resume, not restart.
    }

    [Theory]
    [InlineData("replay")]
    [InlineData("replace")]
    [InlineData("close")]
    [InlineData("failure")]
    public async Task ReplayOrNewMediaLifecycle_ClearsEndedPresentation(string action)
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = await OpenPlayerAsync(backend, timer, TimeSpan.FromSeconds(8));
        player.Play();
        backend.RaiseEnded();
        Assert.True(player.HasEnded);
        switch (action)
        {
            case "replay":
                player.Restart();
                Assert.True(player.IsPlaying);
                Assert.Equal(TimeSpan.Zero, player.Position);
                break;
            case "replace":
                Task<RenderedVideoOpenResult> opening = player.OpenAsync("C:\\cache\\next.mp4");
                Assert.False(player.HasEnded);
                backend.RaiseOpened();
                Assert.True((await opening).Success);
                break;
            case "close": player.Close(); break;
            case "failure": backend.RaiseFailed("Cannot play", "test failure"); break;
        }
        Assert.False(player.HasEnded);
    }

    [Fact]
    public async Task SeekVolumeMuteAndFullscreen_AreDelegatedAndClamped()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = await OpenPlayerAsync(backend, timer, TimeSpan.FromSeconds(8));

        player.Seek(TimeSpan.FromSeconds(20));
        player.SetVolume(2d);
        player.ToggleMute();
        player.ShowFullscreen("Fit");

        Assert.Equal(TimeSpan.FromSeconds(8), backend.Position);
        Assert.Equal(1d, backend.Volume);
        Assert.True(backend.IsMuted);
        Assert.Equal("Fit", backend.LastFullscreenFitMode);
    }

    [Fact]
    public async Task OpeningReplacement_PreservesVolumeAndMutePreferences()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = await OpenPlayerAsync(backend, timer, TimeSpan.FromSeconds(8));
        player.SetVolume(0.35d);
        player.ToggleMute();
        backend.ResetAudioOnClose = true;

        Task<RenderedVideoOpenResult> replacement = player.OpenAsync("C:\\cache\\replacement.mp4");
        backend.Duration = TimeSpan.FromSeconds(6);
        backend.RaiseOpened();
        Assert.True((await replacement).Success);

        Assert.Equal(0.35d, player.Volume, 3);
        Assert.True(player.IsMuted);
    }

    [Fact]
    public async Task MediaFailure_StopsTimerAndExposesFriendlyAndTechnicalDetails()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = await OpenPlayerAsync(backend, timer, TimeSpan.FromSeconds(3));
        player.Play();

        backend.RaiseFailed("Rendered video cannot be played.", "native codec detail");

        Assert.False(player.IsOpen);
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);
        Assert.Equal("Rendered video cannot be played.", player.FailureMessage);
        Assert.Equal("native codec detail", player.TechnicalDetail);
        Assert.True(backend.CloseCalls > 0);
    }

    [Fact]
    public async Task CloseAndDispose_StopTimerAndReleaseBackendExactlyOnce()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        RenderedVideoPlayer player = await OpenPlayerAsync(backend, timer, TimeSpan.FromSeconds(4));
        player.Play();

        player.Close();
        player.Dispose();
        player.Dispose();

        Assert.False(timer.IsRunning);
        Assert.Equal(1, timer.DisposeCalls);
        Assert.Equal(1, backend.DisposeCalls);
        Assert.True(backend.StopCalls >= 1);
        Assert.True(backend.CloseCalls >= 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedOrDisposedOpening_IgnoresLateEvents(bool dispose)
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = new(backend, timer);
        Task<RenderedVideoOpenResult> opening = player.OpenAsync("C:\\cache\\late.mp4");
        if (dispose)
        {
            player.Dispose();
        }
        else
        {
            player.Close();
        }
        backend.RaiseOpened();
        backend.RaiseFailed("Late failure", "old event");
        Assert.False((await opening).Success);
        Assert.False(player.IsOpen);
        Assert.False(timer.IsRunning);
        Assert.Null(player.SourcePath);
        Assert.Null(player.FailureMessage);
    }

    [Fact]
    public async Task SupersededOpeningCancellation_CannotCloseNewerReview()
    {
        FakePlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using RenderedVideoPlayer player = new(backend, timer);
        using CancellationTokenSource cancellation = new();
        Task<RenderedVideoOpenResult> old = player.OpenAsync("C:\\cache\\old.mp4", cancellation.Token);
        Task<RenderedVideoOpenResult> current = player.OpenAsync("C:\\cache\\current.mp4");
        cancellation.Cancel();
        backend.Duration = TimeSpan.FromSeconds(8);
        backend.RaiseOpened();
        Assert.True((await current).Success);
        try
        {
            Assert.False((await old).Success);
        }
        catch (OperationCanceledException)
        {
            Assert.True(cancellation.IsCancellationRequested);
        }
        Assert.True(player.IsOpen);
        Assert.Equal("C:\\cache\\current.mp4", player.SourcePath);
        player.Seek(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(3), player.Position);
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);
    }

    private static async Task<RenderedVideoPlayer> OpenPlayerAsync(
        FakePlaybackBackend backend,
        FakePositionTimer timer,
        TimeSpan duration)
    {
        RenderedVideoPlayer player = new(backend, timer, TimeSpan.FromSeconds(1));
        Task<RenderedVideoOpenResult> opening = player.OpenAsync("C:\\cache\\video.mp4");
        backend.Duration = duration;
        backend.RaiseOpened();
        Assert.True((await opening).Success);
        return player;
    }

    private sealed class FakePlaybackBackend : IRenderedVideoPlaybackBackend
    {
        public event EventHandler? MediaOpened;

        public event EventHandler? MediaEnded;

        public event EventHandler<RenderedVideoPlaybackFailedEventArgs>? MediaFailed;

        public ImageSource VisualSource { get; } = new DrawingImage();

        public TimeSpan Position { get; set; }

        public TimeSpan? Duration { get; set; }

        public double Volume { get; set; } = 0.75d;

        public bool IsMuted { get; set; }

        public int PlayCalls { get; private set; }

        public int PauseCalls { get; private set; }

        public int PauseCallsAfterOpen => Math.Max(0, PauseCalls - 1);

        public int StopCalls { get; private set; }

        public int CloseCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public string? LastFullscreenFitMode { get; private set; }

        public bool ResetAudioOnClose { get; set; }

        public void Open(string filePath)
        {
        }

        public void Play() => PlayCalls++;

        public void Pause() => PauseCalls++;

        public void Stop() => StopCalls++;

        public void Close()
        {
            CloseCalls++;
            if (ResetAudioOnClose)
            {
                Volume = 0.75d;
                IsMuted = false;
            }
        }

        public void ShowFullscreen(string fitMode) => LastFullscreenFitMode = fitMode;

        public void Dispose() => DisposeCalls++;

        public void RaiseOpened() => MediaOpened?.Invoke(this, EventArgs.Empty);

        public void RaiseEnded() => MediaEnded?.Invoke(this, EventArgs.Empty);

        public void RaiseFailed(string message, string? detail) => MediaFailed?.Invoke(
            this,
            new RenderedVideoPlaybackFailedEventArgs(message, detail));
    }

    private sealed class FakePositionTimer : IRenderedVideoPositionTimer
    {
        public event EventHandler? Tick;

        public bool IsRunning { get; private set; }

        public int DisposeCalls { get; private set; }

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Dispose()
        {
            IsRunning = false;
            DisposeCalls++;
        }

        public void RaiseTick() => Tick?.Invoke(this, EventArgs.Empty);
    }
}
