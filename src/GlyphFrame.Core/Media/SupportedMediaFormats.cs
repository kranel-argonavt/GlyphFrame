using GlyphFrame.Core.Enums;

namespace GlyphFrame.Core.Media;

public static class SupportedMediaFormats
{
    public static IReadOnlyList<string> ImageExtensions { get; } =
        [".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    public static IReadOnlyList<string> VideoExtensions { get; } =
        [".mp4", ".mov", ".mkv", ".avi"];

    public static IReadOnlyList<string> AllExtensions { get; } =
        ImageExtensions.Concat(VideoExtensions).ToArray();

    public static MediaSelectionKind DetectFromExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return MediaSelectionKind.None;
        }

        string extension = Path.GetExtension(path);
        if (ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return MediaSelectionKind.Image;
        }

        return VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            ? MediaSelectionKind.Video
            : MediaSelectionKind.None;
    }

    public static string GetCanonicalFormat(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "PNG",
            ".jpg" or ".jpeg" => "JPEG",
            ".webp" => "WebP",
            ".bmp" => "BMP",
            ".mp4" => "MP4",
            ".mov" => "MOV",
            ".mkv" => "MKV",
            ".avi" => "AVI",
            _ => "Unknown"
        };
    }
}
