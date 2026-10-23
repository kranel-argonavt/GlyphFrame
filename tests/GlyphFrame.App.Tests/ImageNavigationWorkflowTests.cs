using System.IO;
using System.Windows;
using System.Windows.Controls;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.ViewModels;
using GlyphFrame.App.Views;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Tests;

public sealed partial class MainWindowVideoPreviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImageNavigationWorkflow_ReplacementRetainsOrResetsOnlyOnAcceptance(bool cancelled) => StatusSta(async () =>
    {
        using TestMediaFiles files = new(); TestHarness harness = ReadyImageHarness();
        harness.ImagePipeline.DecodeResult = new(1000, 600, new byte[1000 * 600 * 4]);
        using MainWindowViewModel vm = harness.CreateViewModel();
        Window host = Phase13UiTests.LoadLooseMainWindow(realMediaComponents: true); host.DataContext = vm;
        host.Left = -10000; host.ShowActivated = false; host.Width = 1024; host.Height = 720;
        try
        {
            host.Show(); await vm.DropMediaCommand.ExecuteAsync([files.Create("A.png")]); await StatusDrain(host);
            ImageNavigationState navigation = vm.ImageNavigation; Assert.True(navigation.IsFit);
            navigation.SetScale(2, navigation.ViewportCenter); navigation.Pan(new(-50, 25));
            ImageViewGeometry accepted = navigation.Geometry;
            TaskCompletionSource<MediaInputResult> pending = NewCompletion<MediaInputResult>();
            harness.MediaInput.Handler = (_, _) => pending.Task;
            Task replacement = vm.DropMediaCommand.ExecuteAsync([files.Create("rejected-B.png")]);
            await StatusDrain(host); Assert.Equal(accepted, navigation.Geometry);
            pending.SetResult(MediaInputResult.Failure(cancelled ? MediaInputErrorCategory.Cancelled : MediaInputErrorCategory.InvalidImage, "Rejected test image"));
            await replacement; await StatusDrain(host); Assert.Equal(accepted, navigation.Geometry);
            harness.MediaInput.Handler = (path, _) => Task.FromResult(ImageResult(path!));
            await vm.DropMediaCommand.ExecuteAsync([files.Create("accepted-C.png")]); await StatusDrain(host);
            Assert.Same(navigation, vm.ImageNavigation); Assert.True(navigation.IsFit);
            Assert.Equal(new Point(0.5, 0.5), navigation.Geometry.Center);
            Assert.Equal(2, harness.ImagePipeline.DecodeCalls);
            ReviewViewport viewport = InputSettingsPolishTests.Descendants(host).OfType<ReviewViewport>().First(v => v.ImageNavigation == navigation);
            navigation.SetScale(3, navigation.ViewportCenter); Assert.True(viewport.BeginImageDrag(navigation.ViewportCenter));
            harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
            await vm.DropMediaCommand.ExecuteAsync([files.Create("video.mp4")]); await StatusDrain(host);
            Assert.Null(vm.CurrentImageFrame); Assert.False(navigation.IsReady); Assert.True(navigation.IsFit);
            Assert.False(viewport.IsMouseCaptured); Assert.False(viewport.IsImageDragging);
        }
        finally { host.Close(); }
    });

    [Fact]
    public void ImageNavigationWorkflow_TabAndRerenderFramingAndExportedPixelsRemainViewOnly() => StatusSta(async () =>
    {
        using TestMediaFiles files = new(); TestHarness harness = ReadyImageHarness();
        harness.ImagePipeline.DecodeResult = new(1000, 600, new byte[1000 * 600 * 4]);
        harness.ImagePipeline.RenderHandler = (_, _, _, _) => Task.FromResult(RenderResult(new RenderedImage(500, 300,
            Enumerable.Range(0, 500 * 300 * 4).Select(i => (byte)(i % 4 == 3 ? 255 : i % 251)).ToArray())));
        using MainWindowViewModel vm = harness.CreateViewModel();
        Window host = Phase13UiTests.LoadLooseMainWindow(realMediaComponents: true); host.DataContext = vm;
        host.Left = -10000; host.ShowActivated = false;
        try
        {
            host.Show(); await vm.DropMediaCommand.ExecuteAsync([files.Create("image.png")]); await StatusDrain(host);
            await vm.RenderImageCommand.ExecuteAsync(null); await StatusDrain(host);
            harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("first.png")); await vm.ExportImageCommand.ExecuteAsync(null);
            RenderedImage artifact = Assert.IsType<RenderedImage>(harness.ImageExporter.LastImage);
            byte[] pixels = artifact.RgbaPixels.ToArray(); var settings = harness.ImagePipeline.LastRenderSettings;
            GlyphFrame.Rendering.Skia.SkiaImageExporter encoder = new();
            string beforeFile = files.PathFor("pixels-before.png"), afterFile = files.PathFor("pixels-after.png");
            await encoder.ExportAsync(artifact, beforeFile, ImageExportFormat.Png, overwrite: false);
            var status = vm.WorkflowStatus; int renders = harness.ImagePipeline.RenderCalls, exports = harness.ImageExporter.Calls;
            ImageNavigationState nav = vm.ImageNavigation; nav.SetScale(4, nav.ViewportCenter); nav.Pan(new(-40, 20));
            Point center = nav.Geometry.Center; double relative = nav.Geometry.Scale / nav.Geometry.FitScale;
            await vm.SelectOriginalCommand.ExecuteAsync(null); await StatusDrain(host);
            Assert.Equal(relative, nav.Geometry.Scale / nav.Geometry.FitScale, 6);
            Assert.Equal(center.X, nav.Geometry.Center.X, 6); Assert.Equal(center.Y, nav.Geometry.Center.Y, 6);
            vm.SelectSymbolizedCommand.Execute(null); await StatusDrain(host);
            Assert.Equal(relative, nav.Geometry.Scale / nav.Geometry.FitScale, 6);
            Assert.Equal(status, vm.WorkflowStatus); Assert.False(vm.IsImageRenderStale);
            Assert.Equal(renders, harness.ImagePipeline.RenderCalls); Assert.Equal(1, harness.ImagePipeline.DecodeCalls);
            Assert.Equal(exports, harness.ImageExporter.Calls); Assert.Equal(0, harness.Settings.SaveCalls);
            Assert.Same(settings, harness.ImagePipeline.LastRenderSettings); Assert.Equal(pixels, artifact.RgbaPixels.ToArray());
            await encoder.ExportAsync(artifact, afterFile, ImageExportFormat.Png, overwrite: false);
            Assert.Equal(await File.ReadAllBytesAsync(beforeFile), await File.ReadAllBytesAsync(afterFile));
            harness.SaveDialogs.ImageOutputPaths.Enqueue(files.PathFor("second.png")); await vm.ExportImageCommand.ExecuteAsync(null);
            Assert.Same(artifact, harness.ImageExporter.LastImage); Assert.Equal(pixels, harness.ImageExporter.LastImage!.RgbaPixels.ToArray());
            double scale = nav.Geometry.Scale;
            await vm.RenderImageCommand.ExecuteAsync(null); await StatusDrain(host);
            Assert.False(nav.IsFit); Assert.Equal(scale, nav.Geometry.Scale, 7);
            Assert.Equal(center.X, nav.Geometry.Center.X, 6); Assert.Equal(center.Y, nav.Geometry.Center.Y, 6);
            Assert.Equal(1, harness.ImagePipeline.DecodeCalls); Assert.Equal(renders + 1, harness.ImagePipeline.RenderCalls);
        }
        finally { host.Close(); }
    });
}
