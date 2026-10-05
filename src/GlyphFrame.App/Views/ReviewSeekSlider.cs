using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GlyphFrame.App.Views;

/// <summary>Synchronizes the displayed range/position without publishing range coercion
/// as a seek, or letting clock updates interrupt an active thumb drag.</summary>
public sealed class ReviewSeekSlider : Slider
{
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(double), typeof(ReviewSeekSlider),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PositionChanged));
    private bool _synchronizing;
    private bool _dragging;
    public ReviewSeekSlider()
    {
        AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => _dragging = true));
        AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => { _dragging = false; SetCurrentValue(PositionProperty, Value); }));
    }
    public double Position { get => (double)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    private static void PositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ReviewSeekSlider)d).SynchronizePosition();

    private void SynchronizePosition()
    {
        if (_dragging || IsMouseCaptureWithin) return;
        bool wasSynchronizing = _synchronizing;
        _synchronizing = true;
        try { SetCurrentValue(ValueProperty, Math.Clamp(Position, Minimum, Maximum)); }
        finally { _synchronizing = wasSynchronizing; }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.Property != MinimumProperty && e.Property != MaximumProperty)
        {
            base.OnPropertyChanged(e);
            return;
        }

        // RangeBase coerces Value inside its range metadata callbacks, before
        // OnMaximumChanged/OnMinimumChanged. Guard that entire notification so
        // a temporary zero/smaller range cannot write back to Position's source.
        bool wasSynchronizing = _synchronizing;
        _synchronizing = true;
        try
        {
            base.OnPropertyChanged(e);
            SynchronizePosition();
        }
        finally { _synchronizing = wasSynchronizing; }
    }
    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (!_synchronizing) SetCurrentValue(PositionProperty, newValue);
    }
}
