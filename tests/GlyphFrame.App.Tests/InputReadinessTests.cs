using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Tests;

public sealed partial class MainWindowVideoPreviewTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void InputReadiness_TracksAcceptedMediaLoadingRejectedAndSuccessfulReplacement(bool image, bool cancelled) => StatusSta(async () =>
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        using TestMediaFiles files = new();
        TestHarness harness = image ? ReadyImageHarness() : ReadyVideoHarness();
        harness.ManagedVideoRenders.Handler = (job, _, _) => Task.FromResult(
            ManagedVideoRenderResult.Completed(Artifact(files.Create("accepted-A.mp4"), job.Identity)));
        using MainWindowViewModel vm = harness.CreateViewModel();
        Window window = Phase13UiTests.LoadLooseMainWindow(); window.DataContext = vm;
        window.Width = 1024; window.Height = 720; window.Left = -10000; window.ShowActivated = false;
        try
        {
            window.Show();
            string first = files.Create("source_" + new string('A', 60) + (image ? ".png" : ".mp4"));
            File.WriteAllBytes(first, new byte[108461]);
            await vm.DropMediaCommand.ExecuteAsync([first]);
            if (image) { await vm.RenderImageCommand.ExecuteAsync(null); }
            else { await vm.RenderVideoCommand.ExecuteAsync(null); }
            await StatusDrain(window);
            StackPanel ready = InputSettingsPolishTests.Descendants(window).OfType<StackPanel>().Single(p => p.Name == "SourceReady");
            TextBlock metadata = InputSettingsPolishTests.Descendants(window).OfType<TextBlock>().Single(t => t.Name == "InputMetadata");
            TextBlock filename = InputSettingsPolishTests.Descendants(window).OfType<TextBlock>().Single(t => t.Name == "InputFileName");
            Assert.True(vm.IsInputReady); Assert.True(ready.IsVisible);
            string acceptedSummary = metadata.Text;
            Assert.Equal(vm.InputMetadataSummary, acceptedSummary);
            Assert.Contains(vm.FormattedFileSize, acceptedSummary);
            Assert.Contains(",", vm.FormattedFileSize);
            Assert.Equal(1, acceptedSummary.Split(vm.MediaTypeText).Length - 1);
            Assert.Equal(first, filename.ToolTip);
            TaskCompletionSource<MediaInputResult> pending = NewCompletion<MediaInputResult>();
            harness.MediaInput.Handler = (_, _) => pending.Task;
            string rejected = files.Create(image ? "bad-B.png" : "bad-B.mp4");
            Task loading = vm.DropMediaCommand.ExecuteAsync([rejected]);
            await StatusDrain(window);
            Assert.True(vm.IsLoadingMedia); Assert.False(vm.IsInputReady); Assert.False(ready.IsVisible);
            Assert.Equal(acceptedSummary, metadata.Text);
            Assert.Equal(first, image ? vm.ImageMetadata?.FilePath : vm.VideoMetadata?.FilePath);
            Assert.Equal(rejected, vm.SelectedFilePath); // Existing pending-path help during validation.
            pending.SetResult(MediaInputResult.Failure(cancelled ? MediaInputErrorCategory.Cancelled : image ? MediaInputErrorCategory.InvalidImage : MediaInputErrorCategory.InvalidVideo,
                "Replacement was not accepted."));
            await loading; await StatusDrain(window);
            Assert.Equal(first, vm.SelectedFilePath); Assert.Equal(acceptedSummary, metadata.Text);
            Assert.Equal(!vm.HasMediaInputError, vm.IsInputReady); Assert.Equal(vm.IsInputReady, ready.IsVisible);
            Assert.True(image ? vm.ShowImageResultPanel : vm.ShowVideoResultPanel); Assert.False(vm.ShowReadyToRender);
            Assert.Single(InputSettingsPolishTests.Descendants(window).OfType<Border>(), b =>
                b.Name is "ReadyToRenderCard" or "ProcessingContent" or "ImageResultCard" or "VideoResultCard" && b.IsVisible);
            harness.MediaInput.Handler = (path, _) => Task.FromResult(image ? ImageResult(path!) : VideoResult(path!));
            string accepted = files.Create(image ? "accepted-B.png" : "accepted-B.mp4");
            await vm.DropMediaCommand.ExecuteAsync([accepted]); await StatusDrain(window);
            Assert.True(vm.IsInputReady); Assert.True(ready.IsVisible); Assert.Equal(accepted, vm.SelectedFilePath);
            Assert.True(vm.ShowReadyToRender); Assert.False(vm.ShowImageResultPanel); Assert.False(vm.ShowVideoResultPanel);
            Assert.Equal(vm.InputMetadataSummary, metadata.Text); Assert.Equal(accepted, filename.ToolTip);
            InputSettingsPolishTests.Capture(window, $"accepted-input-{image}-{cancelled}");
        }
        finally { window.Close(); }
    });
}
