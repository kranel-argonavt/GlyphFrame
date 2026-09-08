using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

/// <summary>
/// Retains exactly one accepted review artifact. Candidates remain caller-owned until accepted.
/// </summary>
public interface IManagedVideoArtifactSession : IAsyncDisposable
{
    ManagedVideoArtifact? Current { get; }

    int PendingCleanupCount { get; }

    IReadOnlyList<string> CleanupDiagnostics { get; }

    Task AcceptAsync(ManagedVideoArtifact artifact, CancellationToken cancellationToken = default);

    Task RejectAsync(ManagedVideoArtifact artifact, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Serializes a bounded borrower with replacement, deletion and shutdown.
    /// The callback must not require a UI dispatcher continuation.</summary>
    Task<T> UseCurrentAsync<T>(ManagedVideoArtifact artifact,
        Func<CancellationToken, Task<T>> borrower, CancellationToken cancellationToken = default);

    Task<ArtifactCleanupResult> RecoverOrphansAsync(CancellationToken cancellationToken = default);
}
