using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

/// <summary>
/// Names a store-owned candidate. The pending file is never a reviewable artifact.
/// </summary>
public sealed record ManagedVideoArtifactTarget(
    Guid ArtifactId,
    string SessionId,
    string PendingPath,
    string CompletedPath);

/// <summary>
/// Metadata for one completed store-owned MP4. A render caller owns the candidate lifecycle
/// until it accepts or rejects the artifact. After acceptance, consumers borrow the file and
/// the managed artifact session remains responsible for physical deletion.
/// </summary>
public sealed record ManagedVideoArtifact(
    Guid ArtifactId,
    string FilePath,
    FullVideoRenderIdentity Identity,
    PixelDimensions OutputDimensions,
    double FramesPerSecond,
    long RenderedFrameCount,
    TimeSpan Duration,
    AudioExportMode AudioMode,
    AudioMuxOutcome AudioOutcome,
    long FileSizeBytes);

public sealed record ManagedVideoRenderJob(
    string InputPath,
    VideoMetadata Metadata,
    ImageRenderSettings RenderSettings,
    CharacterPresetOption CharacterPreset,
    AudioExportMode AudioMode,
    FullVideoRenderIdentity Identity);

public sealed record ManagedVideoRenderResult(
    ManagedVideoArtifactStatus Status,
    ManagedVideoArtifactErrorCategory ErrorCategory,
    VideoExportErrorCategory? PipelineErrorCategory,
    string UserMessage,
    string? TechnicalDetail,
    ManagedVideoArtifact? Artifact)
{
    public bool Success => Status == ManagedVideoArtifactStatus.Completed && Artifact is not null;

    public static ManagedVideoRenderResult Completed(ManagedVideoArtifact artifact) => new(
        ManagedVideoArtifactStatus.Completed,
        ManagedVideoArtifactErrorCategory.None,
        null,
        "The managed symbolized video artifact is ready for review.",
        null,
        artifact);

    public static ManagedVideoRenderResult Failure(
        ManagedVideoArtifactErrorCategory category,
        string userMessage,
        string? technicalDetail = null,
        VideoExportErrorCategory? pipelineErrorCategory = null) => new(
        ManagedVideoArtifactStatus.Failed,
        category,
        pipelineErrorCategory,
        userMessage,
        technicalDetail,
        null);

    public static ManagedVideoRenderResult Cancelled() => new(
        ManagedVideoArtifactStatus.Cancelled,
        ManagedVideoArtifactErrorCategory.Cancelled,
        null,
        "Video rendering was cancelled. No managed artifact was retained.",
        null,
        null);
}

public sealed record ArtifactCleanupResult(int DeletedEntries, IReadOnlyList<string> Failures)
{
    public bool Complete => Failures.Count == 0;
}
