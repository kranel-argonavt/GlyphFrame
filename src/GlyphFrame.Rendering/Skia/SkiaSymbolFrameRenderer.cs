using System.Runtime.InteropServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using SkiaSharp;

namespace GlyphFrame.Rendering.Skia;

public sealed class SkiaSymbolFrameRenderer : ISymbolFrameRenderer
{
    public RenderedImage Render(SymbolFrame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        cancellationToken.ThrowIfCancellationRequested();

        PixelDimensions dimensions = frame.Grid.OutputDimensions;
        try
        {
            SKImageInfo info = new(
                dimensions.Width,
                dimensions.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Premul);
            using SKBitmap bitmap = new(info);
            using SKCanvas canvas = new(bitmap);
            using SKPaint paint = new() { IsAntialias = true };
            using SkiaTypefaceResolver.ResolvedTypeface resolved = SkiaTypefaceResolver.Resolve(
                frame.GlyphMetrics.ResolvedFontFamily,
                new string(frame.Cells.Span.ToArray().Select(cell => cell.Character).Distinct().ToArray()));
            using SKFont font = new(resolved.Typeface, frame.GlyphMetrics.FontSizePixels);

            PixelColor background = PixelColor.SymbolBackground;
            canvas.Clear(new SKColor(background.Red, background.Green, background.Blue, 255));

            Dictionary<char, GlyphDrawingInfo> glyphs = [];
            ReadOnlySpan<SymbolCell> cells = frame.Cells.Span;
            double cellWidth = frame.Grid.CellWidth;
            double cellHeight = frame.Grid.CellHeight;

            for (int row = 0; row < frame.Grid.Rows; row++)
            {
                if ((row & 7) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                float top = (float)(row * cellHeight);
                float baseline = top
                    + (float)Math.Max(0, (cellHeight - frame.GlyphMetrics.LineHeight) / 2d)
                    + frame.GlyphMetrics.BaselineOffset;

                for (int column = 0; column < frame.Grid.Columns; column++)
                {
                    SymbolCell cell = cells[row * frame.Grid.Columns + column];
                    if (cell.Character == ' ')
                    {
                        continue;
                    }

                    if (!glyphs.TryGetValue(cell.Character, out GlyphDrawingInfo glyph))
                    {
                        string text = cell.Character.ToString();
                        glyph = new GlyphDrawingInfo(text, font.MeasureText(text, paint));
                        glyphs.Add(cell.Character, glyph);
                    }

                    float left = (float)(column * cellWidth);
                    float x = left + (float)Math.Max(0, (cellWidth - glyph.Width) / 2d);
                    paint.Color = new SKColor(cell.Color.Red, cell.Color.Green, cell.Color.Blue, 255);
                    canvas.DrawText(glyph.Text, x, baseline, SKTextAlign.Left, font, paint);
                }
            }

            canvas.Flush();
            cancellationToken.ThrowIfCancellationRequested();
            int byteCount = checked(dimensions.Width * dimensions.Height * 4);
            byte[] pixels = new byte[byteCount];
            Marshal.Copy(bitmap.GetPixels(), pixels, 0, byteCount);
            return new RenderedImage(dimensions.Width, dimensions.Height, pixels);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ImageProcessingException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ImageProcessingException(
                ImageProcessingErrorCategory.RenderingFailed,
                "GlyphFrame could not render the symbolized image.",
                exception.Message,
                exception);
        }
    }

    private readonly record struct GlyphDrawingInfo(string Text, float Width);
}
