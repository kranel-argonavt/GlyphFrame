using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Tests;

public sealed partial class MainWindowVideoPreviewTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PreviewBadge_DoesNotDuplicateReadyOrExportedMetricsButRetainsStaleGuidance(bool video, bool exported) => StatusSta(async () =>
    {
        using TestMediaFiles files = new();
        TestHarness harness = video ? ReadyVideoHarness() : ReadyImageHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("managed-preview.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        Window host = Phase13UiTests.LoadLooseMainWindow(); host.DataContext = vm;
        host.Left = -10000; host.ShowActivated = false;
        try
        {
            host.Show();
            Border badge = InputSettingsPolishTests.Descendants(host).OfType<Border>().Single(border =>
                BindingOperations.GetBinding(border, UIElement.VisibilityProperty)?.Path.Path == nameof(vm.ShowPreviewStatusBadge));
            TextBlock caption = Assert.IsType<TextBlock>(badge.Child);
            await vm.DropMediaCommand.ExecuteAsync([files.Create(video ? "source.mp4" : "source.png")]);
            await Render(); await StatusDrain(host);
            AssertFresh();
            if (exported)
            {
                if (video) { harness.SaveDialogs.VideoOutputPath = files.PathFor("export.mp4"); await vm.ExportVideoCommand.ExecuteAsync(null); }
                else { harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("export.png")); await vm.ExportImageCommand.ExecuteAsync(null); }
                await StatusDrain(host); AssertFresh();
            }
            List<string?> changes = []; vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            vm.SelectedFontSize += 1; await StatusDrain(host);
            Assert.True(vm.IsSymbolizedPreviewStale); Assert.True(vm.ShowPreviewStatusBadge); Assert.True(badge.IsVisible);
            Assert.Contains(nameof(vm.ShowPreviewStatusBadge), changes);
            Assert.Contains("changed", caption.Text); Assert.DoesNotContain("symbols", caption.Text);
            await vm.SelectOriginalCommand.ExecuteAsync(null); await StatusDrain(host); Assert.False(badge.IsVisible);
            vm.SelectSymbolizedCommand.Execute(null); await StatusDrain(host); Assert.True(badge.IsVisible);
            changes.Clear(); await Render(); await StatusDrain(host); AssertFresh();
            Assert.Contains(nameof(vm.ShowPreviewStatusBadge), changes);

            Task Render() => video ? vm.RenderVideoCommand.ExecuteAsync(null) : vm.RenderImageCommand.ExecuteAsync(null);
            void AssertFresh()
            {
                Assert.True(vm.IsSymbolizedPreviewSelected); Assert.False(vm.IsSymbolizedPreviewStale);
                Assert.False(vm.ShowPreviewStatusBadge); Assert.Equal(Visibility.Collapsed, badge.Visibility); Assert.False(badge.IsVisible);
                Assert.Empty(vm.PreviewStaleMessage); Assert.Empty(caption.Text);
                Assert.True(video ? vm.ShowVideoRenderMetrics : vm.ShowImageRenderMetrics);
                string output = video ? vm.VideoOutputDimensionsText : vm.ImageOutputDimensionsText;
                Assert.False(string.IsNullOrWhiteSpace(output));
                Assert.Contains(InputSettingsPolishTests.Descendants(host).OfType<TextBlock>(), field => field.IsVisible && field.Text == output);
                if (!video)
                {
                    Assert.Contains(InputSettingsPolishTests.Descendants(host).OfType<TextBlock>(), field => field.IsVisible && field.Text == vm.ImageGridDimensionsText);
                }
            }
        }
        finally { host.Close(); }
    });

    [Fact]
    public void PreviewBadge_VideoFramePreviewHasNoDimensionCaptionAndStillWarnsWhenStale() => StatusSta(async () =>
    {
        using TestMediaFiles files = new(); TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.DropMediaCommand.ExecuteAsync([files.Create("frame-preview.mp4")]);
        await vm.PreviewFrameCommand.ExecuteAsync(null);
        Assert.True(vm.HasSymbolizedPreview); Assert.False(vm.HasVideoArtifact);
        Assert.False(vm.ShowPreviewStatusBadge); Assert.Empty(vm.PreviewStaleMessage);
        vm.SelectedFontSize += 1;
        Assert.True(vm.IsSymbolizedPreviewStale); Assert.True(vm.ShowPreviewStatusBadge);
        Assert.Contains("generate a new symbolized frame", vm.PreviewStaleMessage);
        await vm.PreviewFrameCommand.ExecuteAsync(null);
        Assert.False(vm.ShowPreviewStatusBadge); Assert.Empty(vm.PreviewStaleMessage);
    });
}
