using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GlyphFrame.App.Presentation;

namespace GlyphFrame.App.Views;

public sealed partial class ReviewViewport
{
    public static readonly DependencyProperty ImageFrameProperty = DependencyProperty.Register(
        nameof(ImageFrame), typeof(ImagePreviewFrame), typeof(ReviewViewport), new PropertyMetadata(null, NavigationChanged));
    public static readonly DependencyProperty ImageNavigationProperty = DependencyProperty.Register(
        nameof(ImageNavigation), typeof(ImageNavigationState), typeof(ReviewViewport), new PropertyMetadata(null, NavigationChanged));
    private readonly MatrixTransform _imageNavigationTransform = new();
    private ImageNavigationState? _subscribedNavigation;
    private Point? _dragPosition;
    private bool? _focusableBeforeDrag;

    public ImagePreviewFrame? ImageFrame { get => (ImagePreviewFrame?)GetValue(ImageFrameProperty); set => SetValue(ImageFrameProperty, value); }
    public ImageNavigationState? ImageNavigation { get => (ImageNavigationState?)GetValue(ImageNavigationProperty); set => SetValue(ImageNavigationProperty, value); }
    internal bool IsImageDragging => _dragPosition.HasValue;

    private void InitializeImageNavigation()
    {
        Loaded += (_, _) => { SubscribeNavigation(); Refresh(); };
        Unloaded += (_, _) => { EndImageDrag(); UnsubscribeNavigation(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { EndImageDrag(); } };
    }

    private static void NavigationChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        ReviewViewport viewport = (ReviewViewport)target;
        if (args.Property == ImageFrameProperty && Equals(args.OldValue, args.NewValue)) { return; }
        viewport.EndImageDrag();
        viewport.SubscribeNavigation(); viewport.Refresh();
    }

    private void SubscribeNavigation()
    {
        if (ReferenceEquals(_subscribedNavigation, ImageNavigation)) { return; }
        UnsubscribeNavigation();
        if (IsLoaded && ImageNavigation is { } navigation)
        {
            _subscribedNavigation = navigation;
            PropertyChangedEventManager.AddHandler(navigation, NavigationGeometryChanged, nameof(ImageNavigationState.Geometry));
        }
    }

    private void UnsubscribeNavigation()
    {
        if (_subscribedNavigation is { } previous)
        { PropertyChangedEventManager.RemoveHandler(previous, NavigationGeometryChanged, nameof(ImageNavigationState.Geometry)); }
        _subscribedNavigation = null;
    }

    private void NavigationGeometryChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (ImageNavigation?.IsReady != true) { EndImageDrag(); }
        ApplyImageNavigation();
    }

    private bool RefreshImageNavigation()
    {
        if (ImageFrame is not { } frame || ImageNavigation is not { } navigation)
        {
            _scroll.UsesImageNavigation = false;
            EndImageDrag(); Cursor = null;
            return false;
        }
        _image.Source = frame.Source;
        _scroll.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        _scroll.ScrollToHorizontalOffset(0); _scroll.ScrollToVerticalOffset(0);
        if (ActualWidth > 0 && ActualHeight > 0 && frame.PixelWidth > 0 && frame.PixelHeight > 0
            && double.IsFinite(frame.PixelWidth) && double.IsFinite(frame.PixelHeight))
        {
            _canvas.Width = ActualWidth; _canvas.Height = ActualHeight;
            navigation.Configure(new(frame.PixelWidth, frame.PixelHeight), new(ActualWidth, ActualHeight), VisualTreeHelper.GetDpi(this));
            ApplyImageNavigation();
        }
        return true;
    }

    private void ApplyImageNavigation()
    {
        if (ImageFrame is not { } frame || ImageNavigation is not { } navigation) { return; }
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        _scroll.UsesImageNavigation = navigation.IsReady;
        _image.Width = navigation.IsReady ? frame.PixelWidth / dpi.DpiScaleX : 0;
        _image.Height = navigation.IsReady ? frame.PixelHeight / dpi.DpiScaleY : 0;
        Canvas.SetLeft(_image, 0); Canvas.SetTop(_image, 0);
        _image.RenderTransformOrigin = new Point();
        ImageViewGeometry geometry = navigation.Geometry;
        _imageNavigationTransform.Matrix = new Matrix(geometry.Scale, 0, 0, geometry.Scale, geometry.Translation.X, geometry.Translation.Y);
        _image.RenderTransform = _imageNavigationTransform;
        Cursor = IsImageDragging ? Cursors.ScrollAll : navigation.IsOversized ? Cursors.Hand : null;
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            HandleImageWheel(e, e.GetPosition(this), true);
        }
        base.OnPreviewMouseWheel(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 && ImageNavigation is { IsReady: true } navigation)
        {
            // Ordinary wheel needs only a direction, not a cursor anchor. The same
            // bubbling event came through our passive image-only child scroller.
            HandleImageWheel(e, navigation.ViewportCenter, false);
        }
        base.OnMouseWheel(e);
    }

    internal void HandleImageWheel(MouseWheelEventArgs e, Point anchor, bool control)
    {
        if (!e.Handled) { e.Handled = NavigateWheel(e.Delta, anchor, control); }
    }

    internal bool NavigateWheel(int delta, Point anchor, bool control)
    {
        if (ImageFrame is null || ImageNavigation is not { IsReady: true } navigation
            || !new Rect(RenderSize).Contains(anchor)) { return false; }
        if (control)
        {
            navigation.Zoom(Math.Pow(ImageNavigationState.ZoomStep, Math.Clamp(delta / 120d,
                -ImageNavigationState.MaximumWheelSteps, ImageNavigationState.MaximumWheelSteps)), anchor);
            return true;
        }
        double step = SystemParameters.WheelScrollLines < 0 ? ActualHeight : SystemParameters.WheelScrollLines * ImageNavigationState.WheelLineDips;
        return navigation.Pan(new Vector(0, delta / 120d * step));
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // ScrollViewer consumes the bubbling left-button event for focus. Start
        // image capture on the tunnel, before that legacy child handles it.
        base.OnPreviewMouseLeftButtonDown(e);
        if (ImageFrame is null || ImageNavigation is not { IsOversized: true } navigation) { return; }
        Point point = e.GetPosition(this);
        if (!new Rect(navigation.Geometry.Translation, navigation.Geometry.Extent).Contains(point)) { return; }
        e.Handled = BeginImageDrag(point);
    }

    internal bool BeginImageDrag(Point point)
    {
        if (ImageFrame is null || ImageNavigation is not { IsOversized: true } navigation
            || !new Rect(navigation.Geometry.Translation, navigation.Geometry.Extent).Contains(point) || !CaptureMouse()) { return false; }
        _focusableBeforeDrag ??= Focusable;
        Focusable = true; Focus(); _dragPosition = point; Cursor = Cursors.ScrollAll;
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragPosition is not Point previous) { return; }
        if (e.LeftButton != MouseButtonState.Pressed) { EndImageDrag(); return; }
        Point next = e.GetPosition(this);
        ImageNavigation?.Pan(next - previous); _dragPosition = next; e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (IsImageDragging) { EndImageDrag(); e.Handled = true; }
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    { _dragPosition = null; RestoreDragFocus(); ApplyImageNavigation(); base.OnLostMouseCapture(e); }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsImageDragging) { EndImageDrag(); e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }

    private void EndImageDrag()
    {
        _dragPosition = null;
        if (IsMouseCaptured) { ReleaseMouseCapture(); }
        RestoreDragFocus();
        Cursor = ImageFrame is not null && ImageNavigation?.IsOversized == true ? Cursors.Hand : null;
    }

    private void RestoreDragFocus()
    {
        if (_focusableBeforeDrag is bool previous) { Focusable = previous; _focusableBeforeDrag = null; }
    }
}
