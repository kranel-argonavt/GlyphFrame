using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class ManagedVideoArtifactStoreTests
{
    [Fact]
    public void CreateTarget_UsesUniqueOwnedMp4PathsInUnicodeSession()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "сесія-日本語");

        ManagedVideoArtifactTarget first = store.CreateTarget();
        ManagedVideoArtifactTarget second = store.CreateTarget();

        Assert.NotEqual(first.ArtifactId, second.ArtifactId);
        Assert.EndsWith(".pending.mp4", first.PendingPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".mp4", first.CompletedPath, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(first.PendingPath, first.CompletedPath);
        Assert.True(store.OwnsPath(first.PendingPath));
        Assert.Contains("сесія-日本語", first.CompletedPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromoteAndDelete_AcceptsOnlyNonEmptyOwnedCandidate()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        ManagedVideoArtifactTarget target = store.CreateTarget();
        await File.WriteAllBytesAsync(target.PendingPath, [1, 2, 3]);

        await store.PromoteAsync(target);

        Assert.False(File.Exists(target.PendingPath));
        Assert.True(File.Exists(target.CompletedPath));
        ArtifactCleanupResult cleanup = await store.DeleteAsync(target);
        Assert.True(cleanup.Complete);
        Assert.False(File.Exists(target.CompletedPath));
        Assert.True((await store.DeleteAsync(target)).Complete);
    }

    [Fact]
    public async Task Cleanup_RejectsOutsidePathAndNeverDeletesIt()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        Directory.CreateDirectory(data.RootPath);
        string outside = Path.Combine(data.RootPath, "user-output.mp4");
        await File.WriteAllBytesAsync(outside, [9, 8, 7]);
        ManagedVideoArtifact artifact = Artifact(outside);

        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteArtifactAsync(artifact));

        Assert.True(File.Exists(outside));
        Assert.False(store.OwnsPath(outside));
    }

    [Fact]
    public async Task RecoverOrphans_RemovesOldSessionsButPreservesCurrentSession()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore current = new(data.Paths, "session-current");
        ManagedVideoArtifactTarget currentTarget = current.CreateTarget();
        await File.WriteAllBytesAsync(currentTarget.PendingPath, [1]);
        string orphanDirectory = Path.Combine(current.CacheRoot, "session-orphan");
        Directory.CreateDirectory(orphanDirectory);
        string orphan = Path.Combine(orphanDirectory, "artifact-old.mp4");
        await File.WriteAllBytesAsync(orphan, [2]);
        MarkAsStale(orphanDirectory);

        ArtifactCleanupResult result = await current.RecoverOrphansAsync();

        Assert.True(result.Complete);
        Assert.True(File.Exists(currentTarget.PendingPath));
        Assert.False(Directory.Exists(orphanDirectory));
    }

    [Fact]
    public async Task RecoverOrphans_PreservesEveryActiveLeasedSession()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore first = new(data.Paths, "session-active-one");
        using ManagedVideoArtifactStore second = new(data.Paths, "session-active-two");
        ManagedVideoArtifact firstArtifact = await CreateCompletedArtifactAsync(first, 1);
        ManagedVideoArtifact secondArtifact = await CreateCompletedArtifactAsync(second, 2);

        ArtifactCleanupResult result = await first.RecoverOrphansAsync();

        Assert.True(result.Complete);
        Assert.True(File.Exists(firstArtifact.FilePath));
        Assert.True(File.Exists(secondArtifact.FilePath));
        Assert.True(Directory.Exists(first.SessionDirectory));
        Assert.True(Directory.Exists(second.SessionDirectory));
    }

    [Fact]
    public void ActiveLease_PreventsASecondOwnerFromClaimingTheSameSessionDirectory()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore active = new(data.Paths, "session-shared-name");

        Assert.Throws<IOException>(() => new ManagedVideoArtifactStore(data.Paths, "session-shared-name"));

        Assert.True(Directory.Exists(active.SessionDirectory));
    }

    [Fact]
    public async Task RecoverOrphans_DeletesSessionAfterItsLeaseIsReleased()
    {
        using TemporaryApplicationData data = new();
        ManagedVideoArtifactStore stale = new(data.Paths, "session-released");
        ManagedVideoArtifact artifact = await CreateCompletedArtifactAsync(stale, 1);
        string staleDirectory = stale.SessionDirectory;
        stale.Dispose();
        using ManagedVideoArtifactStore current = new(data.Paths, "session-current");

        ArtifactCleanupResult result = await current.RecoverOrphansAsync();

        Assert.True(result.Complete);
        Assert.False(File.Exists(artifact.FilePath));
        Assert.False(Directory.Exists(staleDirectory));
        Assert.True(Directory.Exists(current.SessionDirectory));
    }

    [Fact]
    public async Task RecoverOrphans_PreservesFreshDirectoryDuringLeaseInitializationWindow()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore current = new(data.Paths, "session-current");
        string startingDirectory = Path.Combine(current.CacheRoot, "session-starting");
        Directory.CreateDirectory(startingDirectory);

        ArtifactCleanupResult result = await current.RecoverOrphansAsync();

        Assert.True(result.Complete);
        Assert.True(Directory.Exists(startingDirectory));
    }

    [Fact]
    public async Task RecoverOrphans_ContinuesAfterOneLockedEntryFails()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-current");
        _ = store.CreateTarget();
        string lockedDirectory = Path.Combine(store.CacheRoot, "session-locked");
        string removableDirectory = Path.Combine(store.CacheRoot, "session-removable");
        Directory.CreateDirectory(lockedDirectory);
        Directory.CreateDirectory(removableDirectory);
        string lockedPath = Path.Combine(lockedDirectory, "locked.mp4");
        await File.WriteAllBytesAsync(lockedPath, [1]);
        await File.WriteAllBytesAsync(Path.Combine(removableDirectory, "remove.mp4"), [2]);
        MarkAsStale(lockedDirectory);
        MarkAsStale(removableDirectory);
        using FileStream locked = new(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);

        ArtifactCleanupResult result = await store.RecoverOrphansAsync();

        Assert.NotEmpty(result.Failures);
        Assert.False(Directory.Exists(removableDirectory));
        Assert.True(Directory.Exists(lockedDirectory));
    }

    [Fact]
    public async Task RecoverOrphans_MixedActiveStaleAndLockedSessionsRemainIsolated()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore current = new(data.Paths, "session-current");
        using ManagedVideoArtifactStore active = new(data.Paths, "session-active");
        ManagedVideoArtifact activeArtifact = await CreateCompletedArtifactAsync(active, 1);
        string removable = Path.Combine(current.CacheRoot, "session-stale-removable");
        string locked = Path.Combine(current.CacheRoot, "session-stale-locked");
        Directory.CreateDirectory(removable);
        Directory.CreateDirectory(locked);
        await File.WriteAllBytesAsync(Path.Combine(removable, "artifact-old.mp4"), [2]);
        string lockedFile = Path.Combine(locked, "artifact-locked.mp4");
        await File.WriteAllBytesAsync(lockedFile, [3]);
        MarkAsStale(removable);
        MarkAsStale(locked);
        using FileStream lockHandle = new(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);

        ArtifactCleanupResult result = await current.RecoverOrphansAsync();

        Assert.NotEmpty(result.Failures);
        Assert.True(File.Exists(activeArtifact.FilePath));
        Assert.False(Directory.Exists(removable));
        Assert.True(Directory.Exists(locked));
    }

    [Fact]
    public async Task Session_AcceptsNewArtifactBeforeDeletingPreviousAndRejectsCandidates()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        await using ManagedVideoArtifactSession session = new(store);
        ManagedVideoArtifact first = await CreateCompletedArtifactAsync(store, 1);
        ManagedVideoArtifact second = await CreateCompletedArtifactAsync(store, 2);
        ManagedVideoArtifact rejected = await CreateCompletedArtifactAsync(store, 3);

        await session.AcceptAsync(first);
        await session.AcceptAsync(second);

        Assert.Equal(second, session.Current);
        Assert.False(File.Exists(first.FilePath));
        Assert.True(File.Exists(second.FilePath));
        await session.RejectAsync(rejected);
        Assert.False(File.Exists(rejected.FilePath));
        Assert.True(File.Exists(second.FilePath));
    }

    [Fact]
    public async Task Session_RejectsForeignArtifactsWithoutDeletingEitherSessionFile()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore firstStore = new(data.Paths, "session-first");
        using ManagedVideoArtifactStore secondStore = new(data.Paths, "session-second");
        await using ManagedVideoArtifactSession firstSession = new(firstStore);
        await using ManagedVideoArtifactSession secondSession = new(secondStore);
        ManagedVideoArtifact first = await CreateCompletedArtifactAsync(firstStore, 1);
        ManagedVideoArtifact foreign = await CreateCompletedArtifactAsync(secondStore, 2);
        await firstSession.AcceptAsync(first);

        await Assert.ThrowsAsync<ArgumentException>(() => firstSession.AcceptAsync(foreign));
        await Assert.ThrowsAsync<ArgumentException>(() => firstSession.RejectAsync(foreign));

        Assert.Equal(first, firstSession.Current);
        Assert.False(firstStore.OwnsPath(foreign.FilePath));
        Assert.True(File.Exists(first.FilePath));
        Assert.True(File.Exists(foreign.FilePath));
    }

    [Fact]
    public async Task Session_RejectingCurrentArtifactFailsAndPreservesIt()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        await using ManagedVideoArtifactSession session = new(store);
        ManagedVideoArtifact artifact = await CreateCompletedArtifactAsync(store, 1);
        await session.AcceptAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RejectAsync(artifact));

        Assert.Equal(artifact, session.Current);
        Assert.True(File.Exists(artifact.FilePath));
    }

    [Fact]
    public async Task Session_RejectsPendingNoncanonicalEmptyAndOutsideArtifacts()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        await using ManagedVideoArtifactSession session = new(store);
        ManagedVideoArtifactTarget pendingTarget = store.CreateTarget();
        await File.WriteAllBytesAsync(pendingTarget.PendingPath, [1]);
        ManagedVideoArtifact pending = Artifact(pendingTarget.PendingPath, pendingTarget.ArtifactId);
        string noncanonicalPath = Path.Combine(store.SessionDirectory, "renamed.mp4");
        await File.WriteAllBytesAsync(noncanonicalPath, [1]);
        ManagedVideoArtifact noncanonical = Artifact(noncanonicalPath);
        ManagedVideoArtifactTarget emptyTarget = store.CreateTarget();
        await File.WriteAllBytesAsync(emptyTarget.CompletedPath, []);
        ManagedVideoArtifact empty = Artifact(emptyTarget.CompletedPath, emptyTarget.ArtifactId);
        string outsidePath = Path.Combine(data.RootPath, "outside.mp4");
        await File.WriteAllBytesAsync(outsidePath, [1]);
        ManagedVideoArtifact outside = Artifact(outsidePath);

        await Assert.ThrowsAsync<ArgumentException>(() => session.AcceptAsync(pending));
        await Assert.ThrowsAsync<ArgumentException>(() => session.AcceptAsync(noncanonical));
        await Assert.ThrowsAsync<ArgumentException>(() => session.AcceptAsync(empty));
        await Assert.ThrowsAsync<ArgumentException>(() => session.AcceptAsync(outside));

        Assert.Null(session.Current);
        Assert.False(store.OwnsPath(noncanonicalPath));
        Assert.True(File.Exists(pendingTarget.PendingPath));
        Assert.True(File.Exists(noncanonicalPath));
        Assert.True(File.Exists(emptyTarget.CompletedPath));
        Assert.True(File.Exists(outsidePath));
    }

    [Fact]
    public async Task FailedReplacementCleanup_IsRetriedWithoutRemovingNewCurrentArtifact()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-test");
        await using ManagedVideoArtifactSession session = new(store);
        ManagedVideoArtifact first = await CreateCompletedArtifactAsync(store, 1);
        ManagedVideoArtifact second = await CreateCompletedArtifactAsync(store, 2);
        await session.AcceptAsync(first);
        using (FileStream lockHandle = new(first.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await session.AcceptAsync(second);

            Assert.Equal(second, session.Current);
            Assert.True(File.Exists(first.FilePath));
            Assert.True(File.Exists(second.FilePath));
            Assert.Equal(1, session.PendingCleanupCount);
            Assert.NotEmpty(session.CleanupDiagnostics);

            await session.AcceptAsync(second);
            Assert.Equal(1, session.PendingCleanupCount);
        }

        await session.AcceptAsync(second);

        Assert.Equal(second, session.Current);
        Assert.False(File.Exists(first.FilePath));
        Assert.True(File.Exists(second.FilePath));
        Assert.Equal(0, session.PendingCleanupCount);
    }

    [Fact]
    public async Task Session_DisposeRemovesAcceptedArtifactAndOwnedSessionDirectory()
    {
        using TemporaryApplicationData data = new();
        using ManagedVideoArtifactStore store = new(data.Paths, "session-shutdown");
        ManagedVideoArtifactSession session = new(store);
        ManagedVideoArtifact artifact = await CreateCompletedArtifactAsync(store, 4);
        await session.AcceptAsync(artifact);

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Null(session.Current);
        Assert.False(File.Exists(artifact.FilePath));
        Assert.False(Directory.Exists(store.SessionDirectory));

        using ManagedVideoArtifactStore reopened = new(data.Paths, "session-shutdown");
        Assert.True(Directory.Exists(reopened.SessionDirectory));
    }

    [Fact]
    public async Task Session_DisposeRetriesTrackedCleanupAndRemainsIdempotent()
    {
        ControlledCleanupStore store = new();
        ManagedVideoArtifactSession session = new(store);
        ManagedVideoArtifact artifact = Artifact(Path.Combine(store.SessionDirectory, $"artifact-{store.Id:N}.mp4"), store.Id);
        await session.AcceptAsync(artifact);

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(2, store.DeleteCalls);
        Assert.Equal(1, store.SessionCleanupCalls);
        Assert.Equal(0, session.PendingCleanupCount);
        Assert.NotEmpty(session.CleanupDiagnostics);
    }

    [Fact]
    public async Task Session_ClearTracksFailureAndRetriesItAtNextLifecycleBoundary()
    {
        ControlledCleanupStore store = new();
        await using ManagedVideoArtifactSession session = new(store);
        ManagedVideoArtifact artifact = Artifact(Path.Combine(store.SessionDirectory, $"artifact-{store.Id:N}.mp4"), store.Id);
        await session.AcceptAsync(artifact);

        await session.ClearAsync();

        Assert.Null(session.Current);
        Assert.Equal(1, session.PendingCleanupCount);
        Assert.NotEmpty(session.CleanupDiagnostics);

        await session.ClearAsync();

        Assert.Equal(0, session.PendingCleanupCount);
        Assert.Equal(2, store.DeleteCalls);
    }

    [Fact]
    public async Task Session_LifecycleOperationsRejectUseAfterDisposal()
    {
        ControlledCleanupStore store = new();
        ManagedVideoArtifactSession session = new(store);
        ManagedVideoArtifact artifact = Artifact(Path.Combine(store.SessionDirectory, $"artifact-{store.Id:N}.mp4"), store.Id);
        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.AcceptAsync(artifact));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.RejectAsync(artifact));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.ClearAsync());
    }

    private static async Task<ManagedVideoArtifact> CreateCompletedArtifactAsync(
        ManagedVideoArtifactStore store,
        byte content)
    {
        ManagedVideoArtifactTarget target = store.CreateTarget();
        await File.WriteAllBytesAsync(target.PendingPath, [content]);
        await store.PromoteAsync(target);
        return Artifact(target.CompletedPath, target.ArtifactId);
    }

    private static void MarkAsStale(string directory) =>
        Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow - TimeSpan.FromMinutes(5));

    private static ManagedVideoArtifact Artifact(string path, Guid? id = null) => new(
        id ?? Guid.NewGuid(),
        path,
        new FullVideoRenderIdentity(
            1,
            1,
            0,
            CharacterPresetKind.DetailedAscii,
            CharacterPresetOption.Default.Characters,
            "Cascadia Mono",
            10,
            OutputResolution.SameAsSource,
            SymbolPolarity.LightOnDark,
            AudioExportMode.KeepAudio),
        new PixelDimensions(2, 2),
        30,
        1,
        TimeSpan.FromSeconds(1d / 30d),
        AudioExportMode.KeepAudio,
        AudioMuxOutcome.NoSourceAudio,
        1);

    private sealed class ControlledCleanupStore : GlyphFrame.Core.Interfaces.IManagedVideoArtifactStore
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string CacheRoot { get; } = Path.GetTempPath();

        public string SessionDirectory { get; } = Path.Combine(Path.GetTempPath(), "controlled-artifact-session");

        public int DeleteCalls { get; private set; }

        public int SessionCleanupCalls { get; private set; }

        public ManagedVideoArtifactTarget CreateTarget() => throw new NotSupportedException();

        public bool OwnsPath(string path) => true;

        public void ValidateCompletedArtifact(ManagedVideoArtifact artifact, bool requireNonEmptyFile)
        {
        }

        public Task PromoteAsync(
            ManagedVideoArtifactTarget target,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ArtifactCleanupResult> DeleteAsync(
            ManagedVideoArtifactTarget target,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ArtifactCleanupResult> DeleteArtifactAsync(
            ManagedVideoArtifact artifact,
            CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.FromResult(DeleteCalls == 1
                ? new ArtifactCleanupResult(0, ["Controlled cleanup failure."])
                : new ArtifactCleanupResult(1, []));
        }

        public Task<ArtifactCleanupResult> RecoverOrphansAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ArtifactCleanupResult(0, []));

        public Task<ArtifactCleanupResult> CleanupSessionAsync(
            CancellationToken cancellationToken = default)
        {
            SessionCleanupCalls++;
            return Task.FromResult(new ArtifactCleanupResult(0, []));
        }
    }
}
