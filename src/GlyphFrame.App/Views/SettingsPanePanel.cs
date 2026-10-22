using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>Content-sized Input/Characters and a finite remaining-height Render card.
/// Render owns normal parameter overflow; the outer scroller owns only exceptional overflow.</summary>
public sealed class SettingsPanePanel : Panel
{
    public static readonly DependencyProperty ViewportHeightProperty = DependencyProperty.Register(
        nameof(ViewportHeight), typeof(double), typeof(SettingsPanePanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double ViewportHeight
    {
        get => (double)GetValue(ViewportHeightProperty);
        set => SetValue(ViewportHeightProperty, value);
    }

    public const double Gap = 10;
    public bool UsesWholePaneScrolling { get; private set; }
    private double _renderHeight;

    protected override Size MeasureOverride(Size availableSize)
    {
        double preceding = 0;
        for (int i = 0; i < InternalChildren.Count - 1; i++)
        {
            InternalChildren[i].Measure(new Size(availableSize.Width, double.PositiveInfinity));
            preceding += InternalChildren[i].DesiredSize.Height + Gap;
        }
        if (InternalChildren.Count == 0) { return new Size(); }
        UIElement render = InternalChildren[^1];
        double budget = Math.Max(0, (double.IsFinite(ViewportHeight) ? ViewportHeight : 0) - preceding);
        if (render is Border border && border.Child is RenderSettingsPanel body)
        {
            double inset = border.Padding.Top + border.Padding.Bottom + border.BorderThickness.Top + border.BorderThickness.Bottom;
            double horizontal = border.Padding.Left + border.Padding.Right + border.BorderThickness.Left + border.BorderThickness.Right;
            UsesWholePaneScrolling = budget < body.MeasurePinnedMinimum(Math.Max(0, availableSize.Width - horizontal)) + inset;
            body.UseWholePaneScrolling = UsesWholePaneScrolling;
        }
        render.Measure(new Size(availableSize.Width, UsesWholePaneScrolling ? double.PositiveInfinity : budget));
        _renderHeight = Math.Max(render.DesiredSize.Height, budget);
        render.Measure(new Size(availableSize.Width, _renderHeight));
        return new Size(availableSize.Width, preceding + _renderHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double top = 0;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            double height = i == InternalChildren.Count - 1 ? _renderHeight : InternalChildren[i].DesiredSize.Height;
            InternalChildren[i].Arrange(new Rect(0, top, finalSize.Width, height));
            top += height + Gap;
        }
        return finalSize;
    }
}

/// <summary>One pair of normal-sized font fields; no duplicate controls or window-size assumptions.</summary>
public sealed class FontSettingsPanel : Panel
{
    public bool IsHorizontal { get; private set; }
    // Each pair includes the common 86-DIP label column and a normal-sized field.
    private const double FamilyWidth = 230;
    private const double SizeWidth = 150;
    private const double Gap = 10;

    protected override Size MeasureOverride(Size availableSize)
    {
        IsHorizontal = availableSize.Width >= FamilyWidth + SizeWidth + Gap;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Measure(new Size(IsHorizontal
                ? i == 0 ? Math.Max(FamilyWidth, availableSize.Width - SizeWidth - Gap) : SizeWidth
                : availableSize.Width, double.PositiveInfinity));
        }
        double height = IsHorizontal ? InternalChildren.Cast<UIElement>().Max(c => c.DesiredSize.Height)
            : InternalChildren.Cast<UIElement>().Sum(c => c.DesiredSize.Height) + Gap;
        return new Size(availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double top = 0;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            double width = IsHorizontal ? i == 0 ? finalSize.Width - SizeWidth - Gap : SizeWidth : finalSize.Width;
            double left = IsHorizontal && i > 0 ? finalSize.Width - SizeWidth : 0;
            InternalChildren[i].Arrange(new Rect(left, IsHorizontal ? 0 : top, width, InternalChildren[i].DesiredSize.Height));
            top += InternalChildren[i].DesiredSize.Height + Gap;
        }
        return finalSize;
    }
}
