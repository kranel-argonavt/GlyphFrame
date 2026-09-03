using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

/// <summary>
/// Owns physical files below one verified application cache root and one process session.
/// </summary>
public interface IManagedVideoArtifactStore
{
    string CacheRoot { get; }

    string SessionDirectory { get; }

    ManagedVideoArtifactTarget CreateTarget();

    bool OwnsPath(string path);

    void ValidateCompletedArtifact(ManagedVideoArtifact artifact, bool requireNonEmptyFile);

    Task PromoteAsync(ManagedVideoArtifactTarget target, CancellationToken cancellationToken = default);

    Task<ArtifactCleanupResult> DeleteAsync(
        ManagedVideoArtifactTarget target,
        CancellationToken cancellationToken = default);

    Task<ArtifactCleanupResult> DeleteArtifactAsync(
        ManagedVideoArtifact artifact,
        CancellationToken cancellationToken = default);

    Task<ArtifactCleanupResult> RecoverOrphansAsync(CancellationToken cancellationToken = default);

    Task<ArtifactCleanupResult> CleanupSessionAsync(CancellationToken cancellationToken = default);
}
