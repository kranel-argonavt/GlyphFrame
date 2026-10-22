using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Presentation;

/// <summary>Transient clipboard feedback, scoped to one displayed result and successful destination.</summary>
public sealed class ExportPathCopyPresentation : ObservableObject, IDisposable
{
    private readonly IClipboardService _clipboard;
    private readonly Func<CancellationToken, Task> _hold;
    private CancellationTokenSource? _feedback;
    private object? _identity;
    private string? _path;
    private string _label = "Copy path";
    private bool _failed;
    private bool _disposed;

    public ExportPathCopyPresentation(IClipboardService clipboard, Func<CancellationToken, Task>? hold = null)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _hold = hold ?? (token => Task.Delay(TimeSpan.FromSeconds(2), token));
        Command = new AsyncRelayCommand(CopyAsync, () => !_disposed && _path is not null,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }

    public IAsyncRelayCommand Command { get; }
    public string Path => _path ?? string.Empty;
    public string Label => _label;
    public string ToolTip => _failed
        ? $"{Path}\nCould not access the clipboard. Try Copy path again."
        : Path;
    public string AccessibleName => _failed ? "Copy export path. Clipboard unavailable; try again." : "Copy export path";

    public void SetDestination(object? identity, string? path)
    {
        if (_disposed) { return; }
        path = string.IsNullOrWhiteSpace(path) ? null : path;
        if (ReferenceEquals(identity, _identity) && path == _path) { return; }
        CancelFeedback();
        _identity = identity;
        _path = identity is null ? null : path;
        ResetFeedback();
        OnPropertyChanged(nameof(Path));
        Command.NotifyCanExecuteChanged();
    }

    public void ClearFeedback() { CancelFeedback(); ResetFeedback(); }

    private async Task CopyAsync()
    {
        if (_disposed || _path is not string path) { return; }
        CancelFeedback();
        CancellationTokenSource feedback = new();
        _feedback = feedback;
        ClipboardWriteResult result = _clipboard.SetText(path);
        _label = result.Success ? "Copied!" : "Copy failed";
        _failed = !result.Success;
        NotifyFeedback();
        try
        {
            await _hold(feedback.Token);
            if (!_disposed && ReferenceEquals(_feedback, feedback)) { ResetFeedback(); }
        }
        catch (OperationCanceledException) when (feedback.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_feedback, feedback)) { _feedback = null; }
            feedback.Dispose();
        }
    }

    private void CancelFeedback()
    {
        CancellationTokenSource? old = _feedback;
        _feedback = null;
        old?.Cancel(); // The owning async operation disposes its own token source.
    }

    private void ResetFeedback() { _label = "Copy path"; _failed = false; NotifyFeedback(); }
    private void NotifyFeedback()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(ToolTip));
        OnPropertyChanged(nameof(AccessibleName));
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        CancelFeedback();
        _identity = null;
        _path = null;
        ResetFeedback();
        OnPropertyChanged(nameof(Path));
        Command.NotifyCanExecuteChanged();
    }
}
