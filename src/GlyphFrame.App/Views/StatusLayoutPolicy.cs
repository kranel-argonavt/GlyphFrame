using System.Windows;

namespace GlyphFrame.App.Views;

public enum StatusLayoutMode { Automatic, Narrow, Intermediate, CompactWide, Below, Spacious }
public enum StatusLayoutFamily { Result, Planned, Working }

/// <summary>Local geometry. The populated panel chooses composition; the review pane
/// measures it before allocating the remaining viewport to the preview.</summary>
public static class StatusLayoutPolicy
{
    public const double Badge = 42, BadgeGap = 12;
    public const double WorkingIndicator = 54, PracticalResultTextWidth = 180;
    public const double GroupGap = 16, RowGap = 12, CardGap = 12;
    public const double VideoCompactWidth = 360, ImageCompactWidth = 296;
    public const double ActionWidth = 252;

    public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
        "Mode", typeof(StatusLayoutMode), typeof(StatusLayoutPolicy),
        new FrameworkPropertyMetadata(StatusLayoutMode.Automatic,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static StatusLayoutMode GetMode(DependencyObject target) => (StatusLayoutMode)target.GetValue(ModeProperty);
    public static void SetMode(DependencyObject target, StatusLayoutMode value) => target.SetValue(ModeProperty, value);
}
