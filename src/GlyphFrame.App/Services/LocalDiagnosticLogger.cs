using System.IO;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using GlyphFrame.Core.Interfaces;

namespace GlyphFrame.App.Services;

/// <summary>Accepts only catalog identifiers. Diagnostic text never crosses this boundary.</summary>
public interface IApplicationDiagnosticLogger
{
    void Record(ApplicationErrorCode code, ErrorPresentationContext context);
}

public sealed class LocalDiagnosticLogger : IApplicationDiagnosticLogger, IAsyncDisposable
{
    public const int MaximumFileBytes = 1_048_576;
    public const int MaximumRecordBytes = 1_024;
    public const int MaximumCompletedFiles = 10;
    public const long MaximumCompletedBytes = 10_485_760;
    public static readonly TimeSpan CompletedRetention = TimeSpan.FromDays(14);

    private static readonly Regex OwnedName = new(
        "^glyphframe-[0-9a-f]{32}-[0-9]{6}\\.log$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ActiveName = new(
        "^glyphframe-[0-9a-f]{32}-[0-9]{6}\\.active$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly string _directory;
    private readonly string _applicationDataDirectory;
    private readonly TimeProvider _clock;
    private readonly string _session = Guid.NewGuid().ToString("N");
    private readonly Channel<LogItem> _queue = Channel.CreateBounded<LogItem>(new BoundedChannelOptions(256)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly Task _worker;
    private FileStream? _active;
    private string? _activePath;
    private int _sequence;
    private int _disposed;

    public LocalDiagnosticLogger(IApplicationPaths paths, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _directory = paths.LogsDirectory;
        _applicationDataDirectory = paths.ApplicationDataDirectory;
        _clock = clock ?? TimeProvider.System;
        _worker = Task.Run(RunAsync);
    }

    public void Record(ApplicationErrorCode code, ErrorPresentationContext context)
    {
        if (Volatile.Read(ref _disposed) != 0 || !Enum.IsDefined(code) || !Enum.IsDefined(context))
        {
            return;
        }

        _queue.Writer.TryWrite(new LogItem(code, context, _clock.GetUtcNow(), null));
    }

    /// <summary>Test/diagnostic barrier; never needed on the UI thread.</summary>
    public async Task FlushAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            await _worker.ConfigureAwait(false);
            return;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await _queue.Writer.WriteAsync(new LogItem(default, default, default, completion))
                .ConfigureAwait(false);
            await completion.Task.ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            await _worker.ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _queue.Writer.TryComplete();
        }

        await _worker.ConfigureAwait(false);
    }

    private async Task RunAsync()
    {
        try
        {
            TryMaintainDirectory();
            await foreach (LogItem item in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (item.Completion is not null)
                {
                    if (_active is not null)
                    {
                        try
                        {
                            await _active.FlushAsync().ConfigureAwait(false);
                        }
                        catch (Exception exception) when (IsStorageFailure(exception))
                        {
                            AbandonActive();
                        }
                    }

                    item.Completion.TrySetResult();
                    continue;
                }

                try
                {
                    await AppendAsync(item).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsStorageFailure(exception))
                {
                    AbandonActive();
                }
            }
        }
        finally
        {
            try
            {
                CompleteActive();
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                AbandonActive();
            }
            TryMaintainDirectory();
        }
    }

    private async Task AppendAsync(LogItem item)
    {
        byte[] record = JsonSerializer.SerializeToUtf8Bytes(new LogRecord(
            item.Timestamp.ToUniversalTime().ToString("O"), item.Code.ToString(), item.Context.ToString()));
        if (record.Length + 1 > MaximumRecordBytes)
        {
            return;
        }

        if (_active is not null && _active.Length + record.Length + 1 > MaximumFileBytes)
        {
            CompleteActive();
            TryMaintainDirectory();
        }

        if (_active is null)
        {
            if (!IsOwnedDirectory())
            {
                return;
            }

            Directory.CreateDirectory(_directory);
            if (!IsOwnedDirectory())
            {
                return;
            }

            _activePath = Path.Combine(_directory, $"glyphframe-{_session}-{_sequence++:D6}.active");
            _active = new FileStream(_activePath, FileMode.CreateNew, FileAccess.Write,
                FileShare.Read | FileShare.Delete,
                4096, FileOptions.Asynchronous);
        }

        byte[] line = new byte[record.Length + 1];
        record.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        await _active.WriteAsync(line).ConfigureAwait(false);
        await _active.FlushAsync().ConfigureAwait(false);
    }

    private void CompleteActive()
    {
        FileStream? stream = _active;
        string? path = _activePath;
        _active = null;
        _activePath = null;
        if (stream is null || path is null)
        {
            return;
        }

        try
        {
            stream.Flush();
            // Rename while the writer still holds the file: recovery cannot mistake
            // the close-to-rename interval for an abandoned active file.
            File.Move(path, Path.ChangeExtension(path, ".log"));
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            // An uncompleted active file is recovered on the next launch.
        }
        finally
        {
            stream.Dispose();
        }
    }

    private void AbandonActive()
    {
        FileStream? stream = _active;
        string? path = _activePath;
        _active = null;
        _activePath = null;
        try
        {
            stream?.Dispose();
            if (path is not null)
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            // The next startup can remove this abandoned active file.
        }
    }

    private void TryMaintainDirectory()
    {
        try
        {
            if (!Directory.Exists(_directory) || !IsOwnedDirectory())
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(_directory, "glyphframe-*.active"))
            {
                if (path == _activePath || !ActiveName.IsMatch(Path.GetFileName(path)))
                {
                    continue;
                }

                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    // A live writer denies write access. Holding this handle also
                    // prevents two recovery workers from acting on the same orphan.
                    using FileStream orphan = new(path, FileMode.Open, FileAccess.ReadWrite,
                        FileShare.Delete);
                    File.Delete(path);
                }
                catch (Exception exception) when (IsStorageFailure(exception)) { }
            }

            List<FileInfo> completed = Directory.EnumerateFiles(_directory, "glyphframe-*.log")
                .Where(path => OwnedName.IsMatch(Path.GetFileName(path)))
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists && (file.Attributes & FileAttributes.ReparsePoint) == 0)
                .OrderBy(file => file.LastWriteTimeUtc)
                .ToList();
            DateTime expiry = _clock.GetUtcNow().UtcDateTime - CompletedRetention;
            long total = completed.Sum(file => file.Length);
            int count = completed.Count;
            foreach (FileInfo file in completed)
            {
                if (file.LastWriteTimeUtc >= expiry
                    && count <= MaximumCompletedFiles && total <= MaximumCompletedBytes)
                {
                    continue;
                }

                try
                {
                    long length = file.Length;
                    file.Delete();
                    count--;
                    total -= length;
                }
                catch (Exception exception) when (IsStorageFailure(exception)) { }
            }
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            // Local logging must never change a media workflow or prevent shutdown.
        }
    }

    private static bool IsStorageFailure(Exception exception) => exception is IOException
        or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;

    private bool IsOwnedDirectory() =>
        Path.GetFullPath(_directory).Equals(
            Path.Combine(Path.GetFullPath(_applicationDataDirectory), "logs"),
            StringComparison.OrdinalIgnoreCase)
        && (!Directory.Exists(_applicationDataDirectory)
            || (File.GetAttributes(_applicationDataDirectory) & FileAttributes.ReparsePoint) == 0)
        && (!Directory.Exists(_directory)
            || (File.GetAttributes(_directory) & FileAttributes.ReparsePoint) == 0);

    private sealed record LogRecord(string TimestampUtc, string Code, string Context);
    private sealed record LogItem(ApplicationErrorCode Code, ErrorPresentationContext Context,
        DateTimeOffset Timestamp, TaskCompletionSource? Completion);
}
