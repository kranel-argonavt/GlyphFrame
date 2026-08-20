using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Services;

public interface IImageSaveDialogService
{
    string? SelectOutputFile(string sourcePath, ImageExportFormat format);

    string? SelectVideoOutputFile(string sourcePath);
}
