using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlyphFrame.App.Views;

/// <summary>Leading status text and trailing metadata share a row when both fit.
/// The progress footer can instead wrap its leading text beside the metadata.
/// When no readable leading column fits, each gets the full width.</summary>
public sealed class StatusTextPairPanel : Panel
{
    private Rect _leadingSlot, _trailingSlot;
    private const double ColumnGap = 12, RowGap = 4;
    // About twenty average Latin characters, rather than a single-glyph column.
    // An em follows the effective font size; actual glyph advances/ink also fit.
    private const double ReadingColumnEms = 10;
    private (object Key, double Width)? _cachedMinimum;
    internal double MinimumReadableLeadingWidth { get; private set; }

    public static readonly DependencyProperty WrapLeadingTextProperty = DependencyProperty.Register(
        nameof(WrapLeadingText), typeof(bool), typeof(StatusTextPairPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public bool WrapLeadingText { get => (bool)GetValue(WrapLeadingTextProperty); set => SetValue(WrapLeadingTextProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement? leading = InternalChildren.Count > 0 ? InternalChildren[0] : null;
        UIElement? trailing = InternalChildren.Count > 1 ? InternalChildren[1] : null;
        leading?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        trailing?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        bool hasLeading = HasContent(leading), hasTrailing = HasContent(trailing);
        double left = hasLeading ? leading!.DesiredSize.Width : 0, right = hasTrailing ? trailing!.DesiredSize.Width : 0;
        double gap = hasLeading && hasTrailing ? ColumnGap : 0;
        double naturalWidth = left + gap + right;
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : naturalWidth;
        MinimumReadableLeadingWidth = WrapLeadingText && hasLeading ? LeadingMinimum(leading!, left) : left;
        // Reserve a readable leading column as well as the measured metadata.
        // Keep the legacy title/frame policy unless this footer mode is enabled.
        bool inline = !hasLeading || !hasTrailing || (WrapLeadingText
            ? MinimumReadableLeadingWidth + gap + right <= width : naturalWidth <= width);
        leading?.Measure(new Size(Math.Max(0, inline ? width - right - gap : width), double.PositiveInfinity));
        trailing?.Measure(new Size(width, double.PositiveInfinity));
        double leftHeight = hasLeading ? leading!.DesiredSize.Height : 0, rightHeight = hasTrailing ? trailing!.DesiredSize.Height : 0;
        double height = inline ? Math.Max(leftHeight, rightHeight) : leftHeight + (hasTrailing ? RowGap + rightHeight : 0);
        _leadingSlot = hasLeading ? new Rect(0, inline && !WrapLeadingText ? (height - leftHeight) / 2 : 0,
            Math.Max(0, inline ? width - right - gap : width), leftHeight) : default;
        _trailingSlot = hasTrailing ? new Rect(Math.Max(0, width - trailing!.DesiredSize.Width),
            inline ? WrapLeadingText ? 0 : (height - rightHeight) / 2 : leftHeight + RowGap, trailing.DesiredSize.Width, rightHeight) : default;
        return new Size(width, height);
    }

    private static bool HasContent(UIElement? child) => child is not null && child.Visibility != Visibility.Collapsed
        && (child is not TextBlock text || !string.IsNullOrWhiteSpace(text.Text));

    private double LeadingMinimum(UIElement leading, double naturalWidth)
    {
        if (leading is not TextBlock text) { return naturalWidth; }
        double dpi = VisualTreeHelper.GetDpi(text).PixelsPerDip;
        object key = (text.Text, text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch,
            text.FontSize, text.FlowDirection, text.Padding, text.Margin, dpi, CultureInfo.CurrentUICulture.Name);
        if (_cachedMinimum is { } cached && Equals(key, cached.Key)) { return cached.Width; }
        double glyphWidth = 0;
        var elements = StringInfo.GetTextElementEnumerator(text.Text);
        Typeface typeface = new(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
        while (elements.MoveNext())
        {
            FormattedText glyph = new(elements.GetTextElement(), CultureInfo.CurrentUICulture, text.FlowDirection,
                typeface, text.FontSize, Brushes.Black, dpi);
            glyphWidth = Math.Max(glyphWidth, glyph.WidthIncludingTrailingWhitespace
                + Math.Max(0, glyph.OverhangLeading) + Math.Max(0, glyph.OverhangTrailing));
        }
        double chrome = text.Padding.Left + text.Padding.Right + text.Margin.Left + text.Margin.Right;
        double minimum = Math.Max(glyphWidth + chrome, Math.Min(naturalWidth, ReadingColumnEms * text.FontSize + chrome));
        _cachedMinimum = (key, minimum);
        return minimum;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count > 0) { InternalChildren[0].Arrange(_leadingSlot); }
        if (InternalChildren.Count > 1) { InternalChildren[1].Arrange(_trailingSlot); }
        return finalSize;
    }
}
