using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

public sealed class ManagedVideoArtifactSession(IManagedVideoArtifactStore store)
    : IManagedVideoArtifactSession
{
    private readonly HashSet<string> _cleanupDiagnostics = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, ManagedVideoArtifact> _pendingCleanup =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IManagedVideoArtifactStore _store = store
        ?? throw new ArgumentNullException(nameof(store));
    private IReadOnlyList<string> _cleanupDiagnosticsSnapshot = [];
    private ManagedVideoArtifact? _current;
    private int _pendingCleanupCount;
    private bool _disposed;

    public ManagedVideoArtifact? Current => Volatile.Read(ref _current);

    public int PendingCleanupCount => Volatile.Read(ref _pendingCleanupCount);

    public IReadOnlyList<string> CleanupDiagnostics => Volatile.Read(ref _cleanupDiagnosticsSnapshot);

    public async Task AcceptAsync(
        ManagedVideoArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            _store.ValidateCompletedArtifact(artifact, requireNonEmptyFile: true);
            await RetryPendingCleanupAsync().ConfigureAwait(false);

            ManagedVideoArtifact? previous = _current;
            _current = artifact;
            if (previous is not null && previous.ArtifactId != artifact.ArtifactId)
            {
                await DeleteOrTrackAsync(previous).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RejectAsync(
        ManagedVideoArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            _store.ValidateCompletedArtifact(artifact, requireNonEmptyFile: false);
            if (_current is ManagedVideoArtifact current
                && current.ArtifactId == artifact.ArtifactId
                && current.FilePath.Equals(artifact.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The current managed video artifact cannot be rejected.");
            }

            await RetryPendingCleanupAsync().ConfigureAwait(false);
            await DeleteOrTrackAsync(artifact).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await RetryPendingCleanupAsync().ConfigureAwait(false);
            ManagedVideoArtifact? previous = _current;
            _current = null;
            if (previous is not null)
            {
                await DeleteOrTrackAsync(previous).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<ArtifactCleanupResult> RecoverOrphansAsync(
        CancellationToken cancellationToken = default) => _store.RecoverOrphansAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await RetryPendingCleanupAsync().ConfigureAwait(false);
            ManagedVideoArtifact? current = _current;
            _current = null;
            if (current is not null)
            {
                await DeleteOrTrackAsync(current).ConfigureAwait(false);
            }

            await RetryPendingCleanupAsync().ConfigureAwait(false);
            ArtifactCleanupResult sessionCleanup = await _store.CleanupSessionAsync().ConfigureAwait(false);
            RecordDiagnostics(sessionCleanup.Failures);
            if (sessionCleanup.Complete)
            {
                _pendingCleanup.Clear();
                PublishCleanupState();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RetryPendingCleanupAsync()
    {
        foreach (ManagedVideoArtifact artifact in _pendingCleanup.Values.ToArray())
        {
            ArtifactCleanupResult result = await _store.DeleteArtifactAsync(
                artifact,
                CancellationToken.None).ConfigureAwait(false);
            if (result.Complete)
            {
                _pendingCleanup.Remove(artifact.FilePath);
            }

            RecordDiagnostics(result.Failures);
        }

        PublishCleanupState();
    }

    private async Task DeleteOrTrackAsync(ManagedVideoArtifact artifact)
    {
        ArtifactCleanupResult result = await _store.DeleteArtifactAsync(
            artifact,
            CancellationToken.None).ConfigureAwait(false);
        if (result.Complete)
        {
            _pendingCleanup.Remove(artifact.FilePath);
        }
        else
        {
            _pendingCleanup[artifact.FilePath] = artifact;
        }

        RecordDiagnostics(result.Failures);
        PublishCleanupState();
    }

    private void RecordDiagnostics(IEnumerable<string> failures)
    {
        foreach (string failure in failures)
        {
            _cleanupDiagnostics.Add(failure);
        }
    }

    private void PublishCleanupState()
    {
        Volatile.Write(ref _pendingCleanupCount, _pendingCleanup.Count);
        Volatile.Write(ref _cleanupDiagnosticsSnapshot, _cleanupDiagnostics.ToArray());
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
