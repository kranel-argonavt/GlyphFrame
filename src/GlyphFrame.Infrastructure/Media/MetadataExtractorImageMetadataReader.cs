using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Media;
using GlyphFrame.Core.Models;
using MetadataDirectory = MetadataExtractor.Directory;

namespace GlyphFrame.Infrastructure.Media;

public sealed class MetadataExtractorImageMetadataReader : IImageMetadataReader
{
    public Task<ImageMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        return Task.Run(() => Read(inputPath, cancellationToken), cancellationToken);
    }

    private static ImageMetadata Read(string inputPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MetadataDirectory> directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(inputPath);
        cancellationToken.ThrowIfCancellationRequested();

        string expectedDirectory = Path.GetExtension(inputPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "JpegDirectory",
            ".png" => "PngDirectory",
            ".webp" => "WebPDirectory",
            ".bmp" => "BmpHeaderDirectory",
            _ => throw new InvalidDataException("The image extension is not supported.")
        };

        MetadataDirectory? formatDirectory = directories.FirstOrDefault(
            directory => directory.GetType().Name.Equals(expectedDirectory, StringComparison.Ordinal));
        if (formatDirectory is null)
        {
            throw new InvalidDataException("The file content does not match its image extension.");
        }

        int? width = MetadataValueReader.FindPositiveInt(
            [formatDirectory],
            _ => true,
            "Image Width",
            "Width");
        int? height = MetadataValueReader.FindPositiveInt(
            [formatDirectory],
            _ => true,
            "Image Height",
            "Height");
        if (width is null || height is null)
        {
            throw new InvalidDataException("The image dimensions are unavailable.");
        }

        MetadataDirectory? orientationDirectory = directories.FirstOrDefault(
            directory => directory.GetType().Name.Equals("ExifIfd0Directory", StringComparison.Ordinal));
        int? rawOrientation = orientationDirectory is null
            ? null
            : MetadataValueReader.FindPositiveInt([orientationDirectory], _ => true, "Orientation");
        ImageOrientation orientation = rawOrientation is >= 1 and <= 8
            ? (ImageOrientation)rawOrientation.Value
            : rawOrientation.HasValue
                ? ImageOrientation.Unspecified
                : ImageOrientation.Unspecified;
        int displayWidth = orientation.SwapsDimensions() ? height.Value : width.Value;
        int displayHeight = orientation.SwapsDimensions() ? width.Value : height.Value;
        FileInfo file = new(inputPath);

        return new ImageMetadata(
            file.FullName,
            file.Name,
            width.Value,
            height.Value,
            displayWidth,
            displayHeight,
            SupportedMediaFormats.GetCanonicalFormat(inputPath),
            file.Length,
            orientation,
            rawOrientation);
    }
}
