using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace GlyphFrame.Infrastructure.Ffmpeg;

internal sealed record ProcessCleanupTiming(
    TimeSpan TerminationRequestWait,
    TimeSpan PostTerminationWait,
    TimeSpan OutputDrainWait)
{
    public static ProcessCleanupTiming Default { get; } = new(
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2));
}

internal sealed record ProcessCleanupResult(
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string TechnicalDetails);

internal interface IProcessTerminationController
{
    bool HasExited { get; }

    int ExitCode { get; }

    void KillEntireProcessTree();

    Task WaitForExitAsync(CancellationToken cancellationToken);
}

internal sealed class SystemProcessTerminationController(Process process) : IProcessTerminationController
{
    private readonly Process _process = process ?? throw new ArgumentNullException(nameof(process));

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public void KillEntireProcessTree() => _process.Kill(entireProcessTree: true);

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);
}

internal static class ProcessCancellationCleanup
{
    public static async Task<ProcessCleanupResult> CompleteAsync(
        IProcessTerminationController process,
        Task<string> standardOutputTask,
        Task<string> standardErrorTask,
        ProcessCleanupTiming timing)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(standardOutputTask);
        ArgumentNullException.ThrowIfNull(standardErrorTask);
        ArgumentNullException.ThrowIfNull(timing);

        List<string> diagnostics = [];
        bool exitedBeforeTermination = TryHasExited(process, diagnostics);
        if (exitedBeforeTermination)
        {
            diagnostics.Add("The process had already exited before termination was attempted.");
        }
        else
        {
            await TryKillProcessTreeBoundedAsync(
                process,
                timing.TerminationRequestWait,
                diagnostics).ConfigureAwait(false);
            await WaitForExitBoundedAsync(process, timing.PostTerminationWait, diagnostics).ConfigureAwait(false);
        }

        await DrainOutputBoundedAsync(
            standardOutputTask,
            standardErrorTask,
            timing.OutputDrainWait,
            diagnostics).ConfigureAwait(false);

        int? exitCode = TryGetExitCode(process, diagnostics);
        return new ProcessCleanupResult(
            exitCode,
            GetCompletedOutput(standardOutputTask),
            GetCompletedOutput(standardErrorTask),
            string.Join(" ", diagnostics));
    }

    private static bool TryHasExited(IProcessTerminationController process, ICollection<string> diagnostics)
    {
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            diagnostics.Add($"Could not determine process state: {exception.Message}");
            return false;
        }
    }

    private static async Task TryKillProcessTreeBoundedAsync(
        IProcessTerminationController process,
        TimeSpan timeout,
        ICollection<string> diagnostics)
    {
        Task terminationTask = Task.Run(process.KillEntireProcessTree);
        try
        {
            await terminationTask.WaitAsync(timeout).ConfigureAwait(false);
            diagnostics.Add("Process-tree termination was requested.");
        }
        catch (TimeoutException)
        {
            diagnostics.Add($"Process-tree termination did not complete within {timeout.TotalMilliseconds:F0} ms.");
            ObserveFaults(terminationTask);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException
            or ObjectDisposedException)
        {
            diagnostics.Add($"Process-tree termination failed: {exception.Message}");
        }
    }

    private static async Task WaitForExitBoundedAsync(
        IProcessTerminationController process,
        TimeSpan timeout,
        ICollection<string> diagnostics)
    {
        using CancellationTokenSource waitTimeout = new(timeout);
        try
        {
            await process.WaitForExitAsync(waitTimeout.Token).ConfigureAwait(false);
            diagnostics.Add("The process exited after termination was requested.");
        }
        catch (OperationCanceledException) when (waitTimeout.IsCancellationRequested)
        {
            diagnostics.Add($"The process did not exit within the {timeout.TotalMilliseconds:F0} ms post-termination wait.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            diagnostics.Add($"Post-termination exit wait failed: {exception.Message}");
        }
    }

    private static async Task DrainOutputBoundedAsync(
        Task<string> standardOutputTask,
        Task<string> standardErrorTask,
        TimeSpan timeout,
        ICollection<string> diagnostics)
    {
        Task combinedDrain = Task.WhenAll(standardOutputTask, standardErrorTask);
        try
        {
            await combinedDrain.WaitAsync(timeout).ConfigureAwait(false);
            diagnostics.Add("Redirected output streams were drained.");
        }
        catch (TimeoutException)
        {
            diagnostics.Add($"Redirected output did not finish draining within {timeout.TotalMilliseconds:F0} ms.");
            ObserveFaults(combinedDrain);
        }
        catch (OperationCanceledException exception)
        {
            diagnostics.Add($"Redirected output drain was cancelled: {exception.Message}");
            ObserveFaults(combinedDrain);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or ObjectDisposedException
            or DecoderFallbackException)
        {
            diagnostics.Add($"Redirected output drain failed: {exception.Message}");
            ObserveFaults(combinedDrain);
        }

        ObserveFaults(standardOutputTask);
        ObserveFaults(standardErrorTask);
    }

    private static int? TryGetExitCode(IProcessTerminationController process, ICollection<string> diagnostics)
    {
        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or ObjectDisposedException)
        {
            diagnostics.Add($"Exit code was unavailable: {exception.Message}");
            return null;
        }
    }

    private static string GetCompletedOutput(Task<string> outputTask) =>
        outputTask.IsCompletedSuccessfully ? outputTask.GetAwaiter().GetResult() : string.Empty;

    private static void ObserveFaults(Task task)
    {
        if (task.IsFaulted)
        {
            _ = task.Exception;
            return;
        }

        if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
