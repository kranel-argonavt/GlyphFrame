using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class ManagedVideoRenderServiceTests
{
    [Theory]
    [InlineData(AudioExportMode.RemoveAudio, AudioMuxOutcome.Removed)]
    [InlineData(AudioExportMode.KeepAudio, AudioMuxOutcome.NoSourceAudio)]
    [InlineData(AudioExportMode.KeepAudio, AudioMuxOutcome.StreamCopied)]
    [InlineData(AudioExportMode.KeepAudio, AudioMuxOutcome.TranscodedToAac)]
    public async Task RenderAsync_CreatesManagedArtifactWithCapturedAudioOutcome(
        AudioExportMode mode,
        AudioMuxOutcome outcome)
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "сесія artifacts");
        FakeVideoExportService exporter = new();
        exporter.Handler = async (job, _, progress) =>
        {
            progress?.Report(new VideoExportProgress(
                VideoExportStage.ProcessingFrames,
                1,
                60,
                1d / 60d * 100d,
                TimeSpan.Zero,
                null,
                false,
                "Processing frames"));
            await File.WriteAllBytesAsync(job.OutputPath, [1, 2, 3, 4]);
            return VideoExportResult.Completed(
                job.OutputPath,
                60,
                new PixelDimensions(320, 240),
                30,
                outcome);
        };
        ManagedVideoRenderService service = new(exporter, store);
        List<VideoExportProgress> progressUpdates = [];
        bool completionObservedAfterPromotion = false;

        ManagedVideoRenderResult result = await service.RenderAsync(
            CreateJob(data, mode),
            progress: new InlineProgress<VideoExportProgress>(update =>
            {
                progressUpdates.Add(update);
                if (update.Stage == VideoExportStage.Completed)
                {
                    completionObservedAfterPromotion = Directory
                        .EnumerateFiles(store.SessionDirectory, "artifact-*.mp4")
                        .Any(path => !path.EndsWith(".pending.mp4", StringComparison.OrdinalIgnoreCase));
                }
            }));

        Assert.True(result.Success);
        Assert.NotNull(result.Artifact);
        Assert.Equal(mode, result.Artifact.AudioMode);
        Assert.Equal(outcome, result.Artifact.AudioOutcome);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Artifact.Duration);
        Assert.Equal(4, result.Artifact.FileSizeBytes);
        Assert.True(store.OwnsPath(result.Artifact.FilePath));
        Assert.EndsWith(".mp4", result.Artifact.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".pending.", result.Artifact.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, progressUpdates.Count);
        Assert.Equal(VideoExportStage.ProcessingFrames, progressUpdates[0].Stage);
        Assert.Equal(VideoExportStage.Completed, progressUpdates[1].Stage);
        Assert.Equal(100d, progressUpdates[1].Percentage);
        Assert.True(File.Exists(result.Artifact.FilePath));
        Assert.True(completionObservedAfterPromotion);
    }

    [Fact]
    public async Task RenderAsync_FailureAndCancellationLeaveNoCandidateArtifacts()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        FakeVideoExportService exporter = new();
        ManagedVideoRenderService service = new(exporter, store);
        exporter.Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [1]);
            return VideoExportResult.Failure(VideoExportErrorCategory.EncoderFailed, "Encoding failed.");
        };

        ManagedVideoRenderResult failed = await service.RenderAsync(
            CreateJob(data, AudioExportMode.RemoveAudio));

        Assert.Equal(ManagedVideoArtifactStatus.Failed, failed.Status);
        Assert.Equal(VideoExportErrorCategory.EncoderFailed, failed.PipelineErrorCategory);
        Assert.Empty(Directory.EnumerateFiles(store.SessionDirectory, "*.mp4"));

        exporter.Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [2]);
            return VideoExportResult.Cancelled();
        };
        ManagedVideoRenderResult cancelled = await service.RenderAsync(
            CreateJob(data, AudioExportMode.RemoveAudio));

        Assert.Equal(ManagedVideoArtifactStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.PipelineErrorCategory);
        Assert.Empty(Directory.EnumerateFiles(store.SessionDirectory, "*.mp4"));
    }

    [Theory]
    [InlineData(VideoExportErrorCategory.DecodeFailed)]
    [InlineData(VideoExportErrorCategory.RenderingFailed)]
    [InlineData(VideoExportErrorCategory.EncoderFailed)]
    [InlineData(VideoExportErrorCategory.AudioProbeFailed)]
    [InlineData(VideoExportErrorCategory.AudioMuxFailed)]
    [InlineData(VideoExportErrorCategory.OutputFinalizationFailed)]
    public async Task RenderAsync_AllStreamingPipelineFailuresCleanCandidate(
        VideoExportErrorCategory exportCategory)
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        FakeVideoExportService exporter = new();
        exporter.Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [1, 2]);
            return VideoExportResult.Failure(exportCategory, "Managed rendering failed.");
        };
        ManagedVideoRenderService service = new(exporter, store);

        ManagedVideoRenderResult result = await service.RenderAsync(
            CreateJob(data, AudioExportMode.KeepAudio));

        Assert.Equal(ManagedVideoArtifactStatus.Failed, result.Status);
        Assert.Equal(ManagedVideoArtifactErrorCategory.RenderFailed, result.ErrorCategory);
        Assert.Equal(exportCategory, result.PipelineErrorCategory);
        Assert.Empty(Directory.EnumerateFiles(store.SessionDirectory, "*.mp4"));
    }

    [Fact]
    public async Task FailedReplacement_DoesNotTouchPreviouslyAcceptedArtifact()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        await using ManagedVideoArtifactSession session = new(store);
        FakeVideoExportService exporter = new();
        ManagedVideoRenderService service = new(exporter, store);
        exporter.Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [1]);
            return VideoExportResult.Completed(
                job.OutputPath,
                1,
                new PixelDimensions(2, 2),
                30,
                AudioMuxOutcome.Removed);
        };
        ManagedVideoRenderResult first = await service.RenderAsync(CreateJob(data, AudioExportMode.RemoveAudio));
        await session.AcceptAsync(first.Artifact!);
        exporter.Handler = (_, _, _) => Task.FromResult(VideoExportResult.Failure(
            VideoExportErrorCategory.DecodeFailed,
            "Decode failed."));

        ManagedVideoRenderResult replacement = await service.RenderAsync(
            CreateJob(data, AudioExportMode.RemoveAudio));

        Assert.False(replacement.Success);
        Assert.Equal(first.Artifact, session.Current);
        Assert.True(File.Exists(first.Artifact!.FilePath));
    }

    [Fact]
    public async Task CancelledReplacement_DoesNotTouchPreviouslyAcceptedArtifact()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        await using ManagedVideoArtifactSession session = new(store);
        FakeVideoExportService exporter = new();
        ManagedVideoRenderService service = new(exporter, store);
        exporter.Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [1]);
            return VideoExportResult.Completed(
                job.OutputPath,
                1,
                new PixelDimensions(2, 2),
                30,
                AudioMuxOutcome.Removed);
        };
        ManagedVideoRenderResult first = await service.RenderAsync(CreateJob(data, AudioExportMode.RemoveAudio));
        await session.AcceptAsync(first.Artifact!);
        exporter.Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [2]);
            return VideoExportResult.Cancelled();
        };

        ManagedVideoRenderResult replacement = await service.RenderAsync(
            CreateJob(data, AudioExportMode.RemoveAudio));

        Assert.Equal(ManagedVideoArtifactStatus.Cancelled, replacement.Status);
        Assert.Equal(first.Artifact, session.Current);
        Assert.True(File.Exists(first.Artifact!.FilePath));
        Assert.Single(Directory.EnumerateFiles(store.SessionDirectory, "*.mp4"));
    }

    [Fact]
    public async Task RenderAsync_UsesUnicodeSourceAndNeverCreatesFrameImages()
    {
        using TemporaryApplicationData data = new();
        string unicodeDirectory = Path.Combine(data.RootPath, "відео 日本語");
        Directory.CreateDirectory(unicodeDirectory);
        string input = Path.Combine(unicodeDirectory, "тестове відео.mov");
        await File.WriteAllBytesAsync(input, [7]);
        using ManagedVideoArtifactStore store = new(data.Paths, "кеш сесія");
        FakeVideoExportService exporter = new();
        exporter.Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [8]);
            return VideoExportResult.Completed(
                job.OutputPath,
                1,
                new PixelDimensions(2, 2),
                30,
                AudioMuxOutcome.Removed);
        };
        ManagedVideoRenderService service = new(exporter, store);
        ManagedVideoRenderJob job = CreateJob(data, AudioExportMode.RemoveAudio) with
        {
            InputPath = input,
            Metadata = CreateMetadata(input)
        };

        ManagedVideoRenderResult result = await service.RenderAsync(job);

        Assert.True(result.Success);
        Assert.Equal(input, exporter.LastJob!.InputPath);
        Assert.Empty(Directory.EnumerateFiles(store.SessionDirectory, "*.png"));
        Assert.Empty(Directory.EnumerateFiles(store.SessionDirectory, "*.raw"));
    }

    [Fact]
    public async Task RenderAsync_RejectsIdentityThatDoesNotMatchCapturedSettings()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        FakeVideoExportService exporter = new();
        ManagedVideoRenderService service = new(exporter, store);
        ManagedVideoRenderJob job = CreateJob(data, AudioExportMode.RemoveAudio);
        job = job with
        {
            Identity = job.Identity with { FontSize = job.Identity.FontSize + 1 }
        };

        ManagedVideoRenderResult result = await service.RenderAsync(job);

        Assert.False(result.Success);
        Assert.Equal(ManagedVideoArtifactErrorCategory.InvalidJob, result.ErrorCategory);
        Assert.Null(exporter.LastJob);
        Assert.Empty(Directory.EnumerateFiles(store.SessionDirectory, "*.mp4"));
    }

    [Fact]
    public async Task RenderAsync_ThrowingOrdinaryProgressObserverDoesNotAbortOrLoseArtifact()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        FakeVideoExportService exporter = SuccessfulExporter();
        exporter.ReportProgress = true;
        ThrowingProgress progress = new(VideoExportStage.ProcessingFrames);
        ManagedVideoRenderService service = new(exporter, store);

        ManagedVideoRenderResult result = await service.RenderAsync(
            CreateJob(data, AudioExportMode.RemoveAudio),
            progress: progress);

        Assert.True(result.Success);
        Assert.True(File.Exists(result.Artifact!.FilePath));
        Assert.Equal(1, progress.Count(VideoExportStage.ProcessingFrames));
        Assert.Equal(1, progress.Count(VideoExportStage.Completed));
    }

    [Fact]
    public async Task RenderAsync_ThrowingCompletedObserverStillReturnsOwnedCandidateAndAttemptsFinalOnce()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        FakeVideoExportService exporter = SuccessfulExporter();
        exporter.ReportProgress = true;
        ThrowingProgress progress = new(VideoExportStage.Completed);
        ManagedVideoRenderService service = new(exporter, store);

        ManagedVideoRenderResult result = await service.RenderAsync(
            CreateJob(data, AudioExportMode.RemoveAudio),
            progress: progress);

        Assert.True(result.Success);
        Assert.True(store.OwnsPath(result.Artifact!.FilePath));
        Assert.True(File.Exists(result.Artifact.FilePath));
        Assert.Equal(1, progress.Count(VideoExportStage.Completed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenderAsync_ThrowingProgressCannotPreventFailureOrCancellationCleanup(bool cancel)
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        FakeVideoExportService exporter = new() { ReportProgress = true };
        exporter.Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [1]);
            return cancel
                ? VideoExportResult.Cancelled()
                : VideoExportResult.Failure(VideoExportErrorCategory.DecodeFailed, "Decode failed.");
        };
        ManagedVideoRenderService service = new(exporter, store);

        ManagedVideoRenderResult result = await service.RenderAsync(
            CreateJob(data, AudioExportMode.RemoveAudio),
            progress: new ThrowingProgress(VideoExportStage.ProcessingFrames));

        Assert.Equal(
            cancel ? ManagedVideoArtifactStatus.Cancelled : ManagedVideoArtifactStatus.Failed,
            result.Status);
        Assert.Empty(Directory.EnumerateFiles(store.SessionDirectory, "*.mp4"));
    }

    private static ManagedVideoRenderJob CreateJob(
        TemporaryApplicationData data,
        AudioExportMode mode)
    {
        Directory.CreateDirectory(data.RootPath);
        string input = Path.Combine(data.RootPath, "source.mp4");
        if (!File.Exists(input))
        {
            File.WriteAllBytes(input, [1]);
        }

        ImageRenderSettings settings = ImageRenderSettings.Default;
        FullVideoRenderIdentity identity = new(
            1,
            1,
            0,
            CharacterPresetOption.Default.Kind,
            CharacterPresetOption.Default.Characters,
            settings.FontFamily,
            settings.FontSize,
            settings.OutputResolution,
            settings.SymbolPolarity,
            mode);
        return new ManagedVideoRenderJob(
            input,
            CreateMetadata(input),
            settings,
            CharacterPresetOption.Default,
            mode,
            identity);
    }

    private static VideoMetadata CreateMetadata(string input) => new(
        input,
        Path.GetFileName(input),
        2,
        2,
        TimeSpan.FromSeconds(2),
        30,
        60,
        "MP4",
        1,
        "h264",
        false,
        null,
        null);

    private static FakeVideoExportService SuccessfulExporter() => new()
    {
        Handler = async (job, _, _) =>
        {
            await File.WriteAllBytesAsync(job.OutputPath, [1, 2, 3]);
            return VideoExportResult.Completed(
                job.OutputPath,
                1,
                new PixelDimensions(2, 2),
                30,
                AudioMuxOutcome.Removed);
        }
    };

    private sealed class FakeVideoExportService : IVideoExportService
    {
        public Func<VideoExportJob, CancellationToken, IProgress<VideoExportProgress>?, Task<VideoExportResult>> Handler { get; set; } =
            (_, _, _) => throw new InvalidOperationException("No export handler was configured.");

        public VideoExportJob? LastJob { get; private set; }

        public bool ReportProgress { get; set; }

        public Task<VideoExportResult> ExportAsync(
            VideoExportJob job,
            CancellationToken cancellationToken = default,
            IProgress<VideoExportProgress>? progress = null)
        {
            LastJob = job;
            if (ReportProgress)
            {
                progress?.Report(new VideoExportProgress(
                    VideoExportStage.ProcessingFrames,
                    1,
                    1,
                    99,
                    TimeSpan.FromSeconds(1),
                    null,
                    false,
                    "Processing frames"));
                progress?.Report(new VideoExportProgress(
                    VideoExportStage.Completed,
                    1,
                    1,
                    100,
                    TimeSpan.FromSeconds(1),
                    null,
                    false,
                    "Direct export completed"));
            }

            return Handler(job, cancellationToken, progress);
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private sealed class ThrowingProgress(VideoExportStage stageToThrow) : IProgress<VideoExportProgress>
    {
        private readonly List<VideoExportStage> _observed = [];

        public int Count(VideoExportStage stage) => _observed.Count(value => value == stage);

        public void Report(VideoExportProgress value)
        {
            _observed.Add(value.Stage);
            if (value.Stage == stageToThrow)
            {
                throw new InvalidOperationException("Controlled observer failure.");
            }
        }
    }
}
