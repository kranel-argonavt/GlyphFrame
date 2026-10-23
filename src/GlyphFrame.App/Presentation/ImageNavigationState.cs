using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GlyphFrame.App.Presentation;

public sealed record ImagePreviewFrame(ImageSource Source, double PixelWidth, double PixelHeight);

public readonly record struct ImageViewGeometry(Size Extent, Point Translation, Point Center, double Scale, double FitScale);

/// <summary>Transient view geometry only: never owns a raster, render identity or persisted preference.</summary>
public sealed class ImageNavigationState : ObservableObject
{
    public const double ZoomStep = 1.25;
    public const double MaximumManualScale = 32;
    public const int MaximumWheelSteps = 16;
    public const double WheelLineDips = 16;
    private Size _pixels, _viewport;
    private DpiScale _dpi = new(1, 1);
    private double _scale = 1;
    private double _minimumScale, _maximumScale = MaximumManualScale;
    private Point _center = new(0.5, 0.5);

    public ImageViewGeometry Geometry { get; private set; }
    public bool IsFit { get; private set; } = true;
    public bool IsReady => Geometry.Scale > 0;
    public bool IsOversized => IsReady && (Geometry.Extent.Width > _viewport.Width || Geometry.Extent.Height > _viewport.Height);
    public Point ViewportCenter => new(_viewport.Width / 2, _viewport.Height / 2);
    public void ResetForMedia()
    {
        IsFit = true; _center = new(0.5, 0.5); _pixels = new(); _scale = 1;
        Publish();
    }

    public void Configure(Size pixels, Size viewport, DpiScale dpi)
    {
        if (!Valid(pixels) || !Valid(viewport) || !Positive(dpi.DpiScaleX) || !Positive(dpi.DpiScaleY)) { return; }
        double baseWidth = pixels.Width / dpi.DpiScaleX, baseHeight = pixels.Height / dpi.DpiScaleY;
        if (!Positive(baseWidth) || !Positive(baseHeight)
            || !Positive(Math.Min(viewport.Width / baseWidth, viewport.Height / baseHeight))) { return; }
        if (pixels == _pixels && viewport == _viewport && dpi.Equals(_dpi)) { return; }
        double relative = IsReady ? _scale / Geometry.FitScale : 1;
        bool rasterChanged = pixels != _pixels;
        _pixels = pixels; _viewport = viewport; _dpi = dpi;
        double fit = FitScale;
        if (!Positive(fit)) { return; }
        _minimumScale = fit;
        _maximumScale = Math.Max(MaximumManualScale, fit);
        if (!rasterChanged && !IsFit)
        {
            // Preserve an existing large manual scale without reversing zoom direction.
            _maximumScale = Math.Max(_maximumScale, _scale);
        }
        if (IsFit) { _scale = fit; _center = new(0.5, 0.5); }
        else if (rasterChanged) { _scale = Bound(fit * relative); }
        if (_scale <= fit) { _scale = fit; IsFit = true; _center = new(0.5, 0.5); }
        Publish();
    }

    public void Fit()
    {
        IsFit = true; _center = new(0.5, 0.5); _scale = FitScale; Publish();
    }

    public void Zoom(double factor, Point anchor)
    {
        if (Positive(factor)) { SetScale(_scale * factor, anchor); }
    }

    public void SetScale(double scale, Point anchor)
    {
        if (!IsReady || !Positive(scale) || !Finite(anchor)) { return; }
        scale = Bound(scale);
        if (scale <= FitScale) { Fit(); return; }
        Point imagePoint = new((anchor.X - Geometry.Translation.X) / Geometry.Extent.Width,
            (anchor.Y - Geometry.Translation.Y) / Geometry.Extent.Height);
        Size extent = Extent(scale);
        if (!Valid(extent)) { return; }
        _center = new((ViewportCenter.X - anchor.X) / extent.Width + imagePoint.X,
            (ViewportCenter.Y - anchor.Y) / extent.Height + imagePoint.Y);
        _scale = scale; IsFit = false; Publish();
    }

    public bool Pan(Vector delta)
    {
        if (!IsOversized || !double.IsFinite(delta.X) || !double.IsFinite(delta.Y)) { return false; }
        Point previous = Geometry.Translation;
        Size extent = Geometry.Extent;
        double x = extent.Width <= _viewport.Width ? previous.X : Math.Clamp(previous.X + delta.X, _viewport.Width - extent.Width, 0);
        double y = extent.Height <= _viewport.Height ? previous.Y : Math.Clamp(previous.Y + delta.Y, _viewport.Height - extent.Height, 0);
        // Sub-micro-DIP rounding at a clamped edge is not visible movement and
        // must not consume a wheel gesture intended for the enclosing pane.
        if (Math.Abs(x - previous.X) <= 1e-7 && Math.Abs(y - previous.Y) <= 1e-7) { return false; }
        _center = new((ViewportCenter.X - x) / extent.Width, (ViewportCenter.Y - y) / extent.Height);
        Publish();
        return previous != Geometry.Translation;
    }

    private double FitScale => !Valid(_pixels) || !Valid(_viewport) ? 0
        : Math.Min(_viewport.Width / (_pixels.Width / _dpi.DpiScaleX), _viewport.Height / (_pixels.Height / _dpi.DpiScaleY));
    private Size Extent(double scale) => new(_pixels.Width / _dpi.DpiScaleX * scale, _pixels.Height / _dpi.DpiScaleY * scale);
    private double Bound(double scale) => Math.Clamp(scale, _minimumScale, _maximumScale);
    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
    private static bool Valid(Size size) => Positive(size.Width) && Positive(size.Height);
    private static bool Finite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private void Publish()
    {
        if (!Valid(_pixels) || !Positive(_scale)) { Geometry = default; }
        else
        {
            Size extent = Extent(_scale);
            if (!Valid(extent) || !Finite(_center)) { return; }
            double x = Offset(extent.Width, _viewport.Width, _center.X);
            double y = Offset(extent.Height, _viewport.Height, _center.Y);
            _center = new(extent.Width <= _viewport.Width ? 0.5 : (ViewportCenter.X - x) / extent.Width,
                extent.Height <= _viewport.Height ? 0.5 : (ViewportCenter.Y - y) / extent.Height);
            Geometry = new(extent, new(x, y), _center, _scale, FitScale);
        }
        OnPropertyChanged(nameof(Geometry)); OnPropertyChanged(nameof(IsFit));
    }

    private static double Offset(double extent, double viewport, double center) => extent <= viewport
        ? (viewport - extent) / 2 : Math.Clamp(viewport / 2 - center * extent, viewport - extent, 0);
}
