using System.ComponentModel;
using System.Diagnostics;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Infrastructure.Ffmpeg;

public sealed class ProcessRunner : IProcessRunner
{
    private readonly ProcessCleanupTiming _cleanupTiming;

    public ProcessRunner()
        : this(ProcessCleanupTiming.Default)
    {
    }

    internal ProcessRunner(ProcessCleanupTiming cleanupTiming)
    {
        _cleanupTiming = cleanupTiming ?? throw new ArgumentNullException(nameof(cleanupTiming));
    }

    public async Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
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
        ProcessStartOutcome startOutcome = await Task.Run(() => TryStart(process)).ConfigureAwait(false);
        if (!startOutcome.Started)
        {
            stopwatch.Stop();
            return new ProcessExecutionResult(
                startOutcome.Status,
                null,
                string.Empty,
                string.Empty,
                stopwatch.Elapsed,
                startOutcome.TechnicalDetail);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeoutSource = new(request.Timeout);
        using CancellationTokenSource linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedSource.IsCancellationRequested)
        {
            ProcessCleanupResult cleanup = await ProcessCancellationCleanup.CompleteAsync(
                new SystemProcessTerminationController(process),
                outputTask,
                errorTask,
                _cleanupTiming).ConfigureAwait(false);
            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    $"Process execution was cancelled. {cleanup.TechnicalDetails}",
                    cancellationToken);
            }

            return new ProcessExecutionResult(
                ProcessExecutionStatus.TimedOut,
                cleanup.ExitCode,
                cleanup.StandardOutput,
                cleanup.StandardError,
                stopwatch.Elapsed,
                $"The process exceeded the {request.Timeout} timeout. {cleanup.TechnicalDetails}");
        }

        string[] redirectedOutput = await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        stopwatch.Stop();
        return new ProcessExecutionResult(
            ProcessExecutionStatus.Completed,
            process.ExitCode,
            redirectedOutput[0],
            redirectedOutput[1],
            stopwatch.Elapsed);
    }

    private static ProcessStartInfo CreateStartInfo(ProcessExecutionRequest request)
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
                : new ProcessStartOutcome(
                    false,
                    ProcessExecutionStatus.StartFailed,
                    "The operating system did not start the process.");
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

    private readonly record struct ProcessStartOutcome(
        bool Started,
        ProcessExecutionStatus Status,
        string? TechnicalDetail);
}
