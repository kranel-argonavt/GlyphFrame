using System.IO;
using System.Runtime.ExceptionServices;

namespace GlyphFrame.App.Services;

/// <summary>
/// Joins asynchronous application lifetime work at WPF's synchronous exit boundary.
/// </summary>
public sealed class ApplicationShutdownCoordinator(Action<string>? diagnosticWriter = null)
{
    private readonly Action<string> _diagnosticWriter = diagnosticWriter ?? (_ => { });
    private int _shutdownStarted;

    /// <summary>
    /// Cancels startup recovery, waits for it to stop, then disposes the application lifetime owner.
    /// </summary>
    /// <returns><see langword="true"/> only for the call that performed shutdown.</returns>
    public bool Shutdown(
        CancellationTokenSource? recoveryCancellation,
        Task? recoveryTask,
        IAsyncDisposable? lifetimeOwner)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return false;
        }

        ExceptionDispatchInfo? deferredFailure = null;
        CancellationToken recoveryToken = recoveryCancellation?.Token ?? CancellationToken.None;

        try
        {
            ExecuteStep(
                () => recoveryCancellation?.Cancel(),
                "Managed artifact recovery cancellation failed.",
                ref deferredFailure);

            if (recoveryTask is not null)
            {
                try
                {
                    recoveryTask.ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (recoveryToken.IsCancellationRequested)
                {
                    // Shutdown requested the cancellation; recovery has now stopped.
                }
                catch (Exception exception) when (!IsFatal(exception))
                {
                    CaptureOrReport(
                        exception,
                        "Managed artifact recovery did not finish cleanly.",
                        ref deferredFailure);
                }
            }

            if (lifetimeOwner is not null)
            {
                ExecuteStep(
                    () => lifetimeOwner.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult(),
                    "Application service disposal did not finish cleanly.",
                    ref deferredFailure);
            }
        }
        finally
        {
            ExecuteStep(
                () => recoveryCancellation?.Dispose(),
                "Managed artifact recovery cancellation disposal failed.",
                ref deferredFailure);
        }

        deferredFailure?.Throw();
        return true;
    }

    private void ExecuteStep(
        Action action,
        string context,
        ref ExceptionDispatchInfo? deferredFailure)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (!IsFatal(exception))
        {
            CaptureOrReport(exception, context, ref deferredFailure);
        }
    }

    private void CaptureOrReport(
        Exception exception,
        string context,
        ref ExceptionDispatchInfo? deferredFailure)
    {
        if (IsExpectedShutdownFailure(exception))
        {
            _diagnosticWriter($"{context} {exception}");
            return;
        }

        if (deferredFailure is null)
        {
            deferredFailure = ExceptionDispatchInfo.Capture(exception);
        }
        else
        {
            _diagnosticWriter($"{context} A secondary failure occurred: {exception}");
        }
    }

    private static bool IsExpectedShutdownFailure(Exception exception) => exception is
        IOException
        or UnauthorizedAccessException
        or ArgumentException
        or InvalidOperationException
        or NotSupportedException
        or System.ComponentModel.Win32Exception
        or OperationCanceledException;

    private static bool IsFatal(Exception exception) => exception is
        OutOfMemoryException
        or StackOverflowException
        or AccessViolationException;
}
