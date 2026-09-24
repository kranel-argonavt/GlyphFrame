using System.IO;
using System.Text;
using System.Text.Json;
using GlyphFrame.App.Services;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Infrastructure.Paths;

namespace GlyphFrame.App.Tests;

public sealed class LocalDiagnosticLoggerTests
{
    [Fact]
    public async Task Record_IsAllowlistedUtcAndNeverContainsDiagnosticSentinels()
    {
        using TestRoot root = new();
        DateTimeOffset now = new(2026, 9, 24, 12, 30, 0, TimeSpan.FromHours(2));
        await using (LocalDiagnosticLogger logger = root.CreateLogger(now))
        {
            ErrorPresentation presentation = ErrorPresentation.Create(
                ApplicationErrorCode.VideoDecodeFailed,
                ErrorPresentationContext.VideoRendering,
                "SECRET_PATH SECRET_EXCEPTION SECRET_STDERR SECRET_CLIPBOARD");
            logger.Record(presentation.Code, presentation.Context);
            await logger.FlushAsync();
        }

        byte[] bytes = File.ReadAllBytes(Assert.Single(Directory.GetFiles(root.Logs, "*.log")));
        Assert.InRange(bytes.Length, 1, LocalDiagnosticLogger.MaximumRecordBytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        string content = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("SECRET_", content, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(content);
        Assert.Equal(3, document.RootElement.EnumerateObject().Count());
        Assert.Equal("2026-09-24T10:30:00.0000000+00:00",
            document.RootElement.GetProperty("TimestampUtc").GetString());
        Assert.Equal("VideoDecodeFailed", document.RootElement.GetProperty("Code").GetString());
        Assert.Equal("VideoRendering", document.RootElement.GetProperty("Context").GetString());
    }

    [Fact]
    public async Task RotationOccursBeforeAppendExceedsExactOneMibLimit()
    {
        using TestRoot root = new();
        LocalDiagnosticLogger logger = root.CreateLogger(DateTimeOffset.UtcNow);
        logger.Record(ApplicationErrorCode.FrameDecodeFailed, ErrorPresentationContext.VideoFramePreview);
        await logger.FlushAsync();
        string active = Assert.Single(Directory.GetFiles(root.Logs, "*.active"));
        int recordBytes = checked((int)new FileInfo(active).Length);
        int needed = LocalDiagnosticLogger.MaximumFileBytes / recordBytes + 2;
        for (int index = 1; index < needed; index++)
        {
            logger.Record(ApplicationErrorCode.FrameDecodeFailed, ErrorPresentationContext.VideoFramePreview);
            if (index % 100 == 0)
            {
                await logger.FlushAsync();
            }
        }

        await logger.DisposeAsync();
        string[] files = Directory.GetFiles(root.Logs, "*.log");
        Assert.Equal(2, files.Length);
        Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1,
            LocalDiagnosticLogger.MaximumFileBytes));
        Assert.Equal(needed, files.Sum(file => File.ReadLines(file).Count()));
    }

    [Fact]
    public async Task MaintenanceExpiresUtcOldFilesAndPreservesNonOwnedFiles()
    {
        using TestRoot root = new();
        DateTimeOffset now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(root.Logs);
        string expired = root.Completed(0);
        string retained = root.Completed(1);
        string foreign = Path.Combine(root.Logs, "other-application.log");
        File.WriteAllText(expired, "old\n");
        File.WriteAllText(retained, "new\n");
        File.WriteAllText(foreign, "private\n");
        File.SetLastWriteTimeUtc(expired, now.UtcDateTime.AddDays(-15));
        File.SetLastWriteTimeUtc(retained, now.UtcDateTime.AddDays(-13));
        await using (LocalDiagnosticLogger logger = root.CreateLogger(now))
        {
            await logger.FlushAsync();
        }

        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(retained));
        Assert.True(File.Exists(foreign));
    }

    [Fact]
    public async Task MaintenanceRemovesOldestForCompletedCountAndTotalBytes()
    {
        using TestRoot root = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(root.Logs);
        for (int index = 0; index < 11; index++)
        {
            string path = root.Completed(index);
            File.WriteAllBytes(path, new byte[LocalDiagnosticLogger.MaximumFileBytes]);
            File.SetLastWriteTimeUtc(path, now.UtcDateTime.AddMinutes(index - 30));
        }

        await using (LocalDiagnosticLogger logger = root.CreateLogger(now))
        {
            await logger.FlushAsync();
        }

        string[] remaining = Directory.GetFiles(root.Logs, "*.log");
        Assert.Equal(LocalDiagnosticLogger.MaximumCompletedFiles, remaining.Length);
        Assert.False(File.Exists(root.Completed(0)));
        Assert.Equal(LocalDiagnosticLogger.MaximumCompletedBytes,
            remaining.Sum(path => new FileInfo(path).Length));
    }

    [Fact]
    public async Task MaintenanceEnforcesCompletedByteLimitIndependentlyOfFileCount()
    {
        using TestRoot root = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(root.Logs);
        string older = root.Completed(0);
        string newer = root.Completed(1);
        File.WriteAllBytes(older, new byte[6_000_000]);
        File.WriteAllBytes(newer, new byte[6_000_000]);
        File.SetLastWriteTimeUtc(older, now.UtcDateTime.AddHours(-2));
        File.SetLastWriteTimeUtc(newer, now.UtcDateTime.AddHours(-1));

        await using (LocalDiagnosticLogger logger = root.CreateLogger(now))
        {
            await logger.FlushAsync();
        }

        Assert.False(File.Exists(older));
        Assert.True(File.Exists(newer));
        Assert.True(new FileInfo(newer).Length <= LocalDiagnosticLogger.MaximumCompletedBytes);
    }

    [Fact]
    public async Task ConcurrentInstancesKeepSeparateActiveFilesAndDoNotDeleteOneAnother()
    {
        using TestRoot root = new();
        await using LocalDiagnosticLogger first = root.CreateLogger(DateTimeOffset.UtcNow);
        first.Record(ApplicationErrorCode.InvalidImage, ErrorPresentationContext.MediaInput);
        await first.FlushAsync();
        string stale = root.Active(0);
        File.WriteAllText(stale, "abandoned partial");
        await using LocalDiagnosticLogger second = root.CreateLogger(DateTimeOffset.UtcNow);
        second.Record(ApplicationErrorCode.FrameDecodeFailed, ErrorPresentationContext.VideoFramePreview);
        await second.FlushAsync();

        string[] active = Directory.GetFiles(root.Logs, "*.active");
        Assert.Equal(2, active.Length);
        Assert.NotEqual(active[0], active[1]);
        Assert.False(File.Exists(stale));
        await second.DisposeAsync();
        Assert.True(File.Exists(Assert.Single(Directory.GetFiles(root.Logs, "*.active"))));
        await first.DisposeAsync();
        Assert.Equal(2, Directory.GetFiles(root.Logs, "*.log").Length);
    }

    [Fact]
    public async Task LockedAbandonedEntryDoesNotStopOtherOrphanRecovery()
    {
        using TestRoot root = new();
        Directory.CreateDirectory(root.Logs);
        string locked = root.Active(0);
        string removable = root.Active(1);
        File.WriteAllText(locked, "locked");
        File.WriteAllText(removable, "stale");
        using FileStream held = new(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

        await using (LocalDiagnosticLogger logger = root.CreateLogger(DateTimeOffset.UtcNow))
        {
            await logger.FlushAsync();
        }

        Assert.True(File.Exists(locked));
        Assert.False(File.Exists(removable));
    }

    [Fact]
    public async Task UnavailableDirectoryDoesNotThrowOrChangeTheFailure()
    {
        using TestRoot root = new();
        Directory.CreateDirectory(Path.GetDirectoryName(root.Logs)!);
        File.WriteAllText(root.Logs, "occupied by a file");
        ErrorPresentation original = ErrorPresentation.Create(
            ApplicationErrorCode.InputNotFound, ErrorPresentationContext.MediaInput,
            "source C:\\secret\\video.mkv");
        await using LocalDiagnosticLogger logger = root.CreateLogger(DateTimeOffset.UtcNow);

        logger.Record(original.Code, original.Context);
        await logger.FlushAsync();

        Assert.Equal(ApplicationErrorCode.InputNotFound, original.Code);
        Assert.True(File.Exists(root.Logs));
    }

    [Fact]
    public async Task NonOwnedDirectoryIsNeverWrittenOrCleaned()
    {
        using TestRoot root = new();
        string outside = Path.Combine(Path.GetDirectoryName(root.Logs)!, "other-logs");
        Directory.CreateDirectory(outside);
        string apparentLog = Path.Combine(outside,
            "glyphframe-00000000000000000000000000000001-000000.log");
        File.WriteAllText(apparentLog, "do not touch");
        await using LocalDiagnosticLogger logger = new(
            new RedirectedPaths(Path.GetDirectoryName(root.Logs)!, outside));

        logger.Record(ApplicationErrorCode.InvalidImage, ErrorPresentationContext.MediaInput);
        await logger.FlushAsync();

        Assert.Equal("do not touch", File.ReadAllText(apparentLog));
        Assert.Single(Directory.GetFiles(outside));
    }

    private sealed class TestRoot : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"GlyphFrameLogs-{Guid.NewGuid():N}");

        public string Logs => Path.Combine(_path, "GlyphFrame", "logs");

        public LocalDiagnosticLogger CreateLogger(DateTimeOffset now) => new(
            new ApplicationPaths(_path), new FixedClock(now));

        public string Completed(int index) => Path.Combine(Logs,
            $"glyphframe-00000000000000000000000000000001-{index:D6}.log");

        public string Active(int index) => Path.Combine(Logs,
            $"glyphframe-00000000000000000000000000000001-{index:D6}.active");

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, recursive: true);
            }
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RedirectedPaths(string applicationDataDirectory, string logsDirectory) : IApplicationPaths
    {
        public string ApplicationDataDirectory => applicationDataDirectory;

        public string SettingsFilePath => Path.Combine(applicationDataDirectory, "settings.json");

        public string LogsDirectory => logsDirectory;

        public void EnsureDirectories() { }
    }
}
