using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Rendering.Conversion;

public sealed class FrameToSymbolConverter : IFrameToSymbolConverter
{
    private readonly IBrightnessToSymbolMapper _symbolMapper;

    public FrameToSymbolConverter(IBrightnessToSymbolMapper symbolMapper)
    {
        _symbolMapper = symbolMapper ?? throw new ArgumentNullException(nameof(symbolMapper));
    }

    public SymbolFrame Convert(
        DecodedImage source,
        SymbolGrid grid,
        GlyphMetrics glyphMetrics,
        CharacterPresetOption preset,
        SymbolPolarity polarity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(glyphMetrics);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrEmpty(preset.Characters);

        ReadOnlySpan<byte> pixels = source.RgbaPixels.Span;
        SymbolCell[] cells = new SymbolCell[grid.CellCount];
        PixelColor background = SymbolBackgroundResolver.Resolve(polarity);

        for (int row = 0; row < grid.Rows; row++)
        {
            if ((row & 7) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            int sourceTop = row * source.Height / grid.Rows;
            int sourceBottom = Math.Max(sourceTop + 1, (row + 1) * source.Height / grid.Rows);
            sourceBottom = Math.Min(sourceBottom, source.Height);

            for (int column = 0; column < grid.Columns; column++)
            {
                int sourceLeft = column * source.Width / grid.Columns;
                int sourceRight = Math.Max(sourceLeft + 1, (column + 1) * source.Width / grid.Columns);
                sourceRight = Math.Min(sourceRight, source.Width);

                long red = 0;
                long green = 0;
                long blue = 0;
                long count = 0;

                for (int y = sourceTop; y < sourceBottom; y++)
                {
                    int pixelOffset = checked((y * source.Width + sourceLeft) * 4);
                    for (int x = sourceLeft; x < sourceRight; x++)
                    {
                        byte alpha = pixels[pixelOffset + 3];
                        red += Composite(pixels[pixelOffset], background.Red, alpha);
                        green += Composite(pixels[pixelOffset + 1], background.Green, alpha);
                        blue += Composite(pixels[pixelOffset + 2], background.Blue, alpha);
                        count++;
                        pixelOffset += 4;
                    }
                }

                byte averageRed = checked((byte)(red / count));
                byte averageGreen = checked((byte)(green / count));
                byte averageBlue = checked((byte)(blue / count));
                byte luminance = CalculateLuminance(averageRed, averageGreen, averageBlue);
                char character = _symbolMapper.Map(luminance, preset.Characters, polarity);
                cells[row * grid.Columns + column] = new SymbolCell(
                    character,
                    new PixelColor(averageRed, averageGreen, averageBlue),
                    luminance);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new SymbolFrame(grid, glyphMetrics, cells, background);
    }

    public static byte CalculateLuminance(byte red, byte green, byte blue) =>
        (byte)Math.Clamp((int)Math.Round(0.2126 * red + 0.7152 * green + 0.0722 * blue), 0, 255);

    private static byte Composite(byte source, byte background, byte alpha) =>
        (byte)((source * alpha + background * (255 - alpha) + 127) / 255);
}
