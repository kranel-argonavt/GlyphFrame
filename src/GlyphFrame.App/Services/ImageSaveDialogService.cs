using GlyphFrame.Core.Enums;
using Microsoft.Win32;
using System.IO;

namespace GlyphFrame.App.Services;

public sealed class ImageSaveDialogService : IImageSaveDialogService
{
    public string? SelectOutputFile(string sourcePath, ImageExportFormat format)
    {
        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = format == ImageExportFormat.Jpeg ? ".jpg" : ".png";
        string filter = format == ImageExportFormat.Jpeg
            ? "JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg"
            : "PNG image (*.png)|*.png";

        SaveFileDialog dialog = new()
        {
            Title = "Export Symbolized Image - GlyphFrame",
            AddExtension = true,
            CheckPathExists = true,
            DefaultExt = extension,
            FileName = $"{sourceName}_symbolized{extension}",
            Filter = filter,
            OverwritePrompt = false,
            ValidateNames = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectVideoOutputFile(string sourcePath)
    {
        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        SaveFileDialog dialog = new()
        {
            Title = "Export Symbolized Video - GlyphFrame",
            AddExtension = true,
            CheckPathExists = true,
            DefaultExt = ".mp4",
            FileName = $"{sourceName}_symbolized.mp4",
            Filter = "MP4 video (*.mp4)|*.mp4",
            OverwritePrompt = false,
            ValidateNames = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
