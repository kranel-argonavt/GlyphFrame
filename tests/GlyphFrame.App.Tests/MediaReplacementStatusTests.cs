using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed partial class MainWindowVideoPreviewTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AcceptedVideoReplacement_PublishesOneCurrentStatusWithoutOldReview(bool exported, bool animations) => StatusSta(async () =>
    {
        using TestMediaFiles files = new();
        string first = files.Create("A.mp4"), second = files.Create("B.mp4");
        TestHarness harness = ReadyVideoHarness();
        harness.ExportStatusPresentation.AnimationsEnabled = animations;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("accepted-A.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout();
        view.DataContext = vm;
        Window host = new() { Content = view, Width = 900, SizeToContent = SizeToContent.Height, Left = -10000, ShowActivated = false };
        try
        {
            host.Show();
            await vm.DropMediaCommand.ExecuteAsync([first]);
            await vm.RenderVideoCommand.ExecuteAsync(null);
            if (exported)
            {
                harness.SaveDialogs.VideoOutputPath = files.PathFor("A-export.mp4");
                await vm.ExportVideoCommand.ExecuteAsync(null);
            }
            Assert.True(vm.ShowVideoResultPanel);
            Assert.True(((Border)view.FindName("VideoResultCard")).IsVisible);
            TaskCompletionSource clearStarted = NewCompletion(), releaseClear = NewCompletion();
            harness.ManagedArtifacts.ClearHandler = async _ => { clearStarted.SetResult(); await releaseClear.Task; };
            Task replacement = vm.DropMediaCommand.ExecuteAsync([second]);
            await clearStarted.Task;
            // Asynchronous cleanup is a real yield point. The old accepted
            // source may remain presented here, but never alongside planned B.
            await StatusDrain(host);
            Assert.InRange(PrimaryStatusCount(view), 0, 1);
            releaseClear.SetResult();
            await replacement;
            await StatusDrain(host);
            CaptureReplacement(view, vm, $"accepted-{exported}-{animations}");
            Assert.Equal(second, vm.VideoMetadata?.FilePath);
            Assert.True(vm.ShowReadyToRender); Assert.False(vm.ShowVideoResultPanel);
            Assert.False(vm.HasVideoArtifact); Assert.False(vm.ShowVideoRenderMetrics);
            Assert.False(vm.HasVideoExportDestination); Assert.False(vm.CanOpenExportFolder);
            Assert.Null(vm.VideoReviewErrorPresentation); Assert.Null(vm.VideoRenderErrorPresentation);
            Assert.Single(VisiblePrimaryStatuses(view));
            Assert.True(((Border)view.FindName("ReadyToRenderCard")).IsVisible);
            Assert.False(((Border)view.FindName("VideoResultCard")).IsVisible);
            Assert.False(((FrameworkElement)view.FindName("VideoResultMetrics")).IsVisible);
            Assert.False(((FrameworkElement)view.FindName("VideoResultActions")).IsVisible);
        }
        finally { host.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VideoReplacement_LateRecoveryAndPlayerCallbacksCannotRestoreOldStatus(bool animations) => StatusSta(async () =>
    {
        using TestMediaFiles files = new(); TestHarness harness = ReadyVideoHarness();
        harness.ExportStatusPresentation.AnimationsEnabled = animations;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("old-accepted.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout(); view.DataContext = vm;
        Window host = new() { Content = view, Width = 900, SizeToContent = SizeToContent.Height, Left = -10000, ShowActivated = false };
        try
        {
            host.Show(); await vm.DropMediaCommand.ExecuteAsync([files.Create("A.mp4")]); await vm.RenderVideoCommand.ExecuteAsync(null);
            vm.UnloadReviewView();
            TaskCompletionSource opening = NewCompletion();
            TaskCompletionSource<GlyphFrame.App.Services.RenderedVideoOpenResult> opened = NewCompletion<GlyphFrame.App.Services.RenderedVideoOpenResult>();
            harness.RenderedVideoPlayer.Handler = (_, _) => { opening.SetResult(); return opened.Task; };
            Task recovery = vm.LoadReviewViewAsync(null); await opening.Task;
            string next = files.Create("B.mp4");
            bool overlap = false;
            vm.PropertyChanged += (_, _) => { if (vm.VideoMetadata?.FilePath == next) { host.UpdateLayout(); overlap |= PrimaryStatusCount(view) > 1; } };
            await vm.DropMediaCommand.ExecuteAsync([next]); await StatusDrain(host);
            string message = vm.StatusMessage;
            opened.SetResult(GlyphFrame.App.Services.RenderedVideoOpenResult.Failed("Obsolete A recovery", "old detail"));
            await recovery;
            harness.RenderedVideoPlayer.FailPlayback(); // Delayed transport notification, with no accepted A identity.
            await StatusDrain(host);
            CaptureReplacement(view, vm, $"late-recovery-{animations}");
            Assert.False(overlap); Assert.True(vm.ShowReadyToRender); Assert.False(vm.ShowVideoResultPanel);
            Assert.Null(harness.ManagedArtifacts.Current); Assert.False(harness.RenderedVideoPlayer.IsOpen);
            Assert.Null(vm.VideoReviewErrorPresentation); Assert.Equal(message, vm.StatusMessage);
            Assert.Equal("ReadyToRenderCard", Assert.Single(VisiblePrimaryStatuses(view)).Name);
            Assert.False(((FrameworkElement)view.FindName("VideoResultMetrics")).IsVisible);
        }
        finally { host.Close(); }
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RejectedVideoReplacement_PreservesAcceptedExportAndOneBoundResult(bool cancelled, bool animations) => StatusSta(async () =>
    {
        using TestMediaFiles files = new(); TestHarness harness = ReadyVideoHarness();
        harness.ExportStatusPresentation.AnimationsEnabled = animations;
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("kept.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout(); view.DataContext = vm;
        Window host = new() { Content = view, Width = 900, SizeToContent = SizeToContent.Height, Left = -10000, ShowActivated = false };
        try
        {
            host.Show(); string first = files.Create("A.mp4");
            await vm.DropMediaCommand.ExecuteAsync([first]); await vm.RenderVideoCommand.ExecuteAsync(null);
            harness.SaveDialogs.VideoOutputPath = files.PathFor("accepted-export.mp4"); await vm.ExportVideoCommand.ExecuteAsync(null);
            ManagedVideoArtifact? artifact = harness.ManagedArtifacts.Current;
            int clears = harness.ManagedArtifacts.ClearCalls;
            string accepted = vm.ExportPathCopy.Path;
            harness.MediaInput.Handler = (_, _) => Task.FromResult(MediaInputResult.Failure(
                cancelled ? MediaInputErrorCategory.Cancelled : MediaInputErrorCategory.InvalidVideo, "Replacement was not accepted."));
            await vm.DropMediaCommand.ExecuteAsync([files.Create("rejected-B.mp4")]); await StatusDrain(host);
            Assert.Equal(first, vm.VideoMetadata?.FilePath); Assert.Same(artifact, harness.ManagedArtifacts.Current);
            Assert.Equal(clears, harness.ManagedArtifacts.ClearCalls); Assert.True(harness.RenderedVideoPlayer.IsOpen);
            Assert.Equal(accepted, vm.ExportPathCopy.Path); Assert.True(vm.CanOpenExportFolder);
            Assert.True(vm.ShowVideoResultPanel); Assert.False(vm.ShowReadyToRender);
            Assert.Equal("VideoResultCard", Assert.Single(VisiblePrimaryStatuses(view)).Name);
            Assert.True(((FrameworkElement)view.FindName("VideoResultMetrics")).IsVisible);
            Assert.Equal("Video Exported", ((TextBlock)view.FindName("VideoResultTitleText")).Text);
        }
        finally { host.Close(); }
    });

    private static IEnumerable<Border> VisiblePrimaryStatuses(UserControl view) =>
        new[] { "ReadyToRenderCard", "ProcessingContent", "ImageResultCard", "VideoResultCard" }
            .Select(name => (Border)view.FindName(name)).Where(card => card.IsVisible);
    private static int PrimaryStatusCount(UserControl view) => VisiblePrimaryStatuses(view).Count();
    private static async Task StatusDrain(Window host)
    { await host.Dispatcher.InvokeAsync(() => host.UpdateLayout(), DispatcherPriority.ApplicationIdle); }

    private static void CaptureReplacement(UserControl view, MainWindowViewModel vm, string name)
    {
        string? folder = Environment.GetEnvironmentVariable("GLYPHFRAME_REPLACEMENT_EVIDENCE");
        if (folder is null) { return; }
        Directory.CreateDirectory(folder);
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view); PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(Path.Combine(folder, name + ".png"))) { encoder.Save(stream); }
        File.WriteAllText(Path.Combine(folder, name + ".json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Kind = "Populated WPF fixture with actual view model; not native visual acceptance",
            vm.SelectedFileName,
            vm.WorkflowStatus,
            vm.ShowReadyToRender,
            vm.ShowVideoResultPanel,
            vm.HasVideoArtifact,
            VisibleCards = VisiblePrimaryStatuses(view).Select(b => b.Name).ToArray(),
            BoundVideoTitle = ((TextBlock)view.FindName("VideoResultTitleText")).Text
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static void StatusSta(Func<Task> action)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try { await action(); }
                catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
                finally { dispatcher.InvokeShutdown(); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Media/status transition did not settle."); failure?.Throw();
    }
}
