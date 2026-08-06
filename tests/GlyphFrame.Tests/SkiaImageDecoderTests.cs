using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Rendering.Skia;
using GlyphFrame.Tests.TestInfrastructure;
using SkiaSharp;

namespace GlyphFrame.Tests;

public sealed class SkiaImageDecoderTests
{
    private readonly SkiaImageDecoder _decoder = new();

    [Theory]
    [InlineData("valid.png")]
    [InlineData("valid.jpg")]
    [InlineData("valid.jpeg")]
    [InlineData("valid.webp")]
    [InlineData("valid.bmp")]
    public async Task DecodeAsync_DecodesEveryApprovedRasterFormat(string fileName)
    {
        using TemporaryMediaDirectory media = new();
        string path = Phase6TestImageFactory.Create(media, fileName, 7, 5);

        DecodedImage image = await _decoder.DecodeAsync(path, ImageOrientation.Unspecified);

        Assert.Equal(7, image.Width);
        Assert.Equal(5, image.Height);
        Assert.Equal(7 * 5 * 4, image.RgbaPixels.Length);
    }

    [Fact]
    public async Task DecodeAsync_ReturnsStraightAlphaRgbaPixelsFromRealPng()
    {
        using TemporaryMediaDirectory media = new();
        string path = Phase6TestImageFactory.CreateStraightAlphaPng(
            media,
            "straight-alpha.png",
            3,
            1,
            (x, _) => x switch
            {
                0 => new SKColor(17, 34, 51, 255),
                1 => new SKColor(255, 0, 0, 128),
                _ => new SKColor(73, 149, 211, 0)
            });

        DecodedImage image = await _decoder.DecodeAsync(path, ImageOrientation.Normal);

        Assert.Equal(new byte[] { 17, 34, 51, 255 }, image.RgbaPixels.Span[..4].ToArray());
        Assert.Equal(new byte[] { 255, 0, 0, 128 }, image.RgbaPixels.Span[4..8].ToArray());
        Assert.Equal((byte)0, image.RgbaPixels.Span[11]);
    }

    [Theory]
    [InlineData(ImageOrientation.Unspecified, 2, 3, new byte[] { 1, 2, 3, 4, 5, 6 })]
    [InlineData(ImageOrientation.Normal, 2, 3, new byte[] { 1, 2, 3, 4, 5, 6 })]
    [InlineData(ImageOrientation.MirroredHorizontally, 2, 3, new byte[] { 2, 1, 4, 3, 6, 5 })]
    [InlineData(ImageOrientation.Rotated180, 2, 3, new byte[] { 6, 5, 4, 3, 2, 1 })]
    [InlineData(ImageOrientation.MirroredVertically, 2, 3, new byte[] { 5, 6, 3, 4, 1, 2 })]
    [InlineData(ImageOrientation.MirroredHorizontallyThenRotated270Clockwise, 3, 2, new byte[] { 1, 3, 5, 2, 4, 6 })]
    [InlineData(ImageOrientation.Rotated90Clockwise, 3, 2, new byte[] { 5, 3, 1, 6, 4, 2 })]
    [InlineData(ImageOrientation.MirroredHorizontallyThenRotated90Clockwise, 3, 2, new byte[] { 6, 4, 2, 5, 3, 1 })]
    [InlineData(ImageOrientation.Rotated270Clockwise, 3, 2, new byte[] { 2, 4, 6, 1, 3, 5 })]
    public async Task DecodeAsync_NormalizesEveryOrientationWithCompletePixelOrder(
        ImageOrientation orientation,
        int expectedWidth,
        int expectedHeight,
        byte[] expectedOrder)
    {
        using TemporaryMediaDirectory media = new();
        string path = Phase6TestImageFactory.CreateStraightAlphaPng(
            media,
            "orientation.png",
            2,
            3,
            (x, y) => new SKColor((byte)(1 + x + y * 2), 20, 30, 255));

        DecodedImage image = await _decoder.DecodeAsync(path, orientation);

        Assert.Equal(expectedWidth, image.Width);
        Assert.Equal(expectedHeight, image.Height);
        Assert.Equal(expectedOrder, ReadRedChannel(image));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, ReadRedChannel(image).Order().ToArray());
    }

    [Fact]
    public async Task MetadataOrientation_FlowsIntoDecoderAndNormalizesPixelsExactlyOnce()
    {
        using TemporaryMediaDirectory media = new();
        string path = Phase6TestImageFactory.Create(
            media,
            "metadata-orientation.jpg",
            2,
            3,
            (x, y) => new SKColor((byte)(20 + (x + y * 2) * 35), (byte)(10 + y * 20), 5, 255));
        Phase6TestImageFactory.AddExifOrientation(path, 6);
        MetadataExtractorImageMetadataReader metadataReader = new();

        ImageMetadata metadata = await metadataReader.ReadAsync(path);
        DecodedImage baseline = await _decoder.DecodeAsync(path, ImageOrientation.Normal);
        DecodedImage normalized = await _decoder.DecodeAsync(path, metadata.Orientation);

        Assert.Equal(ImageOrientation.Rotated90Clockwise, metadata.Orientation);
        Assert.Equal(3, normalized.Width);
        Assert.Equal(2, normalized.Height);
        Assert.Equal(ReorderPixels(baseline.RgbaPixels.Span, [4, 2, 0, 5, 3, 1]), normalized.RgbaPixels.ToArray());
    }

    [Fact]
    public async Task DecodeAsync_SupportsUnicodePathAndReleasesSourceFile()
    {
        using TemporaryMediaDirectory media = new();
        string path = Phase6TestImageFactory.Create(media, "画像-фото.png", 4, 3);

        _ = await _decoder.DecodeAsync(path, ImageOrientation.Normal);

        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    [Fact]
    public async Task DecodeAsync_RejectsMissingAndCorruptImagesWithFriendlyCategories()
    {
        using TemporaryMediaDirectory media = new();
        string missing = Path.Combine(media.RootPath, "missing.png");
        string corrupt = media.CopyFixture("broken.png", "corrupt.png");

        ImageProcessingException missingError = await Assert.ThrowsAsync<ImageProcessingException>(
            () => _decoder.DecodeAsync(missing, ImageOrientation.Normal));
        ImageProcessingException corruptError = await Assert.ThrowsAsync<ImageProcessingException>(
            () => _decoder.DecodeAsync(corrupt, ImageOrientation.Normal));

        Assert.Equal(ImageProcessingErrorCategory.FileNotFound, missingError.Category);
        Assert.Equal(ImageProcessingErrorCategory.InvalidImage, corruptError.Category);
        Assert.DoesNotContain("SkiaSharp", corruptError.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] ReadRedChannel(DecodedImage image)
    {
        ReadOnlySpan<byte> pixels = image.RgbaPixels.Span;
        byte[] red = new byte[image.Width * image.Height];
        for (int index = 0; index < red.Length; index++)
        {
            red[index] = pixels[index * 4];
        }

        return red;
    }

    private static byte[] ReorderPixels(ReadOnlySpan<byte> source, int[] pixelOrder)
    {
        byte[] result = new byte[pixelOrder.Length * 4];
        for (int index = 0; index < pixelOrder.Length; index++)
        {
            source.Slice(pixelOrder[index] * 4, 4).CopyTo(result.AsSpan(index * 4, 4));
        }

        return result;
    }
}
