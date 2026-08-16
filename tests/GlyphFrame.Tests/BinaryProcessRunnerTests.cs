using System.Diagnostics;
using System.Text;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class BinaryProcessRunnerTests
{
    private readonly BinaryProcessRunner _runner = new();

    [Fact]
    public async Task RunAsync_CapturesBinaryOutputAndConcurrentStandardError()
    {
        const string script = "[Console]::OpenStandardOutput().Write([byte[]](1,2,3,254),0,4); [Console]::Error.Write('diagnostic')";

        BinaryProcessExecutionResult result = await RunPowerShellAsync(script, maximumBytes: 16);

        Assert.True(result.Succeeded, result.TechnicalDetail);
        Assert.Equal([1, 2, 3, 254], result.StandardOutput);
        Assert.Equal("diagnostic", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_DrainsBusyBinaryAndErrorStreamsWithoutDeadlock()
    {
        const string script = "$o=[Console]::OpenStandardOutput(); 1..2000 | % { $o.WriteByte([byte]($_%251)); [Console]::Error.WriteLine(\"err$_\") }";

        BinaryProcessExecutionResult result = await RunPowerShellAsync(
            script,
            maximumBytes: 2500,
            timeout: TimeSpan.FromSeconds(15),
            maximumErrorCharacters: 40_000);

        Assert.True(result.Succeeded, result.TechnicalDetail);
        Assert.Equal(2000, result.StandardOutput.Length);
        Assert.Contains("err2000", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_BoundsUnexpectedOutputWhileStillDrainingProcess()
    {
        const string script = "$o=[Console]::OpenStandardOutput(); 1..128 | % { $o.WriteByte([byte]$_) }";

        BinaryProcessExecutionResult result = await RunPowerShellAsync(script, maximumBytes: 16);

        Assert.True(result.Succeeded);
        Assert.Equal(16, result.StandardOutput.Length);
        Assert.True(result.StandardOutputExceededLimit);
    }

    [Fact]
    public async Task RunAsync_PreservesUnicodeAndSpacesAsOneArgument()
    {
        using TemporaryMediaDirectory media = new("бінарний процес 日本語");
        string output = Path.Combine(media.RootPath, "аргумент.txt");
        string scriptPath = Path.Combine(media.RootPath, "запис аргументу.ps1");
        string value = "відео-日本語-测试 with spaces";
        const string script = "[IO.File]::WriteAllText($args[1],$args[0],[Text.UTF8Encoding]::new($false))";
        await File.WriteAllTextAsync(scriptPath, script, Encoding.UTF8);
        BinaryProcessExecutionRequest request = new(
            PowerShellPath,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath, value, output],
            TimeSpan.FromSeconds(10),
            16);

        BinaryProcessExecutionResult result = await _runner.RunAsync(request);

        Assert.True(result.Succeeded, result.TechnicalDetail);
        Assert.Equal(value, await File.ReadAllTextAsync(output, Encoding.UTF8));
    }

    [Fact]
    public async Task RunAsync_ReturnsNonZeroExitAndCapturedError()
    {
        BinaryProcessExecutionResult result = await RunPowerShellAsync(
            "[Console]::Error.Write('failed'); exit 17",
            maximumBytes: 16);

        Assert.Equal(ProcessExecutionStatus.Completed, result.Status);
        Assert.Equal(17, result.ExitCode);
        Assert.Equal("failed", result.StandardError);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_TimeoutIsBoundedAndTerminatesProcessTree()
    {
        using TemporaryMediaDirectory media = new();
        string sentinel = Path.Combine(media.RootPath, "child-finished.txt");
        string childScript = $"Start-Sleep -Milliseconds 1500; [IO.File]::WriteAllText('{Escape(sentinel)}','orphan')";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(childScript));
        string parent = $"Start-Process -FilePath '{Escape(PowerShellPath)}' -ArgumentList '-NoProfile','-NonInteractive','-EncodedCommand','{encoded}' -WindowStyle Hidden; Start-Sleep -Seconds 30";
        Stopwatch stopwatch = Stopwatch.StartNew();

        BinaryProcessExecutionResult result = await RunPowerShellAsync(
            parent,
            maximumBytes: 16,
            timeout: TimeSpan.FromMilliseconds(400));
        stopwatch.Stop();
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.Equal(ProcessExecutionStatus.TimedOut, result.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"Timeout took {stopwatch.Elapsed}.");
        Assert.False(File.Exists(sentinel), "The child process survived timeout cleanup.");
    }

    [Fact]
    public async Task RunAsync_ExternalCancellationThrowsAndLeavesNoChildProcess()
    {
        using TemporaryMediaDirectory media = new();
        string sentinel = Path.Combine(media.RootPath, "cancel-child-finished.txt");
        string childScript = $"Start-Sleep -Milliseconds 1500; [IO.File]::WriteAllText('{Escape(sentinel)}','orphan')";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(childScript));
        string parent = $"Start-Process -FilePath '{Escape(PowerShellPath)}' -ArgumentList '-NoProfile','-NonInteractive','-EncodedCommand','{encoded}' -WindowStyle Hidden; Start-Sleep -Seconds 30";
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(400));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _runner.RunAsync(
            CreateRequest(parent, 16, TimeSpan.FromSeconds(30)),
            cancellation.Token));
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.False(File.Exists(sentinel), "The child process survived cancellation cleanup.");
    }

    [Fact]
    public async Task RunAsync_MissingExecutableReturnsStructuredStartFailure()
    {
        using TemporaryMediaDirectory media = new();

        BinaryProcessExecutionResult result = await _runner.RunAsync(new BinaryProcessExecutionRequest(
            Path.Combine(media.RootPath, "missing.exe"),
            [],
            TimeSpan.FromSeconds(2),
            16));

        Assert.Equal(ProcessExecutionStatus.StartFailed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.TechnicalDetail));
    }

    private Task<BinaryProcessExecutionResult> RunPowerShellAsync(
        string script,
        int maximumBytes,
        TimeSpan? timeout = null,
        int maximumErrorCharacters = 32_768) =>
        _runner.RunAsync(new BinaryProcessExecutionRequest(
            PowerShellPath,
            ["-NoProfile", "-NonInteractive", "-Command", script],
            timeout ?? TimeSpan.FromSeconds(10),
            maximumBytes,
            maximumErrorCharacters));

    private static BinaryProcessExecutionRequest CreateRequest(
        string script,
        int maximumBytes,
        TimeSpan timeout) => new(
            PowerShellPath,
            ["-NoProfile", "-NonInteractive", "-Command", script],
            timeout,
            maximumBytes);

    private static string PowerShellPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
