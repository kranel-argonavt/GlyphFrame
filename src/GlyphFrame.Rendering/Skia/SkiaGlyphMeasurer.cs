using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using SkiaSharp;

namespace GlyphFrame.Rendering.Skia;

public sealed class SkiaGlyphMeasurer : IGlyphMeasurer
{
    private const float PointsToPixels = 96f / 72f;

    public GlyphMetrics Measure(string fontFamily, int fontSize, string characters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        ArgumentException.ThrowIfNullOrEmpty(characters);

        float fontSizePixels = fontSize * PointsToPixels;
        using SkiaTypefaceResolver.ResolvedTypeface resolved = SkiaTypefaceResolver.Resolve(fontFamily, characters);
        using SKFont font = new(resolved.Typeface, fontSizePixels);
        using SKPaint paint = new() { IsAntialias = true };

        SKFontMetrics fontMetrics = font.Metrics;
        float rawLineHeight = fontMetrics.Descent - fontMetrics.Ascent + fontMetrics.Leading;
        float lineHeight = Math.Max(1, (float)Math.Ceiling(rawLineHeight));
        float baselineOffset = -fontMetrics.Ascent + Math.Max(0, (lineHeight - rawLineHeight) / 2f);
        float advanceWidth = 0;

        foreach (char character in characters)
        {
            advanceWidth = Math.Max(advanceWidth, font.MeasureText(character.ToString(), paint));
        }

        advanceWidth = Math.Max(1, (float)Math.Ceiling(advanceWidth));
        return new GlyphMetrics(
            fontFamily,
            resolved.FamilyName,
            fontSizePixels,
            advanceWidth,
            lineHeight,
            baselineOffset,
            resolved.UsedFallback,
            resolved.Diagnostic);
    }
}
