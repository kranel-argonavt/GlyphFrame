using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

/// <summary>Copies borrowed review bytes; never renders, moves, or deletes the managed artifact.</summary>
public interface IVideoArtifactPublisher
{
    Task<VideoPublicationDestination> PrepareAsync(string outputPath, string originalInputPath,
        CancellationToken cancellationToken = default);

    Task<VideoExportResult> PublishAsync(VideoArtifactPublicationJob job,
        CancellationToken cancellationToken = default, IProgress<VideoExportProgress>? progress = null);
}
