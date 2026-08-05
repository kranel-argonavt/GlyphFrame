using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IMediaInputService
{
    Task<MediaInputResult> LoadAsync(string? inputPath, CancellationToken cancellationToken = default);
}
