using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlyphFrame.App.Presentation;

namespace GlyphFrame.App.Views;

/// <summary>A media-only viewport. Controls never inherit the media's scale.</summary>
public sealed class ReviewViewport : Grid
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ReviewViewport), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty PixelWidthProperty = DependencyProperty.Register(nameof(PixelWidth), typeof(double), typeof(ReviewViewport), new PropertyMetadata(0d, Changed));
    public static readonly DependencyProperty PixelHeightProperty = DependencyProperty.Register(nameof(PixelHeight), typeof(double), typeof(ReviewViewport), new PropertyMetadata(0d, Changed));
    public static readonly DependencyProperty ScaleModeProperty = DependencyProperty.Register(nameof(ScaleMode), typeof(string), typeof(ReviewViewport), new PropertyMetadata("Fit", Changed));
    // The computed size preserves the media aspect ratio. Pixel alignment prevents
    // centering at fractional device pixels from softening a natural-size image.
    private readonly Image _image = new() { Stretch = Stretch.Fill, UseLayoutRounding = true, SnapsToDevicePixels = true };
    private readonly Canvas _canvas = new();
    private readonly ScrollViewer _scroll = new() { CanContentScroll = false, Focusable = true };

    public ReviewViewport()
    {
        ClipToBounds = true;
        _scroll.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/GlyphFrame.App;component/Resources/ReviewViewportResources.xaml", UriKind.Relative)
        });
        _canvas.Children.Add(_image);
        _scroll.Content = _canvas;
        Children.Add(_scroll);
        SizeChanged += (_, _) => Refresh();
        _scroll.ScrollChanged += (_, e) => { if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) Refresh(); };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
    }

    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double PixelWidth { get => (double)GetValue(PixelWidthProperty); set => SetValue(PixelWidthProperty, value); }
    public double PixelHeight { get => (double)GetValue(PixelHeightProperty); set => SetValue(PixelHeightProperty, value); }
    public string ScaleMode { get => (string)GetValue(ScaleModeProperty); set => SetValue(ScaleModeProperty, value); }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) { base.OnDpiChanged(oldDpi, newDpi); Refresh(); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ReviewViewport)d).Refresh();
    private void Refresh()
    {
        _image.Source = Source;
        bool natural = ScaleMode == "100%";
        _scroll.HorizontalScrollBarVisibility = natural ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = natural ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        double width = _scroll.ViewportWidth > 0 ? _scroll.ViewportWidth : ActualWidth;
        double height = _scroll.ViewportHeight > 0 ? _scroll.ViewportHeight : ActualHeight;
        Size media = ReviewScaleLayout.Calculate(PixelWidth, PixelHeight, new Size(width, height), VisualTreeHelper.GetDpi(this), ScaleMode);
        _image.Width = media.Width;
        _image.Height = media.Height;
        _canvas.Width = natural ? Math.Max(width, media.Width) : width;
        _canvas.Height = natural ? Math.Max(height, media.Height) : height;
        Canvas.SetLeft(_image, (_canvas.Width - media.Width) / 2);
        Canvas.SetTop(_image, (_canvas.Height - media.Height) / 2);
        if (!natural) { _scroll.ScrollToHorizontalOffset(0); _scroll.ScrollToVerticalOffset(0); }
    }
}
