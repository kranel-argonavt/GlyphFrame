using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Rendering.Conversion;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Rendering.Mapping;
using GlyphFrame.Rendering.Skia;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class SymbolPolarityExportTests
{
    [Theory]
    [InlineData(SymbolPolarity.LightOnDark, ImageExportFormat.Png, "polarity.png")]
    [InlineData(SymbolPolarity.LightOnDark, ImageExportFormat.Jpeg, "polarity.jpg")]
    [InlineData(SymbolPolarity.DarkOnLight, ImageExportFormat.Png, "polarity.png")]
    [InlineData(SymbolPolarity.DarkOnLight, ImageExportFormat.Jpeg, "polarity.jpg")]
    public async Task ExportAndReopen_PreservesSelectedOpaqueBackground(
        SymbolPolarity polarity,
        ImageExportFormat format,
        string fileName)
    {
        using TemporaryMediaDirectory media = new();
        string path = Path.Combine(media.RootPath, fileName);
        PixelColor expected = SymbolBackgroundResolver.Resolve(polarity);
        ImagePipelineService pipeline = CreatePipeline();
        SkiaImageExporter exporter = new();
        SkiaImageDecoder decoder = new();
        DecodedImage transparentSource = SolidTransparentImage(24, 16);

        ImagePipelineResult result = await pipeline.RenderAsync(
            transparentSource,
            new ImageRenderSettings("Consolas", 9, OutputResolution.SameAsSource, polarity),
            new CharacterPresetOption(CharacterPresetKind.SimpleAscii, "Simple ASCII", " .#"));
        await exporter.ExportAsync(result.RenderedImage, path, format, overwrite: false);
        DecodedImage reopened = await decoder.DecodeAsync(path, ImageOrientation.Normal);

        Assert.Equal(new PixelDimensions(24, 16), reopened.Dimensions);
        AssertOpaqueBackground(result.RenderedImage.RgbaPixels.Span, expected, tolerance: 0);
        AssertOpaqueBackground(reopened.RgbaPixels.Span, expected, format == ImageExportFormat.Png ? 0 : 2);
        using (FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(stream.Length > 0);
        }

        Assert.Empty(Directory.EnumerateFiles(media.RootPath, "*.tmp", SearchOption.TopDirectoryOnly));
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

    private static DecodedImage SolidTransparentImage(int width, int height) =>
        new(width, height, new byte[checked(width * height * 4)]);

    private static void AssertOpaqueBackground(
        ReadOnlySpan<byte> pixels,
        PixelColor expected,
        int tolerance)
    {
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            Assert.InRange(pixels[offset], Math.Max(0, expected.Red - tolerance), Math.Min(255, expected.Red + tolerance));
            Assert.InRange(pixels[offset + 1], Math.Max(0, expected.Green - tolerance), Math.Min(255, expected.Green + tolerance));
            Assert.InRange(pixels[offset + 2], Math.Max(0, expected.Blue - tolerance), Math.Min(255, expected.Blue + tolerance));
            Assert.Equal((byte)255, pixels[offset + 3]);
        }
    }
}
