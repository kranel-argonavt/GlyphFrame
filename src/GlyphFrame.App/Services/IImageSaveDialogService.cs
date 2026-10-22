using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Services;

public interface IImageSaveDialogService
{
    ImageSaveDialogResult? SelectOutputFile(string sourcePath, ImageExportFormat initialFormat);

    string? SelectVideoOutputFile(string sourcePath);
}

public sealed record ImageSaveDialogResult(string FilePath, ImageExportFormat Format);
