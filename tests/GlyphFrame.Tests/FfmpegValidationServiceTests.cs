using System.Collections.Concurrent;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;

namespace GlyphFrame.Tests;

public sealed class FfmpegValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_ReportsBothValidToolsAndVersions()
    {
        RecordingRunner runner = CreateSuccessfulRunner();
        using FfmpegValidationService service = new(CreateLocator(), runner);

        FfmpegValidationResult result = await service.ValidateAsync();

        Assert.True(result.IsAvailable);
        Assert.Equal("Available", result.StatusLabel);
        Assert.Equal("9.0-test", result.Ffmpeg.Version);
        Assert.Equal("9.0-test", result.Ffprobe.Version);
        Assert.Contains("Bundled FFmpeg", result.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_CachesSuccessfulSessionResultUntilRefresh()
    {
        RecordingRunner runner = CreateSuccessfulRunner();
        using FfmpegValidationService service = new(CreateLocator(), runner);

        FfmpegValidationResult first = await service.ValidateAsync();
        FfmpegValidationResult second = await service.ValidateAsync();
        FfmpegValidationResult refreshed = await service.ValidateAsync(forceRefresh: true);

        Assert.Same(first, second);
        Assert.NotSame(first, refreshed);
        Assert.Equal(4, runner.ExecutionCount);
    }

    [Fact]
    public async Task ValidateAsync_MissingFfmpegIsFriendlyAndKeepsFfprobeStatus()
    {
        RecordingRunner runner = CreateSuccessfulRunner();
        StaticLocator locator = CreateLocator(ffmpegExists: false);
        using FfmpegValidationService service = new(locator, runner);

        FfmpegValidationResult result = await service.ValidateAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal(FfmpegToolValidationStatus.Missing, result.Ffmpeg.Status);
        Assert.True(result.Ffprobe.IsAvailable);
        Assert.Contains("ffmpeg.exe is missing", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_MissingFfprobeIsReportedIndependently()
    {
        using FfmpegValidationService service = new(CreateLocator(ffprobeExists: false), CreateSuccessfulRunner());

        FfmpegValidationResult result = await service.ValidateAsync();

        Assert.True(result.Ffmpeg.IsAvailable);
        Assert.Equal(FfmpegToolValidationStatus.Missing, result.Ffprobe.Status);
        Assert.Contains("ffprobe.exe is missing", result.Ffprobe.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_NonZeroExitPreservesDiagnostics()
    {
        RecordingRunner runner = CreateSuccessfulRunner();
        runner.Results["ffprobe.exe"] = Completed(7, string.Empty, "damaged executable");
        using FfmpegValidationService service = new(CreateLocator(), runner);

        FfmpegValidationResult result = await service.ValidateAsync();

        Assert.Equal(FfmpegToolValidationStatus.NonZeroExitCode, result.Ffprobe.Status);
        Assert.Contains("reported an error", result.Ffprobe.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exit code: 7", result.Ffprobe.TechnicalDetails, StringComparison.Ordinal);
        Assert.Contains("damaged executable", result.TechnicalDetails, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnexpectedVersionOutput()
    {
        RecordingRunner runner = CreateSuccessfulRunner();
        runner.Results["ffmpeg.exe"] = Completed(0, "not a version line", string.Empty);
        using FfmpegValidationService service = new(CreateLocator(), runner);

        FfmpegValidationResult result = await service.ValidateAsync();

        Assert.Equal(FfmpegToolValidationStatus.InvalidVersionOutput, result.Ffmpeg.Status);
        Assert.Contains("unexpected version response", result.Ffmpeg.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("not a version line", result.Ffmpeg.TechnicalDetails);
    }

    [Fact]
    public async Task ValidateAsync_ReportsTimeout()
    {
        RecordingRunner runner = CreateSuccessfulRunner();
        runner.Results["ffmpeg.exe"] = new ProcessExecutionResult(
            ProcessExecutionStatus.TimedOut, null, string.Empty, string.Empty, TimeSpan.FromSeconds(10), "killed tree");
        using FfmpegValidationService service = new(CreateLocator(), runner);

        FfmpegValidationResult result = await service.ValidateAsync();

        Assert.Equal(FfmpegToolValidationStatus.TimedOut, result.Ffmpeg.Status);
        Assert.Contains("did not respond in time", result.Ffmpeg.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("killed tree", result.Ffmpeg.TechnicalDetails, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProcessExecutionStatus.StartFailed, FfmpegToolValidationStatus.StartFailed, "could not be started")]
    [InlineData(ProcessExecutionStatus.AccessDenied, FfmpegToolValidationStatus.AccessDenied, "permission")]
    public async Task ValidateAsync_MapsProcessStartFailuresToFriendlyStatuses(
        ProcessExecutionStatus processStatus,
        FfmpegToolValidationStatus expectedStatus,
        string expectedMessage)
    {
        RecordingRunner runner = CreateSuccessfulRunner();
        runner.Results["ffprobe.exe"] = new ProcessExecutionResult(
            processStatus, null, string.Empty, string.Empty, TimeSpan.FromMilliseconds(2), "Win32 diagnostic");
        using FfmpegValidationService service = new(CreateLocator(), runner);

        FfmpegValidationResult result = await service.ValidateAsync();

        Assert.Equal(expectedStatus, result.Ffprobe.Status);
        Assert.Contains(expectedMessage, result.Ffprobe.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Win32 diagnostic", result.Ffprobe.TechnicalDetails, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_PropagatesCancellationWithoutCachingPartialResult()
    {
        DelayingRunner runner = new();
        using FfmpegValidationService service = new(CreateLocator(), runner);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ValidateAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    public void StatusPresentation_PropagatesAvailableVersionsAndPathsForSettings()
    {
        FfmpegValidationResult result = ValidationResult(available: true);

        FfmpegStatusPresentation presentation = FfmpegStatusPresentation.FromValidation(result);

        Assert.Equal("Available", presentation.Availability);
        Assert.Equal("9.0-test", presentation.FfmpegVersion);
        Assert.Equal("9.0-test", presentation.FfprobeVersion);
        Assert.Equal("C:\\app\\Tools\\ffmpeg", presentation.ToolsDirectory);
    }

    [Fact]
    public void StatusPresentation_PropagatesUnavailableStateWithoutRawExceptionAsMessage()
    {
        FfmpegValidationResult result = ValidationResult(available: false);

        FfmpegStatusPresentation presentation = FfmpegStatusPresentation.FromValidation(result);

        Assert.Equal("Unavailable", presentation.Availability);
        Assert.Equal("Unavailable", presentation.FfmpegVersion);
        Assert.Contains("missing", presentation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", presentation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("technical stack trace", presentation.TechnicalDetails, StringComparison.Ordinal);
    }

    private static RecordingRunner CreateSuccessfulRunner()
    {
        RecordingRunner runner = new();
        runner.Results["ffmpeg.exe"] = Completed(0, "ffmpeg version 9.0-test Copyright test", string.Empty);
        runner.Results["ffprobe.exe"] = Completed(0, string.Empty, "ffprobe version 9.0-test Copyright test");
        return runner;
    }

    private static StaticLocator CreateLocator(bool ffmpegExists = true, bool ffprobeExists = true) =>
        new(new BundledFfmpegLocation(
            "C:\\app",
            "C:\\app\\Tools\\ffmpeg",
            new BundledToolPath(FfmpegToolKind.Ffmpeg, "C:\\app\\Tools\\ffmpeg\\ffmpeg.exe", ffmpegExists, false),
            new BundledToolPath(FfmpegToolKind.Ffprobe, "C:\\app\\Tools\\ffmpeg\\ffprobe.exe", ffprobeExists, false)));

    private static ProcessExecutionResult Completed(int exitCode, string output, string error) =>
        new(ProcessExecutionStatus.Completed, exitCode, output, error, TimeSpan.FromMilliseconds(5));

    private static FfmpegValidationResult ValidationResult(bool available)
    {
        FfmpegToolValidationResult ffmpeg = available
            ? new(FfmpegToolKind.Ffmpeg, FfmpegToolValidationStatus.Available, "C:\\app\\Tools\\ffmpeg\\ffmpeg.exe", "9.0-test", "FFmpeg is available.", "ok")
            : new(FfmpegToolKind.Ffmpeg, FfmpegToolValidationStatus.Missing, "C:\\app\\Tools\\ffmpeg\\ffmpeg.exe", null, "FFmpeg is missing.", "technical stack trace");
        FfmpegToolValidationResult ffprobe = new(
            FfmpegToolKind.Ffprobe,
            FfmpegToolValidationStatus.Available,
            "C:\\app\\Tools\\ffmpeg\\ffprobe.exe",
            "9.0-test",
            "ffprobe is available.",
            "ok");
        return new FfmpegValidationResult("C:\\app\\Tools\\ffmpeg", ffmpeg, ffprobe, DateTimeOffset.UtcNow);
    }

    private sealed class StaticLocator(BundledFfmpegLocation location) : IBundledFfmpegLocator
    {
        public BundledFfmpegLocation Locate() => location;
    }

    private sealed class RecordingRunner : IProcessRunner
    {
        private int _executionCount;

        public ConcurrentDictionary<string, ProcessExecutionResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int ExecutionCount => _executionCount;

        public Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _executionCount);
            return Task.FromResult(Results[Path.GetFileName(request.ExecutablePath)]);
        }
    }

    private sealed class DelayingRunner : IProcessRunner
    {
        public async Task<ProcessExecutionResult> RunAsync(
            ProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
