using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IVideoExportService
{
    Task<VideoExportResult> ExportAsync(
        VideoExportJob job,
        CancellationToken cancellationToken = default,
        IProgress<VideoExportProgress>? progress = null);
}
