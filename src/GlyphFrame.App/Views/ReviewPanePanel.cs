using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>
/// Allocates the first child (preview) one finite pane-height slot and measures
/// subsequent status/results below it. A vertically scrolling parent must not
/// let result height or retained media extents determine the preview allocation.
/// </summary>
public sealed class ReviewPanePanel : Panel
{
    public static readonly DependencyProperty ViewportHeightProperty = DependencyProperty.Register(
        nameof(ViewportHeight), typeof(double), typeof(ReviewPanePanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ViewportHeight
    {
        get => (double)GetValue(ViewportHeightProperty);
        set => SetValue(ViewportHeightProperty, value);
    }

    private double PreviewHeight => Math.Max(
        double.IsFinite(ViewportHeight) ? Math.Max(0, ViewportHeight) : 0,
        InternalChildren.Count > 0 && InternalChildren[0] is FrameworkElement preview ? preview.MinHeight : 0);

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = PreviewHeight;
        double width = 0;
        for (int index = 0; index < InternalChildren.Count; index++)
        {
            UIElement child = InternalChildren[index];
            child.Measure(new Size(availableSize.Width, index == 0 ? PreviewHeight : double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            if (index > 0) { height += child.DesiredSize.Height; }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double top = 0;
        for (int index = 0; index < InternalChildren.Count; index++)
        {
            UIElement child = InternalChildren[index];
            double height = index == 0 ? PreviewHeight : child.DesiredSize.Height;
            child.Arrange(new Rect(0, top, finalSize.Width, height));
            top += height;
        }
        return finalSize;
    }
}
