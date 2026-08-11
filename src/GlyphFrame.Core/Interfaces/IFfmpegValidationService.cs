using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IFfmpegValidationService
{
    Task<FfmpegValidationResult> ValidateAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
