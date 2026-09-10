using System.IO;
using System.Windows.Media;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class OriginalVideoPlayerTests
{
    [Fact]
    public async Task Open_RequiresMediaOpenedAndPositiveNaturalDimensions()
    {
        FakeMediaPlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using OriginalVideoPlayer player = CreatePlayer(backend, timer);

        Task<OriginalVideoOpenResult> opening = player.OpenAsync("C:\\media\\source.mp4");
        Assert.True(player.IsOpening);
        Assert.False(player.IsPlaybackReady);
        backend.RaiseOpened(backend.CurrentGeneration, 1920, 1080, TimeSpan.FromSeconds(12));
        OriginalVideoOpenResult result = await opening;

        Assert.True(result.IsReady);
        Assert.False(result.FallbackRequired);
        Assert.True(player.IsPlaybackReady);
        Assert.False(player.IsOpening);
        Assert.Equal(1920, player.NaturalVideoWidth);
        Assert.Equal(1080, player.NaturalVideoHeight);
        Assert.Equal(TimeSpan.FromSeconds(12), player.Duration);
        Assert.Equal(TimeSpan.Zero, player.Position);
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);
        Assert.Null(player.FailureMessage);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(0, 0)]
    public async Task MediaOpenedWithoutPositiveDimensions_RequiresFallback(int width, int height)
    {
        FakeMediaPlaybackBackend backend = new();
        using OriginalVideoPlayer player = CreatePlayer(backend);
        Task<OriginalVideoOpenResult> opening = player.OpenAsync("C:\\media\\unsupported.mkv");

        backend.RaiseOpened(backend.CurrentGeneration, width, height, TimeSpan.FromSeconds(5));
        OriginalVideoOpenResult result = await opening;

        Assert.Equal(OriginalVideoOpenOutcome.FallbackRequired, result.Outcome);
        Assert.True(player.FallbackRequired);
        Assert.False(player.IsPlaybackReady);
        Assert.Equal(OriginalVideoPlayer.PlaybackUnavailableMessage, player.FailureMessage);
        Assert.Contains($"{width} x {height}", player.TechnicalDetail);
        Assert.True(backend.CloseCalls > 0);
    }

    [Fact]
    public async Task MediaFailureBeforeReadiness_RequiresFallbackWithSeparateDetail()
    {
        FakeMediaPlaybackBackend backend = new();
        using OriginalVideoPlayer player = CreatePlayer(backend);
        Task<OriginalVideoOpenResult> opening = player.OpenAsync("C:\\media\\source.avi");

        backend.RaiseFailed(backend.CurrentGeneration, "native codec failure");
        OriginalVideoOpenResult result = await opening;

        Assert.Equal(OriginalVideoOpenOutcome.FallbackRequired, result.Outcome);
        Assert.Equal(OriginalVideoPlayer.PlaybackUnavailableMessage, result.UserMessage);
        Assert.Equal("native codec failure", result.TechnicalDetail);
        Assert.True(player.FallbackRequired);
        Assert.False(player.IsPlaybackReady);
    }

    [Fact]
    public async Task PlaybackFailureAfterReadiness_TransitionsToFallbackWithoutInvalidatingSource()
    {
        FakeMediaPlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using OriginalVideoPlayer player = await OpenPlayerAsync(backend, timer, "C:\\media\\source.mov");
        player.Play();

        backend.RaiseFailed(backend.CurrentGeneration, "decoder stopped after opening");

        Assert.True(player.FallbackRequired);
        Assert.False(player.IsPlaybackReady);
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);
        Assert.Equal("C:\\media\\source.mov", player.SourcePath);
        Assert.Equal(OriginalVideoPlayer.PlaybackUnavailableMessage, player.FailureMessage);
        Assert.Equal("decoder stopped after opening", player.TechnicalDetail);
    }

    [Fact]
    public async Task Timeout_IsDeterministicAndDelayedOpenedCannotReverseFallback()
    {
        FakeMediaPlaybackBackend backend = new();
        ControlledOpenDeadline deadline = new();
        using OriginalVideoPlayer player = CreatePlayer(backend, deadline: deadline);
        Task<OriginalVideoOpenResult> opening = player.OpenAsync("C:\\media\\slow.mp4");
        await deadline.WaitUntilObservedAsync();

        deadline.TriggerTimeout("controlled timeout");
        OriginalVideoOpenResult result = await opening;
        backend.RaiseOpened(backend.CurrentGeneration, 640, 360, TimeSpan.FromSeconds(3));
        backend.RaiseFailed(backend.CurrentGeneration, "late failure after timeout");

        Assert.Equal(OriginalVideoOpenOutcome.FallbackRequired, result.Outcome);
        Assert.True(player.FallbackRequired);
        Assert.False(player.IsPlaybackReady);
        Assert.Equal("controlled timeout", player.TechnicalDetail);
    }

    [Fact]
    public async Task Cancellation_RequiresFallbackAndRejectsDelayedSuccess()
    {
        FakeMediaPlaybackBackend backend = new();
        using OriginalVideoPlayer player = CreatePlayer(backend);
        using CancellationTokenSource cancellation = new();
        Task<OriginalVideoOpenResult> opening = player.OpenAsync("C:\\media\\cancel.mp4", cancellation.Token);
        long generation = backend.CurrentGeneration;

        cancellation.Cancel();
        OriginalVideoOpenResult result = await opening;
        backend.RaiseOpened(generation, 640, 360, TimeSpan.FromSeconds(3));

        Assert.Equal(OriginalVideoOpenOutcome.Cancelled, result.Outcome);
        Assert.True(player.FallbackRequired);
        Assert.False(player.IsPlaybackReady);
        Assert.False(player.IsOpening);
        Assert.True(backend.CloseCalls > 0);
    }

    [Fact]
    public async Task AlreadyCancelledOpening_DoesNotStartTransportAndRequiresFallback()
    {
        FakeMediaPlaybackBackend backend = new();
        using OriginalVideoPlayer player = CreatePlayer(backend);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OriginalVideoOpenResult result = await player.OpenAsync(
            "C:\\media\\cancelled-before-open.mp4",
            cancellation.Token);

        Assert.Equal(OriginalVideoOpenOutcome.Cancelled, result.Outcome);
        Assert.Empty(backend.OpenedPaths);
        Assert.True(player.FallbackRequired);
    }

    [Fact]
    public async Task SupersededOpen_CannotPublishStateIntoCurrentGeneration()
    {
        FakeMediaPlaybackBackend backend = new();
        using OriginalVideoPlayer player = CreatePlayer(backend);
        Task<OriginalVideoOpenResult> oldOpening = player.OpenAsync("C:\\media\\old.mp4");
        long oldGeneration = backend.CurrentGeneration;
        Task<OriginalVideoOpenResult> currentOpening = player.OpenAsync("C:\\media\\current.mp4");
        long currentGeneration = backend.CurrentGeneration;

        backend.RaiseOpened(oldGeneration, 320, 180, TimeSpan.FromSeconds(2));
        backend.RaiseFailed(oldGeneration, "obsolete failure");
        Assert.True(player.IsOpening);
        Assert.False(player.IsPlaybackReady);
        backend.RaiseOpened(currentGeneration, 1280, 720, TimeSpan.FromSeconds(7));

        OriginalVideoOpenResult oldResult = await oldOpening;
        OriginalVideoOpenResult currentResult = await currentOpening;
        Assert.Equal(OriginalVideoOpenOutcome.Superseded, oldResult.Outcome);
        Assert.True(currentResult.IsReady);
        Assert.Equal("C:\\media\\current.mp4", player.SourcePath);
        Assert.Equal(1280, player.NaturalVideoWidth);
        Assert.Equal(TimeSpan.FromSeconds(7), player.Duration);
    }

    [Fact]
    public async Task CloseAndDispose_IgnoreDelayedCallbacksAndAreIdempotent()
    {
        FakeMediaPlaybackBackend backend = new();
        FakePositionTimer timer = new();
        OriginalVideoPlayer player = CreatePlayer(backend, timer);
        Task<OriginalVideoOpenResult> opening = player.OpenAsync("C:\\media\\closing.mp4");
        long generation = backend.CurrentGeneration;

        player.Close();
        backend.RaiseOpened(generation, 640, 360, TimeSpan.FromSeconds(4));
        backend.RaiseFailed(generation, "late failure");
        Assert.Equal(OriginalVideoOpenOutcome.Superseded, (await opening).Outcome);
        Assert.Null(player.SourcePath);
        Assert.False(player.FallbackRequired);

        player.Dispose();
        player.Dispose();
        backend.RaiseOpened(generation, 640, 360, TimeSpan.FromSeconds(4));
        Assert.Equal(1, backend.DisposeCalls);
        Assert.Equal(1, timer.DisposeCalls);
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public async Task SynchronousOpenFailure_RequiresFallbackWithoutEscapingRawException()
    {
        FakeMediaPlaybackBackend backend = new()
        {
            OpenException = new IOException("controlled open failure")
        };
        using OriginalVideoPlayer player = CreatePlayer(backend);

        OriginalVideoOpenResult result = await player.OpenAsync("C:\\media\\locked.mp4");

        Assert.Equal(OriginalVideoOpenOutcome.FallbackRequired, result.Outcome);
        Assert.Equal(OriginalVideoPlayer.PlaybackUnavailableMessage, result.UserMessage);
        Assert.Equal("controlled open failure", result.TechnicalDetail);
        Assert.True(player.FallbackRequired);
    }

    [Fact]
    public async Task PlaybackControlsClampStateAndManageTimer()
    {
        FakeMediaPlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using OriginalVideoPlayer player = await OpenPlayerAsync(backend, timer);

        player.Seek(TimeSpan.FromSeconds(-2));
        Assert.Equal(TimeSpan.Zero, backend.Position);
        player.Seek(TimeSpan.FromSeconds(99));
        Assert.Equal(TimeSpan.FromSeconds(8), backend.Position);
        player.SetVolume(double.PositiveInfinity);
        Assert.Equal(0.75d, backend.Volume, 3);
        player.SetVolume(1.4d);
        Assert.Equal(1d, backend.Volume);
        player.ToggleMute();
        Assert.True(player.IsMuted);

        player.Play();
        Assert.True(player.IsPlaying);
        Assert.True(timer.IsRunning);
        backend.Position = TimeSpan.FromSeconds(7.9);
        backend.RaiseEnded(backend.CurrentGeneration);
        Assert.True(player.HasEnded);
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);

        player.Play();
        Assert.Equal(TimeSpan.Zero, backend.Position);
        Assert.True(player.IsPlaying);
        player.Pause();
        Assert.False(player.IsPlaying);
        Assert.False(timer.IsRunning);
        player.Restart();
        Assert.True(player.IsPlaying);
        Assert.Equal(TimeSpan.Zero, backend.Position);
    }

    [Fact]
    public async Task UnicodeSourcePath_IsPassedToTransportUnchanged()
    {
        const string path = "C:\\Медіа Files\\日本語 відео.mov";
        FakeMediaPlaybackBackend backend = new();
        using OriginalVideoPlayer player = CreatePlayer(backend);
        Task<OriginalVideoOpenResult> opening = player.OpenAsync(path);
        backend.RaiseOpened(backend.CurrentGeneration, 640, 360, TimeSpan.FromSeconds(2));

        Assert.True((await opening).IsReady);
        Assert.Equal(path, Assert.Single(backend.OpenedPaths).Path);
        Assert.Equal(path, player.SourcePath);
    }

    [Fact]
    public async Task ReplacementOpen_PreservesVolumeAndMutePreferences()
    {
        FakeMediaPlaybackBackend backend = new();
        FakePositionTimer timer = new();
        using OriginalVideoPlayer player = await OpenPlayerAsync(backend, timer);
        player.SetVolume(0.35d);
        player.ToggleMute();
        backend.ResetAudioOnClose = true;

        Task<OriginalVideoOpenResult> replacement = player.OpenAsync("C:\\media\\replacement.mp4");
        backend.RaiseOpened(backend.CurrentGeneration, 640, 360, TimeSpan.FromSeconds(4));
        Assert.True((await replacement).IsReady);

        Assert.Equal(0.35d, player.Volume, 3);
        Assert.True(player.IsMuted);
    }

    [Fact]
    public async Task OriginalAndRenderedPlayers_HaveIndependentBackendsTimersAndEvents()
    {
        FakeMediaPlaybackBackend originalBackend = new();
        FakePositionTimer originalTimer = new();
        FakeRenderedBackend renderedBackend = new();
        FakeRenderedTimer renderedTimer = new();
        OriginalVideoPlayer original = CreatePlayer(originalBackend, originalTimer);
        RenderedVideoPlayer rendered = new(renderedBackend, renderedTimer, TimeSpan.FromSeconds(1));
        int originalChanges = 0;
        int renderedChanges = 0;
        original.StateChanged += (_, _) => originalChanges++;
        rendered.StateChanged += (_, _) => renderedChanges++;
        try
        {
            Task<OriginalVideoOpenResult> openingOriginal = original.OpenAsync("C:\\media\\original.mp4");
            originalBackend.RaiseOpened(
                originalBackend.CurrentGeneration,
                640,
                360,
                TimeSpan.FromSeconds(8));
            Assert.True((await openingOriginal).IsReady);

            Task<RenderedVideoOpenResult> openingRendered = rendered.OpenAsync("C:\\cache\\symbolized.mp4");
            renderedBackend.Duration = TimeSpan.FromSeconds(6);
            renderedBackend.RaiseOpened();
            Assert.True((await openingRendered).Success);

            int renderedChangesBeforeOriginalActions = renderedChanges;
            original.SetVolume(0.2d);
            original.ToggleMute();
            original.Play();
            Assert.Equal(0.75d, rendered.Volume, 3);
            Assert.False(rendered.IsMuted);
            Assert.False(rendered.IsPlaying);
            Assert.Equal(renderedChangesBeforeOriginalActions, renderedChanges);

            originalBackend.RaiseFailed(originalBackend.CurrentGeneration, "original failure");
            Assert.True(original.FallbackRequired);
            Assert.True(rendered.IsOpen);
            Assert.Equal("C:\\cache\\symbolized.mp4", rendered.SourcePath);
            Assert.Equal(renderedChangesBeforeOriginalActions, renderedChanges);
            Assert.True(originalChanges > 0);

            int originalChangesBeforeRenderedFailure = originalChanges;
            renderedBackend.RaiseFailed("rendered failure", "independent rendered detail");
            Assert.Equal(originalChangesBeforeRenderedFailure, originalChanges);
            Assert.True(original.FallbackRequired);
            Assert.False(rendered.IsOpen);
            Assert.Equal("rendered failure", rendered.FailureMessage);

            original.Dispose();
            Assert.Equal(1, originalBackend.DisposeCalls);
            Assert.Equal(0, renderedBackend.DisposeCalls);
            Assert.Equal("rendered failure", rendered.FailureMessage);
        }
        finally
        {
            original.Dispose();
            rendered.Dispose();
        }

        Assert.Equal(1, renderedBackend.DisposeCalls);
        Assert.Equal(1, originalTimer.DisposeCalls);
        Assert.Equal(1, renderedTimer.DisposeCalls);
    }

    private static OriginalVideoPlayer CreatePlayer(
        FakeMediaPlaybackBackend backend,
        FakePositionTimer? timer = null,
        ControlledOpenDeadline? deadline = null) => new(
            backend,
            timer ?? new FakePositionTimer(),
            deadline ?? new ControlledOpenDeadline(passThrough: true),
            TimeSpan.FromSeconds(1));

    private static async Task<OriginalVideoPlayer> OpenPlayerAsync(
        FakeMediaPlaybackBackend backend,
        FakePositionTimer timer,
        string path = "C:\\media\\source.mp4")
    {
        OriginalVideoPlayer player = CreatePlayer(backend, timer);
        Task<OriginalVideoOpenResult> opening = player.OpenAsync(path);
        backend.RaiseOpened(backend.CurrentGeneration, 1280, 720, TimeSpan.FromSeconds(8));
        Assert.True((await opening).IsReady);
        return player;
    }

    private sealed class FakeMediaPlaybackBackend : IMediaPlaybackBackend
    {
        public event EventHandler<MediaPlaybackOpenedEventArgs>? MediaOpened;

        public event EventHandler<MediaPlaybackEndedEventArgs>? MediaEnded;

        public event EventHandler<MediaPlaybackFailedEventArgs>? MediaFailed;

        public ImageSource VisualSource { get; } = new DrawingImage();

        public TimeSpan Position { get; set; }

        public TimeSpan? Duration { get; private set; }

        public int NaturalVideoWidth { get; private set; }

        public int NaturalVideoHeight { get; private set; }

        public double Volume { get; set; } = 0.75d;

        public bool IsMuted { get; set; }

        public long CurrentGeneration { get; private set; }

        public List<(string Path, long Generation)> OpenedPaths { get; } = [];

        public Exception? OpenException { get; set; }

        public int PlayCalls { get; private set; }

        public int PauseCalls { get; private set; }

        public int StopCalls { get; private set; }

        public int CloseCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public bool ResetAudioOnClose { get; set; }

        public void Open(string filePath, long generation)
        {
            CurrentGeneration = generation;
            OpenedPaths.Add((filePath, generation));
            if (OpenException is not null)
            {
                throw OpenException;
            }
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

        public void Dispose() => DisposeCalls++;

        public void RaiseOpened(long generation, int width, int height, TimeSpan duration)
        {
            NaturalVideoWidth = width;
            NaturalVideoHeight = height;
            Duration = duration;
            MediaOpened?.Invoke(this, new MediaPlaybackOpenedEventArgs(generation));
        }

        public void RaiseEnded(long generation) =>
            MediaEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(generation));

        public void RaiseFailed(long generation, string detail) =>
            MediaFailed?.Invoke(this, new MediaPlaybackFailedEventArgs(
                generation,
                technicalDetail: detail));
    }

    private sealed class FakePositionTimer : IMediaPlaybackPositionTimer
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

    private sealed class ControlledOpenDeadline(bool passThrough = false) : IMediaPlaybackOpenDeadline
    {
        private readonly TaskCompletionSource _observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _timeout = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitAsync(
            Task operation,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _observed.TrySetResult();
            if (passThrough)
            {
                await operation.WaitAsync(cancellationToken);
                return;
            }

            Task completed = await Task.WhenAny(operation, _timeout.Task);
            await completed;
        }

        public Task WaitUntilObservedAsync() => _observed.Task;

        public void TriggerTimeout(string message) =>
            _timeout.TrySetException(new TimeoutException(message));
    }

    private sealed class FakeRenderedBackend : IRenderedVideoPlaybackBackend
    {
        public event EventHandler? MediaOpened;

        public event EventHandler? MediaEnded;

        public event EventHandler<RenderedVideoPlaybackFailedEventArgs>? MediaFailed;

        public ImageSource VisualSource { get; } = new DrawingImage();

        public TimeSpan Position { get; set; }

        public TimeSpan? Duration { get; set; }

        public double Volume { get; set; } = 0.75d;

        public bool IsMuted { get; set; }

        public int DisposeCalls { get; private set; }

        public void Open(string filePath)
        {
        }

        public void Play()
        {
        }

        public void Pause()
        {
        }

        public void Stop()
        {
        }

        public void Close()
        {
        }

        public void ShowFullscreen(string fitMode)
        {
        }

        public void Dispose() => DisposeCalls++;

        public void RaiseOpened() => MediaOpened?.Invoke(this, EventArgs.Empty);

        public void RaiseEnded() => MediaEnded?.Invoke(this, EventArgs.Empty);

        public void RaiseFailed(string message, string? detail) => MediaFailed?.Invoke(
            this,
            new RenderedVideoPlaybackFailedEventArgs(message, detail));
    }

    private sealed class FakeRenderedTimer : IRenderedVideoPositionTimer
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
