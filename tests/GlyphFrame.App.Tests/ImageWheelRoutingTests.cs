using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class ImageWheelRoutingTests
{
    [Theory]
    [InlineData("fit", -120, true)]
    [InlineData("middle", -120, false)]
    [InlineData("top", 120, true)]
    [InlineData("top", -120, false)]
    [InlineData("bottom", -120, true)]
    [InlineData("bottom", 120, false)]
    public void ActualImageWheelHasExactlyOneScrollOwner(string position, int delta, bool expectOuter) => InputSettingsPolishTests.Sta(() =>
    {
        ImageNavigationState navigation = new();
        ReviewViewport viewport = new() { Width = 360, Height = 240, ImageFrame = new(ImageNavigationTests.Bitmap(), 1000, 600), ImageNavigation = navigation };
        WithOuter(viewport, (host, outer, image) =>
        {
            if (position != "fit") { navigation.SetScale(2, navigation.ViewportCenter); }
            if (position == "top") { navigation.Pan(new(0, 1e9)); }
            if (position == "bottom") { navigation.Pan(new(0, -1e9)); }
            double initialOffset = outer.VerticalOffset;
            ImageViewGeometry before = navigation.Geometry;
            ScrollViewer inner = InputSettingsPolishTests.Descendants(viewport).OfType<ScrollViewer>().Single();
            Assert.Equal(0, inner.ScrollableHeight); Assert.True(outer.ScrollableHeight > 500);
            int deliveries = 0;
            MouseWheelEventArgs wheel = new(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = Mouse.MouseWheelEvent };
            outer.AddHandler(Mouse.MouseWheelEvent, new MouseWheelEventHandler((_, e) =>
            { deliveries++; Assert.Same(wheel, e); Assert.Same(image, e.OriginalSource); }), true);
            image.RaiseEvent(wheel); InputSettingsPolishTests.Settle(host);
            Assert.Equal(1, deliveries); Assert.True(wheel.Handled);
            if (expectOuter)
            {
                Assert.Equal(before, navigation.Geometry);
                Assert.Equal(initialOffset - delta / 120d * WheelStep(outer), outer.VerticalOffset, 6);
            }
            else
            {
                Assert.Equal(initialOffset, outer.VerticalOffset);
                Assert.Equal(before.Translation.Y + delta / 120d * WheelStep(viewport), navigation.Geometry.Translation.Y, 6);
                Assert.Equal(before.Scale, navigation.Geometry.Scale);
            }
        });
    });

    [Theory]
    [InlineData("zoom", 120)]
    [InlineData("fit-limit", -120)]
    [InlineData("upper-limit", 120)]
    public void RoutedControlWheelStaysLocalIncludingZoomLimits(string position, int delta) => InputSettingsPolishTests.Sta(() =>
    {
        ImageNavigationState navigation = new();
        ReviewViewport viewport = new() { Width = 360, Height = 240, ImageFrame = new(ImageNavigationTests.Bitmap(), 1000, 600), ImageNavigation = navigation };
        WithOuter(viewport, (host, outer, image) =>
        {
            if (position != "fit-limit") { navigation.SetScale(position == "zoom" ? 2 : ImageNavigationState.MaximumManualScale, navigation.ViewportCenter); }
            Point anchor = new(123, 87);
            ImageViewGeometry before = navigation.Geometry;
            Point imagePoint = PixelAt(before, anchor);
            double outerBefore = outer.VerticalOffset;
            // Deterministic modifier dispatch through the production handler. The
            // actual Image still originates both routed phases; no OS key injection.
            viewport.PreviewMouseWheel += (_, e) => viewport.HandleImageWheel(e, anchor, true);
            MouseWheelEventArgs wheel = new(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
            image.RaiseEvent(wheel); Assert.True(wheel.Handled);
            wheel.RoutedEvent = Mouse.MouseWheelEvent; image.RaiseEvent(wheel);
            InputSettingsPolishTests.Settle(host);
            Assert.Equal(outerBefore, outer.VerticalOffset);
            if (position == "zoom")
            {
                Assert.Equal(before.Scale * ImageNavigationState.ZoomStep, navigation.Geometry.Scale);
                Point after = PixelAt(navigation.Geometry, anchor);
                Assert.Equal(imagePoint.X, after.X, 8); Assert.Equal(imagePoint.Y, after.Y, 8);
            }
            else { Assert.Equal(before, navigation.Geometry); }
        });
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyVideoKeepsInnerScrollingIncludingAfterImageMode(bool switchFromImage) => InputSettingsPolishTests.Sta(() =>
    {
        ReviewViewport viewport = new() { Width = 360, Height = 240, Source = ImageNavigationTests.Bitmap(), PixelWidth = 1000, PixelHeight = 600, ScaleMode = "100%" };
        if (switchFromImage) { viewport.ImageNavigation = new(); viewport.ImageFrame = new(ImageNavigationTests.Bitmap(), 1000, 600); }
        WithOuter(viewport, (host, outer, image) =>
        {
            viewport.ImageFrame = null; InputSettingsPolishTests.Settle(host);
            ScrollViewer inner = InputSettingsPolishTests.Descendants(viewport).OfType<ScrollViewer>().Single();
            Assert.True(inner.ScrollableHeight > 100);
            inner.ScrollToVerticalOffset(100); InputSettingsPolishTests.Settle(host);
            double outerBefore = outer.VerticalOffset;
            image.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.MouseWheelEvent });
            InputSettingsPolishTests.Settle(host);
            Assert.Equal(100 + WheelStep(inner), inner.VerticalOffset, 6);
            Assert.Equal(outerBefore, outer.VerticalOffset);
        });
    });

    [Fact]
    public void ClampedBoundaryDoesNotPublishOrClaimRoundingNoiseAsPan()
    {
        ImageNavigationState navigation = new(); navigation.Configure(new(1000, 600), new(360, 240), new(1.25, 1.25));
        navigation.SetScale(4, navigation.ViewportCenter); navigation.Pan(new(0, -1e9));
        ImageViewGeometry before = navigation.Geometry;
        int publications = 0; navigation.PropertyChanged += (_, _) => publications++;
        for (int i = 0; i < 100; i++) { Assert.False(navigation.Pan(new(0, -48))); }
        Assert.Equal(before, navigation.Geometry); Assert.Equal(0, publications);
    }

    private static double WheelStep(FrameworkElement viewport) => SystemParameters.WheelScrollLines < 0
        ? viewport.ActualHeight : SystemParameters.WheelScrollLines * ImageNavigationState.WheelLineDips;
    private static Point PixelAt(ImageViewGeometry geometry, Point anchor) => new(
        (anchor.X - geometry.Translation.X) / geometry.Extent.Width, (anchor.Y - geometry.Translation.Y) / geometry.Extent.Height);

    private static void WithOuter(ReviewViewport viewport, Action<Window, ScrollViewer, Image> action)
    {
        StackPanel body = new(); body.Children.Add(viewport); body.Children.Add(new Border { Height = 1000 });
        ScrollViewer outer = new() { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Window host = new() { Width = 416, Height = 339, Left = -10000, ShowActivated = false, Content = outer };
        try
        {
            host.Show(); InputSettingsPolishTests.Settle(host);
            outer.ScrollToVerticalOffset(100); InputSettingsPolishTests.Settle(host);
            action(host, outer, InputSettingsPolishTests.Descendants(viewport).OfType<Image>().Single());
        }
        finally { host.Close(); }
    }
}
