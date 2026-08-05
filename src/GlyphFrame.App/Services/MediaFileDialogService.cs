using GlyphFrame.Core.Media;
using Microsoft.Win32;

namespace GlyphFrame.App.Services;

public sealed class MediaFileDialogService : IMediaFileDialogService
{
    public string? SelectMediaFile()
    {
        string allPatterns = BuildPatterns(SupportedMediaFormats.AllExtensions);
        string imagePatterns = BuildPatterns(SupportedMediaFormats.ImageExtensions);
        string videoPatterns = BuildPatterns(SupportedMediaFormats.VideoExtensions);
        OpenFileDialog dialog = new()
        {
            Title = "Browse Media - GlyphFrame",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            ValidateNames = true,
            Filter = $"Supported media|{allPatterns}|Images|{imagePatterns}|Videos|{videoPatterns}",
            FilterIndex = 1
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string BuildPatterns(IEnumerable<string> extensions) =>
        string.Join(';', extensions.Select(extension => $"*{extension}"));
}
