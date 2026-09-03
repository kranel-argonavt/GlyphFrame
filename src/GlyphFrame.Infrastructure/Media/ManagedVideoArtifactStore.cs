using System.Text;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Media;

public sealed class ManagedVideoArtifactStore : IManagedVideoArtifactStore, IDisposable
{
    private const string CacheDirectoryName = "Cache";
    private const string ArtifactDirectoryName = "VideoArtifacts";
    private const string LeaseFileName = ".session.lease";
    private static readonly TimeSpan LeaseInitializationGracePeriod = TimeSpan.FromMinutes(1);
    private readonly object _leaseGate = new();
    private readonly string[] _managedDirectoryChain;
    private readonly string[] _managedRootDirectoryChain;
    private readonly string _rootWithSeparator;
    private readonly string _sessionWithSeparator;
    private FileStream? _lease;
    private bool _sessionClosed;

    public ManagedVideoArtifactStore(IApplicationPaths paths)
        : this(paths, $"session-{Environment.ProcessId}-{Guid.NewGuid():N}")
    {
    }

    public ManagedVideoArtifactStore(IApplicationPaths paths, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (sessionId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || sessionId is "." or "..")
        {
            throw new ArgumentException("The artifact session identifier is not valid.", nameof(sessionId));
        }

        string applicationDataDirectory = Path.GetFullPath(paths.ApplicationDataDirectory);
        string cacheDirectory = Path.GetFullPath(Path.Combine(applicationDataDirectory, CacheDirectoryName));
        CacheRoot = Path.GetFullPath(Path.Combine(cacheDirectory, ArtifactDirectoryName));
        SessionDirectory = Path.GetFullPath(Path.Combine(CacheRoot, sessionId));
        _rootWithSeparator = EnsureTrailingSeparator(CacheRoot);
        _sessionWithSeparator = EnsureTrailingSeparator(SessionDirectory);
        _managedRootDirectoryChain = [applicationDataDirectory, cacheDirectory, CacheRoot];
        _managedDirectoryChain = [.. _managedRootDirectoryChain, SessionDirectory];
        if (!IsContainedBy(SessionDirectory, _rootWithSeparator))
        {
            throw new InvalidOperationException("The artifact session must remain inside the managed cache root.");
        }

        CreateVerifiedDirectory(applicationDataDirectory);
        CreateVerifiedDirectory(cacheDirectory);
        CreateVerifiedDirectory(CacheRoot);
        CreateVerifiedDirectory(SessionDirectory);
        AcquireLease(sessionId);
    }

    public string CacheRoot { get; }

    public string SessionDirectory { get; }

    public ManagedVideoArtifactTarget CreateTarget()
    {
        EnsureSessionActive();
        EnsureManagedDirectoriesSafe();
        Guid artifactId = Guid.NewGuid();
        string baseName = GetArtifactBaseName(artifactId);
        return new ManagedVideoArtifactTarget(
            artifactId,
            Path.GetFileName(SessionDirectory),
            Path.Combine(SessionDirectory, $"{baseName}.pending.mp4"),
            Path.Combine(SessionDirectory, $"{baseName}.mp4"));
    }

    public bool OwnsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            string resolved = Path.GetFullPath(path);
            return IsContainedBy(resolved, _sessionWithSeparator)
                && Path.GetDirectoryName(resolved)!.Equals(
                    SessionDirectory,
                    StringComparison.OrdinalIgnoreCase)
                && IsGeneratedArtifactFileName(Path.GetFileName(resolved))
                && AreManagedDirectoriesSafe()
                && !IsReparsePoint(resolved);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or PathTooLongException)
        {
            return false;
        }
    }

    public void ValidateCompletedArtifact(ManagedVideoArtifact artifact, bool requireNonEmptyFile)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        EnsureSessionActive();
        EnsureCanonicalArtifactPath(artifact.FilePath, artifact.ArtifactId, pending: false);
        if (!requireNonEmptyFile)
        {
            return;
        }

        FileInfo file = new(artifact.FilePath);
        if (!file.Exists || file.Length <= 0)
        {
            throw new ArgumentException(
                "The managed video artifact must exist and contain a completed MP4.",
                nameof(artifact));
        }
    }

    public Task PromoteAsync(
        ManagedVideoArtifactTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSessionActive();
        EnsureTargetOwned(target);
        if (!File.Exists(target.PendingPath) || new FileInfo(target.PendingPath).Length <= 0)
        {
            throw new IOException("The pending managed video artifact is missing or empty.");
        }

        if (File.Exists(target.CompletedPath))
        {
            throw new IOException("The completed managed video artifact path is already occupied.");
        }

        File.Move(target.PendingPath, target.CompletedPath);
        return Task.CompletedTask;
    }

    public Task<ArtifactCleanupResult> DeleteAsync(
        ManagedVideoArtifactTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSessionActive();
        EnsureTargetOwned(target);
        return Task.FromResult(DeleteSessionFiles([target.PendingPath, target.CompletedPath]));
    }

    public Task<ArtifactCleanupResult> DeleteArtifactAsync(
        ManagedVideoArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSessionActive();
        ValidateCompletedArtifact(artifact, requireNonEmptyFile: false);
        return Task.FromResult(DeleteSessionFiles([artifact.FilePath]));
    }

    public Task<ArtifactCleanupResult> RecoverOrphansAsync(
        CancellationToken cancellationToken = default) => Task.Run(
        () => RecoverOrphans(cancellationToken),
        cancellationToken);

    public Task<ArtifactCleanupResult> CleanupSessionAsync(
        CancellationToken cancellationToken = default) => Task.Run(
        () => CloseAndCleanSession(cancellationToken),
        cancellationToken);

    public void Dispose() => ReleaseLease();

    private ArtifactCleanupResult RecoverOrphans(CancellationToken cancellationToken)
    {
        if (!AreDirectoriesSafe(_managedRootDirectoryChain))
        {
            return new ArtifactCleanupResult(0, []);
        }

        int deleted = 0;
        List<string> failures = [];
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(CacheRoot).ToArray();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return new ArtifactCleanupResult(0, [$"{CacheRoot}: {exception.Message}"]);
        }

        foreach (string entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string resolved = Path.GetFullPath(entry);
            if (!IsContainedBy(resolved, _rootWithSeparator)
                || resolved.Equals(SessionDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Directory.Exists(resolved)
                && !IsReparsePoint(resolved)
                && IsSessionLeaseActive(resolved, failures))
            {
                continue;
            }

            ArtifactCleanupResult result = Directory.Exists(resolved)
                ? DeleteCacheTree(resolved, cancellationToken)
                : DeleteCacheFiles([resolved]);
            deleted += result.DeletedEntries;
            failures.AddRange(result.Failures);
        }

        return new ArtifactCleanupResult(deleted, failures);
    }

    private ArtifactCleanupResult CloseAndCleanSession(CancellationToken cancellationToken)
    {
        ReleaseLease();
        return DeleteCacheTree(SessionDirectory, cancellationToken);
    }

    private static bool IsSessionLeaseActive(string sessionDirectory, List<string> failures)
    {
        string leasePath = Path.Combine(sessionDirectory, LeaseFileName);
        if (!File.Exists(leasePath))
        {
            try
            {
                return DateTime.UtcNow - Directory.GetLastWriteTimeUtc(sessionDirectory)
                    < LeaseInitializationGracePeriod;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
            {
                failures.Add($"{sessionDirectory}: session age could not be inspected: {exception.Message}");
                return true;
            }
        }

        if (IsReparsePoint(leasePath))
        {
            return false;
        }

        try
        {
            using FileStream probe = new(
                leasePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException exception)
        {
            failures.Add($"{sessionDirectory}: session lease could not be inspected: {exception.Message}");
            return true;
        }
    }

    private ArtifactCleanupResult DeleteCacheTree(string directory, CancellationToken cancellationToken)
    {
        string resolved = Path.GetFullPath(directory);
        if (!IsContainedBy(resolved, _rootWithSeparator))
        {
            return new ArtifactCleanupResult(0, ["Cleanup rejected a path outside its permitted cache scope."]);
        }

        if (!Directory.Exists(resolved))
        {
            return new ArtifactCleanupResult(0, []);
        }

        int deleted = 0;
        List<string> failures = [];
        try
        {
            if (IsReparsePoint(resolved))
            {
                Directory.Delete(resolved, recursive: false);
                return new ArtifactCleanupResult(1, []);
            }

            foreach (string entry in Directory.EnumerateFileSystemEntries(resolved).ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArtifactCleanupResult result = Directory.Exists(entry)
                    ? DeleteCacheTree(entry, cancellationToken)
                    : DeleteCacheFiles([entry]);
                deleted += result.DeletedEntries;
                failures.AddRange(result.Failures);
            }

            Directory.Delete(resolved, recursive: false);
            deleted++;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            failures.Add($"{resolved}: {exception.Message}");
        }

        return new ArtifactCleanupResult(deleted, failures);
    }

    private ArtifactCleanupResult DeleteSessionFiles(IEnumerable<string> paths)
    {
        int deleted = 0;
        List<string> failures = [];
        foreach (string path in paths)
        {
            try
            {
                EnsureDirectSessionPath(path);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                failures.Add($"{path}: {exception.Message}");
            }
        }

        return new ArtifactCleanupResult(deleted, failures);
    }

    private ArtifactCleanupResult DeleteCacheFiles(IEnumerable<string> paths)
    {
        int deleted = 0;
        List<string> failures = [];
        foreach (string path in paths)
        {
            try
            {
                string resolved = Path.GetFullPath(path);
                if (!IsContainedBy(resolved, _rootWithSeparator))
                {
                    throw new ArgumentException("The recovery path is outside the managed cache root.", nameof(paths));
                }

                if (File.Exists(resolved))
                {
                    File.Delete(resolved);
                    deleted++;
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                failures.Add($"{path}: {exception.Message}");
            }
        }

        return new ArtifactCleanupResult(deleted, failures);
    }

    private void EnsureTargetOwned(ManagedVideoArtifactTarget target)
    {
        if (!target.SessionId.Equals(Path.GetFileName(SessionDirectory), StringComparison.Ordinal)
            || !Path.GetFullPath(target.PendingPath).Equals(
                Path.Combine(SessionDirectory, $"{GetArtifactBaseName(target.ArtifactId)}.pending.mp4"),
                StringComparison.OrdinalIgnoreCase)
            || !Path.GetFullPath(target.CompletedPath).Equals(
                Path.Combine(SessionDirectory, $"{GetArtifactBaseName(target.ArtifactId)}.mp4"),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The artifact target does not belong to the active session.", nameof(target));
        }

        EnsureDirectSessionPath(target.PendingPath);
        EnsureDirectSessionPath(target.CompletedPath);
    }

    private void EnsureCanonicalArtifactPath(string path, Guid artifactId, bool pending)
    {
        string expectedName = pending
            ? $"{GetArtifactBaseName(artifactId)}.pending.mp4"
            : $"{GetArtifactBaseName(artifactId)}.mp4";
        string resolved = Path.GetFullPath(path);
        string expected = Path.Combine(SessionDirectory, expectedName);
        if (!resolved.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The artifact path is not canonical for its identifier.", nameof(path));
        }

        EnsureDirectSessionPath(resolved);
    }

    private void EnsureDirectSessionPath(string path)
    {
        EnsureManagedDirectoriesSafe();
        string resolved = Path.GetFullPath(path);
        if (!IsContainedBy(resolved, _sessionWithSeparator)
            || !Path.GetDirectoryName(resolved)!.Equals(SessionDirectory, StringComparison.OrdinalIgnoreCase)
            || IsReparsePoint(resolved))
        {
            throw new ArgumentException("The path is outside the active managed-artifact session.", nameof(path));
        }
    }

    private void AcquireLease(string sessionId)
    {
        string leasePath = Path.Combine(SessionDirectory, LeaseFileName);
        try
        {
            _lease = new FileStream(
                leasePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 256,
                FileOptions.WriteThrough);
            byte[] marker = Encoding.UTF8.GetBytes(
                $"GlyphFrame managed video artifact session{Environment.NewLine}" +
                $"Session: {sessionId}{Environment.NewLine}" +
                $"Process: {Environment.ProcessId}{Environment.NewLine}" +
                $"StartedUtc: {DateTimeOffset.UtcNow:O}{Environment.NewLine}");
            _lease.SetLength(0);
            _lease.Write(marker);
            _lease.Flush(flushToDisk: true);
        }
        catch
        {
            _lease?.Dispose();
            _lease = null;
            throw;
        }
    }

    private void ReleaseLease()
    {
        lock (_leaseGate)
        {
            if (_sessionClosed)
            {
                return;
            }

            _sessionClosed = true;
            _lease?.Dispose();
            _lease = null;
        }
    }

    private void EnsureSessionActive()
    {
        lock (_leaseGate)
        {
            ObjectDisposedException.ThrowIf(_sessionClosed || _lease is null, this);
        }
    }

    private bool AreManagedDirectoriesSafe()
    {
        try
        {
            return AreDirectoriesSafe(_managedDirectoryChain);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return false;
        }
    }

    private void EnsureManagedDirectoriesSafe()
    {
        if (!AreManagedDirectoriesSafe())
        {
            throw new IOException("The managed artifact cache contains an unsafe directory boundary.");
        }
    }

    private static void CreateVerifiedDirectory(string path)
    {
        Directory.CreateDirectory(path);
        if (IsReparsePoint(path))
        {
            throw new IOException("A managed artifact directory cannot be a reparse point.");
        }
    }

    private static bool AreDirectoriesSafe(IEnumerable<string> directories) =>
        directories.All(directory => Directory.Exists(directory) && !IsReparsePoint(directory));

    private static bool IsReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return false;
        }

        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static string GetArtifactBaseName(Guid artifactId) => $"artifact-{artifactId:N}";

    private static bool IsGeneratedArtifactFileName(string fileName)
    {
        const string prefix = "artifact-";
        const string completedSuffix = ".mp4";
        const string pendingSuffix = ".pending.mp4";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = fileName.EndsWith(pendingSuffix, StringComparison.OrdinalIgnoreCase)
            ? pendingSuffix
            : fileName.EndsWith(completedSuffix, StringComparison.OrdinalIgnoreCase)
                ? completedSuffix
                : string.Empty;
        if (suffix.Length == 0)
        {
            return false;
        }

        string identifier = fileName[prefix.Length..^suffix.Length];
        return Guid.TryParseExact(identifier, "N", out _);
    }

    private static bool IsContainedBy(string path, string parentWithSeparator) =>
        path.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
