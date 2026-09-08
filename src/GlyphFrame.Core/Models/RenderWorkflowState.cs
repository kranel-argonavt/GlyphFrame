using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

/// <summary>
/// Coordinates platform-neutral media render, review, and export transitions.
/// Image render identity is deliberately separate from video-frame preview identity so
/// a managed full-video artifact remains independent from the currently previewed frame.
/// </summary>
public sealed record RenderWorkflowState
{
    private RenderWorkflowState(
        MediaSelectionKind mediaType,
        long mediaVersion,
        long sourceVersion,
        long settingsVersion,
        PreviewIdentity? imageRenderIdentity,
        PreviewIdentity? videoFramePreviewIdentity,
        FullVideoRenderIdentity? videoArtifactIdentity,
        FullVideoRenderIdentity? activeVideoRenderIdentity,
        AudioExportMode videoAudioMode,
        long activeOperationId,
        RenderWorkflowOperation activeOperation,
        RenderWorkflowOperation outcomeOperation,
        RenderWorkflowStatus status,
        bool hasOriginal)
    {
        MediaType = mediaType;
        MediaVersion = mediaVersion;
        SourceVersion = sourceVersion;
        SettingsVersion = settingsVersion;
        ImageRenderIdentity = imageRenderIdentity;
        VideoFramePreviewIdentity = videoFramePreviewIdentity;
        VideoArtifactIdentity = videoArtifactIdentity;
        ActiveVideoRenderIdentity = activeVideoRenderIdentity;
        VideoAudioMode = videoAudioMode;
        ActiveOperationId = activeOperationId;
        ActiveOperation = activeOperation;
        OutcomeOperation = outcomeOperation;
        Status = status;
        HasOriginal = hasOriginal;
    }

    public static RenderWorkflowState Empty { get; } = new(
        MediaSelectionKind.None,
        0,
        0,
        0,
        null,
        null,
        null,
        null,
        AudioExportMode.KeepAudio,
        0,
        RenderWorkflowOperation.None,
        RenderWorkflowOperation.None,
        RenderWorkflowStatus.Empty,
        false);

    public MediaSelectionKind MediaType { get; private init; }

    public long MediaVersion { get; private init; }

    public long SourceVersion { get; private init; }

    public long SettingsVersion { get; private init; }

    public PreviewIdentity? ImageRenderIdentity { get; private init; }

    public PreviewIdentity? VideoFramePreviewIdentity { get; private init; }

    public FullVideoRenderIdentity? VideoArtifactIdentity { get; private init; }

    public FullVideoRenderIdentity? ActiveVideoRenderIdentity { get; private init; }

    public AudioExportMode VideoAudioMode { get; private init; }

    public PreviewIdentity? SymbolizedPreviewIdentity => MediaType == MediaSelectionKind.Image
        ? ImageRenderIdentity
        : VideoFramePreviewIdentity;

    public long? PreviewSettingsVersion => SymbolizedPreviewIdentity?.SettingsVersion;

    public long ActiveOperationId { get; private init; }

    public RenderWorkflowOperation ActiveOperation { get; private init; }

    /// <summary>
    /// Identifies the operation represented by a durable completed, failed, or cancelled status.
    /// Active processing continues to use <see cref="ActiveOperation"/>.
    /// </summary>
    public RenderWorkflowOperation OutcomeOperation { get; private init; }

    public RenderWorkflowStatus Status { get; private init; }

    public bool HasOriginal { get; private init; }

    public bool HasImageRender => ImageRenderIdentity is not null;

    public bool HasVideoFramePreview => VideoFramePreviewIdentity is not null;

    public bool HasVideoArtifact => VideoArtifactIdentity is not null;

    public bool HasSymbolizedPreview => SymbolizedPreviewIdentity is not null;

    public PreviewIdentity? CurrentIdentity => HasOriginal
        ? new PreviewIdentity(MediaType, MediaVersion, SourceVersion, SettingsVersion)
        : null;

    public bool IsImageRenderStale => HasImageRender && ImageRenderIdentity != CurrentIdentity;

    public bool IsVideoFramePreviewStale => HasVideoFramePreview && VideoFramePreviewIdentity != CurrentIdentity;

    public bool IsVideoArtifactStale => VideoArtifactIdentity is not null
        && (VideoArtifactIdentity.MediaVersion != MediaVersion
            || VideoArtifactIdentity.SourceIdentityVersion != MediaVersion
            || VideoArtifactIdentity.RenderSettingsVersion != SettingsVersion
            || VideoArtifactIdentity.AudioMode != VideoAudioMode);

    public bool IsPreviewStale => MediaType == MediaSelectionKind.Image
        ? IsImageRenderStale
        : IsVideoFramePreviewStale;

    public bool CanRenderImage => MediaType == MediaSelectionKind.Image
        && HasOriginal
        && ActiveOperation == RenderWorkflowOperation.None;

    public bool CanPreviewVideoFrame => MediaType == MediaSelectionKind.Video
        && HasOriginal
        && ActiveOperation == RenderWorkflowOperation.None;

    public bool CanRenderVideo => MediaType == MediaSelectionKind.Video
        && ActiveOperation == RenderWorkflowOperation.None;

    public bool CanExportImage => MediaType == MediaSelectionKind.Image
        && HasOriginal
        && HasImageRender
        && !IsImageRenderStale
        && ActiveOperation == RenderWorkflowOperation.None;

    public bool CanExportVideo => MediaType == MediaSelectionKind.Video
        && HasVideoArtifact
        && !IsVideoArtifactStale
        && ActiveOperation == RenderWorkflowOperation.None;

    public RenderWorkflowState SelectMedia(MediaSelectionKind mediaType, long mediaVersion, bool hasOriginal)
    {
        if (mediaVersion <= MediaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaVersion), "Media versions must increase.");
        }

        bool validOriginal = mediaType is MediaSelectionKind.Image or MediaSelectionKind.Video && hasOriginal;
        return new(
            mediaType,
            mediaVersion,
            validOriginal ? 1 : 0,
            SettingsVersion,
            null,
            null,
            null,
            null,
            AudioExportMode.KeepAudio,
            0,
            RenderWorkflowOperation.None,
            RenderWorkflowOperation.None,
            mediaType == MediaSelectionKind.None ? RenderWorkflowStatus.Empty : RenderWorkflowStatus.MediaReady,
            validOriginal);
    }

    public RenderWorkflowState SourceChanged()
    {
        if (MediaType == MediaSelectionKind.None)
        {
            return this;
        }

        return this with
        {
            SourceVersion = checked(SourceVersion + 1),
            Status = MediaType switch
            {
                MediaSelectionKind.Image when HasImageRender => RenderWorkflowStatus.RenderStale,
                MediaSelectionKind.Video when HasVideoArtifact => IsVideoArtifactStale
                    ? RenderWorkflowStatus.RenderStale
                    : RenderWorkflowStatus.RenderReady,
                _ => RenderWorkflowStatus.MediaReady
            }
        };
    }

    public RenderWorkflowState OriginalAvailable(long mediaVersion)
    {
        if (mediaVersion != MediaVersion || MediaType == MediaSelectionKind.None)
        {
            return this;
        }

        return this with
        {
            SourceVersion = SourceVersion == 0 ? 1 : SourceVersion,
            HasOriginal = true,
            Status = MediaType == MediaSelectionKind.Video
                ? ResolveVideoReviewStatus()
                : Status
        };
    }

    public RenderWorkflowState RenderingSettingChanged(ImageRenderSetting setting)
    {
        if (!Enum.IsDefined(setting))
        {
            throw new ArgumentOutOfRangeException(nameof(setting));
        }

        return this with
        {
            SettingsVersion = checked(SettingsVersion + 1),
            Status = MediaType switch
            {
                MediaSelectionKind.Image when HasImageRender => RenderWorkflowStatus.RenderStale,
                MediaSelectionKind.Video when HasVideoArtifact => RenderWorkflowStatus.RenderStale,
                _ => Status
            }
        };
    }

    public RenderWorkflowState VideoAudioModeChanged(AudioExportMode audioMode)
    {
        if (!Enum.IsDefined(audioMode))
        {
            throw new ArgumentOutOfRangeException(nameof(audioMode));
        }

        if (audioMode == VideoAudioMode)
        {
            return this;
        }

        return this with
        {
            VideoAudioMode = audioMode,
            Status = MediaType == MediaSelectionKind.Video && HasVideoArtifact
                ? RenderWorkflowStatus.RenderStale
                : Status
        };
    }

    public FullVideoRenderIdentity CreateFullVideoRenderIdentity(
        ImageRenderSettings settings,
        CharacterPresetOption preset)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(preset);
        if (MediaType != MediaSelectionKind.Video)
        {
            throw new InvalidOperationException("A full-video identity requires selected video media.");
        }

        return new FullVideoRenderIdentity(
            MediaVersion,
            MediaVersion,
            SettingsVersion,
            preset.Kind,
            preset.Characters,
            settings.FontFamily,
            settings.FontSize,
            settings.OutputResolution,
            settings.SymbolPolarity,
            VideoAudioMode);
    }

    public RenderWorkflowState OutputFormatChanged() => this;

    public RenderWorkflowState BeginOperation(long operationId, RenderWorkflowOperation operation)
    {
        if (operationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operationId));
        }

        if (!Enum.IsDefined(operation) || operation == RenderWorkflowOperation.None)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        bool isAllowed = operation switch
        {
            RenderWorkflowOperation.RenderImage => MediaType == MediaSelectionKind.Image && HasOriginal,
            RenderWorkflowOperation.PreviewVideoFrame => MediaType == MediaSelectionKind.Video && HasOriginal,
            RenderWorkflowOperation.RenderVideo => false,
            RenderWorkflowOperation.ExportImage => CanExportImage,
            RenderWorkflowOperation.ExportVideo => CanExportVideo,
            _ => false
        };
        if (!isAllowed)
        {
            throw new InvalidOperationException($"{operation} is not valid in the current workflow state.");
        }

        RenderWorkflowStatus nextStatus = operation switch
        {
            RenderWorkflowOperation.RenderImage => RenderWorkflowStatus.Rendering,
            RenderWorkflowOperation.PreviewVideoFrame => RenderWorkflowStatus.PreviewingFrame,
            RenderWorkflowOperation.ExportImage or RenderWorkflowOperation.ExportVideo => RenderWorkflowStatus.Exporting,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        return this with
        {
            ActiveOperationId = operationId,
            ActiveOperation = operation,
            OutcomeOperation = RenderWorkflowOperation.None,
            Status = nextStatus
        };
    }

    public RenderWorkflowState BeginVideoRender(long operationId, FullVideoRenderIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (operationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operationId));
        }

        if (MediaType != MediaSelectionKind.Video || !IsCurrentVideoRenderIdentity(identity))
        {
            throw new InvalidOperationException("The full-video render identity is not current.");
        }

        return this with
        {
            ActiveOperationId = operationId,
            ActiveOperation = RenderWorkflowOperation.RenderVideo,
            ActiveVideoRenderIdentity = identity,
            OutcomeOperation = RenderWorkflowOperation.None,
            Status = RenderWorkflowStatus.Rendering
        };
    }

    public bool CanAcceptResult(long operationId, PreviewIdentity identity, RenderWorkflowOperation operation) =>
        operationId != 0
        && operationId == ActiveOperationId
        && operation == ActiveOperation
        && Status != RenderWorkflowStatus.Cancelling
        && identity == CurrentIdentity
        && HasOriginal;

    public RenderWorkflowState CompleteImageRender(long operationId, PreviewIdentity identity) =>
        CanAcceptResult(operationId, identity, RenderWorkflowOperation.RenderImage)
            ? this with
            {
                ImageRenderIdentity = identity,
                OutcomeOperation = RenderWorkflowOperation.RenderImage,
                Status = RenderWorkflowStatus.RenderReady
            }
            : this;

    public RenderWorkflowState CompleteVideoFramePreview(long operationId, PreviewIdentity identity) =>
        CanAcceptResult(operationId, identity, RenderWorkflowOperation.PreviewVideoFrame)
            ? this with
            {
                VideoFramePreviewIdentity = identity,
                OutcomeOperation = RenderWorkflowOperation.PreviewVideoFrame,
                Status = ResolveVideoReviewStatus(framePreviewReady: true)
            }
            : this;

    public bool CanAcceptVideoRender(long operationId, FullVideoRenderIdentity identity) =>
        operationId == ActiveOperationId
        && ActiveOperation == RenderWorkflowOperation.RenderVideo
        && Status != RenderWorkflowStatus.Cancelling
        && identity == ActiveVideoRenderIdentity
        && IsCurrentVideoRenderIdentity(identity);

    public RenderWorkflowState CompleteVideoRender(long operationId, FullVideoRenderIdentity identity) =>
        CanAcceptVideoRender(operationId, identity)
            ? this with
            {
                VideoArtifactIdentity = identity,
                OutcomeOperation = RenderWorkflowOperation.RenderVideo,
                Status = RenderWorkflowStatus.RenderReady
            }
            : this;

    public RenderWorkflowState FailVideoReview() => HasVideoArtifact
        ? this with
        {
            OutcomeOperation = RenderWorkflowOperation.RenderVideo,
            Status = RenderWorkflowStatus.Failed
        }
        : this;

    // Session acceptance is a commit: even if settings change while it awaits cleanup,
    // the retained physical artifact must have its matching (possibly stale) identity.
    public RenderWorkflowState RecordAcceptedVideoArtifact(FullVideoRenderIdentity identity) =>
        MediaType == MediaSelectionKind.Video && identity.MediaVersion == MediaVersion
            ? this with
            {
                VideoArtifactIdentity = identity,
                OutcomeOperation = RenderWorkflowOperation.RenderVideo,
                Status = IsCurrentVideoRenderIdentity(identity)
                    ? RenderWorkflowStatus.RenderReady
                    : RenderWorkflowStatus.RenderStale
            }
            : this;

    public RenderWorkflowStatus VideoReviewStatus => HasVideoArtifact
        ? IsVideoArtifactStale ? RenderWorkflowStatus.RenderStale : RenderWorkflowStatus.RenderReady
        : RenderWorkflowStatus.MediaReady;

    public RenderWorkflowState CompleteExport(long operationId) =>
        operationId == ActiveOperationId
        && ActiveOperation is RenderWorkflowOperation.ExportImage or RenderWorkflowOperation.ExportVideo
        && (ActiveOperation != RenderWorkflowOperation.ExportImage || ImageRenderIdentity == CurrentIdentity)
            ? this with
            {
                OutcomeOperation = ActiveOperation,
                Status = RenderWorkflowStatus.Exported
            }
            : this;

    public RenderWorkflowState FailActiveOperation(long operationId)
    {
        if (operationId != ActiveOperationId)
        {
            return this;
        }

        return this with
        {
            OutcomeOperation = ActiveOperation,
            Status = RenderWorkflowStatus.Failed
        };
    }

    public RenderWorkflowState BeginCancellation() => ActiveOperation == RenderWorkflowOperation.None
        ? this
        : this with { Status = RenderWorkflowStatus.Cancelling };

    public RenderWorkflowState CancelActiveOperation() => ActiveOperation == RenderWorkflowOperation.None
        ? this
        : this with
        {
            ActiveOperationId = 0,
            OutcomeOperation = ActiveOperation,
            ActiveOperation = RenderWorkflowOperation.None,
            ActiveVideoRenderIdentity = null,
            Status = RenderWorkflowStatus.Cancelled
        };

    public RenderWorkflowState EndOperation(long operationId)
    {
        if (operationId != ActiveOperationId)
        {
            return this;
        }

        RenderWorkflowStatus restingStatus = Status switch
        {
            RenderWorkflowStatus.Cancelling => RenderWorkflowStatus.Cancelled,
            RenderWorkflowStatus.Rendering or RenderWorkflowStatus.PreviewingFrame or RenderWorkflowStatus.Exporting
                => ResolveRestingStatus(),
            _ => Status
        };

        return this with
        {
            ActiveOperationId = 0,
            OutcomeOperation = restingStatus == RenderWorkflowStatus.Cancelled
                ? ActiveOperation
                : OutcomeOperation,
            ActiveOperation = RenderWorkflowOperation.None,
            ActiveVideoRenderIdentity = null,
            Status = restingStatus
        };
    }

    public RenderWorkflowState ClearResult() => this with
    {
        ImageRenderIdentity = null,
        VideoFramePreviewIdentity = null,
        VideoArtifactIdentity = null,
        ActiveVideoRenderIdentity = null,
        OutcomeOperation = RenderWorkflowOperation.None,
        Status = MediaType == MediaSelectionKind.None
            ? RenderWorkflowStatus.Empty
            : RenderWorkflowStatus.MediaReady
    };

    private RenderWorkflowStatus ResolveRestingStatus()
    {
        if (MediaType == MediaSelectionKind.Image)
        {
            return IsImageRenderStale
                ? RenderWorkflowStatus.RenderStale
                : HasImageRender ? RenderWorkflowStatus.RenderReady : RenderWorkflowStatus.MediaReady;
        }

        if (HasVideoArtifact)
        {
            return IsVideoArtifactStale ? RenderWorkflowStatus.RenderStale : RenderWorkflowStatus.RenderReady;
        }

        return HasVideoFramePreview && !IsVideoFramePreviewStale
            ? RenderWorkflowStatus.FramePreviewReady
            : RenderWorkflowStatus.MediaReady;
    }

    private RenderWorkflowStatus ResolveVideoReviewStatus(bool framePreviewReady = false)
    {
        if (HasVideoArtifact)
        {
            return IsVideoArtifactStale ? RenderWorkflowStatus.RenderStale : RenderWorkflowStatus.RenderReady;
        }

        return framePreviewReady || HasVideoFramePreview && !IsVideoFramePreviewStale
            ? RenderWorkflowStatus.FramePreviewReady
            : RenderWorkflowStatus.MediaReady;
    }

    private bool IsCurrentVideoRenderIdentity(FullVideoRenderIdentity identity) =>
        identity.MediaVersion == MediaVersion
        && identity.SourceIdentityVersion == MediaVersion
        && identity.RenderSettingsVersion == SettingsVersion
        && identity.AudioMode == VideoAudioMode;
}
