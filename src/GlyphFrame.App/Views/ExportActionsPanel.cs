using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>One primary and the applicable secondary controls. No future-row reservation.
/// The parent selects composition and width; this panel only lays out its actions.</summary>
public abstract class ExportActionsPanel : Panel
{
    public const double Gap = 8, RowGap = 8, RailWidth = 150;
    private double _rowHeight;
    private bool _paired = true;
    public bool IsHorizontal => _paired;
    private UIElement[] Visible => InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
    public bool HasSecondaryActions => Visible.Length > 1;
    // Reserve feedback labels as well as current text. Feedback cannot change layout.
    public double PreferredWidth => HasSecondaryActions
        ? Math.Max(Math.Max(StatusLayoutPolicy.ActionWidth, PrimaryMinimum), 2 * SecondaryMinimum + Gap)
        : Math.Max(StatusLayoutPolicy.ActionWidth, MinimumStandaloneWidth);
    public double MinimumStandaloneWidth => Math.Max(RailWidth, PrimaryMinimum);
    public double MinimumRailWidth => HasSecondaryActions
        ? Math.Max(Math.Max(RailWidth, SecondaryMinimum), PrimaryMinimum) : MinimumStandaloneWidth;
    private double PrimaryMinimum
    {
        get
        {
            UIElement? primary = Visible.FirstOrDefault();
            primary?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return primary?.DesiredSize.Width ?? 0;
        }
    }
    private double SecondaryMinimum
    {
        get
        {
            double minimum = 122;
            foreach (Control child in InternalChildren.Cast<UIElement>().Skip(1).OfType<Control>())
            {
                foreach (string label in new[] { "Open Folder", "Copy path", "Copied!", "Copy failed" })
                {
                    var text = new System.Windows.Media.FormattedText(label, System.Globalization.CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight, new System.Windows.Media.Typeface(child.FontFamily, child.FontStyle, child.FontWeight, child.FontStretch),
                        child.FontSize, System.Windows.Media.Brushes.Black, System.Windows.Media.VisualTreeHelper.GetDpi(child).PixelsPerDip);
                    minimum = Math.Max(minimum, text.WidthIncludingTrailingWhitespace + child.Padding.Left + child.Padding.Right + 24);
                }
            }
            return minimum;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement[] controls = Visible;
        if (controls.Length == 0) { return default; }
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : PreferredWidth;
        _paired = StatusLayoutPolicy.GetMode(this) != StatusLayoutMode.Intermediate
            && (controls.Length == 1 || width >= 2 * SecondaryMinimum + Gap);
        _rowHeight = 38;
        for (int i = 0; i < controls.Length; i++)
        {
            controls[i].Measure(new Size(IsHorizontal && i > 0 ? (width - Gap) / (controls.Length - 1) : width, double.PositiveInfinity));
            _rowHeight = Math.Max(_rowHeight, controls[i].DesiredSize.Height);
        }
        int rows = IsHorizontal ? controls.Length > 1 ? 2 : 1 : controls.Length;
        return new Size(width, rows * _rowHeight + (rows - 1) * RowGap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UIElement[] controls = Visible;
        if (controls.Length == 0) { return finalSize; }
        controls[0].Arrange(new Rect(0, 0, finalSize.Width, _rowHeight));
        double secondaryWidth = IsHorizontal ? (finalSize.Width - Gap * (controls.Length - 2)) / Math.Max(1, controls.Length - 1) : finalSize.Width;
        for (int i = 1; i < controls.Length; i++)
        {
            controls[i].Arrange(new Rect(IsHorizontal ? (i - 1) * (secondaryWidth + Gap) : 0,
                IsHorizontal ? _rowHeight + RowGap : i * (_rowHeight + RowGap), secondaryWidth, _rowHeight));
        }
        return finalSize;
    }
}

public sealed class VideoExportActionsPanel : ExportActionsPanel
{
}
