using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>Measure actual status first, returning the remaining viewport to preview.
/// Preview minimum + status overflow uses the existing pane scroll path.</summary>
public sealed class ReviewPanePanel : Panel
{
    public static readonly DependencyProperty ViewportHeightProperty = DependencyProperty.Register(
        nameof(ViewportHeight), typeof(double), typeof(ReviewPanePanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double ViewportHeight { get => (double)GetValue(ViewportHeightProperty); set => SetValue(ViewportHeightProperty, value); }

    public static readonly DependencyProperty ReserveStatusSpaceProperty = DependencyProperty.Register(
        nameof(ReserveStatusSpace), typeof(bool), typeof(ReviewPanePanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public bool ReserveStatusSpace { get => (bool)GetValue(ReserveStatusSpaceProperty); set => SetValue(ReserveStatusSpaceProperty, value); }

    public const double CardGap = StatusLayoutPolicy.CardGap;
    private double _previewHeight, _statusHeight;

    protected override Size MeasureOverride(Size availableSize)
    {
        _statusHeight = 0;
        for (int i = 1; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Measure(new Size(availableSize.Width, double.PositiveInfinity));
            if (ReserveStatusSpace) { _statusHeight += InternalChildren[i].DesiredSize.Height; }
        }
        double gap = _statusHeight > 0 ? CardGap : 0;
        double minimum = InternalChildren.Count > 0 && InternalChildren[0] is FrameworkElement preview ? preview.MinHeight : 0;
        _previewHeight = Math.Max(minimum, (double.IsFinite(ViewportHeight) ? Math.Max(0, ViewportHeight) : 0) - _statusHeight - gap);
        if (InternalChildren.Count > 0) { InternalChildren[0].Measure(new Size(availableSize.Width, _previewHeight)); }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : InternalChildren.Cast<UIElement>().Max(c => c.DesiredSize.Width),
            _previewHeight + gap + _statusHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double top = 0;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            if (i == 1 && _statusHeight > 0) { top += CardGap; }
            double height = i == 0 ? _previewHeight : ReserveStatusSpace ? InternalChildren[i].DesiredSize.Height : 0;
            InternalChildren[i].Arrange(new Rect(0, top, finalSize.Width, height));
            top += height;
        }
        return finalSize;
    }
}
