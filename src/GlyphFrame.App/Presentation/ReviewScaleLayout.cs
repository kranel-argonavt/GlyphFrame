using System.Windows;

namespace GlyphFrame.App.Presentation;

public static class ReviewScaleLayout
{
    public static Size Calculate(double pixelsWide, double pixelsHigh, Size viewport, DpiScale dpi, string mode)
    {
        if (!double.IsFinite(pixelsWide) || !double.IsFinite(pixelsHigh) || pixelsWide <= 0 || pixelsHigh <= 0
            || !double.IsFinite(viewport.Width) || !double.IsFinite(viewport.Height)
            || !double.IsFinite(dpi.DpiScaleX) || !double.IsFinite(dpi.DpiScaleY)
            || viewport.Width <= 0 || viewport.Height <= 0 || dpi.DpiScaleX <= 0 || dpi.DpiScaleY <= 0)
        {
            return new Size(0, 0);
        }
        double width = pixelsWide / dpi.DpiScaleX;
        double height = pixelsHigh / dpi.DpiScaleY;
        if (mode == "100%") return new Size(width, height);
        double factor = mode == "Fill"
            ? Math.Max(viewport.Width / width, viewport.Height / height)
            : Math.Min(viewport.Width / width, viewport.Height / height);
        return new Size(width * factor, height * factor);
    }
}
