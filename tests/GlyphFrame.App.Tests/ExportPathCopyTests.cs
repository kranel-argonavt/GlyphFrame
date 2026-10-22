using GlyphFrame.App.Presentation;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class ExportPathCopyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitCopy_UsesExactUnicodePathAndTruthfulTransientFeedback(bool success)
    {
        ClipboardDouble clipboard = new() { Success = success };
        TaskCompletionSource hold = new();
        using ExportPathCopyPresentation copy = new(clipboard, _ => hold.Task);
        Assert.False(copy.Command.CanExecute(null));
        copy.SetDestination(new object(), "  ");
        Assert.False(copy.Command.CanExecute(null));
        const string path = @"C:\експорт\日本語\результат 漢字.png";
        copy.SetDestination(new object(), path);
        Assert.Equal(path, copy.ToolTip);
        Assert.Null(clipboard.Text); // Never automatic.
        Task pending = copy.Command.ExecuteAsync(null);
        Assert.Equal(path, clipboard.Text);
        Assert.Equal(success ? "Copied!" : "Copy failed", copy.Label);
        Assert.Equal(path, copy.Path);
        if (!success) { Assert.Contains("Try Copy path again", copy.ToolTip); Assert.DoesNotContain("Copied!", copy.Label); }
        hold.SetResult();
        await pending;
        Assert.Equal("Copy path", copy.Label);
        Assert.Equal(path, copy.ToolTip);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacedOrDisposedDestination_RejectsLateFeedbackEvenWhenHoldIgnoresCancellation(bool dispose)
    {
        ClipboardDouble clipboard = new();
        List<TaskCompletionSource> holds = [];
        using ExportPathCopyPresentation copy = new(clipboard, _ =>
        {
            TaskCompletionSource hold = new(); holds.Add(hold); return hold.Task;
        });
        object first = new(), second = new();
        copy.SetDestination(first, @"C:\old\first.mp4");
        Task old = copy.Command.ExecuteAsync(null);
        copy.SetDestination(second, @"C:\new\другий.mp4");
        Assert.Equal("Copy path", copy.Label);
        Task current = copy.Command.ExecuteAsync(null);
        if (dispose) { copy.Dispose(); }
        holds[0].SetResult();
        await old;
        Assert.Equal(dispose ? "Copy path" : "Copied!", copy.Label);
        Assert.Equal(dispose ? string.Empty : @"C:\new\другий.mp4", copy.Path);
        holds[1].SetResult();
        await current;
        Assert.Equal("Copy path", copy.Label);
        Assert.Equal(!dispose, copy.Command.CanExecute(null));
    }

    [Fact]
    public async Task NotificationsForSameIdentity_DoNotResetFeedbackButExplicitInvalidationDoes()
    {
        TaskCompletionSource hold = new();
        using ExportPathCopyPresentation copy = new(new ClipboardDouble(), _ => hold.Task);
        object identity = new();
        copy.SetDestination(identity, @"C:\export\same.png");
        Task pending = copy.Command.ExecuteAsync(null);
        copy.SetDestination(identity, @"C:\export\same.png");
        Assert.Equal("Copied!", copy.Label);
        copy.ClearFeedback();
        Assert.Equal("Copy path", copy.Label);
        hold.SetResult(); await pending;
        Assert.Equal("Copy path", copy.Label);
        copy.SetDestination(null, null);
        Assert.False(copy.Command.CanExecute(null));
        Assert.Empty(copy.Path);
    }

    private sealed class ClipboardDouble : IClipboardService
    {
        public bool Success { get; init; } = true;
        public string? Text { get; private set; }
        public ClipboardWriteResult SetText(string text)
        {
            Text = text;
            return Success ? ClipboardWriteResult.Completed() : ClipboardWriteResult.Failed();
        }
    }
}
