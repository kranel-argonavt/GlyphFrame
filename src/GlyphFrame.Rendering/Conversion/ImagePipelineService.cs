using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.Rendering.Conversion;

public sealed class ImagePipelineService : IImagePipelineService
{
    private readonly IImageDecoder _imageDecoder;
    private readonly IOutputDimensionResolver _dimensionResolver;
    private readonly IGlyphMeasurer _glyphMeasurer;
    private readonly ISymbolGridCalculator _gridCalculator;
    private readonly IFrameToSymbolConverter _converter;
    private readonly ISymbolFrameRenderer _renderer;

    public ImagePipelineService(
        IImageDecoder imageDecoder,
        IOutputDimensionResolver dimensionResolver,
        IGlyphMeasurer glyphMeasurer,
        ISymbolGridCalculator gridCalculator,
        IFrameToSymbolConverter converter,
        ISymbolFrameRenderer renderer)
    {
        _imageDecoder = imageDecoder ?? throw new ArgumentNullException(nameof(imageDecoder));
        _dimensionResolver = dimensionResolver ?? throw new ArgumentNullException(nameof(dimensionResolver));
        _glyphMeasurer = glyphMeasurer ?? throw new ArgumentNullException(nameof(glyphMeasurer));
        _gridCalculator = gridCalculator ?? throw new ArgumentNullException(nameof(gridCalculator));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public Task<DecodedImage> DecodeAsync(
        string inputPath,
        ImageOrientation orientation,
        CancellationToken cancellationToken = default) =>
        _imageDecoder.DecodeAsync(inputPath, orientation, cancellationToken);

    public Task<ImagePipelineResult> RenderAsync(
        DecodedImage source,
        ImageRenderSettings settings,
        CharacterPresetOption preset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(preset);

        return Task.Run(() =>
        {
            ValidateSettings(settings, preset);
            cancellationToken.ThrowIfCancellationRequested();
            PixelDimensions outputDimensions = _dimensionResolver.Resolve(
                source.Dimensions,
                settings.OutputResolution);
            GlyphMetrics glyphMetrics = _glyphMeasurer.Measure(
                settings.FontFamily,
                settings.FontSize,
                preset.Characters);
            SymbolGrid grid = _gridCalculator.Calculate(outputDimensions, glyphMetrics);
            SymbolFrame symbolFrame = _converter.Convert(
                source,
                grid,
                glyphMetrics,
                preset,
                cancellationToken);
            RenderedImage rendered = _renderer.Render(symbolFrame, cancellationToken);
            return new ImagePipelineResult(rendered, grid, glyphMetrics);
        }, cancellationToken);
    }

    private static void ValidateSettings(ImageRenderSettings settings, CharacterPresetOption preset)
    {
        if (!AppSettingsCatalog.FontFamilies.Contains(settings.FontFamily, StringComparer.OrdinalIgnoreCase)
            || !AppSettingsCatalog.FontSizes.Contains(settings.FontSize)
            || !Enum.IsDefined(settings.OutputResolution)
            || string.IsNullOrEmpty(preset.Characters))
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.InvalidSettings,
                "The selected image render settings are not supported.");
        }
    }
}
