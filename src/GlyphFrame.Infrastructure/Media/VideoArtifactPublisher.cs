using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using Microsoft.Win32.SafeHandles;

namespace GlyphFrame.Infrastructure.Media;

/// <summary>Destination-side, bounded byte publication. The caller holds the session borrow
/// for the entire call. The last cancellation check precedes the atomic rename/replace;
/// once that succeeds, cancellation cannot undo or misreport the publication.</summary>
public class VideoArtifactPublisher(IManagedVideoArtifactStore store) : IVideoArtifactPublisher
{
    private const int BufferSize = 128 * 1024;
    private const int ErrorDiskFull = 0x70;
    private const int ErrorHandleDiskFull = 0x27;
    private const int ErrorAccessDenied = 0x05;

    public Task<VideoPublicationDestination> PrepareAsync(string outputPath, string originalInputPath,
        CancellationToken cancellationToken = default) => Task.Run(async () =>
        {
            string path = ValidateDestination(outputPath, originalInputPath);
            cancellationToken.ThrowIfCancellationRequested();
            return new VideoPublicationDestination(path, await ReadVersionAsync(path, cancellationToken).ConfigureAwait(false));
        }, cancellationToken);

    public Task<VideoExportResult> PublishAsync(VideoArtifactPublicationJob job,
        CancellationToken cancellationToken = default, IProgress<VideoExportProgress>? progress = null) =>
        Task.Run(() => PublishCoreAsync(job, cancellationToken, progress), CancellationToken.None);

    private async Task<VideoExportResult> PublishCoreAsync(VideoArtifactPublicationJob job,
        CancellationToken token, IProgress<VideoExportProgress>? progress)
    {
        string? partial = null;
        VideoExportResult result;
        Stopwatch elapsed = Stopwatch.StartNew();
        try
        {
            token.ThrowIfCancellationRequested();
            store.ValidateCompletedArtifact(job.Artifact, requireNonEmptyFile: true);
            string output = ValidateDestination(job.Destination.FilePath, job.OriginalInputPath);
            await CheckVersionAsync(job, output, token).ConfigureAwait(false);
            string partialTarget = Path.Combine(Path.GetDirectoryName(output)!, $".glyphframe-{Guid.NewGuid():N}.partial");
            await using (FileStream source = new(job.Artifact.FilePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream destination = new(partialTarget, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                // Only a successfully created file belongs to this operation.
                partial = partialTarget;
                long length = source.Length;
                if (length <= 0 || length != job.Artifact.FileSizeBytes)
                {
                    throw new IOException("The retained artifact size no longer matches its metadata.");
                }

                byte[] buffer = new byte[BufferSize];
                long copied = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    copied += count;
                    Report(VideoExportStage.CopyingArtifact, Math.Min(99, copied * 99d / length),
                        $"Copying reviewed video — {copied:N0} / {length:N0} bytes");
                }

                await destination.FlushAsync(token).ConfigureAwait(false);
                destination.Flush(flushToDisk: true);
            }

            Report(VideoExportStage.Finalizing, 99, "Publishing reviewed video...");
            await BeforeCommitAsync(token).ConfigureAwait(false);
            // Recheck aliases and the exact overwrite authorization after all asynchronous I/O.
            ValidateDestination(output, job.OriginalInputPath);
            using FileStream? existing = File.Exists(output)
                ? new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)
                : null;
            await CheckVersionAsync(job, output, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            Commit(partial, output, job.Destination.ExistingVersion is not null);
            partial = null;
            Report(VideoExportStage.Completed, 100, "Reviewed video exported.");
            result = VideoExportResult.Completed(output, job.Artifact.RenderedFrameCount,
                job.Artifact.OutputDimensions, job.Artifact.FramesPerSecond, job.Artifact.AudioOutcome);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            result = VideoExportResult.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or InvalidOperationException or Win32Exception)
        {
            result = VideoExportResult.Failure(ClassifyPublicationFailure(exception),
                "Could not export the reviewed video. Check available space, file access, and choose the destination again.",
                exception.ToString());
        }

        if (partial is not null)
        {
            try
            {
                DeletePartial(partial);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                VideoExportErrorCategory category = result.Status == VideoExportStatus.Cancelled
                    ? VideoExportErrorCategory.OutputFinalizationFailed
                    : result.ErrorCategory;
                result = VideoExportResult.Failure(category,
                    "Export did not complete and its temporary file could not be removed. Close applications using the destination and try again.",
                    $"{result.TechnicalDetail}\nOwned incomplete file: {partial}\n{exception}") with
                {
                    IncompleteOutputPath = partial
                };
            }
        }

        return result;

        void Report(VideoExportStage stage, double percentage, string description)
        {
            try
            {
                progress?.Report(new(stage, 0, null, percentage, elapsed.Elapsed, null, false, description));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException
                and not AccessViolationException)
            {
                Debug.WriteLine(exception);
            }
        }
    }

    // Narrow deterministic fault-injection seams; production performs no additional work here.
    protected virtual Task BeforeCommitAsync(CancellationToken token) => Task.CompletedTask;

    protected virtual void Commit(string partial, string output, bool replace)
    {
        if (replace)
        {
            File.Replace(partial, output, null);
        }
        else
        {
            File.Move(partial, output); // Never grants overwrite if a destination appeared meanwhile.
        }
    }

    protected virtual void DeletePartial(string path) => File.Delete(path);

    private static async Task CheckVersionAsync(VideoArtifactPublicationJob job, string path, CancellationToken token)
    {
        string? version = await ReadVersionAsync(path, token).ConfigureAwait(false);
        if (version != job.Destination.ExistingVersion || (version is not null && !job.Overwrite))
        {
            throw new DestinationConflictException(
                "The destination changed or overwrite was not confirmed. Select it again.");
        }
    }

    private static VideoExportErrorCategory ClassifyPublicationFailure(Exception exception) => exception switch
    {
        DestinationConflictException => VideoExportErrorCategory.DestinationConflict,
        UnauthorizedAccessException => VideoExportErrorCategory.DestinationAccessDenied,
        Win32Exception win32Exception when win32Exception.NativeErrorCode == ErrorAccessDenied
            => VideoExportErrorCategory.DestinationAccessDenied,
        IOException ioException when IsDiskFull(ioException.HResult)
            => VideoExportErrorCategory.InsufficientDiskSpace,
        Win32Exception win32Exception when IsDiskFull(win32Exception.NativeErrorCode)
            => VideoExportErrorCategory.InsufficientDiskSpace,
        _ => VideoExportErrorCategory.OutputFinalizationFailed
    };

    private static bool IsDiskFull(int errorCode) => (errorCode & 0xFFFF) is ErrorDiskFull or ErrorHandleDiskFull;

    private sealed class DestinationConflictException(string message) : IOException(message);

    private string ValidateDestination(string output, string original)
    {
        string path = Path.GetFullPath(output);
        string cache = Path.TrimEndingDirectorySeparator(Path.GetFullPath(store.CacheRoot));
        if (!Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            || path.Equals(Path.GetFullPath(original), StringComparison.OrdinalIgnoreCase)
            || path.Equals(cache, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(Path.GetDirectoryName(path)))
        {
            throw new IOException("Choose an MP4 destination outside the source and GlyphFrame cache, in an existing folder.");
        }

        for (string? ancestor = path; ancestor is not null; ancestor = Path.GetDirectoryName(ancestor))
        {
            if ((File.Exists(ancestor) || Directory.Exists(ancestor))
                && (File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Linked destination paths are not supported. Choose a regular folder and file.");
            }
        }

        // Resolve Windows short-name aliases by opening existing files and comparing their file IDs.
        if (File.Exists(path))
        {
            using FileStream target = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            FileIdentity identity = Identity(target);
            if (identity.Links > 1)
            {
                throw new IOException("Hard-linked destination files are not supported.");
            }
            if (File.Exists(original))
            {
                using FileStream input = File.Open(original, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                FileIdentity source = Identity(input);
                if (identity.Volume == source.Volume && identity.IndexHigh == source.IndexHigh && identity.IndexLow == source.IndexLow)
                {
                    throw new IOException("The destination is an alias of the original source.");
                }
            }
        }

        // Reject short-name aliases into the cache as well, including a not-yet-existing destination.
        using SafeFileHandle directory = CreateFile(Path.GetDirectoryName(path)!, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        char[] resolved = new char[32768];
        uint size = GetFinalPathNameByHandle(directory, resolved, (uint)resolved.Length, 0);
        if (size == 0 || size >= resolved.Length)
        {
            throw new IOException("Could not resolve the destination directory safely.");
        }
        string actual = new string(resolved, 0, (int)size);
        if (actual.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            actual = actual[4..];
        }
        if (actual.Equals(cache, StringComparison.OrdinalIgnoreCase)
            || actual.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The destination belongs to the managed artifact cache.");
        }
        return path;
    }

    private static async Task<string?> ReadVersionAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        await using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        FileIdentity identity = Identity(file);
        byte[] hash = await SHA256.HashDataAsync(file, token).ConfigureAwait(false);
        return $"{identity.Volume}:{identity.IndexHigh}:{identity.IndexLow}:{identity.WriteHigh}:{identity.WriteLow}:{Convert.ToHexString(hash)}";
    }

    private static FileIdentity Identity(FileStream file) => GetFileInformationByHandle(file.SafeFileHandle, out FileIdentity identity)
        ? identity : throw new IOException("Could not identify the destination file safely.");

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdentity
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileIdentity information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, [Out] char[] path, uint length, uint flags);
}
