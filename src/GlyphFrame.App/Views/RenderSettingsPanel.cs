using System.Windows;
using System.Windows.Controls;

namespace GlyphFrame.App.Views;

/// <summary>One scroll owner: pinned heading/audio with a finite parameter viewport,
/// or natural content height for the exceptionally constrained whole-pane fallback.</summary>
public sealed class RenderSettingsPanel : Panel
{
    public const double MinimumParameterViewport = 88; // Two ordinary 36-DIP fields plus their spacing.
    public bool UseWholePaneScrolling { get; set; }
    public bool ShowsComingSoon { get; private set; }
    public const double PlaceholderClearance = 16;
    private double _headingHeight, _audioHeight, _bodyHeight;
    private ScrollViewer Body => (ScrollViewer)InternalChildren[1];

    public double MeasurePinnedMinimum(double width)
    {
        InternalChildren[0].Measure(new Size(width, double.PositiveInfinity));
        InternalChildren[2].Measure(new Size(width, double.PositiveInfinity));
        return InternalChildren[0].DesiredSize.Height + InternalChildren[2].DesiredSize.Height + MinimumParameterViewport;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        MeasurePinnedMinimum(availableSize.Width);
        _headingHeight = InternalChildren[0].DesiredSize.Height;
        _audioHeight = InternalChildren[2].DesiredSize.Height;
        Body.VerticalScrollBarVisibility = UseWholePaneScrolling ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Visible;
        double budget = UseWholePaneScrolling ? double.PositiveInfinity : Math.Max(MinimumParameterViewport,
            availableSize.Height - _headingHeight - _audioHeight);
        Body.Measure(new Size(availableSize.Width, budget));
        _bodyHeight = UseWholePaneScrolling ? Body.DesiredSize.Height : budget;
        // This overlay is measured independently and never enters the viewport,
        // pinned minimum, desired height or ScrollViewer's content extent.
        if (InternalChildren.Count > 3)
        {
            UIElement placeholder = InternalChildren[3];
            // Hidden still measures naturally, so it can reappear on expansion.
            // The slot is zero while hidden; no margin/space is reserved.
            placeholder.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }
        return new Size(availableSize.Width, _headingHeight + _bodyHeight + _audioHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        InternalChildren[0].Arrange(new Rect(0, 0, finalSize.Width, _headingHeight));
        Body.Arrange(new Rect(0, _headingHeight, finalSize.Width, _bodyHeight));
        InternalChildren[2].Arrange(new Rect(0, _headingHeight + _bodyHeight, finalSize.Width, _audioHeight));
        ShowsComingSoon = false;
        if (InternalChildren.Count > 3)
        {
            UIElement placeholder = InternalChildren[3];
            double parameters = ((UIElement)Body.Content).DesiredSize.Height;
            double height = placeholder.DesiredSize.Height;
            // Use the current finite allocation, not a stale ViewportHeight
            // from the preceding resize. Natural parameters include margins.
            double spare = _bodyHeight - parameters;
            ShowsComingSoon = !UseWholePaneScrolling && spare >= height + PlaceholderClearance;
            placeholder.Visibility = ShowsComingSoon ? Visibility.Visible : Visibility.Hidden;
            double top = _headingHeight + parameters + (spare - height) / 2;
            placeholder.Arrange(ShowsComingSoon ? new Rect(0, top, finalSize.Width, height) : new Rect());
        }
        return finalSize;
    }
}
