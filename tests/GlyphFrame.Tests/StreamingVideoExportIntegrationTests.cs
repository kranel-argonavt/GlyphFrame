using System.Text.Json;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Presets;
using GlyphFrame.Core.Policies;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Rendering.Conversion;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Rendering.Mapping;
using GlyphFrame.Rendering.Skia;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class StreamingVideoExportIntegrationTests
{
    [Fact]
    public async Task ExportAsync_RealRotatedUnicodeVideoProducesSymbolizedVideoOnlyMp4()
    {
        using TemporaryMediaDirectory media = new("експорт 日本語 测试");
        string input = media.CopyFixture("video-preview-rotated.mov", "обернене джерело 日本語.MOV");
        string output = Path.Combine(media.RootPath, "символізоване відео 测试.mp4");
        BundledFfmpegLocator locator = new(RepositoryRoot);
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

        VideoExportResult result = await service.ExportAsync(job);

        Assert.True(result.Success, result.TechnicalDetail ?? result.UserMessage);
        Assert.Equal(12, result.FramesWritten);
        Assert.Equal(new PixelDimensions(240, 320), result.OutputDimensions);
        Assert.Equal(10, result.FramesPerSecond);
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
}
