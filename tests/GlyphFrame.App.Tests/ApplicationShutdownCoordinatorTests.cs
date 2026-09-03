using System.IO;
using System.Runtime.ExceptionServices;
using GlyphFrame.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GlyphFrame.App.Tests;

public sealed class ApplicationShutdownCoordinatorTests
{
    [Fact]
    public async Task Shutdown_WaitsForRecoveryAndLifetimeDisposalToFinish()
    {
        ApplicationShutdownCoordinator coordinator = new();
        CancellationTokenSource cancellation = new();
        TaskCompletionSource recovery = CompletionSource();
        TaskCompletionSource cancellationObserved = CompletionSource();
        using CancellationTokenRegistration registration = cancellation.Token.Register(
            () => cancellationObserved.TrySetResult());
        ControlledLifetimeOwner owner = new();

        Task shutdown = Task.Run(() => coordinator.Shutdown(cancellation, recovery.Task, owner));
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(shutdown.IsCompleted);
        Assert.Equal(0, owner.DisposeCount);

        recovery.SetResult();
        await owner.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(shutdown.IsCompleted);
        owner.AllowDisposeToFinish();
        await shutdown.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, owner.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => _ = cancellation.Token);
    }

    [Fact]
    public void Shutdown_CancelsRecoveryBeforeDisposingLifetimeOwner()
    {
        ApplicationShutdownCoordinator coordinator = new();
        CancellationTokenSource cancellation = new();
        TaskCompletionSource recovery = CompletionSource();
        cancellation.Token.Register(() => recovery.TrySetCanceled(cancellation.Token));
        InspectingLifetimeOwner owner = new(
            () => (cancellation.IsCancellationRequested, recovery.Task.IsCompleted));

        bool performed = coordinator.Shutdown(cancellation, recovery.Task, owner);

        Assert.True(performed);
        Assert.True(owner.CancellationWasRequested);
        Assert.True(owner.RecoveryHadStopped);
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public void Shutdown_IsIdempotentAndDisposesLifetimeOwnerExactlyOnce()
    {
        ApplicationShutdownCoordinator coordinator = new();
        CancellationTokenSource cancellation = new();
        ImmediateLifetimeOwner owner = new();

        bool first = coordinator.Shutdown(cancellation, Task.CompletedTask, owner);
        bool second = coordinator.Shutdown(cancellation, Task.CompletedTask, owner);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public void Shutdown_CancelledRecoveryStillDisposesLifetimeOwner()
    {
        ApplicationShutdownCoordinator coordinator = new();
        CancellationTokenSource cancellation = new();
        TaskCompletionSource recovery = CompletionSource();
        cancellation.Token.Register(() => recovery.TrySetCanceled(cancellation.Token));
        ImmediateLifetimeOwner owner = new();

        coordinator.Shutdown(cancellation, recovery.Task, owner);

        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public void Shutdown_NonfatalRecoveryFailureIsDiagnosedAndDoesNotSkipDisposal()
    {
        List<string> diagnostics = [];
        ApplicationShutdownCoordinator coordinator = new(diagnostics.Add);
        CancellationTokenSource cancellation = new();
        ImmediateLifetimeOwner owner = new();

        coordinator.Shutdown(
            cancellation,
            Task.FromException(new IOException("recovery failure")),
            owner);

        Assert.Equal(1, owner.DisposeCount);
        Assert.Contains(diagnostics, item => item.Contains("recovery failure", StringComparison.Ordinal));
    }

    [Fact]
    public void Shutdown_UnexpectedRecoveryFailureIsRethrownAfterDisposal()
    {
        ApplicationShutdownCoordinator coordinator = new();
        CancellationTokenSource cancellation = new();
        ImmediateLifetimeOwner owner = new();

        ApplicationException exception = Assert.Throws<ApplicationException>(() => coordinator.Shutdown(
            cancellation,
            Task.FromException(new ApplicationException("unexpected recovery failure")),
            owner));

        Assert.Equal("unexpected recovery failure", exception.Message);
        Assert.Equal(1, owner.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => _ = cancellation.Token);
    }

    [Fact]
    public void Shutdown_CleanupFailureDoesNotSkipLeaseRelease()
    {
        List<string> diagnostics = [];
        ApplicationShutdownCoordinator coordinator = new(diagnostics.Add);
        CancellationTokenSource cancellation = new();
        FailingCleanupLifetimeOwner owner = new();

        coordinator.Shutdown(cancellation, Task.CompletedTask, owner);

        Assert.Equal(1, owner.DisposeCount);
        Assert.True(owner.LeaseReleased);
        Assert.Contains(diagnostics, item => item.Contains("locked artifact", StringComparison.Ordinal));
    }

    [Fact]
    public void Shutdown_DisposesSessionBeforeStoreThroughServiceProvider()
    {
        List<string> disposalOrder = [];
        ServiceCollection services = new();
        services.AddSingleton(_ => new TrackingStore(disposalOrder));
        services.AddSingleton(provider => new TrackingSession(
            provider.GetRequiredService<TrackingStore>(),
            disposalOrder));
        ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TrackingSession>();
        ApplicationShutdownCoordinator coordinator = new();

        coordinator.Shutdown(new CancellationTokenSource(), Task.CompletedTask, provider);

        Assert.Equal(["session", "store"], disposalOrder);
    }

    [Fact]
    public async Task Shutdown_DoesNotRequireSynchronizationContextDispatch()
    {
        TaskCompletionSource completed = CompletionSource();
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new RejectingSynchronizationContext());
                ApplicationShutdownCoordinator coordinator = new();
                coordinator.Shutdown(
                    new CancellationTokenSource(),
                    Task.Run(static () => { }),
                    new ThreadPoolLifetimeOwner());
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                completed.TrySetResult();
            }
        })
        {
            IsBackground = true
        };

        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        failure?.Throw();
    }

    [Fact]
    public void AppExitOverride_IsSynchronousAndCallsBaseFromFinally()
    {
        string source = File.ReadAllText(Path.Combine(
            FindSolutionDirectory(),
            "src",
            "GlyphFrame.App",
            "App.xaml.cs"));

        Assert.Contains("protected override void OnExit(ExitEventArgs e)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("protected override async void OnExit", source, StringComparison.Ordinal);
        int finallyIndex = source.IndexOf("finally", StringComparison.Ordinal);
        int baseIndex = source.IndexOf("base.OnExit(e);", StringComparison.Ordinal);
        Assert.True(finallyIndex >= 0);
        Assert.True(baseIndex > finallyIndex);
        Assert.Equal(1, CountOccurrences(source, "base.OnExit(e);"));
    }

    private static TaskCompletionSource CompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int searchIndex = 0;
        while ((searchIndex = text.IndexOf(value, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += value.Length;
        }

        return count;
    }

    private static string FindSolutionDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GlyphFrame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GlyphFrame solution directory.");
    }

    private sealed class ControlledLifetimeOwner : IAsyncDisposable
    {
        private readonly TaskCompletionSource _finish = CompletionSource();
        private int _disposeCount;

        public TaskCompletionSource DisposeStarted { get; } = CompletionSource();

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public void AllowDisposeToFinish() => _finish.TrySetResult();

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            DisposeStarted.TrySetResult();
            await _finish.Task.ConfigureAwait(false);
        }
    }

    private sealed class InspectingLifetimeOwner(Func<(bool Cancelled, bool RecoveryStopped)> inspect)
        : IAsyncDisposable
    {
        private readonly Func<(bool Cancelled, bool RecoveryStopped)> _inspect = inspect;

        public bool CancellationWasRequested { get; private set; }

        public bool RecoveryHadStopped { get; private set; }

        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            (CancellationWasRequested, RecoveryHadStopped) = _inspect();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ImmediateLifetimeOwner : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingCleanupLifetimeOwner : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public bool LeaseReleased { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            LeaseReleased = true;
            return ValueTask.FromException(new IOException("locked artifact cleanup failed"));
        }
    }

    private sealed class TrackingStore(List<string> disposalOrder) : IDisposable
    {
        private readonly List<string> _disposalOrder = disposalOrder;

        public void Dispose() => _disposalOrder.Add("store");
    }

    private sealed class TrackingSession(TrackingStore store, List<string> disposalOrder)
        : IAsyncDisposable
    {
        private readonly List<string> _disposalOrder = disposalOrder;
        private readonly TrackingStore _store = store;

        public ValueTask DisposeAsync()
        {
            _ = _store;
            _disposalOrder.Add("session");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThreadPoolLifetimeOwner : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(Task.Run(static () => { }));
    }

    private sealed class RejectingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("Shutdown attempted to resume on the synchronization context.");
    }
}
