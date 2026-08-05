using GlyphFrame.Core.Enums;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class ImageMetadataReaderTests
{
    private readonly MetadataExtractorImageMetadataReader _reader = new();

    [Theory]
    [InlineData("landscape.png", "PNG", 3, 2)]
    [InlineData("portrait.jpg", "JPEG", 2, 3)]
    [InlineData("sample.jpeg", "JPEG", 3, 2)]
    [InlineData("sample.webp", "WebP", 4, 3)]
    [InlineData("sample.bmp", "BMP", 3, 2)]
    public async Task ReadAsync_ReadsApprovedImageFormats(
        string fileName,
        string format,
        int width,
        int height)
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture(fileName);

        Core.Models.ImageMetadata metadata = await _reader.ReadAsync(path);

        Assert.Equal(width, metadata.Width);
        Assert.Equal(height, metadata.Height);
        Assert.Equal(format, metadata.Format);
        Assert.Equal(fileName, metadata.FileName);
        Assert.Equal(new FileInfo(path).Length, metadata.FileSizeBytes);
        Assert.Equal(Path.GetFullPath(path), metadata.FilePath);
    }

    [Fact]
    public async Task ReadAsync_UsesExifOrientationForDisplayDimensions()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("rotated.jpeg");

        Core.Models.ImageMetadata metadata = await _reader.ReadAsync(path);

        Assert.True(metadata.HasOrientationMetadata);
        Assert.Equal(6, metadata.RawOrientationValue);
        Assert.Equal(ImageOrientation.Rotated90Clockwise, metadata.Orientation);
        Assert.Equal(2, metadata.DisplayWidth);
        Assert.Equal(3, metadata.DisplayHeight);
    }

    [Fact]
    public async Task ReadAsync_RejectsMalformedImageAndReleasesTheFile()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("broken.png");

        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync(path));

        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanRead);
    }

    [Fact]
    public async Task ReadAsync_ReleasesValidImageForExclusiveReopen()
    {
        using TemporaryMediaDirectory media = new();
        string path = media.CopyFixture("landscape.png", "unicode-画像.png");

        _ = await _reader.ReadAsync(path);

        using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }
}
