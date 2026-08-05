using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Models;

public sealed record ImageMetadata(
    string FilePath,
    string FileName,
    int Width,
    int Height,
    int DisplayWidth,
    int DisplayHeight,
    string Format,
    long FileSizeBytes,
    ImageOrientation Orientation,
    int? RawOrientationValue)
{
    public bool HasOrientationMetadata => RawOrientationValue.HasValue;
}
