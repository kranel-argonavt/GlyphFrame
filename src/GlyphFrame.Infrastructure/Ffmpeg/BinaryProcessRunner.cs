using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Ffmpeg;

/// <summary>
/// Runs processes that emit binary standard output while draining standard error concurrently.
/// Captured data is bounded even when the child process produces unexpected output.
/// </summary>
public sealed class BinaryProcessRunner : IBinaryProcessRunner
{
    private readonly ProcessCleanupTiming _cleanupTiming;

    public BinaryProcessRunner()
        : this(ProcessCleanupTiming.Default)
    {
    }

    internal BinaryProcessRunner(ProcessCleanupTiming cleanupTiming)
    {
        _cleanupTiming = cleanupTiming ?? throw new ArgumentNullException(nameof(cleanupTiming));
    }

    public async Task<BinaryProcessExecutionResult> RunAsync(
        BinaryProcessExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using Process process = new()
        {
            StartInfo = CreateStartInfo(request),
            EnableRaisingEvents = true
        };
        Stopwatch stopwatch = Stopwatch.StartNew();
        ProcessStartOutcome start = await Task.Run(() => TryStart(process)).ConfigureAwait(false);
        if (!start.Started)
        {
            stopwatch.Stop();
            return new BinaryProcessExecutionResult(
                start.Status,
                null,
                [],
                string.Empty,
                stopwatch.Elapsed,
                TechnicalDetail: start.TechnicalDetail);
        }

        Task<BinaryCapture> outputTask = ReadBinaryBoundedAsync(
            process.StandardOutput.BaseStream,
            request.MaximumStandardOutputBytes);
        Task<TextCapture> errorTask = ReadTextBoundedAsync(
            process.StandardError,
            request.MaximumStandardErrorCharacters);
        using CancellationTokenSource timeoutSource = new(request.Timeout);
        using CancellationTokenSource linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedSource.IsCancellationRequested)
        {
            string cleanupDetail = await TerminateAndDrainBoundedAsync(process, outputTask, errorTask)
                .ConfigureAwait(false);
            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    $"Binary process execution was cancelled. {cleanupDetail}",
                    cancellationToken);
            }

            BinaryCapture timedOutOutput = GetCompletedOutput(outputTask);
            TextCapture timedOutError = GetCompletedError(errorTask);
            return new BinaryProcessExecutionResult(
                ProcessExecutionStatus.TimedOut,
                TryGetExitCode(process),
                timedOutOutput.Bytes,
                timedOutError.Text,
                stopwatch.Elapsed,
                timedOutOutput.ExceededLimit,
                timedOutError.WasTruncated,
                $"The process exceeded the {request.Timeout} timeout. {cleanupDetail}");
        }

        string? drainDetail = null;
        try
        {
            await Task.WhenAll(outputTask, errorTask)
                .WaitAsync(_cleanupTiming.OutputDrainWait)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            drainDetail = $"Redirected output did not finish draining within {_cleanupTiming.OutputDrainWait.TotalMilliseconds:F0} ms.";
            ObserveFaults(outputTask);
            ObserveFaults(errorTask);
        }

        stopwatch.Stop();
        BinaryCapture output = GetCompletedOutput(outputTask);
        TextCapture error = GetCompletedError(errorTask);
        return new BinaryProcessExecutionResult(
            ProcessExecutionStatus.Completed,
            process.ExitCode,
            output.Bytes,
            error.Text,
            stopwatch.Elapsed,
            output.ExceededLimit,
            error.WasTruncated,
            drainDetail);
    }

    private async Task<string> TerminateAndDrainBoundedAsync(
        Process process,
        Task<BinaryCapture> outputTask,
        Task<TextCapture> errorTask)
    {
        List<string> diagnostics = [];
        if (!TryHasExited(process))
        {
            Task killTask = Task.Run(() => process.Kill(entireProcessTree: true));
            try
            {
                await killTask.WaitAsync(_cleanupTiming.TerminationRequestWait).ConfigureAwait(false);
                diagnostics.Add("Process-tree termination was requested.");
            }
            catch (TimeoutException)
            {
                diagnostics.Add("Process-tree termination did not complete within its bound.");
                ObserveFaults(killTask);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Win32Exception
                or NotSupportedException
                or ObjectDisposedException)
            {
                diagnostics.Add($"Process-tree termination failed: {exception.Message}");
            }

            using CancellationTokenSource exitWait = new(_cleanupTiming.PostTerminationWait);
            try
            {
                await process.WaitForExitAsync(exitWait.Token).ConfigureAwait(false);
                diagnostics.Add("The process exited after termination was requested.");
            }
            catch (OperationCanceledException) when (exitWait.IsCancellationRequested)
            {
                diagnostics.Add("The process did not exit within the post-termination bound.");
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Win32Exception
                or ObjectDisposedException)
            {
                diagnostics.Add($"Post-termination wait failed: {exception.Message}");
            }
        }
        else
        {
            diagnostics.Add("The process had already exited.");
        }

        try
        {
            await Task.WhenAll(outputTask, errorTask)
                .WaitAsync(_cleanupTiming.OutputDrainWait)
                .ConfigureAwait(false);
            diagnostics.Add("Redirected streams were drained.");
        }
        catch (TimeoutException)
        {
            diagnostics.Add("Redirected streams did not finish draining within their bound.");
            ObserveFaults(outputTask);
            ObserveFaults(errorTask);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or ObjectDisposedException
            or DecoderFallbackException)
        {
            diagnostics.Add($"Redirected stream draining failed: {exception.Message}");
            ObserveFaults(outputTask);
            ObserveFaults(errorTask);
        }

        return string.Join(" ", diagnostics);
    }

    private static async Task<BinaryCapture> ReadBinaryBoundedAsync(Stream stream, int maximumBytes)
    {
        byte[] buffer = new byte[81_920];
        using MemoryStream captured = new(Math.Min(maximumBytes, 1_048_576));
        bool exceeded = false;
        while (true)
        {
            int read = await stream.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            int remaining = maximumBytes - checked((int)captured.Length);
            if (remaining > 0)
            {
                int toCapture = Math.Min(remaining, read);
                await captured.WriteAsync(buffer.AsMemory(0, toCapture)).ConfigureAwait(false);
                exceeded |= toCapture < read;
            }
            else
            {
                exceeded = true;
            }
        }

        return new BinaryCapture(captured.ToArray(), exceeded);
    }

    private static async Task<TextCapture> ReadTextBoundedAsync(StreamReader reader, int maximumCharacters)
    {
        char[] buffer = new char[4096];
        StringBuilder captured = new(Math.Min(maximumCharacters, 4096));
        bool truncated = false;
        while (true)
        {
            int read = await reader.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            int remaining = maximumCharacters - captured.Length;
            if (remaining > 0)
            {
                int toCapture = Math.Min(remaining, read);
                captured.Append(buffer, 0, toCapture);
                truncated |= toCapture < read;
            }
            else
            {
                truncated = true;
            }
        }

        return new TextCapture(captured.ToString(), truncated);
    }

    private static ProcessStartInfo CreateStartInfo(BinaryProcessExecutionRequest request)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static ProcessStartOutcome TryStart(Process process)
    {
        try
        {
            return process.Start()
                ? new ProcessStartOutcome(true, ProcessExecutionStatus.Completed, null)
                : new ProcessStartOutcome(false, ProcessExecutionStatus.StartFailed, "The operating system did not start the process.");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new ProcessStartOutcome(false, ProcessExecutionStatus.AccessDenied, exception.ToString());
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 5)
        {
            return new ProcessStartOutcome(false, ProcessExecutionStatus.AccessDenied, exception.ToString());
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return new ProcessStartOutcome(false, ProcessExecutionStatus.StartFailed, exception.ToString());
        }
    }

    private static bool TryHasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            return false;
        }
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            return null;
        }
    }

    private static BinaryCapture GetCompletedOutput(Task<BinaryCapture> task) =>
        task.IsCompletedSuccessfully ? task.GetAwaiter().GetResult() : new BinaryCapture([], false);

    private static TextCapture GetCompletedError(Task<TextCapture> task) =>
        task.IsCompletedSuccessfully ? task.GetAwaiter().GetResult() : new TextCapture(string.Empty, false);

    private static void ObserveFaults(Task task)
    {
        if (task.IsFaulted)
        {
            _ = task.Exception;
        }
        else if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private sealed record BinaryCapture(byte[] Bytes, bool ExceededLimit);

    private sealed record TextCapture(string Text, bool WasTruncated);

    private readonly record struct ProcessStartOutcome(
        bool Started,
        ProcessExecutionStatus Status,
        string? TechnicalDetail);
}
