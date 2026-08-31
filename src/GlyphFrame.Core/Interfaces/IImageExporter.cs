using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IImageExporter
{
    /// <summary>
    /// Encodes a borrowed rendered image. The caller retains ownership and may export it again.
    /// </summary>
    Task ExportAsync(
        RenderedImage image,
        string outputPath,
        ImageExportFormat format,
        bool overwrite,
        CancellationToken cancellationToken = default);
}
