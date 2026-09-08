using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>
/// Arranges one result icon, text block, metric group, and action group without
/// duplicating controls. The panel uses the measured child widths and a small
/// hysteresis band so resizing near the layout boundary cannot oscillate.
/// </summary>
public sealed class AdaptiveResultPanel : Panel
{
    private const double IconColumnWidth = 64;
    private const double GroupSpacing = 18;
    private const double CompactRowSpacing = 14;
    private const double PracticalTextWidth = 180;
    private const double LayoutHysteresis = 8;

    private static readonly DependencyPropertyKey IsCompactPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsCompact),
        typeof(bool),
        typeof(AdaptiveResultPanel),
        new FrameworkPropertyMetadata(true));

    public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;

    public static readonly DependencyProperty RoleProperty = DependencyProperty.RegisterAttached(
        "Role",
        typeof(AdaptiveResultPanelRole),
        typeof(AdaptiveResultPanel),
        new FrameworkPropertyMetadata(
            AdaptiveResultPanelRole.None,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public bool IsCompact => (bool)GetValue(IsCompactProperty);

    public static void SetRole(DependencyObject element, AdaptiveResultPanelRole value) =>
        element.SetValue(RoleProperty, value);

    public static AdaptiveResultPanelRole GetRole(DependencyObject element) =>
        (AdaptiveResultPanelRole)element.GetValue(RoleProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement? status = ChildFor(AdaptiveResultPanelRole.Status);
        UIElement? text = ChildFor(AdaptiveResultPanelRole.Text);
        UIElement? metrics = ChildFor(AdaptiveResultPanelRole.Metrics);
        UIElement? actions = ChildFor(AdaptiveResultPanelRole.Actions);

        Size unconstrained = new(double.PositiveInfinity, double.PositiveInfinity);
        status?.Measure(unconstrained);
        metrics?.Measure(unconstrained);
        actions?.Measure(unconstrained);

        double fixedWideWidth = IconColumnWidth
            + DesiredWidth(metrics)
            + DesiredWidth(actions)
            + (metrics is null ? 0 : GroupSpacing)
            + (actions is null ? 0 : GroupSpacing);
        double requiredWideWidth = fixedWideWidth + PracticalTextWidth;
        bool compact = SelectCompactMode(availableSize.Width, requiredWideWidth);
        SetValue(IsCompactPropertyKey, compact);

        double textWidth = compact
            ? RemainingWidth(availableSize.Width, IconColumnWidth)
            : RemainingWidth(availableSize.Width, fixedWideWidth);
        text?.Measure(new Size(textWidth, double.PositiveInfinity));

        return compact
            ? MeasureCompact(availableSize.Width, status, text, metrics, actions)
            : MeasureWide(status, text, metrics, actions);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UIElement? status = ChildFor(AdaptiveResultPanelRole.Status);
        UIElement? text = ChildFor(AdaptiveResultPanelRole.Text);
        UIElement? metrics = ChildFor(AdaptiveResultPanelRole.Metrics);
        UIElement? actions = ChildFor(AdaptiveResultPanelRole.Actions);

        if (IsCompact)
        {
            ArrangeCompact(finalSize, status, text, metrics, actions);
        }
        else
        {
            ArrangeWide(finalSize, status, text, metrics, actions);
        }

        return finalSize;
    }

    private bool SelectCompactMode(double availableWidth, double requiredWideWidth)
    {
        if (!double.IsFinite(availableWidth))
        {
            return false;
        }

        return IsCompact
            ? availableWidth < requiredWideWidth + LayoutHysteresis
            : availableWidth < requiredWideWidth - LayoutHysteresis;
    }

    private static Size MeasureCompact(
        double availableWidth,
        UIElement? status,
        UIElement? text,
        UIElement? metrics,
        UIElement? actions)
    {
        double firstRowHeight = Math.Max(DesiredHeight(status), DesiredHeight(text));
        double secondRowHeight = Math.Max(DesiredHeight(metrics), DesiredHeight(actions));
        double secondRowWidth = IconColumnWidth
            + DesiredWidth(metrics)
            + (metrics is null || actions is null ? 0 : GroupSpacing)
            + DesiredWidth(actions);
        double desiredWidth = Math.Max(
            IconColumnWidth + DesiredWidth(text),
            secondRowWidth);
        if (double.IsFinite(availableWidth))
        {
            desiredWidth = Math.Min(desiredWidth, availableWidth);
        }

        return new Size(
            desiredWidth,
            firstRowHeight + (secondRowHeight > 0 ? CompactRowSpacing : 0) + secondRowHeight);
    }

    private static Size MeasureWide(UIElement? status, UIElement? text, UIElement? metrics, UIElement? actions)
    {
        double width = IconColumnWidth + DesiredWidth(text);
        if (metrics is not null)
        {
            width += GroupSpacing + DesiredWidth(metrics);
        }

        if (actions is not null)
        {
            width += GroupSpacing + DesiredWidth(actions);
        }

        return new Size(width, new[]
        {
            DesiredHeight(status), DesiredHeight(text), DesiredHeight(metrics), DesiredHeight(actions)
        }.Max());
    }

    private static void ArrangeCompact(
        Size finalSize,
        UIElement? status,
        UIElement? text,
        UIElement? metrics,
        UIElement? actions)
    {
        double firstRowHeight = Math.Max(DesiredHeight(status), DesiredHeight(text));
        ArrangeCentered(status, 0, 0, IconColumnWidth, firstRowHeight);
        ArrangeCentered(text, IconColumnWidth, 0, RemainingWidth(finalSize.Width, IconColumnWidth), firstRowHeight);

        double secondRowY = firstRowHeight + CompactRowSpacing;
        double secondRowHeight = Math.Max(DesiredHeight(metrics), DesiredHeight(actions));
        ArrangeCentered(metrics, IconColumnWidth, secondRowY, DesiredWidth(metrics), secondRowHeight);
        ArrangeCentered(
            actions,
            Math.Max(IconColumnWidth + DesiredWidth(metrics) + GroupSpacing, finalSize.Width - DesiredWidth(actions)),
            secondRowY,
            DesiredWidth(actions),
            secondRowHeight);
    }

    private static void ArrangeWide(
        Size finalSize,
        UIElement? status,
        UIElement? text,
        UIElement? metrics,
        UIElement? actions)
    {
        double metricsWidth = DesiredWidth(metrics);
        double actionsWidth = DesiredWidth(actions);
        double fixedWidth = IconColumnWidth
            + metricsWidth
            + actionsWidth
            + (metrics is null ? 0 : GroupSpacing)
            + (actions is null ? 0 : GroupSpacing);
        double textWidth = RemainingWidth(finalSize.Width, fixedWidth);
        double rowHeight = finalSize.Height;

        ArrangeCentered(status, 0, 0, IconColumnWidth, rowHeight);
        ArrangeCentered(text, IconColumnWidth, 0, textWidth, rowHeight);
        double metricsX = IconColumnWidth + textWidth + (metrics is null ? 0 : GroupSpacing);
        ArrangeCentered(metrics, metricsX, 0, metricsWidth, rowHeight);
        double actionsX = metricsX + metricsWidth + (actions is null ? 0 : GroupSpacing);
        ArrangeCentered(actions, actionsX, 0, actionsWidth, rowHeight);
    }

    private static void ArrangeCentered(UIElement? element, double x, double y, double width, double rowHeight)
    {
        if (element is null)
        {
            return;
        }

        double height = Math.Min(element.DesiredSize.Height, rowHeight);
        element.Arrange(new Rect(x, y + ((rowHeight - height) / 2), Math.Max(0, width), height));
    }

    private UIElement? ChildFor(AdaptiveResultPanelRole role) =>
        InternalChildren.Cast<UIElement>().FirstOrDefault(child => GetRole(child) == role && child.Visibility != Visibility.Collapsed);

    private static double DesiredWidth(UIElement? element) => element?.DesiredSize.Width ?? 0;

    private static double DesiredHeight(UIElement? element) => element?.DesiredSize.Height ?? 0;

    private static double RemainingWidth(double availableWidth, double consumedWidth) =>
        double.IsFinite(availableWidth) ? Math.Max(0, availableWidth - consumedWidth) : double.PositiveInfinity;
}

public enum AdaptiveResultPanelRole
{
    None,
    Status,
    Text,
    Metrics,
    Actions
}
