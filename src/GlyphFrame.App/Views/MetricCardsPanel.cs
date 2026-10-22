using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>Bounded cards in regular rows; equal columns and a centered orphan
/// in the compact result envelope.</summary>
public sealed class MetricCardsPanel : Panel
{
    public const double MinimumCardWidth = 88, MaximumCardWidth = 112, Gap = 8;
    private readonly List<(UIElement Child, Rect Slot)> _slots = [];
    private readonly Dictionary<TextBlock, (object Key, double Width)> _glyphWidths = [];
    private UIElement[] Visible => InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
    public static readonly DependencyProperty EqualColumnsProperty = DependencyProperty.Register(
        nameof(EqualColumns), typeof(bool), typeof(MetricCardsPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public bool EqualColumns { get => (bool)GetValue(EqualColumnsProperty); set => SetValue(EqualColumnsProperty, value); }
    private double Minimum(UIElement child) => Math.Max(MinimumCardWidth,
        Math.Max(child is FrameworkElement field ? field.MinWidth : 0, GlyphMinimum(child)));
    // Both modes use the same per-column budgets. An explicit MinWidth or a
    // single enlarged glyph plus chrome may exceed the nominal cap; long words
    // instead wrap, rather than silently granting unlimited horizontal growth.
    public double MinimumRowWidth => RowWidth(Visible, false, false);
    public double MaximumRowWidth => RowWidth(Visible, false, true);
    public double PreferredRowWidth
    {
        get
        {
            return RowWidth(Visible, true, false);
        }
    }

    private double RowWidth(UIElement[] children, bool preferred, bool maximum) => children.Length == 0 ? 0
        : children.Length * ColumnWidth(children, preferred, maximum) + (children.Length - 1) * Gap;
    private double ColumnWidth(UIElement[] children, bool preferred, bool maximum)
    {
        double minimum = children.Max(Minimum), cap = Math.Max(minimum, MaximumCardWidth);
        if (maximum) { return cap; }
        if (!preferred) { return minimum; }
        foreach (UIElement child in children) { child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
        return Math.Clamp(children.Max(c => c.DesiredSize.Width), minimum, cap);
    }

    private double GlyphMinimum(UIElement child)
    {
        double width = 0;
        if (child is TextBlock text)
        {
            double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(text).PixelsPerDip;
            object key = (text.Text, text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch, text.FontSize, dpi);
            if (!_glyphWidths.TryGetValue(text, out var cached) || !Equals(key, cached.Key))
            {
                var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text.Text);
                while (elements.MoveNext())
                {
                    var glyph = new System.Windows.Media.FormattedText(elements.GetTextElement(), System.Globalization.CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight, new System.Windows.Media.Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                        text.FontSize, System.Windows.Media.Brushes.Black, dpi);
                    width = Math.Max(width, glyph.WidthIncludingTrailingWhitespace);
                }
                _glyphWidths[text] = (key, width);
            }
            else { width = cached.Width; }
            width += text.Padding.Left + text.Padding.Right;
        }
        else if (child is Border border)
        {
            width = (border.Child is null ? 0 : GlyphMinimum(border.Child)) + border.Padding.Left + border.Padding.Right
                + border.BorderThickness.Left + border.BorderThickness.Right;
        }
        else if (child is Panel panel && panel.Children.Count > 0)
        { width = panel.Children.Cast<UIElement>().Max(GlyphMinimum); }
        if (child is FrameworkElement element) { width += element.Margin.Left + element.Margin.Right; }
        return width;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _slots.Clear();
        UIElement[] children = Visible;
        if (children.Length == 0) { return default; }
        double available = double.IsFinite(availableSize.Width) ? availableSize.Width : PreferredRowWidth;
        if (EqualColumns)
        {
            double minimum = ColumnWidth(children, false, false);
            int columns = Math.Max(1, Math.Min(children.Length, (int)((available + Gap) / (minimum + Gap))));
            double cardWidth = Math.Max(0, Math.Min(ColumnWidth(children, true, false), (available - (columns - 1) * Gap) / columns));
            double top = 0;
            for (int first = 0; first < children.Length; first += columns)
            {
                int count = Math.Min(columns, children.Length - first);
                double height = 0;
                for (int index = first; index < first + count; index++)
                { children[index].Measure(new Size(cardWidth, double.PositiveInfinity)); height = Math.Max(height, children[index].DesiredSize.Height); }
                double offset = (available - (count * cardWidth + (count - 1) * Gap)) / 2;
                for (int index = 0; index < count; index++)
                { _slots.Add((children[first + index], new Rect(offset + index * (cardWidth + Gap), top, cardWidth, height))); }
                top += height + Gap;
            }
            return new Size(available, top - Gap);
        }
        List<UIElement> row = [];
        double y = 0, used = 0;
        foreach (UIElement child in children)
        {
            if (row.Count > 0 && (row.Count + 1) * Math.Max(row.Max(Minimum), Minimum(child)) + Gap * row.Count > available) { MeasureRow(); }
            row.Add(child);
        }
        MeasureRow();
        return new Size(used, y - Gap);

        void MeasureRow()
        {
            if (row.Count == 0) { return; }
            double cardWidth = Math.Max(0, Math.Min(ColumnWidth(row.ToArray(), true, false), (available - (row.Count - 1) * Gap) / row.Count));
            double x = 0, height = 0;
            List<(UIElement Child, double Left, double Width)> measured = [];
            foreach (UIElement child in row)
            {
                child.Measure(new Size(cardWidth, double.PositiveInfinity));
                measured.Add((child, x, cardWidth));
                height = Math.Max(height, child.DesiredSize.Height); x += cardWidth + Gap;
            }
            foreach (var item in measured) { _slots.Add((item.Child, new Rect(item.Left, y, item.Width, height))); }
            y += height + Gap; used = Math.Max(used, x - Gap); row.Clear();
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var slot in _slots) { slot.Child.Arrange(slot.Slot); }
        return finalSize;
    }
}
