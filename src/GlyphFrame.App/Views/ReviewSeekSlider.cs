using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GlyphFrame.App.Views;

/// <summary>Keeps clock updates out of an active thumb drag, without replacing the binding.</summary>
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
    {
        ReviewSeekSlider slider = (ReviewSeekSlider)d;
        if (slider._dragging || slider.IsMouseCaptureWithin) return;
        slider._synchronizing = true;
        try { slider.SetCurrentValue(ValueProperty, Math.Clamp((double)e.NewValue, slider.Minimum, slider.Maximum)); }
        finally { slider._synchronizing = false; }
    }
    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (!_synchronizing) SetCurrentValue(PositionProperty, newValue);
    }
}
