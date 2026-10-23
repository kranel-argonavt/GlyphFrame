using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class ImageNavigationTests
{
    [Theory]
    [InlineData(1920, 1080, 1)]
    [InlineData(1080, 1920, 1.25)]
    [InlineData(32, 16, 1.5)]
    [InlineData(2000000000, 1, 2)]
    public void FitContainsDisplayOrientedRasterAndActualSizeCannotGoBelowFit(double width, double height, double dpi)
    {
        ImageNavigationState state = new(); Size viewport = new(500, 300);
        state.Configure(new(width, height), viewport, new(dpi, dpi));
        Assert.True(state.IsFit); Assert.True(state.IsReady);
        ImageViewGeometry fit = state.Geometry;
        Assert.True(fit.Extent.Width <= 500.00001 && fit.Extent.Height <= 300.00001);
        Assert.Equal(width / height, fit.Extent.Width / fit.Extent.Height, 5);
        Assert.Equal((500 - fit.Extent.Width) / 2, fit.Translation.X, 7);
        Assert.Equal((300 - fit.Extent.Height) / 2, fit.Translation.Y, 7);
        state.SetScale(1, state.ViewportCenter);
        double scale = Math.Max(1, fit.Scale);
        Assert.Equal(scale, state.Geometry.Scale);
        Assert.Equal(width * scale, state.Geometry.Extent.Width * dpi, 5);
        Assert.Equal(height * scale, state.Geometry.Extent.Height * dpi, 5);
        state.Fit(); Assert.Equal(fit, state.Geometry);
    }

    [Fact]
    public void AnchorsZoomLimitsAndInvalidInputsAreDeterministic()
    {
        ImageNavigationState state = State();
        state.SetScale(1, state.ViewportCenter);
        Point anchor = new(123, 87); Point before = PixelAt(state.Geometry, anchor);
        state.Zoom(ImageNavigationState.ZoomStep, anchor);
        AssertPoint(before, PixelAt(state.Geometry, anchor));
        Assert.Equal(1.25, state.Geometry.Scale);
        before = state.Geometry.Center;
        state.Zoom(ImageNavigationState.ZoomStep, state.ViewportCenter); AssertPoint(before, state.Geometry.Center);
        for (int i = 0; i < 100; i++) { state.Zoom(ImageNavigationState.ZoomStep, state.ViewportCenter); }
        Assert.Equal(ImageNavigationState.MaximumManualScale, state.Geometry.Scale);
        for (int i = 0; i < 100; i++) { state.Zoom(1 / ImageNavigationState.ZoomStep, state.ViewportCenter); }
        Assert.Equal(state.Geometry.FitScale, state.Geometry.Scale); Assert.True(state.IsFit);
        ImageViewGeometry valid = state.Geometry;
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1, 0 })
        { state.SetScale(invalid, anchor); state.Zoom(invalid, anchor); Assert.Equal(valid, state.Geometry); }
        state.SetScale(2, new(double.NaN, 1)); state.Pan(new(double.NaN, 0));
        state.Configure(new(20, 20), new(0, 0), new(1, 1));
        state.Configure(new(20, 20), new(200, 100), new(double.NaN, 1));
        Assert.Equal(valid, state.Geometry);
    }

    [Fact]
    public void PanClampsBothEdgesCentersSmallAxisAndResizeRetainsManualScale()
    {
        ImageNavigationState state = State(); state.SetScale(1, state.ViewportCenter);
        state.Pan(new(1e9, 1e9)); Assert.Equal(new Point(0, 0), state.Geometry.Translation);
        state.Pan(new(-1e9, -1e9)); Assert.Equal(new Point(-600, -300), state.Geometry.Translation);
        state.Fit(); state.SetScale(0.6, state.ViewportCenter);
        Assert.Equal(0.6, state.Geometry.Scale);
        Point center = state.Geometry.Center;
        state.Configure(new(1000, 600), new(300, 200), new(1, 1));
        Assert.Equal(0.6, state.Geometry.Scale); AssertPoint(center, state.Geometry.Center);
        state.Configure(new(1000, 600), new(800, 500), new(1, 1));
        Assert.Equal(0.8, state.Geometry.Scale); Assert.True(state.IsFit);
        AssertPoint(new(0, 10), state.Geometry.Translation);
        state.Fit(); state.Configure(new(1000, 600), new(300, 200), new(1, 1));
        Assert.Equal(0.3, state.Geometry.Scale, 7); Assert.Equal(new Point(0, 10), state.Geometry.Translation);
    }

    [Theory]
    [InlineData(438, 698, 1)]
    [InlineData(1920, 1080, 1.5)]
    [InlineData(32, 16, 2)]
    [InlineData(2000000000, 1, 1.25)]
    public void ZoomOutStopsAtCenteredFitAndResizingContinuesToFit(double width, double height, double dpi)
    {
        ImageNavigationState state = new(); state.Configure(new(width, height), new(400, 300), new(dpi, dpi));
        ImageViewGeometry original = state.Geometry;
        state.Zoom(1.25, new(123, 87)); state.Pan(new(-20, 10));
        state.Zoom(1e-12, new(5, 5));
        Assert.Equal(original, state.Geometry); Assert.True(state.IsFit);
        for (int i = 0; i < 10; i++) { state.Zoom(1 / ImageNavigationState.ZoomStep, new(399, 299)); }
        Assert.Equal(original, state.Geometry);
        state.Configure(new(width, height), new(200, 150), new(dpi, dpi));
        Assert.True(state.IsFit); Assert.Equal(state.Geometry.FitScale, state.Geometry.Scale);
        Assert.Equal(original.Extent.Width / 2, state.Geometry.Extent.Width, 7);
        Assert.Equal(original.Extent.Height / 2, state.Geometry.Extent.Height, 7);
        AssertPoint(new(0.5, 0.5), state.Geometry.Center);
    }

    [Theory]
    [InlineData(1000, 600)]
    [InlineData(500, 300)]
    [InlineData(2000, 1200)]
    public void RasterSwitchAndRerenderPreserveNormalizedFraming(double width, double height)
    {
        ImageNavigationState state = State(); state.SetScale(1.2, state.ViewportCenter); state.Pan(new(-30, 20));
        Point center = state.Geometry.Center; double magnification = state.Geometry.Scale / state.Geometry.FitScale;
        state.Configure(new(width, height), new(400, 300), new(1, 1));
        AssertPoint(center, state.Geometry.Center);
        Assert.Equal(magnification, state.Geometry.Scale / state.Geometry.FitScale, 8);
        ImageViewGeometry same = state.Geometry;
        state.Configure(new(width, height), new(400, 300), new(1, 1)); Assert.Equal(same, state.Geometry);
        state.Configure(new(1000, 600), new(400, 300), new(1, 1)); AssertPoint(center, state.Geometry.Center);
        Assert.Equal(1.2, state.Geometry.Scale, 8);
        state.Configure(new(1000, 600), new(400, 300), new(1.5, 1.5));
        Assert.Equal(1.2, state.Geometry.Scale); // Physical-pixel magnification is invariant across DPI.
        state.ResetForMedia(); Assert.True(state.IsFit); Assert.False(state.IsReady);
        state.Configure(new(900, 1200), new(400, 300), new(1, 1)); Assert.Equal(0.25, state.Geometry.Scale);
    }

    [Fact]
    public void ResizingCannotLeaveManualScaleBelowFitOrReverseZoomDirection()
    {
        ImageNavigationState state = new(); state.Configure(new(1, 1), new(400, 400), new(1, 1));
        state.Zoom(ImageNavigationState.ZoomStep, state.ViewportCenter); state.Configure(new(1, 1), new(100, 100), new(1, 1));
        Assert.Equal(100, state.Geometry.Scale); Assert.True(state.IsFit);
        state.Configure(new(1, 1), new(20, 20), new(1, 1)); state.Zoom(ImageNavigationState.ZoomStep, state.ViewportCenter);
        Assert.Equal(25, state.Geometry.Scale); state.Zoom(1 / ImageNavigationState.ZoomStep, state.ViewportCenter);
        Assert.Equal(20, state.Geometry.Scale); Assert.True(state.IsFit);
        state.Configure(new(2000000000, 1), new(100, 100), new(1, 1)); state.Fit(); state.Zoom(ImageNavigationState.ZoomStep, state.ViewportCenter);
        double tiny = state.Geometry.Scale;
        state.Configure(new(2000000000, 1), new(10000, 10000), new(1, 1));
        Assert.True(state.Geometry.Scale > tiny); Assert.True(state.IsFit);
        state.Zoom(1 / ImageNavigationState.ZoomStep, state.ViewportCenter); Assert.Equal(state.Geometry.FitScale, state.Geometry.Scale);
    }

    [Fact]
    public void ExtremeFitRemainsPositiveFiniteAndContainsRasterWithoutManualFloor()
    {
        ImageNavigationState state = State();
        state.Configure(new(2000000000, 1), new(100, 100), new(1, 1)); state.Fit();
        Assert.InRange(state.Geometry.Scale, double.Epsilon, 0.01);
        Assert.True(double.IsFinite(state.Geometry.Scale));
        Assert.Equal(100, state.Geometry.Extent.Width, 7);
        AssertPoint(new(0.5, 0.5), state.Geometry.Center);
    }

    [Fact]
    public void PointerTunnelStartsPanBeforeScrollViewerConsumesBubblingInput() => InputSettingsPolishTests.Sta(() =>
    {
        ImageNavigationState state = new();
        ReviewViewport viewport = new() { ImageFrame = new(Bitmap(), 1000000, 1000000), ImageNavigation = state };
        Window host = new() { Content = viewport, Width = 416, Height = 339, Left = -10000, ShowActivated = false };
        try
        {
            host.Show(); InputSettingsPolishTests.Settle(host); state.SetScale(1, state.ViewportCenter);
            Image image = InputSettingsPolishTests.Descendants(viewport).OfType<Image>().Single();
            MouseButtonEventArgs down = new(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent };
            image.RaiseEvent(down);
            Assert.True(down.Handled); Assert.True(viewport.IsImageDragging); Assert.True(viewport.IsMouseCaptured);
            Mouse.Capture(null); Assert.False(viewport.IsImageDragging);
        }
        finally { host.Close(); }
    });

    [Fact]
    public void ViewportTransformsExistingRasterWheelRoutesAndCaptureCleansUp() => InputSettingsPolishTests.Sta(() =>
    {
        ImageNavigationState state = new(); BitmapSource bitmap = Bitmap();
        ReviewViewport viewport = new() { ImageFrame = new(bitmap, 1000, 600), ImageNavigation = state };
        Grid root = new(); root.Children.Add(viewport);
        Window host = new() { Width = 416, Height = 339, Content = root, Left = -10000, ShowActivated = false };
        try
        {
            host.Show(); InputSettingsPolishTests.Settle(host);
            Image image = InputSettingsPolishTests.Descendants(viewport).OfType<Image>().Single();
            Assert.Same(bitmap, image.Source); Assert.True(state.IsFit);
            ImageViewGeometry fit = state.Geometry;
            Assert.True(viewport.NavigateWheel(-120, new(100, 80), true));
            Assert.Equal(fit, state.Geometry); Assert.True(state.IsFit);
            Assert.True(viewport.NavigateWheel(120, new(100, 80), true));
            Assert.True(state.Geometry.Scale > fit.Scale);
            Assert.True(viewport.NavigateWheel(-12000, new(100, 80), true));
            Assert.Equal(fit, state.Geometry); Assert.True(state.IsFit);
            Size desired = viewport.DesiredSize;
            Assert.False(viewport.NavigateWheel(-120, state.ViewportCenter, false));
            state.SetScale(1, state.ViewportCenter);
            Point anchor = new(100, 80); Point pixel = PixelAt(state.Geometry, anchor);
            Assert.True(viewport.NavigateWheel(120, anchor, true)); AssertPoint(pixel, PixelAt(state.Geometry, anchor));
            double scale = state.Geometry.Scale;
            Assert.True(viewport.NavigateWheel(-120, anchor, false)); Assert.Equal(scale, state.Geometry.Scale);
            Assert.False(viewport.NavigateWheel(120, new(-1, -1), true));
            InputSettingsPolishTests.Settle(host);
            Assert.Same(bitmap, image.Source); Assert.Equal(desired, viewport.DesiredSize);
            Assert.Equal(state.Geometry.Scale, ((MatrixTransform)image.RenderTransform).Matrix.M11);
            Assert.All(InputSettingsPolishTests.Descendants(viewport).OfType<ScrollViewer>(), s =>
            { Assert.Equal(0, s.ScrollableWidth); Assert.Equal(0, s.ScrollableHeight); });
            Assert.True(viewport.BeginImageDrag(state.ViewportCenter)); Assert.True(viewport.IsMouseCaptured);
            // A new wrapper for the identical accepted raster must not cancel dragging.
            viewport.ImageFrame = new(bitmap, 1000, 600); Assert.True(viewport.IsImageDragging);
            viewport.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            Assert.False(viewport.IsMouseCaptured); Assert.False(viewport.IsImageDragging);
            Assert.True(viewport.BeginImageDrag(state.ViewportCenter)); Mouse.Capture(null);
            Assert.False(viewport.IsImageDragging);
            Assert.True(viewport.BeginImageDrag(state.ViewportCenter));
            viewport.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(viewport), 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.False(viewport.IsMouseCaptured); Assert.False(viewport.IsImageDragging);
            Assert.True(viewport.BeginImageDrag(state.ViewportCenter)); state.ResetForMedia();
            Assert.False(viewport.IsMouseCaptured);
            viewport.ImageFrame = new(bitmap, 500, 300); state.SetScale(4, state.ViewportCenter);
            Assert.True(viewport.BeginImageDrag(state.ViewportCenter)); root.Children.Remove(viewport);
            InputSettingsPolishTests.Settle(host); Assert.False(viewport.IsMouseCaptured); Assert.False(viewport.IsImageDragging);
            ImageViewGeometry saved = state.Geometry; root.Children.Add(viewport); InputSettingsPolishTests.Settle(host);
            Assert.Equal(saved, state.Geometry);
            viewport.ImageFrame = null; Assert.False(viewport.NavigateWheel(120, anchor, true));
            Assert.False(viewport.Focusable);
        }
        finally { host.Close(); }
    });

    [Theory]
    [InlineData(1024, 13)]
    [InlineData(1024, 24)]
    [InlineData(1280, 18)]
    public void PopulatedImageLayoutHasNoNavigationToolbarOrReservedGap(double width, double textSize) => InputSettingsPolishTests.Sta(() =>
    {
        Window window = Phase13UiTests.LoadLooseMainWindow(realMediaComponents: true);
        NavigationLayoutData data = new(); window.DataContext = data; window.Width = width; window.Height = 720;
        window.Left = -10000; window.ShowActivated = false; window.FontSize = textSize;
        try
        {
            window.Show(); InputSettingsPolishTests.Settle(window);
            Assert.DoesNotContain(InputSettingsPolishTests.Descendants(window).OfType<Button>(), b =>
                AutomationProperties.GetName(b) is "Zoom in image" or "Zoom out image"
                    or "Image zoom percentage; reset to actual size 100 percent" or "Fit image to viewport");
            ReviewViewport viewport = InputSettingsPolishTests.Descendants(window).OfType<ReviewViewport>()
                .Single(v => ReferenceEquals(v.ImageNavigation, data.ImageNavigation));
            Border imageArea = Assert.IsType<Border>(((FrameworkElement)viewport.Parent).Parent);
            Grid composition = Assert.IsType<Grid>(imageArea.Parent);
            Assert.Equal(0, composition.RowDefinitions[2].ActualHeight);
            Assert.True(viewport.ActualWidth > 0 && viewport.ActualHeight > 0);
            Assert.True(data.ImageNavigation.IsFit);
            Assert.DoesNotContain(InputSettingsPolishTests.Descendants(window).OfType<ComboBox>(), c => AutomationProperties.GetName(c) == "Image review scale");
            Assert.Contains(InputSettingsPolishTests.Descendants(window).OfType<ComboBox>(), c => AutomationProperties.GetName(c) == "Frame review scale");
            InputSettingsPolishTests.Capture(window, $"image-fit-floor-{width}-{textSize}");
        }
        finally { window.Close(); }
    });

    internal static BitmapSource Bitmap() => BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
        new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 255 }, 8);
    private static ImageNavigationState State() { ImageNavigationState state = new(); state.Configure(new(1000, 600), new(400, 300), new(1, 1)); return state; }
    private static Point PixelAt(ImageViewGeometry geometry, Point point) => new((point.X - geometry.Translation.X) / geometry.Extent.Width, (point.Y - geometry.Translation.Y) / geometry.Extent.Height);
    private static void AssertPoint(Point expected, Point actual) { Assert.Equal(expected.X, actual.X, 8); Assert.Equal(expected.Y, actual.Y, 8); }
    private sealed class NavigationLayoutData : Phase13UiTests.MainLayoutData
    {
        public NavigationLayoutData() : base(false) { }
        public ImageNavigationState ImageNavigation { get; } = new();
        public bool ShowImagePreview => true;
        public bool ShowOriginalVideoPlayer => false;
        public bool ShowRenderedVideoPlayer => false;
        public bool ShowOriginalPlaybackOpening => false;
        public bool ShowOriginalPlaybackFallback => false;
        public bool ShowOriginalPlaybackErrorDetails => false;
        public bool ShowPreviewStatusBadge => false;
        public bool ShowEmptyPreview => false;
        public bool ShowLoadingPreview => false;
        public bool ShowErrorPreview => false;
        public bool ShowMediaPlaceholder => false;
        public bool ShowVideoFrameLoading => false;
        public bool ShowVideoFrameError => false;
        public bool ShowVideoFrameErrorDetails => false;
        public bool ShowRenderedVideoReviewError => false;
        public bool ShowVideoReviewErrorDetails => false;
        public bool IsExportActive => false;
        public bool ShowVideoFrameControls => false;
        public bool ShowResolutionWarning => false;
        public bool ShowMediaInputErrorDetails => false;
        public ImagePreviewFrame CurrentImageFrame { get; } = new(Bitmap(), 1000, 600);
    }
}
