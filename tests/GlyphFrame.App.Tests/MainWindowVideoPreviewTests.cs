using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using GlyphFrame.App.Services;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Presets;
using GlyphFrame.Core.Settings;
using ImageMetadata = GlyphFrame.Core.Models.ImageMetadata;

namespace GlyphFrame.App.Tests;

public sealed class MainWindowVideoPreviewTests
{
    [Fact]
    public void Constructor_RejectsNullVideoFrameReader()
    {
        TestHarness harness = new();

        Assert.Throws<ArgumentNullException>(() => harness.CreateViewModel(videoFrameReader: null, allowNull: true));
    }

    [Fact]
    public async Task SelectingVideo_LoadsMiddleFrameAndEnablesVideoOnlyPositionControls()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("middle.mp4");
        TestHarness harness = new();
        DecodedImage middleFrame = SolidFrame(12, 34, 56);
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(middleFrame, position, metadata));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.True(viewModel.IsVideoSelected);
        Assert.True(viewModel.HasVideoFrame);
        Assert.True(viewModel.ShowImagePreview);
        Assert.True(viewModel.IsVideoSeekEnabled);
        Assert.Equal(VideoPreviewDefaults.MiddlePosition, viewModel.TimelinePosition, 3);
        Assert.Equal(middleFrame, harness.ImageSources.LastDecodedImage);
        Assert.True(viewModel.PreviewCommand.CanExecute(null));
        Assert.True(viewModel.ExportCommand.CanExecute(null));
        Assert.Contains("0:05", viewModel.TimelineText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VideoExport_UsesCurrentSettingsAndRestoresUiAfterSuccess()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("export.mp4");
        string outputPath = files.PathFor("символи output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.VideoExports.Handler = (job, _) => Task.FromResult(VideoExportResult.Completed(
            job.OutputPath,
            12,
            new PixelDimensions(64, 48),
            10));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.NotNull(harness.VideoExports.LastJob);
        Assert.Equal(videoPath, harness.VideoExports.LastJob!.InputPath);
        Assert.Equal(outputPath, harness.VideoExports.LastJob.OutputPath);
        Assert.Equal(viewModel.SelectedPreset, harness.VideoExports.LastJob.CharacterPreset);
        Assert.Equal(100, viewModel.ExportProgress);
        Assert.False(viewModel.IsExportActive);
        Assert.Contains("without audio", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(outputPath, viewModel.OutputPathText);
    }

    [Fact]
    public async Task VideoExport_FailureRestoresUiAndShowsFriendlyMessage()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("failure.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = files.PathFor("failure-output.mp4");
        harness.VideoExports.Handler = (_, _) => Task.FromResult(VideoExportResult.Failure(
            VideoExportErrorCategory.EncoderFailed,
            "GlyphFrame could not finish encoding the MP4 video.",
            "raw encoder detail"));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsExportActive);
        Assert.True(viewModel.ExportCommand.CanExecute(null));
        Assert.Contains("could not finish", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw encoder detail", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VideoExport_PreventsConcurrentActionsUntilCompletion()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("concurrent.mp4");
        string outputPath = files.PathFor("concurrent-output.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        TaskCompletionSource<bool> started = NewCompletion<bool>();
        TaskCompletionSource<VideoExportResult> completion = NewCompletion<VideoExportResult>();
        harness.VideoExports.Handler = (_, _) =>
        {
            started.TrySetResult(true);
            return completion.Task;
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Task export = viewModel.ExportCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(viewModel.IsExportActive);
        Assert.False(viewModel.ExportCommand.CanExecute(null));
        Assert.False(viewModel.BrowseMediaCommand.CanExecute(null));
        Assert.False(viewModel.BrowseOutputCommand.CanExecute(null));
        Assert.False(viewModel.AreSettingsEnabled);

        completion.SetResult(VideoExportResult.Completed(
            outputPath,
            12,
            new PixelDimensions(64, 48),
            10));
        await export;
        Assert.False(viewModel.IsExportActive);
        Assert.True(viewModel.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task VideoExport_DeclinedOverwritePreservesFileAndSkipsService()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("source-overwrite.mp4");
        string outputPath = files.Create("existing-output.mp4");
        byte[] original = await File.ReadAllBytesAsync(outputPath);
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.SaveDialogs.VideoOutputPath = outputPath;
        harness.UserDialogs.ConfirmResult = false;
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        await viewModel.ExportCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.VideoExports.Calls);
        Assert.Equal(original, await File.ReadAllBytesAsync(outputPath));
        Assert.Contains("existing file was not changed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MiddleFrameFailure_PreservesVideoMetadataAndShowsFriendlyRetryState()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("broken-preview.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, _, _) => Task.FromResult(
            VideoFrameReadResult.Failure(
                VideoFrameReadStatus.Unavailable,
                VideoFrameReadErrorCategory.DecodeFailed,
                position,
                "GlyphFrame could not decode a frame at this position.",
                "decoder detail"));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.True(viewModel.IsVideoSelected);
        Assert.NotNull(viewModel.VideoMetadata);
        Assert.False(viewModel.HasVideoFrame);
        Assert.True(viewModel.HasVideoFrameError);
        Assert.True(viewModel.ShowVideoFrameError);
        Assert.Contains("could not decode", viewModel.VideoFrameStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("decoder detail", viewModel.VideoFrameStatusMessage, StringComparison.Ordinal);
        Assert.Equal("decoder detail", viewModel.VideoFrameTechnicalDetail);
    }

    [Fact]
    public async Task RapidSliderChanges_CoalesceAndOnlyLatestPositionUpdatesOriginalFrame()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("seeking.mp4");
        TestHarness harness = new();
        DecodedImage initial = SolidFrame(255, 0, 0);
        DecodedImage final = SolidFrame(0, 0, 255);
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(initial, position, metadata));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        TaskCompletionSource<double> seekStarted = NewCompletion<double>();
        int seekCalls = 0;
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
        {
            Interlocked.Increment(ref seekCalls);
            seekStarted.TrySetResult(position);
            return Task.FromResult(FrameResult(final, position, metadata));
        };

        viewModel.TimelinePosition = 0.2d;
        viewModel.TimelinePosition = 0.4d;
        viewModel.TimelinePosition = 0.8d;

        double requestedPosition = await seekStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitUntilAsync(viewModel, () => !viewModel.IsVideoFrameLoading);

        Assert.Equal(0.8d, requestedPosition, 3);
        Assert.Equal(1, seekCalls);
        Assert.Equal(0.8d, viewModel.TimelinePosition, 3);
        Assert.Equal(final, harness.ImageSources.LastDecodedImage);
        Assert.False(viewModel.HasVideoFrameError);
    }

    [Fact]
    public async Task LateFrameFromPreviousVideo_CannotReplaceCurrentVideoFrame()
    {
        using TestMediaFiles files = new();
        string firstPath = files.Create("first.mp4");
        string secondPath = files.Create("second.mp4");
        TestHarness harness = new();
        DecodedImage firstFrame = SolidFrame(255, 0, 0);
        DecodedImage secondFrame = SolidFrame(0, 0, 255);
        TaskCompletionSource<VideoFrameReadResult> firstPending = NewCompletion<VideoFrameReadResult>();
        TaskCompletionSource<bool> firstStarted = NewCompletion<bool>();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (path, position, metadata, _) =>
        {
            if (path.Equals(firstPath, StringComparison.OrdinalIgnoreCase))
            {
                firstStarted.TrySetResult(true);
                return firstPending.Task;
            }

            return Task.FromResult(FrameResult(secondFrame, position, metadata));
        };
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        Task firstSelection = viewModel.DropMediaCommand.ExecuteAsync([firstPath]);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Task secondSelection = viewModel.DropMediaCommand.ExecuteAsync([secondPath]);
        await WaitUntilAsync(
            viewModel,
            () => viewModel.VideoMetadata?.FilePath.Equals(secondPath, StringComparison.OrdinalIgnoreCase) == true);
        firstPending.SetResult(FrameResult(firstFrame, 0.5d, VideoMetadataFor(firstPath)));
        await Task.WhenAll(firstSelection, secondSelection);

        Assert.Equal(secondPath, viewModel.VideoMetadata?.FilePath);
        Assert.Equal(secondFrame, harness.ImageSources.LastDecodedImage);
        Assert.False(viewModel.HasVideoFrameError);
    }

    [Fact]
    public async Task SymbolizedVideoPreview_UsesDisplayedFrameAndBecomesStaleAfterSeeking()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("symbolize.mp4");
        TestHarness harness = new();
        DecodedImage firstFrame = SolidFrame(255, 0, 0);
        DecodedImage secondFrame = SolidFrame(0, 0, 255);
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(position < 0.7d ? firstFrame : secondFrame, position, metadata));
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        viewModel.SelectedPreset = CharacterPresetOption.BuiltIn[2];
        viewModel.SelectedFont = "Consolas";
        viewModel.SelectedFontSize = 12;
        viewModel.SelectedOutputResolution = "720p";
        viewModel.SelectedSymbolPolarity = SymbolPolarity.DarkOnLight;

        await viewModel.PreviewCommand.ExecuteAsync(null);

        Assert.Equal(firstFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.Equal(CharacterPresetKind.Blocks, harness.ImagePipeline.LastRenderedPreset?.Kind);
        Assert.Equal("Consolas", harness.ImagePipeline.LastRenderSettings?.FontFamily);
        Assert.Equal(12, harness.ImagePipeline.LastRenderSettings?.FontSize);
        Assert.Equal(OutputResolution.Hd720, harness.ImagePipeline.LastRenderSettings?.OutputResolution);
        Assert.Equal(SymbolPolarity.DarkOnLight, harness.ImagePipeline.LastRenderSettings?.SymbolPolarity);
        Assert.True(viewModel.HasSymbolizedPreview);
        Assert.False(viewModel.IsSymbolizedPreviewStale);
        Assert.Equal(PreviewMode.Symbolized, viewModel.PreviewMode);

        viewModel.TimelinePosition = 0.8d;
        Assert.True(viewModel.IsSymbolizedPreviewStale);
        Assert.Equal(PreviewMode.Original, viewModel.PreviewMode);
        await WaitUntilAsync(viewModel, () => !viewModel.IsVideoFrameLoading);

        await viewModel.PreviewCommand.ExecuteAsync(null);

        Assert.Equal(secondFrame, harness.ImagePipeline.LastRenderedSource);
        Assert.False(viewModel.IsSymbolizedPreviewStale);
        Assert.Equal(PreviewMode.Symbolized, viewModel.PreviewMode);
    }

    [Fact]
    public async Task RenderSettingChange_InvalidatesVideoPreviewButOutputFormatDoesNot()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("settings.mp4");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.PreviewCommand.ExecuteAsync(null);

        viewModel.SelectedFontSize = 12;

        Assert.True(viewModel.IsSymbolizedPreviewStale);
        await viewModel.PreviewCommand.ExecuteAsync(null);
        Assert.False(viewModel.IsSymbolizedPreviewStale);

        viewModel.SelectedImageOutputFormat = "JPG/JPEG";

        Assert.False(viewModel.IsSymbolizedPreviewStale);
    }

    [Fact]
    public async Task SymbolizationResultFromOldFrame_IsRejectedAfterSliderMoves()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("race.mp4");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        TaskCompletionSource<ImagePipelineResult> oldRender = NewCompletion<ImagePipelineResult>();
        TaskCompletionSource<bool> renderStarted = NewCompletion<bool>();
        harness.ImagePipeline.RenderHandler = (_, _, _, _) =>
        {
            renderStarted.TrySetResult(true);
            return oldRender.Task;
        };

        Task preview = viewModel.PreviewCommand.ExecuteAsync(null);
        await renderStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.TimelinePosition = 0.8d;
        await WaitUntilAsync(viewModel, () => !viewModel.IsVideoFrameLoading);
        oldRender.SetResult(RenderResult());
        await preview;

        Assert.False(viewModel.HasSymbolizedPreview);
        Assert.Equal(PreviewMode.Original, viewModel.PreviewMode);
    }

    [Fact]
    public async Task SwitchingBetweenImageAndVideo_ClearsIncompatiblePreviewState()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("mode.mp4");
        string imagePath = files.Create("mode.png");
        TestHarness harness = ReadyVideoHarness();
        DecodedImage image = SolidFrame(25, 50, 75);
        harness.ImagePipeline.DecodeResult = image;
        harness.MediaInput.Handler = (path, _) => Task.FromResult(
            Path.GetExtension(path!).Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? ImageResult(path!)
                : VideoResult(path!));
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await viewModel.PreviewCommand.ExecuteAsync(null);
        await viewModel.DropMediaCommand.ExecuteAsync([imagePath]);

        Assert.True(viewModel.IsImageSelected);
        Assert.False(viewModel.IsVideoSelected);
        Assert.False(viewModel.HasSymbolizedPreview);
        Assert.Equal(0d, viewModel.TimelinePosition);
        Assert.False(viewModel.IsVideoSeekEnabled);
        Assert.Equal(image, harness.ImageSources.LastDecodedImage);
        Assert.True(viewModel.PreviewCommand.CanExecute(null));
        Assert.True(viewModel.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task UnexpectedFrameReaderFailure_BecomesStableFriendlyState()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("native-failure.mp4");
        TestHarness harness = new();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, _, _, _) => throw new InvalidOperationException("raw native failure");
        using MainWindowViewModel viewModel = harness.CreateViewModel();

        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);

        Assert.True(viewModel.HasVideoFrameError);
        Assert.Equal("GlyphFrame could not load this video frame.", viewModel.VideoFrameStatusMessage);
        Assert.DoesNotContain("raw native failure", viewModel.VideoFrameStatusMessage, StringComparison.Ordinal);
        Assert.Contains("raw native failure", viewModel.VideoFrameTechnicalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NearestDecodedPosition_ReconcilesSliderAndFormattedTime()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("nearest.mp4");
        TestHarness harness = ReadyVideoHarness();
        using MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        harness.VideoFrames.Handler = (_, requested, metadata, _) => Task.FromResult(
            FrameResult(SolidFrame(0, 0, 255), requested, metadata, actualPosition: 0.75d));

        viewModel.TimelinePosition = 0.8d;
        await WaitUntilAsync(viewModel, () => !viewModel.IsVideoFrameLoading);

        Assert.Equal(0.75d, viewModel.TimelinePosition, 3);
        Assert.Contains("0:07", viewModel.TimelineText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisposingViewModel_CancelsAndRejectsLateFrameCompletion()
    {
        using TestMediaFiles files = new();
        string videoPath = files.Create("closing.mp4");
        TestHarness harness = new();
        TaskCompletionSource<bool> started = NewCompletion<bool>();
        TaskCompletionSource<VideoFrameReadResult> pending = NewCompletion<VideoFrameReadResult>();
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, _, _, _) =>
        {
            started.TrySetResult(true);
            return pending.Task;
        };
        MainWindowViewModel viewModel = harness.CreateViewModel();

        Task selection = viewModel.DropMediaCommand.ExecuteAsync([videoPath]);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        viewModel.Dispose();
        pending.SetResult(FrameResult(SolidFrame(255, 0, 0), 0.5d, VideoMetadataFor(videoPath)));
        await selection;

        Assert.Null(harness.ImageSources.LastDecodedImage);
        Assert.False(viewModel.HasVideoFrame);
    }

    private static TestHarness ReadyVideoHarness()
    {
        TestHarness harness = new();
        DecodedImage frame = SolidFrame(100, 120, 140);
        harness.MediaInput.Handler = (path, _) => Task.FromResult(VideoResult(path!));
        harness.VideoFrames.Handler = (_, position, metadata, _) =>
            Task.FromResult(FrameResult(frame, position, metadata));
        return harness;
    }

    private static MediaInputResult VideoResult(string path) => MediaInputResult.ForVideo(VideoMetadataFor(path));

    private static VideoMetadata VideoMetadataFor(string path) => new(
        path,
        Path.GetFileName(path),
        64,
        48,
        TimeSpan.FromSeconds(10),
        10,
        100,
        "MP4",
        new FileInfo(path).Length,
        "h264",
        false,
        null,
        null);

    private static MediaInputResult ImageResult(string path) => MediaInputResult.ForImage(new ImageMetadata(
        path,
        Path.GetFileName(path),
        2,
        1,
        2,
        1,
        "PNG",
        new FileInfo(path).Length,
        ImageOrientation.Normal,
        null));

    private static VideoFrameReadResult FrameResult(
        DecodedImage frame,
        double position,
        VideoMetadata? metadata,
        double? actualPosition = null) => VideoFrameReadResult.ForFrame(
            frame,
            position,
            actualPosition ?? position,
            metadata?.Duration is TimeSpan duration ? TimeSpan.FromTicks((long)(duration.Ticks * position)) : null,
            metadata?.Duration is TimeSpan actualDuration ? TimeSpan.FromTicks((long)(actualDuration.Ticks * (actualPosition ?? position))) : null,
            metadata?.FrameCount is long count ? (long)Math.Round((count - 1) * (actualPosition ?? position)) : null,
            metadata?.Duration,
            metadata?.FrameCount);

    private static DecodedImage SolidFrame(byte red, byte green, byte blue) => new(
        2,
        1,
        [red, green, blue, 255, red, green, blue, 255]);

    private static ImagePipelineResult RenderResult() => new(
        new RenderedImage(2, 1, [10, 20, 30, 255, 10, 20, 30, 255]),
        new SymbolGrid(1, 1, new PixelDimensions(2, 1)),
        new GlyphMetrics("Cascadia Mono", "Cascadia Mono", 10, 5, 10, 8, false));

    private static TaskCompletionSource<T> NewCompletion<T>() => new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(MainWindowViewModel viewModel, Func<bool> predicate)
    {
        if (predicate())
        {
            return;
        }

        TaskCompletionSource<bool> completed = NewCompletion<bool>();
        PropertyChangedEventHandler? handler = null;
        handler = (_, _) =>
        {
            if (predicate())
            {
                viewModel.PropertyChanged -= handler;
                completed.TrySetResult(true);
            }
        };
        viewModel.PropertyChanged += handler;
        try
        {
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            viewModel.PropertyChanged -= handler;
        }
    }

    private sealed class TestHarness
    {
        public FakeMediaInputService MediaInput { get; } = new();

        public FakeVideoFrameReader VideoFrames { get; } = new();

        public FakeImagePipelineService ImagePipeline { get; } = new();

        public FakeImageSourceFactory ImageSources { get; } = new();

        public FakeVideoExportService VideoExports { get; } = new();

        public FakeImageSaveDialogService SaveDialogs { get; } = new();

        public FakeUserDialogService UserDialogs { get; } = new();

        public MainWindowViewModel CreateViewModel(
            IVideoFrameReader? videoFrameReader = null,
            bool allowNull = false) => new(
            ApplicationInfo.Phase9,
            new FakeSettingsManager(),
            new FakeSettingsDialogService(),
            new FakeMediaDialogService(),
            MediaInput,
            allowNull ? videoFrameReader! : videoFrameReader ?? VideoFrames,
            VideoExports,
            ImagePipeline,
            new FakeImageExporter(),
            SaveDialogs,
            ImageSources,
            UserDialogs,
            new CharacterPresetProvider());
    }

    private sealed class FakeVideoFrameReader : IVideoFrameReader
    {
        public Func<string, double, VideoMetadata?, CancellationToken, Task<VideoFrameReadResult>> Handler { get; set; } =
            (_, position, metadata, _) => Task.FromResult(FrameResult(SolidFrame(1, 2, 3), position, metadata));

        public Task<VideoFrameReadResult> ReadAsync(
            string inputPath,
            double normalizedPosition,
            VideoMetadata? metadataContext = null,
            CancellationToken cancellationToken = default) =>
            Handler(inputPath, normalizedPosition, metadataContext, cancellationToken);
    }

    private sealed class FakeMediaInputService : IMediaInputService
    {
        public Func<string?, CancellationToken, Task<MediaInputResult>> Handler { get; set; } =
            (path, _) => Task.FromResult(VideoResult(path!));

        public Task<MediaInputResult> LoadAsync(string? inputPath, CancellationToken cancellationToken = default) =>
            Handler(inputPath, cancellationToken);
    }

    private sealed class FakeVideoExportService : IVideoExportService
    {
        public Func<VideoExportJob, CancellationToken, Task<VideoExportResult>> Handler { get; set; } =
            (job, _) => Task.FromResult(VideoExportResult.Completed(
                job.OutputPath,
                1,
                new PixelDimensions(2, 2),
                30));

        public VideoExportJob? LastJob { get; private set; }

        public int Calls { get; private set; }

        public Task<VideoExportResult> ExportAsync(
            VideoExportJob job,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastJob = job;
            return Handler(job, cancellationToken);
        }
    }

    private sealed class FakeImagePipelineService : IImagePipelineService
    {
        public DecodedImage DecodeResult { get; set; } = SolidFrame(4, 5, 6);

        public DecodedImage? LastRenderedSource { get; private set; }

        public ImageRenderSettings? LastRenderSettings { get; private set; }

        public CharacterPresetOption? LastRenderedPreset { get; private set; }

        public Func<DecodedImage, ImageRenderSettings, CharacterPresetOption, CancellationToken, Task<ImagePipelineResult>> RenderHandler { get; set; } =
            (_, _, _, _) => Task.FromResult(RenderResult());

        public Task<DecodedImage> DecodeAsync(
            string inputPath,
            ImageOrientation orientation,
            CancellationToken cancellationToken = default) => Task.FromResult(DecodeResult);

        public Task<ImagePipelineResult> RenderAsync(
            DecodedImage source,
            ImageRenderSettings settings,
            CharacterPresetOption preset,
            CancellationToken cancellationToken = default)
        {
            LastRenderedSource = source;
            LastRenderSettings = settings;
            LastRenderedPreset = preset;
            return RenderHandler(source, settings, preset, cancellationToken);
        }
    }

    private sealed class FakeImageSourceFactory : IImageSourceFactory
    {
        public DecodedImage? LastDecodedImage { get; private set; }

        public ImageSource Create(DecodedImage image)
        {
            LastDecodedImage = image;
            return new DrawingImage();
        }

        public ImageSource Create(RenderedImage image) => new DrawingImage();
    }

    private sealed class FakeSettingsManager : IApplicationSettingsManager
    {
        public AppSettings Current { get; private set; } = new();

        public string? StartupStatusMessage => null;

        public event EventHandler<AppSettings>? SettingsChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsChanged?.Invoke(this, settings);
            return Task.CompletedTask;
        }

        public Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default)
        {
            Current = new AppSettings();
            SettingsChanged?.Invoke(this, Current);
            return Task.FromResult(Current);
        }
    }

    private sealed class FakeSettingsDialogService : ISettingsDialogService
    {
        public void Show()
        {
        }
    }

    private sealed class FakeMediaDialogService : IMediaFileDialogService
    {
        public string? SelectMediaFile() => null;
    }

    private sealed class FakeImageExporter : IImageExporter
    {
        public Task ExportAsync(
            RenderedImage image,
            string outputPath,
            ImageExportFormat format,
            bool overwrite,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeImageSaveDialogService : IImageSaveDialogService
    {
        public string? VideoOutputPath { get; set; }

        public string? SelectOutputFile(string sourcePath, ImageExportFormat format) => null;

        public string? SelectVideoOutputFile(string sourcePath) => VideoOutputPath;
    }

    private sealed class FakeUserDialogService : IUserDialogService
    {
        public bool ConfirmResult { get; set; } = true;

        public bool Confirm(
            string title,
            string message,
            string confirmButtonText = "Confirm",
            string cancelButtonText = "Cancel") => ConfirmResult;
    }

    private sealed class TestMediaFiles : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "GlyphFrame.App.Tests",
            Guid.NewGuid().ToString("N"));

        public TestMediaFiles() => Directory.CreateDirectory(_root);

        public string Create(string fileName)
        {
            string path = Path.Combine(_root, fileName);
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            return path;
        }

        public string PathFor(string fileName) => Path.Combine(_root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
