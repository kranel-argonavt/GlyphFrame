using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Rendering.Skia;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class SkiaImageExporterTests
{
    private readonly SkiaImageExporter _exporter = new();
    private readonly SkiaImageDecoder _decoder = new();

    [Theory]
    [InlineData("output.png", ImageExportFormat.Png)]
    [InlineData("output.jpg", ImageExportFormat.Jpeg)]
    public async Task ExportAsync_WritesDecodableImageWithRequestedDimensions(
        string fileName,
        ImageExportFormat format)
    {
        using TemporaryMediaDirectory media = new();
        string path = Path.Combine(media.RootPath, fileName);
        RenderedImage source = CreateRenderedImage(37, 23);

        await _exporter.ExportAsync(source, path, format, overwrite: false);
        DecodedImage decoded = await _decoder.DecodeAsync(path, ImageOrientation.Normal);

        Assert.Equal(37, decoded.Width);
        Assert.Equal(23, decoded.Height);
        Assert.All(
            Enumerable.Range(0, decoded.Width * decoded.Height),
            index => Assert.Equal((byte)255, decoded.RgbaPixels.Span[index * 4 + 3]));
        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    [Fact]
    public async Task ExportAsync_DeclinedOverwriteLeavesExistingFileUnchanged()
    {
        using TemporaryMediaDirectory media = new();
        string path = Path.Combine(media.RootPath, "existing.png");
        byte[] original = [10, 20, 30, 40];
        await File.WriteAllBytesAsync(path, original);

        ImageProcessingException error = await Assert.ThrowsAsync<ImageProcessingException>(() =>
            _exporter.ExportAsync(CreateRenderedImage(4, 3), path, ImageExportFormat.Png, overwrite: false));

        Assert.Equal(ImageProcessingErrorCategory.OutputUnavailable, error.Category);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(media.RootPath, ".*.tmp"));
    }

    [Fact]
    public async Task ExportAsync_CancelledExportLeavesNoFinalOrTemporaryFile()
    {
        using TemporaryMediaDirectory media = new();
        string path = Path.Combine(media.RootPath, "cancelled.png");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _exporter.ExportAsync(CreateRenderedImage(4, 3), path, ImageExportFormat.Png, false, cancellation.Token));

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(media.RootPath, ".*.tmp"));
    }

    [Fact]
    public async Task ExportAsync_SupportsUnicodeOutputPath()
    {
        using TemporaryMediaDirectory media = new();
        string path = Path.Combine(media.RootPath, "символ-画像.png");

        await _exporter.ExportAsync(CreateRenderedImage(9, 7), path, ImageExportFormat.Png, false);

        Assert.True(File.Exists(path));
        Assert.Equal(9, (await _decoder.DecodeAsync(path, ImageOrientation.Normal)).Width);
    }

    private static RenderedImage CreateRenderedImage(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = 120;
            pixels[offset + 1] = 80;
            pixels[offset + 2] = 200;
            pixels[offset + 3] = 255;
        }

        return new RenderedImage(width, height, pixels);
    }
}
