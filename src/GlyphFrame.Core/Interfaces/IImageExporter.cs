using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IImageExporter
{
    Task ExportAsync(
        RenderedImage image,
        string outputPath,
        ImageExportFormat format,
        bool overwrite,
        CancellationToken cancellationToken = default);
}
