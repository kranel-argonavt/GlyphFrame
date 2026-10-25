using System.Windows;
using System.Windows.Media;

namespace GlyphFrame.App.Views;

/// <summary>Gives each native window icon its own live vector resource context.</summary>
public static class ThemedWindowIcon
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source", typeof(ImageSource), typeof(ThemedWindowIcon), new PropertyMetadata(null, OnSourceChanged));

    public static ImageSource? GetSource(DependencyObject element) => (ImageSource?)element.GetValue(SourceProperty);
    public static void SetSource(DependencyObject element, ImageSource? value) => element.SetValue(SourceProperty, value);

    private static void OnSourceChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Window window || ReferenceEquals(e.OldValue, e.NewValue)) { return; }
        // Clone retains DynamicResource expressions; CloneCurrentValue would
        // snapshot the old palette. A shared drawing can lose its resource
        // mentor when several windows/native-icon rasterizations reference it.
        // Window.Icon already refreshes its native handles when its OWN drawing
        // changes, so no theme subscriptions, native-handle ownership or polling.
        if (e.NewValue is DrawingImage drawing) { window.Icon = drawing.Clone(); }
        else if (e.NewValue is ImageSource source) { window.Icon = source; }
        else { window.ClearValue(Window.IconProperty); }
    }
}
