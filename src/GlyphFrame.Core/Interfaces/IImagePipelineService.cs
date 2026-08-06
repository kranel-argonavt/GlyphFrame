using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface IImagePipelineService
{
    Task<DecodedImage> DecodeAsync(
        string inputPath,
        ImageOrientation orientation,
        CancellationToken cancellationToken = default);

    Task<ImagePipelineResult> RenderAsync(
        DecodedImage source,
        ImageRenderSettings settings,
        CharacterPresetOption preset,
        CancellationToken cancellationToken = default);
}
