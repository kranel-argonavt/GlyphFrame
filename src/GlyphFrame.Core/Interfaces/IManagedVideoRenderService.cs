using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

/// <summary>
/// Produces a completed store-owned candidate. The caller must accept it into an artifact
/// session or reject it so the physical file is deleted.
/// </summary>
public interface IManagedVideoRenderService
{
    Task<ManagedVideoRenderResult> RenderAsync(
        ManagedVideoRenderJob job,
        CancellationToken cancellationToken = default,
        IProgress<VideoExportProgress>? progress = null);
}
