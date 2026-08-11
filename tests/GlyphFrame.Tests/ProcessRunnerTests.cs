using System.Text;
using System.ComponentModel;
using System.Diagnostics;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new();

    [Fact]
    public async Task RunAsync_CapturesStandardOutputAndSuccessfulExitCode()
    {
        ProcessExecutionResult result = await RunPowerShellAsync("[Console]::Out.Write('ready')");

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ready", result.StandardOutput);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_CapturesStandardErrorAndNonZeroExitCode()
    {
        ProcessExecutionResult result = await RunPowerShellAsync("[Console]::Error.Write('failed'); exit 17");

        Assert.Equal(ProcessExecutionStatus.Completed, result.Status);
        Assert.Equal(17, result.ExitCode);
        Assert.Equal("failed", result.StandardError);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_PreservesArgumentsContainingSpacesAndUnicode()
    {
        using TemporaryMediaDirectory directory = new();
        string scriptPath = Path.Combine(directory.RootPath, "echo argument.ps1");
        string resultPath = Path.Combine(directory.RootPath, "argument result.txt");
        await File.WriteAllTextAsync(
            scriptPath,
            "[IO.File]::WriteAllText($args[1], $args[0], [Text.UTF8Encoding]::new($false))",
            Encoding.UTF8);

        ProcessExecutionResult result = await _runner.RunAsync(new ProcessExecutionRequest(
            PowerShellPath,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "space Ω 日本語", resultPath]));

        Assert.True(result.Succeeded);
        Assert.Equal("space Ω 日本語", await File.ReadAllTextAsync(resultPath, Encoding.UTF8));
    }

    [Fact]
    public async Task RunAsync_DoesNotDeadlockWhenBothOutputStreamsAreBusy()
    {
        const string script = "1..2000 | ForEach-Object { [Console]::Out.WriteLine(\"out$_\"); [Console]::Error.WriteLine(\"err$_\") }";

        ProcessExecutionResult result = await RunPowerShellAsync(script, TimeSpan.FromSeconds(15));

        Assert.True(result.Succeeded);
        Assert.Contains("out2000", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("err2000", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ReturnsTimedOutAndTerminatesProcess()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ProcessExecutionResult result = await RunPowerShellAsync("Start-Sleep -Seconds 30", TimeSpan.FromMilliseconds(250));
        stopwatch.Stop();

        Assert.Equal(ProcessExecutionStatus.TimedOut, result.Status);
        Assert.Contains("process-tree termination was requested", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Timeout returned after {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task RunAsync_CancellationThrowsOperationCancelled()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(250));
        Stopwatch stopwatch = Stopwatch.StartNew();

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() => _runner.RunAsync(
            CreatePowerShellRequest("Start-Sleep -Seconds 30", TimeSpan.FromSeconds(30)),
            cancellation.Token));
        stopwatch.Stop();

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Contains("cancelled", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Cancellation returned after {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task RunAsync_TimeoutTerminatesChildProcessTree()
    {
        using TemporaryMediaDirectory directory = new();
        string sentinel = Path.Combine(directory.RootPath, "child-completed.txt");
        string childScript = $"Start-Sleep -Milliseconds 1500; [IO.File]::WriteAllText('{EscapePowerShell(sentinel)}','done')";
        string childEncoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(childScript));
        string parentScript = $"Start-Process -FilePath '{EscapePowerShell(PowerShellPath)}' -ArgumentList '-NoProfile','-NonInteractive','-EncodedCommand','{childEncoded}' -PassThru | Out-Null; Start-Sleep -Seconds 30";

        ProcessExecutionResult result = await RunPowerShellAsync(parentScript, TimeSpan.FromMilliseconds(500));
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Equal(ProcessExecutionStatus.TimedOut, result.Status);
        Assert.False(File.Exists(sentinel));
    }

    [Fact]
    public async Task RunAsync_InvalidExecutableReturnsStructuredStartFailure()
    {
        using TemporaryMediaDirectory directory = new();

        ProcessExecutionResult result = await _runner.RunAsync(
            new ProcessExecutionRequest(Path.Combine(directory.RootPath, "missing.exe")));

        Assert.Equal(ProcessExecutionStatus.StartFailed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.TechnicalDetail));
    }

    [Fact]
    public async Task CancellationCleanup_TerminationAndOutputDrainCompleteSuccessfully()
    {
        FakeTerminationController process = new(hasExited: false)
        {
            KillAction = controller => controller.HasExitedValue = true
        };

        ProcessCleanupResult result = await ProcessCancellationCleanup.CompleteAsync(
            process,
            Task.FromResult("captured output"),
            Task.FromResult("captured error"),
            FastCleanupTiming);

        Assert.Equal(1, process.KillCount);
        Assert.Equal(1, process.WaitCount);
        Assert.Equal(23, result.ExitCode);
        Assert.Equal("captured output", result.StandardOutput);
        Assert.Equal("captured error", result.StandardError);
        Assert.Contains("process exited", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("streams were drained", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellationCleanup_HandlesProcessExitDuringTerminationRace()
    {
        FakeTerminationController process = new(hasExited: false)
        {
            KillAction = controller =>
            {
                controller.HasExitedValue = true;
                throw new InvalidOperationException("The process exited before Kill completed.");
            }
        };

        ProcessCleanupResult result = await ProcessCancellationCleanup.CompleteAsync(
            process,
            Task.FromResult(string.Empty),
            Task.FromResult(string.Empty),
            FastCleanupTiming);

        Assert.Equal(23, result.ExitCode);
        Assert.Equal(1, process.WaitCount);
        Assert.Contains("termination failed", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("process exited", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellationCleanup_FailedTerminationAndIncompleteStreamsRemainBounded()
    {
        TaskCompletionSource<string> output = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> error = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTerminationController process = new(hasExited: false)
        {
            KillAction = _ => throw new Win32Exception(5, "Access denied"),
            WaitForExitAsyncHandler = cancellationToken => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
        };
        Stopwatch stopwatch = Stopwatch.StartNew();

        ProcessCleanupResult result = await ProcessCancellationCleanup.CompleteAsync(
            process,
            output.Task,
            error.Task,
            FastCleanupTiming);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Cleanup took {stopwatch.Elapsed}.");
        Assert.Null(result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.Contains("termination failed", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("did not exit", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("did not finish draining", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);

        output.SetException(new IOException("late output failure"));
        error.SetException(new IOException("late error failure"));
        await Task.Yield();
    }

    [Fact]
    public async Task CancellationCleanup_IncompleteTerminationRequestDoesNotBlockCaller()
    {
        TaskCompletionSource releaseTermination = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTerminationController process = new(hasExited: false)
        {
            KillAction = _ => releaseTermination.Task.GetAwaiter().GetResult(),
            WaitForExitAsyncHandler = cancellationToken => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
        };
        Stopwatch stopwatch = Stopwatch.StartNew();

        ProcessCleanupResult result = await ProcessCancellationCleanup.CompleteAsync(
            process,
            Task.FromResult(string.Empty),
            Task.FromResult(string.Empty),
            FastCleanupTiming);
        stopwatch.Stop();
        releaseTermination.SetResult();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Cleanup took {stopwatch.Elapsed}.");
        Assert.Null(result.ExitCode);
        Assert.Contains("termination did not complete", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("did not exit", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellationCleanup_IncompleteSingleStreamPreservesCompletedOutputAndRemainsBounded()
    {
        TaskCompletionSource<string> error = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeTerminationController process = new(hasExited: true);
        Stopwatch stopwatch = Stopwatch.StartNew();

        ProcessCleanupResult result = await ProcessCancellationCleanup.CompleteAsync(
            process,
            Task.FromResult("available output"),
            error.Task,
            FastCleanupTiming);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Cleanup took {stopwatch.Elapsed}.");
        Assert.Equal("available output", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.Contains("did not finish draining", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
        error.SetResult("late error");
    }

    [Fact]
    public async Task CancellationCleanup_CancelledDrainDoesNotOverrideOriginalProcessSemantics()
    {
        FakeTerminationController process = new(hasExited: true);
        Task<string> cancelledOutput = Task.FromCanceled<string>(new CancellationToken(canceled: true));

        ProcessCleanupResult result = await ProcessCancellationCleanup.CompleteAsync(
            process,
            cancelledOutput,
            Task.FromResult("completed error"),
            FastCleanupTiming);

        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal("completed error", result.StandardError);
        Assert.Contains("drain was cancelled", result.TechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    private Task<ProcessExecutionResult> RunPowerShellAsync(string script, TimeSpan? timeout = null) =>
        _runner.RunAsync(CreatePowerShellRequest(script, timeout ?? TimeSpan.FromSeconds(10)));

    private static ProcessExecutionRequest CreatePowerShellRequest(string script, TimeSpan timeout) =>
        new(PowerShellPath, ["-NoProfile", "-NonInteractive", "-Command", script], timeout);

    private static string PowerShellPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static ProcessCleanupTiming FastCleanupTiming { get; } = new(
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(40));

    private sealed class FakeTerminationController(bool hasExited) : IProcessTerminationController
    {
        public bool HasExitedValue { get; set; } = hasExited;

        public int KillCount { get; private set; }

        public int WaitCount { get; private set; }

        public Action<FakeTerminationController>? KillAction { get; init; }

        public Func<CancellationToken, Task>? WaitForExitAsyncHandler { get; init; }

        public bool HasExited => HasExitedValue;

        public int ExitCode => HasExitedValue
            ? 23
            : throw new InvalidOperationException("The process is still running.");

        public void KillEntireProcessTree()
        {
            KillCount++;
            KillAction?.Invoke(this);
        }

        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitCount++;
            if (WaitForExitAsyncHandler is not null)
            {
                await WaitForExitAsyncHandler(cancellationToken);
                return;
            }

            HasExitedValue = true;
        }
    }
}
