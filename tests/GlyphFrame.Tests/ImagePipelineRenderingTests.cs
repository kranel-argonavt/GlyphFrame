using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Rendering.Conversion;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Rendering.Mapping;
using GlyphFrame.Rendering.Skia;

namespace GlyphFrame.Tests;

public sealed class ImagePipelineRenderingTests
{
    private readonly ImagePipelineService _pipeline = CreatePipeline();

    [Fact]
    public async Task RenderAsync_ProducesRequestedDimensionsAndColoredGlyphRegions()
    {
        DecodedImage source = SolidImage(160, 90, 220, 30, 30, 255);
        ImageRenderSettings settings = new("Consolas", 10, OutputResolution.Hd720);
        CharacterPresetOption preset = new(CharacterPresetKind.SimpleAscii, "Simple ASCII", " .:-=+*#%@");

        ImagePipelineResult result = await _pipeline.RenderAsync(source, settings, preset);

        Assert.Equal(new PixelDimensions(1280, 720), result.RenderedImage.Dimensions);
        Assert.True(result.Grid.Columns > 0);
        Assert.True(result.Grid.Rows > 0);
        RenderedImageAssertions.ContainsPixelsDifferentFromBackground(result.RenderedImage);
    }

    [Fact]
    public async Task RenderAsync_TransparentImageUsesOpaqueDarkBackgroundConsistently()
    {
        DecodedImage source = SolidImage(32, 18, 255, 0, 0, 0);

        ImagePipelineResult result = await _pipeline.RenderAsync(
            source,
            new ImageRenderSettings("Consolas", 9, OutputResolution.SameAsSource),
            new CharacterPresetOption(CharacterPresetKind.SimpleAscii, "Simple ASCII", " .#"));

        ReadOnlySpan<byte> pixels = result.RenderedImage.RgbaPixels.Span;
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            Assert.Equal(PixelColor.SymbolBackground.Red, pixels[offset]);
            Assert.Equal(PixelColor.SymbolBackground.Green, pixels[offset + 1]);
            Assert.Equal(PixelColor.SymbolBackground.Blue, pixels[offset + 2]);
            Assert.Equal((byte)255, pixels[offset + 3]);
        }
    }

    [Fact]
    public async Task RenderAsync_IsDeterministicForStableInputsAndFont()
    {
        DecodedImage source = SolidImage(96, 54, 40, 180, 220, 255);
        ImageRenderSettings settings = new("Consolas", 10, OutputResolution.SameAsSource);
        CharacterPresetOption preset = new(CharacterPresetKind.Blocks, "Blocks", " ░▒▓█", true);

        ImagePipelineResult first = await _pipeline.RenderAsync(source, settings, preset);
        ImagePipelineResult second = await _pipeline.RenderAsync(source, settings, preset);

        Assert.Equal(first.Grid, second.Grid);
        Assert.Equal(first.RenderedImage.RgbaPixels.ToArray(), second.RenderedImage.RgbaPixels.ToArray());
    }

    [Fact]
    public async Task RenderAsync_HonorsCancellation()
    {
        DecodedImage source = SolidImage(128, 72, 0, 0, 0, 255);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _pipeline.RenderAsync(
            source,
            ImageRenderSettings.Default,
            new CharacterPresetOption(CharacterPresetKind.SimpleAscii, "Simple ASCII", " .#"),
            cancellation.Token));
    }

    private static ImagePipelineService CreatePipeline()
    {
        BrightnessToSymbolMapper mapper = new();
        return new ImagePipelineService(
            new SkiaImageDecoder(),
            new OutputDimensionResolver(),
            new SkiaGlyphMeasurer(),
            new SymbolGridCalculator(),
            new FrameToSymbolConverter(mapper),
            new SkiaSymbolFrameRenderer());
    }

    private static DecodedImage SolidImage(
        int width,
        int height,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = red;
            pixels[offset + 1] = green;
            pixels[offset + 2] = blue;
            pixels[offset + 3] = alpha;
        }

        return new DecodedImage(width, height, pixels);
    }
}

internal static class RenderedImageAssertions
{
    public static void ContainsPixelsDifferentFromBackground(RenderedImage image)
    {
        ReadOnlySpan<byte> pixels = image.RgbaPixels.Span;
        bool found = false;
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset] != PixelColor.SymbolBackground.Red
                || pixels[offset + 1] != PixelColor.SymbolBackground.Green
                || pixels[offset + 2] != PixelColor.SymbolBackground.Blue)
            {
                found = true;
                break;
            }
        }

        Assert.True(found, "Expected at least one rendered glyph pixel outside the background color.");
    }
}
