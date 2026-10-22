using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>Measures one set of controls and places them in the approved composition.
/// All budgets are inner widths; the shell owns border and padding exactly once.</summary>
public sealed class AdaptiveResultPanel : Panel
{
    private const double Gap = StatusLayoutPolicy.GroupGap, RowGap = StatusLayoutPolicy.RowGap;
    private readonly Dictionary<UIElement, Rect> _slots = [];
    public StatusLayoutMode LayoutMode { get; private set; }
    public bool IsCompact => LayoutMode == StatusLayoutMode.Narrow;

    public static readonly DependencyProperty FamilyProperty = DependencyProperty.Register(
        nameof(Family), typeof(StatusLayoutFamily), typeof(AdaptiveResultPanel),
        new FrameworkPropertyMetadata(StatusLayoutFamily.Result, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public StatusLayoutFamily Family { get => (StatusLayoutFamily)GetValue(FamilyProperty); set => SetValue(FamilyProperty, value); }

    public static readonly DependencyProperty CompactContentWidthProperty = DependencyProperty.Register(
        nameof(CompactContentWidth), typeof(double), typeof(AdaptiveResultPanel),
        new FrameworkPropertyMetadata(StatusLayoutPolicy.VideoCompactWidth, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double CompactContentWidth { get => (double)GetValue(CompactContentWidthProperty); set => SetValue(CompactContentWidthProperty, value); }

    public static readonly DependencyProperty RoleProperty = DependencyProperty.RegisterAttached(
        "Role", typeof(AdaptiveResultPanelRole), typeof(AdaptiveResultPanel),
        new FrameworkPropertyMetadata(AdaptiveResultPanelRole.None,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));
    public static void SetRole(DependencyObject element, AdaptiveResultPanelRole value) => element.SetValue(RoleProperty, value);
    public static AdaptiveResultPanelRole GetRole(DependencyObject element) => (AdaptiveResultPanelRole)element.GetValue(RoleProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        _slots.Clear();
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 702;
        UIElement? status = Child(AdaptiveResultPanelRole.Status), text = Child(AdaptiveResultPanelRole.Text);
        UIElement? actions = Child(AdaptiveResultPanelRole.Actions);
        MetricCardsPanel? metrics = Child(AdaptiveResultPanelRole.Metrics) as MetricCardsPanel;
        double badgeWidth = Family == StatusLayoutFamily.Working ? StatusLayoutPolicy.WorkingIndicator : StatusLayoutPolicy.Badge;
        double icon = badgeWidth + StatusLayoutPolicy.BadgeGap;
        status?.Measure(new Size(badgeWidth, double.PositiveInfinity));
        text?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double naturalText = text?.DesiredSize.Width ?? 0;
        double metricMinimum = metrics?.MinimumRowWidth ?? 0;
        bool exported = actions is ExportActionsPanel exportActions && exportActions.HasSecondaryActions;
        double actionWidth = actions is ExportActionsPanel exports ? exports.PreferredWidth : actions is null ? 0 : 108;
        double standaloneMinimum = actions is ExportActionsPanel standalone ? standalone.MinimumStandaloneWidth : actionWidth;
        double railWidth = actions is ExportActionsPanel rail ? rail.MinimumRailWidth : actionWidth;
        // Normal descriptions use the reference's 328-DIP left budget; longer
        // guidance can grow to 360.
        double titleWidth = text is Panel headerText ? headerText.Children.OfType<TextBlock>().FirstOrDefault()?.DesiredSize.Width ?? 0 : naturalText;
        double preferredHeader = Math.Max(naturalText > 360 ? 360 : 328,
            icon + Math.Min(titleWidth, 306));
        double minimumHeader = icon + Math.Min(naturalText, 238);
        // Protect a practical reading column before giving any spare width to
        // metrics. Minimum readable cards, not maximum cards, admit this mode.
        double spaciousHeader = icon + Math.Max(StatusLayoutPolicy.PracticalResultTextWidth, Math.Min(naturalText, 274));

        LayoutMode = Family switch
        {
            StatusLayoutFamily.Planned => width >= icon + naturalText + Gap + (metrics?.PreferredRowWidth ?? 0)
                ? StatusLayoutMode.CompactWide : StatusLayoutMode.Narrow,
            StatusLayoutFamily.Working => width >= Math.Max(318, icon + Math.Min(naturalText, 264)) + (actions is null ? 0 : Gap + actionWidth)
                ? StatusLayoutMode.CompactWide : StatusLayoutMode.Narrow,
            _ when actions is null => StatusLayoutMode.Narrow,
            _ when metrics is not null && width >= spaciousHeader + metricMinimum + actionWidth + 2 * Gap => StatusLayoutMode.Spacious,
            _ when exported => width >= Math.Max(preferredHeader, metricMinimum) + Gap + actionWidth
                ? StatusLayoutMode.CompactWide : width >= Math.Max(minimumHeader, metricMinimum) + Gap + railWidth
                    ? StatusLayoutMode.Intermediate : StatusLayoutMode.Narrow,
            _ => width >= metricMinimum + Gap + standaloneMinimum ? StatusLayoutMode.CompactWide
                : width >= Math.Max(preferredHeader, actionWidth + icon + Math.Min(naturalText, 164) + StatusLayoutPolicy.BadgeGap)
                    ? StatusLayoutMode.Below : StatusLayoutMode.Narrow
        };
        bool compactResult = Family == StatusLayoutFamily.Result && LayoutMode == StatusLayoutMode.Narrow && metrics is not null;
        double contentWidth = compactResult ? Math.Min(width, CompactContentWidth) : width;
        double origin = (width - contentWidth) / 2;
        if (metrics is not null) { metrics.EqualColumns = compactResult; }
        if (actions is not null) { StatusLayoutPolicy.SetMode(actions, LayoutMode); }

        double height;
        if (LayoutMode == StatusLayoutMode.Spacious)
        {
            Measure(metrics, Math.Min(metrics!.MaximumRowWidth, width - spaciousHeader - actionWidth - 2 * Gap));
            double headerWidth = width - W(metrics) - actionWidth - 2 * Gap;
            double header = Header(0, 0, headerWidth);
            Measure(actions, actionWidth);
            height = Math.Max(header, Math.Max(H(metrics), H(actions)));
            OffsetContent((height - header) / 2);
            Slot(metrics, headerWidth + Gap, (height - H(metrics)) / 2, W(metrics), H(metrics));
            Slot(actions, width - actionWidth, (height - H(actions)) / 2, actionWidth, H(actions));
        }
        else if (Family == StatusLayoutFamily.Planned && LayoutMode == StatusLayoutMode.CompactWide)
        {
            Measure(metrics, metrics?.PreferredRowWidth ?? 0);
            double left = width - W(metrics) - Gap;
            double header = Header(0, 0, left);
            height = Math.Max(header, H(metrics));
            OffsetContent((height - header) / 2);
            Slot(metrics, left + Gap, (height - H(metrics)) / 2, W(metrics), H(metrics));
        }
        else if (Family == StatusLayoutFamily.Working)
        {
            double left = LayoutMode == StatusLayoutMode.CompactWide && actions is not null ? width - actionWidth - Gap : width;
            height = Header(0, 0, left);
            if (actions is not null)
            {
                Measure(actions, actionWidth);
                if (LayoutMode == StatusLayoutMode.CompactWide)
                {
                    double bodyHeight = height;
                    height = Math.Max(bodyHeight, H(actions));
                    OffsetContent((height - bodyHeight) / 2);
                    Slot(actions, left + Gap, (height - H(actions)) / 2, actionWidth, H(actions));
                }
                else { Slot(actions, width - actionWidth, height + RowGap, actionWidth, H(actions)); height += RowGap + H(actions); }
            }
        }
        else if (Family == StatusLayoutFamily.Result && exported && LayoutMode != StatusLayoutMode.Narrow)
        {
            double right = LayoutMode == StatusLayoutMode.Intermediate ? railWidth : actionWidth;
            double left = width - right - Gap;
            double header = Header(0, 0, left);
            Measure(metrics, left); Slot(metrics, 0, header + RowGap, W(metrics), H(metrics));
            Measure(actions, right);
            double leftHeight = header + (metrics is null ? 0 : RowGap + H(metrics));
            height = Math.Max(leftHeight, H(actions));
            OffsetContent((height - leftHeight) / 2);
            Slot(actions, left + Gap, (height - H(actions)) / 2, right, H(actions));
        }
        else
        {
            double header = Header(origin, 0, contentWidth);
            bool beside = Family == StatusLayoutFamily.Result && actions is not null && LayoutMode == StatusLayoutMode.CompactWide;
            if (beside)
            {
                // Only the standalone primary can contract continuously. A
                // published result keeps its distinct paired/rail compositions.
                actionWidth = Math.Clamp(contentWidth - metricMinimum - Gap, standaloneMinimum, actionWidth);
            }
            Measure(metrics, beside ? contentWidth - actionWidth - Gap : contentWidth);
            double row = metrics is null ? header : header + RowGap;
            if (beside)
            {
                Measure(actions, actionWidth);
                double rowHeight = Math.Max(H(metrics), H(actions));
                Slot(metrics, origin, row + (rowHeight - H(metrics)) / 2, W(metrics), H(metrics));
                Slot(actions, origin + contentWidth - actionWidth, row + (rowHeight - H(actions)) / 2, actionWidth, H(actions));
                height = row + rowHeight;
            }
            else
            {
                Slot(metrics, origin, row, compactResult ? contentWidth : W(metrics), H(metrics));
                height = metrics is null ? header : row + H(metrics);
                if (actions is not null)
                {
                    double aw = compactResult ? contentWidth : Math.Min(actionWidth, contentWidth);
                    Measure(actions, aw);
                    Slot(actions, origin + contentWidth - aw, height + RowGap, aw, H(actions));
                    height += RowGap + H(actions);
                }
            }
        }
        return new Size(width, height);

        double Header(double x, double y, double budget)
        {
            double textWidth = Math.Max(0, budget - icon);
            Measure(text, textWidth);
            double hh = Math.Max(H(status), H(text));
            Slot(status, x, y + (hh - H(status)) / 2, badgeWidth, H(status));
            Slot(text, x + icon, y + (hh - H(text)) / 2, textWidth, H(text));
            return hh;
        }
    }

    private void OffsetContent(double y)
    {
        foreach (UIElement element in _slots.Keys.ToArray())
        { Rect slot = _slots[element]; slot.Offset(0, y); _slots[element] = slot; }
    }
    private void Slot(UIElement? element, double x, double y, double width, double height)
    { if (element is not null) { _slots[element] = new Rect(Math.Max(0, x), Math.Max(0, y), Math.Max(0, width), Math.Max(0, height)); } }
    private static void Measure(UIElement? child, double width) => child?.Measure(new Size(Math.Max(0, width), double.PositiveInfinity));
    private static double H(UIElement? child) => child?.DesiredSize.Height ?? 0;
    private static double W(UIElement? child) => child?.DesiredSize.Width ?? 0;
    private UIElement? Child(AdaptiveResultPanelRole role) => InternalChildren.Cast<UIElement>()
        .FirstOrDefault(child => GetRole(child) == role && child.Visibility != Visibility.Collapsed);
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var (child, slot) in _slots) { child.Arrange(slot); }
        return finalSize;
    }
}

public enum AdaptiveResultPanelRole { None, Status, Text, Metrics, Actions }
