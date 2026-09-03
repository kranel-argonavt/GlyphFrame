using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

public sealed class ManagedVideoRenderService(
    IVideoExportService videoExportService,
    IManagedVideoArtifactStore artifactStore) : IManagedVideoRenderService
{
    private readonly IVideoExportService _videoExportService = videoExportService
        ?? throw new ArgumentNullException(nameof(videoExportService));
    private readonly IManagedVideoArtifactStore _artifactStore = artifactStore
        ?? throw new ArgumentNullException(nameof(artifactStore));

    public async Task<ManagedVideoRenderResult> RenderAsync(
        ManagedVideoRenderJob job,
        CancellationToken cancellationToken = default,
        IProgress<VideoExportProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        ManagedVideoArtifactTarget? target = null;
        bool completed = false;
        ManagedArtifactProgress? managedProgress = progress is null
            ? null
            : new ManagedArtifactProgress(progress);
        try
        {
            ValidateJob(job);
        }
        catch (ArgumentException exception)
        {
            return ManagedVideoRenderResult.Failure(
                ManagedVideoArtifactErrorCategory.InvalidJob,
                "The managed video render request is incomplete.",
                exception.Message);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            target = _artifactStore.CreateTarget();
            VideoExportResult export = await _videoExportService.ExportAsync(
                new VideoExportJob(
                    job.InputPath,
                    target.PendingPath,
                    job.Metadata,
                    job.RenderSettings,
                    job.CharacterPreset,
                    Overwrite: false,
                    job.AudioMode),
                cancellationToken,
                managedProgress).ConfigureAwait(false);

            if (export.Status == VideoExportStatus.Cancelled)
            {
                return ManagedVideoRenderResult.Cancelled();
            }

            if (!export.Success
                || export.OutputDimensions is not PixelDimensions dimensions
                || export.FramesPerSecond is not double framesPerSecond
                || !double.IsFinite(framesPerSecond)
                || framesPerSecond <= 0
                || export.AudioOutcome is not AudioMuxOutcome audioOutcome
                || !IsAudioOutcomeValid(job.AudioMode, audioOutcome)
                || export.FramesWritten <= 0)
            {
                return ManagedVideoRenderResult.Failure(
                    ManagedVideoArtifactErrorCategory.RenderFailed,
                    export.UserMessage,
                    export.TechnicalDetail);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _artifactStore.PromoteAsync(target, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo file = new(target.CompletedPath);
            if (!file.Exists || file.Length <= 0)
            {
                return ManagedVideoRenderResult.Failure(
                    ManagedVideoArtifactErrorCategory.ValidationFailed,
                    "GlyphFrame could not validate the completed managed video artifact.");
            }

            TimeSpan duration = TimeSpan.FromSeconds(export.FramesWritten / framesPerSecond);
            ManagedVideoArtifact artifact = new(
                target.ArtifactId,
                target.CompletedPath,
                job.Identity,
                dimensions,
                framesPerSecond,
                export.FramesWritten,
                duration,
                job.AudioMode,
                audioOutcome,
                file.Length);
            completed = true;
            managedProgress?.Complete(artifact);
            return ManagedVideoRenderResult.Completed(artifact);
        }
        catch (OperationCanceledException)
        {
            return ManagedVideoRenderResult.Cancelled();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or OverflowException)
        {
            return ManagedVideoRenderResult.Failure(
                target is null
                    ? ManagedVideoArtifactErrorCategory.CacheUnavailable
                    : ManagedVideoArtifactErrorCategory.FinalizationFailed,
                "GlyphFrame could not create the managed video review artifact.",
                exception.Message);
        }
        finally
        {
            if (!completed && target is not null)
            {
                await _artifactStore.DeleteAsync(target, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static void ValidateJob(ManagedVideoRenderJob job)
    {
        if (string.IsNullOrWhiteSpace(job.InputPath)
            || job.Metadata is null
            || job.RenderSettings is null
            || job.CharacterPreset is null
            || job.Identity is null
            || job.Identity.MediaVersion <= 0
            || job.Identity.SourceIdentityVersion <= 0
            || job.AudioMode != job.Identity.AudioMode
            || job.CharacterPreset.Kind != job.Identity.CharacterPreset
            || !job.CharacterPreset.Characters.Equals(job.Identity.CharacterRamp, StringComparison.Ordinal)
            || !job.RenderSettings.FontFamily.Equals(job.Identity.FontFamily, StringComparison.Ordinal)
            || job.RenderSettings.FontSize != job.Identity.FontSize
            || job.RenderSettings.OutputResolution != job.Identity.OutputResolution
            || job.RenderSettings.SymbolPolarity != job.Identity.SymbolPolarity)
        {
            throw new ArgumentException("The managed video render job is incomplete.", nameof(job));
        }
    }

    private static bool IsAudioOutcomeValid(AudioExportMode mode, AudioMuxOutcome outcome) => mode switch
    {
        AudioExportMode.RemoveAudio => outcome == AudioMuxOutcome.Removed,
        AudioExportMode.KeepAudio => outcome is AudioMuxOutcome.NoSourceAudio
            or AudioMuxOutcome.StreamCopied
            or AudioMuxOutcome.TranscodedToAac,
        _ => false
    };

    private sealed class ManagedArtifactProgress(IProgress<VideoExportProgress> destination)
        : IProgress<VideoExportProgress>
    {
        private readonly IProgress<VideoExportProgress> _destination = destination;
        private VideoExportProgress? _latest;
        private int _completedAttempted;

        public void Report(VideoExportProgress value)
        {
            _latest = value;
            if (value.Stage != VideoExportStage.Completed)
            {
                ReportObserverSafely(value);
            }
        }

        public void Complete(ManagedVideoArtifact artifact)
        {
            if (Interlocked.Exchange(ref _completedAttempted, 1) != 0)
            {
                return;
            }

            VideoExportProgress? latest = _latest;
            ReportObserverSafely(new VideoExportProgress(
                VideoExportStage.Completed,
                artifact.RenderedFrameCount,
                latest?.TotalFrames,
                100d,
                latest?.Elapsed ?? TimeSpan.Zero,
                null,
                false,
                "Managed video artifact completed."));
        }

        private void ReportObserverSafely(VideoExportProgress value)
        {
            try
            {
                _destination.Report(value);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
            {
                // Progress observers are presentation-only and cannot own the artifact lifecycle.
            }
        }
    }
}
