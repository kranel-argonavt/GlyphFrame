using System.Text.Json;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Presets;
using GlyphFrame.Core.Policies;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Infrastructure.Paths;
using GlyphFrame.Rendering.Conversion;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Rendering.Mapping;
using GlyphFrame.Rendering.Skia;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class StreamingVideoExportIntegrationTests
{
    [Theory]
    [InlineData("video-preview.mp4", 64, 48)]
    [InlineData("video-preview.mov", 64, 48)]
    [InlineData("video-preview.mkv", 64, 48)]
    [InlineData("video-preview.avi", 64, 48)]
    [InlineData("video-preview-rotated.mov", 240, 320)]
    public async Task ManagedRender_RealUnicodeContainerRendersAndPublishesAcceptedArtifact(
        string fixture, int displayWidth, int displayHeight)
    {
        using TemporaryMediaDirectory media = new("керований кеш 日本語");
        string input = media.CopyFixture(fixture, "джерело артефакту 日本語" + Path.GetExtension(fixture));
        byte[] sourceBytes = await File.ReadAllBytesAsync(input);
        BundledFfmpegLocator locator = new(Path.Combine(RepositoryRoot, "artifacts", "local-runtime", "controlled-9.0-glyphframe.2"));
        ProcessRunner processRunner = new();
        using FfmpegValidationService validation = new(locator, processRunner);
        FfprobeVideoMetadataReader metadataReader = new(
            new MetadataExtractorVideoMetadataReader(),
            validation,
            processRunner);
        VideoMetadata metadata = await metadataReader.ReadAsync(input);
        Assert.Equal(displayWidth, metadata.DisplayWidth);
        Assert.Equal(displayHeight, metadata.DisplayHeight);
        ImagePipelineService pipeline = CreatePipeline();
        StreamingVideoExportService exportService = new(
            new FfmpegSequentialVideoFrameReader(locator),
            new FfmpegVideoEncoder(locator),
            pipeline,
            new OutputDimensionResolver(),
            new FfprobeAudioStreamProbe(validation, processRunner),
            new Mp4AudioCompatibilityPolicy(),
            new FfmpegAudioMuxer(locator));
        ApplicationPaths paths = new(media.RootPath);
        using ManagedVideoArtifactStore store = new(paths, "сесія review");
        ManagedVideoRenderService renderService = new(exportService, store);
        ImageRenderSettings settings = new(
            "Consolas",
            9,
            OutputResolution.SameAsSource,
            SymbolPolarity.LightOnDark);
        CharacterPresetOption preset = new CharacterPresetProvider().Get(CharacterPresetKind.SimpleAscii);
        FullVideoRenderIdentity identity = new(
            1,
            1,
            0,
            preset.Kind,
            preset.Characters,
            settings.FontFamily,
            settings.FontSize,
            settings.OutputResolution,
            settings.SymbolPolarity,
            AudioExportMode.RemoveAudio);

        ManagedVideoRenderResult result = await renderService.RenderAsync(new ManagedVideoRenderJob(
            input,
            metadata,
            settings,
            preset,
            AudioExportMode.RemoveAudio,
            identity));

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        ManagedVideoArtifact artifact = result.Artifact!;
        Assert.True(store.OwnsPath(artifact.FilePath));
        Assert.Equal(new PixelDimensions(displayWidth, displayHeight), artifact.OutputDimensions);
        Assert.Equal(12, artifact.RenderedFrameCount);
        Assert.Equal(10, artifact.FramesPerSecond);
        Assert.Equal(TimeSpan.FromSeconds(1.2), artifact.Duration);
        Assert.Equal(AudioMuxOutcome.Removed, artifact.AudioOutcome);
        Assert.True(artifact.FileSizeBytes > 1_000);
        Assert.Empty(Directory.GetFiles(store.SessionDirectory, "*.partial"));
        Assert.Empty(Directory.GetFiles(store.SessionDirectory, "*.pending.mp4"));

        using JsonDocument probe = await ProbeAsync(artifact.FilePath, locator, processRunner);
        JsonElement streams = probe.RootElement.GetProperty("streams");
        JsonElement video = streams.EnumerateArray()
            .Single(stream => stream.GetProperty("codec_type").GetString() == "video");
        Assert.Equal("h264", video.GetProperty("codec_name").GetString());
        Assert.Equal("yuv420p", video.GetProperty("pix_fmt").GetString());
        Assert.Equal(displayWidth, video.GetProperty("width").GetInt32());
        Assert.Equal(displayHeight, video.GetProperty("height").GetInt32());
        Assert.Equal("12", video.GetProperty("nb_frames").GetString());
        Assert.Equal("10/1", video.GetProperty("avg_frame_rate").GetString());
        Assert.Contains("mp4", probe.RootElement.GetProperty("format").GetProperty("format_name").GetString()!);
        Assert.DoesNotContain(
            streams.EnumerateArray(),
            stream => stream.GetProperty("codec_type").GetString() == "audio");
        VideoMetadata renderedMetadata = await metadataReader.ReadAsync(artifact.FilePath);
        FfmpegVideoFrameReader frames = new(locator, new BinaryProcessRunner(), metadataReader);
        VideoFrameReadResult sourceFrame = await frames.ReadAsync(input, 0.5d, metadata);
        VideoFrameReadResult renderedFrame = await frames.ReadAsync(artifact.FilePath, 0.5d, renderedMetadata);
        Assert.True(sourceFrame.Success, sourceFrame.TechnicalDetail ?? sourceFrame.UserMessage);
        Assert.True(renderedFrame.Success, renderedFrame.TechnicalDetail ?? renderedFrame.UserMessage);
        ImagePipelineResult expected = await pipeline.RenderAsync(sourceFrame.Frame!, settings, preset);
        double symbolError = RgbMeanAbsoluteError(renderedFrame.Frame!, expected.RenderedImage);
        double sourceError = RgbMeanAbsoluteError(renderedFrame.Frame!, sourceFrame.Frame!);
        Assert.True(symbolError < 18d, $"{fixture}: symbol-art RGB MAE was {symbolError:F3}.");
        Assert.True(symbolError * 2d < sourceError, $"{fixture}: output did not match symbol art more closely than source pixels.");
        (double coverage, double agreement) = BackgroundAgreement(
            expected.RenderedImage, renderedFrame.Frame!, PixelColor.SymbolBackground, tolerance: 24);
        Assert.True(coverage > 0.45d);
        Assert.True(agreement > 0.80d);
        await using ManagedVideoArtifactSession session = new(store);
        await session.AcceptAsync(artifact);
        Assert.Same(artifact, session.Current);
        VideoArtifactPublisher publisher = new(store);
        DateTime artifactWriteTime = File.GetLastWriteTimeUtc(artifact.FilePath);
        List<string> destinations = [];
        for (int copy = 0; copy < 2; copy++)
        {
            string destination = Path.Combine(media.RootPath, $"переглянутий 日本語 {copy}.mp4");
            destinations.Add(destination);
            VideoPublicationDestination prepared = await publisher.PrepareAsync(destination, input);
            VideoExportResult published = await session.UseCurrentAsync(artifact,
                token => publisher.PublishAsync(new(artifact, input, prepared, false), token));
            Assert.True(published.Success, published.TechnicalDetail);
            Assert.Same(artifact, session.Current);
            Assert.Equal(artifactWriteTime, File.GetLastWriteTimeUtc(artifact.FilePath));
            Assert.Equal(await File.ReadAllBytesAsync(artifact.FilePath), await File.ReadAllBytesAsync(destination));
            using JsonDocument exportedProbe = await ProbeAsync(destination, locator, processRunner);
            Assert.Equal(video.GetProperty("nb_frames").GetString(), exportedProbe.RootElement.GetProperty("streams")[0].GetProperty("nb_frames").GetString());
            using FileStream destinationExclusive = new(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.pending.mp4", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.png", SearchOption.AllDirectories));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(input));
        using (FileStream sourceExclusive = new(input, FileMode.Open, FileAccess.Read, FileShare.None))
        using (FileStream artifactExclusive = new(artifact.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.True(sourceExclusive.CanRead);
            Assert.True(artifactExclusive.CanRead);
        }
        await session.DisposeAsync();
        Assert.Null(session.Current);
        Assert.False(File.Exists(artifact.FilePath));
        Assert.False(Directory.Exists(store.SessionDirectory));
        Assert.All(destinations, destination => Assert.True(File.Exists(destination)));
    }

    [Fact]
    public async Task ExportAsync_RealRotatedUnicodeVideoProducesSymbolizedVideoOnlyMp4()
    {
        using TemporaryMediaDirectory media = new("експорт 日本語 测试");
        string input = media.CopyFixture("video-preview-rotated.mov", "обернене джерело 日本語.MOV");
        string output = Path.Combine(media.RootPath, "символізоване відео 测试.mp4");
        BundledFfmpegLocator locator = new(Path.Combine(RepositoryRoot, "artifacts", "local-runtime", "controlled-9.0-glyphframe.2"));
        ProcessRunner processRunner = new();
        using FfmpegValidationService validation = new(locator, processRunner);
        FfprobeVideoMetadataReader metadataReader = new(
            new MetadataExtractorVideoMetadataReader(),
            validation,
            processRunner);
        VideoMetadata inputMetadata = await metadataReader.ReadAsync(input);
        ImagePipelineService pipeline = CreatePipeline();
        StreamingVideoExportService service = new(
            new FfmpegSequentialVideoFrameReader(locator),
            new FfmpegVideoEncoder(locator),
            pipeline,
            new OutputDimensionResolver(),
            new FfprobeAudioStreamProbe(validation, processRunner),
            new Mp4AudioCompatibilityPolicy(),
            new FfmpegAudioMuxer(locator));
        CharacterPresetOption preset = new CharacterPresetProvider().Get(CharacterPresetKind.SimpleAscii);
        VideoExportJob job = new(
            input,
            output,
            inputMetadata,
            new ImageRenderSettings("Consolas", 9, OutputResolution.SameAsSource, SymbolPolarity.LightOnDark),
            preset,
            Overwrite: false,
            AudioMode: AudioExportMode.RemoveAudio);
        RecordingProgress progress = new();

        VideoExportResult result = await service.ExportAsync(
            job,
            CancellationToken.None,
            progress);

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(12, result.FramesWritten);
        Assert.Equal(new PixelDimensions(240, 320), result.OutputDimensions);
        Assert.Equal(10, result.FramesPerSecond);
        Assert.True(progress.Values.Count(update => update.Stage == VideoExportStage.ProcessingFrames) > 2);
        Assert.Equal(12, progress.Values.Last(update => update.ProcessedFrames > 0).ProcessedFrames);
        Assert.Equal(VideoExportStage.Completed, progress.Values[^1].Stage);
        Assert.Equal(100d, progress.Values[^1].Percentage);
        Assert.True(new FileInfo(output).Length > 1_000);
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.raw"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.png"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.tmp"));

        JsonDocument probe = await ProbeAsync(output, locator, processRunner);
        JsonElement streams = probe.RootElement.GetProperty("streams");
        JsonElement video = streams.EnumerateArray()
            .Single(stream => stream.GetProperty("codec_type").GetString() == "video");
        Assert.Equal("h264", video.GetProperty("codec_name").GetString());
        Assert.Equal("yuv420p", video.GetProperty("pix_fmt").GetString());
        Assert.Equal(240, video.GetProperty("width").GetInt32());
        Assert.Equal(320, video.GetProperty("height").GetInt32());
        Assert.Equal("10/1", video.GetProperty("avg_frame_rate").GetString());
        Assert.Equal("12", video.GetProperty("nb_frames").GetString());
        Assert.DoesNotContain(
            streams.EnumerateArray(),
            stream => stream.GetProperty("codec_type").GetString() == "audio");
        Assert.Contains(
            "mp4",
            probe.RootElement.GetProperty("format").GetProperty("format_name").GetString()!,
            StringComparison.OrdinalIgnoreCase);

        VideoMetadata outputMetadata = await metadataReader.ReadAsync(output);
        FfmpegVideoFrameReader previewReader = new(locator, new BinaryProcessRunner(), metadataReader);
        VideoFrameReadResult sourceFrame = await previewReader.ReadAsync(input, 0.5d, inputMetadata);
        VideoFrameReadResult exportedFrame = await previewReader.ReadAsync(output, 0.5d, outputMetadata);
        Assert.True(sourceFrame.Success, sourceFrame.TechnicalDetail ?? sourceFrame.UserMessage);
        Assert.True(exportedFrame.Success, exportedFrame.TechnicalDetail ?? exportedFrame.UserMessage);
        Assert.Equal(sourceFrame.Frame!.Dimensions, exportedFrame.Frame!.Dimensions);
        ImagePipelineResult expectedResult = await pipeline.RenderAsync(
            sourceFrame.Frame,
            job.RenderSettings,
            preset);
        RenderedImage expectedSymbolized = expectedResult.RenderedImage;
        double expectedMeanError = RgbMeanAbsoluteError(exportedFrame.Frame, expectedSymbolized);
        double originalMeanError = RgbMeanAbsoluteError(exportedFrame.Frame, sourceFrame.Frame);
        (double expectedBackgroundCoverage, double exportedBackgroundAgreement) = BackgroundAgreement(
            expectedSymbolized,
            exportedFrame.Frame,
            PixelColor.SymbolBackground,
            tolerance: 24);

        Assert.True(
            expectedMeanError < 18d,
            $"Exported frame RGB MAE from expected symbol art was {expectedMeanError:F3}.");
        Assert.True(
            expectedMeanError * 2d < originalMeanError,
            $"Expected symbol-art MAE {expectedMeanError:F3} was not substantially lower than original-frame MAE {originalMeanError:F3}.");
        Assert.True(
            expectedBackgroundCoverage > 0.45d,
            $"Expected symbol-art background coverage was only {expectedBackgroundCoverage:P2}.");
        Assert.True(
            exportedBackgroundAgreement > 0.80d,
            $"Only {exportedBackgroundAgreement:P2} of expected background pixels retained the glyph-canvas structure.");

        probe.Dispose();
        using FileStream inputExclusive = new(input, FileMode.Open, FileAccess.Read, FileShare.None);
        using FileStream outputExclusive = new(output, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(inputExclusive.CanRead);
        Assert.True(outputExclusive.CanRead);
    }

    [Fact]
    public async Task ExportAsync_RealFfmpegCancellationAfterAcceptedFramesCleansOutputAndReleasesSource()
    {
        using TemporaryMediaDirectory media = new("скасування 日本語 测试");
        string input = media.CopyFixture("video-preview-rotated.mov", "джерело скасування 日本語.MOV");
        string output = Path.Combine(media.RootPath, "скасований результат 测试.mp4");
        BundledFfmpegLocator locator = new(Path.Combine(RepositoryRoot, "artifacts", "local-runtime", "controlled-9.0-glyphframe.2"));
        ProcessRunner processRunner = new();
        using FfmpegValidationService validation = new(locator, processRunner);
        FfprobeVideoMetadataReader metadataReader = new(
            new MetadataExtractorVideoMetadataReader(),
            validation,
            processRunner);
        VideoMetadata metadata = await metadataReader.ReadAsync(input);
        StreamingVideoExportService service = new(
            new FfmpegSequentialVideoFrameReader(locator),
            new FfmpegVideoEncoder(locator),
            CreatePipeline(),
            new OutputDimensionResolver(),
            new FfprobeAudioStreamProbe(validation, processRunner),
            new Mp4AudioCompatibilityPolicy(),
            new FfmpegAudioMuxer(locator));
        VideoExportJob job = new(
            input,
            output,
            metadata,
            new ImageRenderSettings("Consolas", 9, OutputResolution.SameAsSource, SymbolPolarity.LightOnDark),
            new CharacterPresetProvider().Get(CharacterPresetKind.SimpleAscii),
            Overwrite: false,
            AudioMode: AudioExportMode.RemoveAudio);
        using CancellationTokenSource cancellation = new();
        RecordingProgress progress = new(update =>
        {
            if (update.Stage == VideoExportStage.ProcessingFrames && update.ProcessedFrames >= 2)
            {
                cancellation.Cancel();
            }
        });

        VideoExportResult result = await service.ExportAsync(job, cancellation.Token, progress);

        Assert.Equal(VideoExportStatus.Cancelled, result.Status);
        Assert.Contains(progress.Values, update => update.ProcessedFrames >= 2);
        Assert.Equal(VideoExportStage.Cancelling, progress.Values[^1].Stage);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.partial"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.tmp"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.raw"));
        Assert.Empty(Directory.GetFiles(media.RootPath, "*.png"));
        using FileStream inputExclusive = new(input, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(inputExclusive.CanRead);
    }

    private static double RgbMeanAbsoluteError(DecodedImage actual, RenderedImage expected) =>
        RgbMeanAbsoluteError(actual.RgbaPixels.Span, expected.RgbaPixels.Span, actual.Dimensions, expected.Dimensions);

    private static double RgbMeanAbsoluteError(DecodedImage actual, DecodedImage expected) =>
        RgbMeanAbsoluteError(actual.RgbaPixels.Span, expected.RgbaPixels.Span, actual.Dimensions, expected.Dimensions);

    private static double RgbMeanAbsoluteError(
        ReadOnlySpan<byte> actual,
        ReadOnlySpan<byte> expected,
        PixelDimensions actualDimensions,
        PixelDimensions expectedDimensions)
    {
        Assert.Equal(expectedDimensions, actualDimensions);
        long absoluteDifference = 0;
        for (int offset = 0; offset < actual.Length; offset += 4)
        {
            absoluteDifference += Math.Abs(actual[offset] - expected[offset]);
            absoluteDifference += Math.Abs(actual[offset + 1] - expected[offset + 1]);
            absoluteDifference += Math.Abs(actual[offset + 2] - expected[offset + 2]);
        }

        return absoluteDifference / (actualDimensions.Width * actualDimensions.Height * 3d);
    }

    private static (double ExpectedCoverage, double ExportedAgreement) BackgroundAgreement(
        RenderedImage expected,
        DecodedImage actual,
        PixelColor background,
        int tolerance)
    {
        Assert.Equal(expected.Dimensions, actual.Dimensions);
        ReadOnlySpan<byte> expectedPixels = expected.RgbaPixels.Span;
        ReadOnlySpan<byte> actualPixels = actual.RgbaPixels.Span;
        int expectedBackground = 0;
        int matchingBackground = 0;
        for (int offset = 0; offset < expectedPixels.Length; offset += 4)
        {
            if (expectedPixels[offset] != background.Red
                || expectedPixels[offset + 1] != background.Green
                || expectedPixels[offset + 2] != background.Blue)
            {
                continue;
            }

            expectedBackground++;
            if (Math.Abs(actualPixels[offset] - background.Red) <= tolerance
                && Math.Abs(actualPixels[offset + 1] - background.Green) <= tolerance
                && Math.Abs(actualPixels[offset + 2] - background.Blue) <= tolerance)
            {
                matchingBackground++;
            }
        }

        int pixelCount = expected.Dimensions.Width * expected.Dimensions.Height;
        return (
            expectedBackground / (double)pixelCount,
            matchingBackground / (double)Math.Max(1, expectedBackground));
    }

    private static async Task<JsonDocument> ProbeAsync(
        string output,
        BundledFfmpegLocator locator,
        ProcessRunner runner)
    {
        ProcessExecutionResult result = await runner.RunAsync(new ProcessExecutionRequest(
            locator.Locate().Ffprobe.ResolvedPath,
            ["-v", "error", "-show_streams", "-show_format", "-of", "json", output],
            TimeSpan.FromSeconds(20)));
        Assert.True(result.Succeeded, result.StandardError);
        return JsonDocument.Parse(result.StandardOutput);
    }

    private static ImagePipelineService CreatePipeline() => new(
        new SkiaImageDecoder(),
        new OutputDimensionResolver(),
        new SkiaGlyphMeasurer(),
        new SymbolGridCalculator(),
        new FrameToSymbolConverter(new BrightnessToSymbolMapper()),
        new SkiaSymbolFrameRenderer());

    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class RecordingProgress(Action<VideoExportProgress>? callback = null)
        : IProgress<VideoExportProgress>
    {
        public List<VideoExportProgress> Values { get; } = [];

        public void Report(VideoExportProgress value)
        {
            Values.Add(value);
            callback?.Invoke(value);
        }
    }
}
