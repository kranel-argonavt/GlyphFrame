using GlyphFrame.Core.Enums;
using Microsoft.Win32;
using System.IO;

namespace GlyphFrame.App.Services;

public sealed class ImageSaveDialogService : IImageSaveDialogService
{
    public ImageSaveDialogResult? SelectOutputFile(string sourcePath, ImageExportFormat initialFormat)
    {
        SaveFileDialog dialog = CreateImageDialog(sourcePath, initialFormat);
        return dialog.ShowDialog() == true ? AcceptedImageSelection(dialog) : null;
    }

    internal static SaveFileDialog CreateImageDialog(string sourcePath, ImageExportFormat initialFormat)
    {
        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = initialFormat == ImageExportFormat.Jpeg ? ".jpg" : ".png";

        SaveFileDialog dialog = new()
        {
            Title = "Export Symbolized Image - GlyphFrame",
            // Normalize after acceptance from the selected filter, before policy
            // checks. Native auto-append can otherwise produce name.png.jpg.
            AddExtension = false,
            CheckPathExists = true,
            DefaultExt = extension,
            FileName = $"{sourceName}_symbolized{extension}",
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            FilterIndex = initialFormat == ImageExportFormat.Jpeg ? 2 : 1,
            OverwritePrompt = false,
            ValidateNames = true
        };

        return dialog;
    }

    internal static ImageSaveDialogResult AcceptedImageSelection(SaveFileDialog dialog) => new(
        dialog.FileName,
        dialog.FilterIndex switch
        {
            1 => ImageExportFormat.Png,
            2 => ImageExportFormat.Jpeg,
            _ => throw new InvalidOperationException("The image Save dialog returned an unsupported format.")
        });

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
