using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Threading;
using System.Xml.Linq;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

public sealed class ReviewSurfaceTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    public void ReviewScrollCorner_IsDarkAndScoped_WithoutChangingNativeScrolling(bool fullscreen, bool horizontal, bool vertical)
    {
        RunSta(() =>
        {
            VideoReviewSurface surface = new() { Width = 590, Height = 340, IsFullscreen = fullscreen };
            XDocument styles = ReadView("../Resources/Controls.xaml");
            styles.Root!.AddFirst(ReadView("../Resources/Colors.xaml").Root!.Elements());
            surface.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(styles.ToString()));
            ReviewViewport viewport = Assert.IsType<ReviewViewport>(surface.FindName("Viewport"));
            viewport.Source = new DrawingImage(new GeometryDrawing(Brushes.MediumPurple, null, new RectangleGeometry(new Rect(0, 0, 100, 100))));
            Window window = new() { Content = surface, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                DpiScale dpi = VisualTreeHelper.GetDpi(viewport);
                viewport.PixelWidth = (horizontal ? 1200 : 100) * dpi.DpiScaleX;
                viewport.PixelHeight = (vertical ? 800 : 100) * dpi.DpiScaleY;
                viewport.ScaleMode = "100%";
                window.UpdateLayout();
                ScrollViewer scroll = Assert.IsType<ScrollViewer>(Assert.Single(viewport.Children.Cast<UIElement>()));
                ScrollBar horizontalBar = Assert.IsType<ScrollBar>(scroll.Template.FindName("PART_HorizontalScrollBar", scroll));
                ScrollBar verticalBar = Assert.IsType<ScrollBar>(scroll.Template.FindName("PART_VerticalScrollBar", scroll));
                Assert.IsType<ScrollContentPresenter>(scroll.Template.FindName("PART_ScrollContentPresenter", scroll));
                Assert.Equal(horizontal, horizontalBar.IsVisible);
                Assert.Equal(vertical, verticalBar.IsVisible);
                Assert.True(viewport.ClipToBounds);
                Assert.True(scroll.Focusable);
                Assert.NotNull(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(scroll));
                Assert.Null(scroll.ReadLocalValue(Control.TemplateProperty) as ControlTemplate); // Native template, not a replacement.
                Grid row = Assert.Single(((Grid)surface.Content).Children.OfType<Grid>(), child => Grid.GetRow(child) == 1);
                Assert.Equal(Colors.Transparent, Assert.IsType<SolidColorBrush>(row.Background).Color);
                Rect viewportBounds = viewport.TransformToAncestor(surface).TransformBounds(new Rect(viewport.RenderSize));
                Assert.True(row.TransformToAncestor(surface).Transform(new Point()).Y >= viewportBounds.Bottom);

                if (horizontal && vertical)
                {
                    Rectangle corner = Assert.Single(VisualDescendants(scroll).OfType<Rectangle>(), element =>
                        ReferenceEquals(element.TemplatedParent, scroll) && Grid.GetRow(element) == 1 && Grid.GetColumn(element) == 1);
                    Color expected = Assert.IsType<SolidColorBrush>(surface.FindResource("PreviewBackgroundBrush")).Color;
                    Assert.Equal(expected, Assert.IsType<SolidColorBrush>(corner.Fill).Color);
                    Point sample = corner.TransformToAncestor(surface).Transform(new Point(corner.ActualWidth / 2, corner.ActualHeight / 2));
                    RenderTargetBitmap after = RenderComponent(surface);
                    Assert.Equal(expected, SamplePixel(after, sample));

                    // Reconstruct the pre-fix resource lookup without changing its native template.
                    scroll.Resources.MergedDictionaries.Clear();
                    window.UpdateLayout();
                    Assert.Equal(SystemColors.ControlBrush.Color, Assert.IsType<SolidColorBrush>(corner.Fill).Color);
                    RenderTargetBitmap before = RenderComponent(surface);
                    scroll.Resources.MergedDictionaries.Add(new ResourceDictionary
                    { Source = new Uri("/GlyphFrame.App;component/Resources/ReviewViewportResources.xaml", UriKind.Relative) });
                    window.UpdateLayout();
                    Assert.Equal(expected, Assert.IsType<SolidColorBrush>(corner.Fill).Color);
                    // Optional component captures are opt-in and outside the repository.
                    string? captureDirectory = Environment.GetEnvironmentVariable("GLYPHFRAME_REVIEW_CAPTURE_DIRECTORY");
                    if (!fullscreen && captureDirectory is not null)
                    {
                        SaveComponent(before, System.IO.Path.Combine(captureDirectory, "review-corner-before.png"));
                        SaveComponent(after, System.IO.Path.Combine(captureDirectory, "review-corner-after.png"));
                    }
                }

                ScrollBar.ScrollToRightEndCommand.Execute(null, scroll);
                ScrollBar.ScrollToBottomCommand.Execute(null, scroll);
                window.UpdateLayout();
                Assert.Equal(scroll.ScrollableWidth, scroll.HorizontalOffset, 4);
                Assert.Equal(scroll.ScrollableHeight, scroll.VerticalOffset, 4);
                if (vertical)
                {
                    double previous = scroll.VerticalOffset;
                    scroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.MouseWheelEvent });
                    window.UpdateLayout();
                    Assert.True(scroll.VerticalOffset < previous);
                }
                foreach (string mode in new[] { "Fit", "Fill", "100%" })
                {
                    viewport.ScaleMode = mode;
                    window.UpdateLayout();
                    Assert.Equal(mode == "100%" && horizontal, horizontalBar.IsVisible);
                    Assert.Equal(mode == "100%" && vertical, verticalBar.IsVisible);
                    Assert.Equal(0, scroll.HorizontalOffset);
                    Assert.Equal(0, scroll.VerticalOffset);
                }
                Canvas canvas = Assert.IsType<Canvas>(scroll.Content);
                Image image = Assert.IsType<Image>(Assert.Single(canvas.Children.Cast<UIElement>()));
                Assert.Equal(viewport.PixelWidth, image.Width * dpi.DpiScaleX, 4);
                Assert.Equal(viewport.PixelHeight, image.Height * dpi.DpiScaleY, 4);
                ScrollViewer unrelated = new();
                Assert.Same(SystemColors.ControlBrush, unrelated.FindResource(SystemColors.ControlBrushKey));
            }
            finally { window.Close(); }
        });
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in VisualDescendants(child)) yield return descendant;
        }
    }

    private static RenderTargetBitmap RenderComponent(FrameworkElement element)
    {
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }

    private static Color SamplePixel(BitmapSource bitmap, Point point)
    {
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect((int)point.X, (int)point.Y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    private static void SaveComponent(BitmapSource bitmap, string path)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    [Fact]
    public void BuiltApplication_EmbedsPerMonitorDpiManifest()
    {
        // Read the apphost's resource bytes; this checks packaging, not monitor movement.
        string executable = Path.Combine(AppContext.BaseDirectory, "GlyphFrame.App.exe");
        string resources = Encoding.UTF8.GetString(File.ReadAllBytes(executable));
        Assert.Contains("PerMonitorV2", resources, StringComparison.Ordinal);
        Assert.Contains("true/pm", resources, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void NaturalSize_MapsEachMediaPixelToOnePhysicalPixel(double dpi)
    {
        Size result = ReviewScaleLayout.Calculate(1080, 1920, new Size(600, 400), new DpiScale(dpi, dpi), "100%");
        Assert.Equal(1080, result.Width * dpi, 8);
        Assert.Equal(1920, result.Height * dpi, 8);
        Assert.True(result.Height > 400);
        Assert.Equal(result, ReviewScaleLayout.Calculate(1080, 1920, new Size(900, 800), new DpiScale(dpi, dpi), "100%"));
    }

    [Theory]
    [InlineData(1920, 1080, "Fit", 600, 337.5)]
    [InlineData(1920, 1080, "Fill", 800, 450)]
    [InlineData(1080, 1920, "Fit", 253.125, 450)]
    [InlineData(1080, 1920, "Fill", 600, 1066.666666666667)]
    public void FitAndFill_PreserveAspectRatio(double width, double height, string mode, double expectedWidth, double expectedHeight)
    {
        Size result = ReviewScaleLayout.Calculate(width, height, new Size(600, 450), new DpiScale(1.5, 1.5), mode);
        Assert.Equal(expectedWidth, result.Width, 7);
        Assert.Equal(expectedHeight, result.Height, 7);
        Assert.Equal(width / height, result.Width / result.Height, 8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDimensions_DoNotCreateNonfiniteLayout(double dimension)
    {
        Assert.Equal(new Size(0, 0), ReviewScaleLayout.Calculate(dimension, 1080, new Size(600, 450), new DpiScale(1, 1), "100%"));
    }

    [Fact]
    public void Viewport_ClipsFill_AndScrollsNaturalSize_WithoutScalingControls()
    {
        RunSta(() =>
        {
            ReviewViewport viewport = new() { Width = 400, Height = 300, PixelWidth = 1920, PixelHeight = 1080 };
            Window window = new() { Content = viewport, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                ScrollViewer scroll = Assert.IsType<ScrollViewer>(Assert.Single(viewport.Children.Cast<UIElement>()));
                Canvas canvas = Assert.IsType<Canvas>(scroll.Content);
                Image media = Assert.IsType<Image>(Assert.Single(canvas.Children.Cast<UIElement>()));
                Assert.True(viewport.ClipToBounds);
                Assert.Equal(400, media.Width, 4);
                Assert.Equal(225, media.Height, 4);
                viewport.ScaleMode = "Fill";
                window.UpdateLayout();
                Assert.Equal(300, media.Height, 4);
                Assert.True(media.Width > viewport.ActualWidth);
                Assert.True(Canvas.GetLeft(media) < 0);
                Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                viewport.ScaleMode = "100%";
                window.UpdateLayout();
                DpiScale dpi = VisualTreeHelper.GetDpi(viewport);
                Assert.Equal(1920, media.Width * dpi.DpiScaleX, 4);
                Assert.Equal(1080, media.Height * dpi.DpiScaleY, 4);
                Assert.True(scroll.ScrollableWidth > 0);
                Assert.True(scroll.ScrollableHeight > 0);
                scroll.ScrollToHorizontalOffset(100);
                scroll.ScrollToVerticalOffset(80);
                window.UpdateLayout();
                Assert.Equal(100, scroll.HorizontalOffset, 4);
                Assert.Equal(80, scroll.VerticalOffset, 4);
                viewport.ScaleMode = "Fit";
                window.UpdateLayout();
                Assert.Equal(0, scroll.HorizontalOffset);
                Assert.Equal(0, scroll.VerticalOffset);
                Assert.Equal(400, media.Width, 4);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void SeekSlider_ClockUpdatesDoNotFightDrag_AndBindingRemainsLive()
    {
        RunSta(() =>
        {
            PositionModel model = new();
            ReviewSeekSlider slider = new() { Maximum = 100 };
            slider.SetBinding(ReviewSeekSlider.PositionProperty, new Binding(nameof(PositionModel.Position))
            { Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            model.Position = 10;
            Assert.Equal(10, slider.Value);
            int writes = model.Writes;
            model.Position = 12;
            Assert.Equal(writes + 1, model.Writes); // Clock update is not a feedback seek.
            slider.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            slider.Value = 40;
            Assert.Equal(40, model.Position);
            model.Position = 14; // A queued position callback while the thumb is held.
            Assert.Equal(40, slider.Value);
            slider.Value = 45;
            slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            Assert.Equal(45, model.Position);
            Assert.NotNull(BindingOperations.GetBindingExpression(slider, ReviewSeekSlider.PositionProperty));
            model.Position = 46;
            Assert.Equal(46, slider.Value);
        });
    }

    [Fact]
    public void SliderTemplate_RetainsTrackThumbAndKeyboardCommands()
    {
        RunSta(() =>
        {
            VideoReviewSurface surface = new();
            ReviewSeekSlider slider = new() { Maximum = 100, SmallChange = 1, LargeChange = 5, Style = (Style)surface.FindResource("PlayerSliderStyle") };
            Window window = new() { Content = slider, Width = 400, Height = 100, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                Track track = Assert.IsType<Track>(slider.Template.FindName("PART_Track", slider));
                Assert.NotNull(track.Thumb);
                Assert.Equal(12, track.Thumb.Width);
                Assert.True(slider.IsMoveToPointEnabled);
                Slider.IncreaseSmall.Execute(null, slider);
                Assert.Equal(1, slider.Value);
                Slider.IncreaseLarge.Execute(null, slider);
                Assert.Equal(6, slider.Value);
                Assert.Equal(6, slider.Position);
                Assert.True(slider.Focusable);
                Assert.Equal(32, slider.ActualHeight);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Replay_ReducedMotionFocusAndUnload_ArePresentationOnly()
    {
        RunSta(() =>
        {
            VideoReviewSurface surface = new() { AnimationsEnabled = false };
            Window window = new() { Content = surface, Width = 610, Height = 400, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                Border overlay = Assert.IsType<Border>(surface.FindName("ReplayOverlay"));
                Button replay = Assert.IsType<Button>(surface.FindName("ReplayButton"));
                Button play = Assert.IsType<Button>(surface.FindName("PlayButton"));
                surface.ShowReplay = true;
                Assert.Equal(Visibility.Visible, overlay.Visibility);
                Assert.Equal(1, overlay.Opacity);
                Assert.False(overlay.HasAnimatedProperties);
                replay.Focus();
                Assert.Equal("Replay video", AutomationProperties.GetName(replay));
                surface.ShowReplay = false;
                Assert.Equal(Visibility.Collapsed, overlay.Visibility);
                Assert.False(replay.IsKeyboardFocusWithin);
                Assert.True(play.IsKeyboardFocused);
                surface.AnimationsEnabled = true;
                surface.ShowReplay = true;
                if (SystemParameters.ClientAreaAnimation) Assert.True(overlay.HasAnimatedProperties);
                window.Content = null;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                Assert.False(overlay.HasAnimatedProperties);
                surface.IsFullscreen = true;
                Assert.Equal("Exit fullscreen", surface.FullscreenActionLabel);
                surface.IsFullscreen = false;
                Assert.Equal("Enter fullscreen", surface.FullscreenActionLabel);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ThemedControls_FitMinimumPreviewWidth_AndKeepTimelineUsable()
    {
        RunSta(() =>
        {
            VideoReviewSurface surface = new() { Width = 590, Height = 300, FontSize = 14 };
            // Resolve shared StaticResources in one test dictionary without constructing
            // a process-wide Application on a disposable STA test thread.
            XDocument styles = ReadView("../Resources/Controls.xaml");
            styles.Root!.AddFirst(ReadView("../Resources/Colors.xaml").Root!.Elements());
            surface.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(styles.ToString()));
            Grid root = Assert.IsType<Grid>(surface.Content);
            Grid row = Assert.Single(root.Children.Cast<UIElement>().OfType<Grid>(), child => Grid.GetRow(child) == 1);
            foreach (TextBlock text in row.Children.OfType<TextBlock>()) text.Text = "00:59:59";
            ComboBox scale = Assert.Single(row.Children.OfType<ComboBox>());
            scale.ItemsSource = new[] { "Fit", "Fill", "100%" };
            scale.SelectedItem = "100%";
            Window window = new() { Content = surface, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                ReviewSeekSlider seek = Assert.Single(row.Children.OfType<ReviewSeekSlider>());
                Assert.True(seek.ActualWidth >= 100, $"Timeline width: {seek.ActualWidth}");
                foreach (FrameworkElement element in row.Children)
                {
                    Rect bounds = element.TransformToAncestor(row).TransformBounds(new Rect(element.RenderSize));
                    Assert.True(bounds.Left >= 0 && bounds.Right <= row.ActualWidth + 0.1);
                }
                Assert.Equal("100%", scale.SelectionBoxItem);
                Assert.True(scale.ActualWidth - scale.Padding.Left - scale.Padding.Right >= 40);
                Assert.Equal(Visibility.Visible, row.Visibility); // No fullscreen auto-hide behavior.
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(590, true, true)] // Conservative editor-pane width at MainWindow.MinWidth=1024.
    [InlineData(590, false, true)]
    [InlineData(750, true, false)] // Conservative editor-pane width at the default 1280 width.
    [InlineData(750, false, false)]
    public void VideoResultPanel_AdaptsWithoutClippingOrOverlap(
        double availableWidth,
        bool exported,
        bool expectedCompact)
    {
        RunSta(() =>
        {
            UserControl view = LoadExportStatusViewForLayout();
            view.DataContext = new VideoResultLayoutModel(exported);
            view.Measure(new Size(availableWidth, double.PositiveInfinity));
            view.Arrange(new Rect(0, 0, availableWidth, view.DesiredSize.Height));
            view.UpdateLayout();

            Border card = Assert.IsType<Border>(view.FindName("VideoResultCard"));
            AdaptiveResultPanel layout = Assert.IsType<AdaptiveResultPanel>(view.FindName("VideoResultLayout"));
            StackPanel text = Assert.IsType<StackPanel>(view.FindName("VideoResultText"));
            Grid metrics = Assert.IsType<Grid>(view.FindName("VideoResultMetrics"));
            StackPanel actions = Assert.IsType<StackPanel>(view.FindName("VideoResultActions"));
            TextBlock title = Assert.IsType<TextBlock>(view.FindName("VideoResultTitleText"));
            TextBlock summary = Assert.IsType<TextBlock>(view.FindName("VideoResultSummaryText"));
            TextBlock destination = Assert.IsType<TextBlock>(view.FindName("VideoExportDestinationTextBlock"));
            Button export = Assert.IsType<Button>(view.FindName("VideoExportButton"));
            Button openFolder = Assert.IsType<Button>(view.FindName("OpenVideoExportFolderButton"));
            Border[] metricCards =
            [
                Assert.IsType<Border>(view.FindName("VideoFramesMetricCard")),
                Assert.IsType<Border>(view.FindName("VideoOutputMetricCard")),
                Assert.IsType<Border>(view.FindName("VideoAudioMetricCard"))
            ];

            Assert.Equal(Visibility.Visible, card.Visibility);
            Assert.Equal(Visibility.Visible, metrics.Visibility);
            Assert.Equal(Visibility.Visible, actions.Visibility);
            Assert.Equal(expectedCompact, layout.IsCompact);
            Assert.True(text.ActualWidth >= 180, $"Result text width at {availableWidth}: {text.ActualWidth}");
            Assert.Equal(TextTrimming.None, title.TextTrimming);
            Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
            Assert.True(summary.ActualWidth > 0 && summary.ActualWidth <= text.ActualWidth);
            Assert.Equal(TextTrimming.CharacterEllipsis, destination.TextTrimming);
            Assert.True(export.IsEnabled);
            Assert.True(export.ActualWidth >= 112);
            Assert.Equal(exported ? Visibility.Visible : Visibility.Collapsed, openFolder.Visibility);
            if (exported)
            {
                Assert.True(openFolder.IsEnabled);
                Assert.True(openFolder.ActualWidth >= 112);
            }

            FrameworkElement[] visibleElements = exported
                ? [title, summary, destination, export, openFolder, .. metricCards]
                : [title, summary, export, .. metricCards];
            foreach (FrameworkElement element in visibleElements)
            {
                Rect bounds = BoundsWithin(element, card);
                Assert.True(bounds.Left >= -0.1 && bounds.Top >= -0.1,
                    $"{element.Name} starts outside the result card: {bounds}");
                Assert.True(bounds.Right <= card.ActualWidth + 0.1 && bounds.Bottom <= card.ActualHeight + 0.1,
                    $"{element.Name} extends outside the result card: {bounds}; card={card.RenderSize}");
            }

            Rect textBounds = BoundsWithin(text, layout);
            Rect metricsBounds = BoundsWithin(metrics, layout);
            Rect actionBounds = BoundsWithin(actions, layout);
            Assert.False(Overlaps(textBounds, actionBounds));
            Assert.False(Overlaps(metricsBounds, actionBounds));
            if (expectedCompact)
            {
                Assert.True(textBounds.Bottom <= metricsBounds.Top + 0.1);
                Assert.True(Math.Abs(CenterY(metricsBounds) - CenterY(actionBounds)) <= 0.1);
            }
            else
            {
                Assert.True(textBounds.Right <= metricsBounds.Left + 0.1);
                Assert.True(metricsBounds.Right <= actionBounds.Left + 0.1);
                Assert.True(Math.Abs(CenterY(textBounds) - CenterY(metricsBounds)) <= 0.1);
                Assert.True(Math.Abs(CenterY(metricsBounds) - CenterY(actionBounds)) <= 0.1);
            }

            for (int index = 1; index < metricCards.Length; index++)
            {
                Assert.False(Overlaps(BoundsWithin(metricCards[index - 1], card), BoundsWithin(metricCards[index], card)));
            }
            Assert.DoesNotContain(VisualDescendants(view).OfType<ScrollViewer>(), scroll =>
                scroll.ComputedHorizontalScrollBarVisibility == Visibility.Visible);
        });
    }

    [Fact]
    public void VideoResultPanel_WideReadyLayoutIsMateriallyMoreCompact()
    {
        RunSta(() =>
        {
            Border compact = ArrangeVideoResultCard(590, exported: false);
            Border wide = ArrangeVideoResultCard(750, exported: false);

            Assert.True(compact.ActualHeight - wide.ActualHeight >= 30,
                $"Compact={compact.ActualHeight}; wide={wide.ActualHeight}");
        });
    }

    [Fact]
    public void VideoResultPanel_UsesHysteresisAroundItsMeasuredBreakpoint()
    {
        RunSta(() =>
        {
            UserControl view = LoadExportStatusViewForLayout();
            view.DataContext = new VideoResultLayoutModel(exported: false);

            Arrange(750);
            AdaptiveResultPanel panel = Assert.IsType<AdaptiveResultPanel>(view.FindName("VideoResultLayout"));
            Assert.False(panel.IsCompact);

            Arrange(720); // Inside the hysteresis band: retain the current wide mode.
            Assert.False(panel.IsCompact);
            Arrange(700); // Cross the lower boundary.
            Assert.True(panel.IsCompact);
            Arrange(720); // Inside the band again: retain compact mode.
            Assert.True(panel.IsCompact);
            Arrange(750); // Cross the upper boundary.
            Assert.False(panel.IsCompact);

            void Arrange(double width)
            {
                view.Measure(new Size(width, double.PositiveInfinity));
                view.Arrange(new Rect(0, 0, width, view.DesiredSize.Height));
                view.UpdateLayout();
            }
        });
    }

    [Fact]
    public void SharedViewContract_HasOnePrimaryPlayControl_OverlayOnlyReplay_AndAccessibleActions()
    {
        XDocument document = ReadView("VideoReviewSurface.xaml");
        XElement play = Assert.Single(document.Descendants(), e => e.Attribute("Command")?.Value == "{Binding ToggleRenderedVideoPlaybackCommand}");
        Assert.Equal("{Binding RenderedVideoPlaybackActionLabel}", play.Attribute("AutomationProperties.Name")?.Value);
        XElement replay = Assert.Single(document.Descendants(), e => e.Attribute("Command")?.Value == "{Binding RestartRenderedVideoCommand}");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        Assert.Equal("ReplayOverlay", replay.Parent?.Attribute(x + "Name")?.Value);
        Assert.Single(document.Descendants(), e => e.Attribute("Command")?.Value == "{Binding ToggleRenderedVideoMuteCommand}");
        Assert.Single(document.Descendants(), e => e.Attribute("SelectedItem")?.Value == "{Binding SelectedFitMode}");
        foreach (XElement control in document.Descendants().Where(e => e.Name.LocalName is "Button" or "Slider" or "ReviewSeekSlider" or "ComboBox"))
        {
            Assert.NotNull(control.Attribute("ToolTip"));
            Assert.NotNull(control.Attribute("AutomationProperties.Name"));
        }
        XDocument preview = ReadView("PreviewPanelView.xaml");
        Assert.DoesNotContain(preview.Descendants(), e => e.Name.LocalName == "Viewbox");
        Assert.Single(preview.Descendants(), e => e.Name.LocalName == "VideoReviewSurface");
        XElement frame = Assert.Single(preview.Descendants(), e => e.Attribute("Visibility")?.Value.Contains("ShowVideoFrameControls", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(frame.Descendants(), e => e.Name.LocalName == "Button");
        Assert.Contains(frame.Descendants(), e => e.Attribute("Text")?.Value == "Frame position");
        XElement media = Assert.Single(preview.Descendants(), e => e.Name.LocalName == "ReviewViewport");
        Assert.Equal("{Binding SelectedFitMode}", media.Attribute("ScaleMode")?.Value);
        Assert.Equal("{Binding CurrentPreviewPixelWidth}", media.Attribute("PixelWidth")?.Value);
    }

    private static XDocument ReadView(string name)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GlyphFrame.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(directory.FullName, "src", "GlyphFrame.App", "Views", name));
    }

    private static UserControl LoadExportStatusViewForLayout()
    {
        XDocument document = ReadView("ExportStatusView.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace appPresentation = "clr-namespace:GlyphFrame.App.Presentation;assembly=GlyphFrame.App";
        XNamespace appViews = "clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App";
        XElement root = Assert.IsType<XElement>(document.Root);
        root.Attribute(x + "Class")?.Remove();
        root.Attribute("Visibility")?.Remove(); // Root StaticResource resolves before local resources during loose-XAML parsing.
        foreach (XElement element in root.DescendantsAndSelf().Where(element =>
                     element.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views"))
        {
            element.Name = appViews + element.Name.LocalName;
        }
        foreach (XAttribute attribute in root.DescendantsAndSelf().SelectMany(element => element.Attributes()).Where(attribute =>
                     !attribute.IsNamespaceDeclaration
                     && attribute.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views").ToArray())
        {
            XElement parent = attribute.Parent!;
            XName name = appViews + attribute.Name.LocalName;
            string value = attribute.Value;
            attribute.Remove();
            parent.SetAttributeValue(name, value);
        }
        foreach (XAttribute attribute in root.DescendantsAndSelf().SelectMany(element => element.Attributes()).Where(attribute =>
                     !attribute.IsNamespaceDeclaration
                     && attribute.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Presentation").ToArray())
        {
            XElement parent = attribute.Parent!;
            XName name = appPresentation + attribute.Name.LocalName;
            string value = attribute.Value;
            attribute.Remove();
            parent.SetAttributeValue(name, value);
        }
        root.SetAttributeValue(XNamespace.Xmlns + "presentation", appPresentation.NamespaceName);
        root.SetAttributeValue(XNamespace.Xmlns + "views", appViews.NamespaceName);
        XElement resources = new(presentation + "ResourceDictionary");
        foreach (string fileName in new[] { "Colors.xaml", "Typography.xaml", "Controls.xaml", "Components.xaml" })
        {
            foreach (XElement resource in ReadView($"../Resources/{fileName}").Root!.Elements())
            {
                resources.Add(new XElement(resource));
            }
        }
        root.AddFirst(new XElement(presentation + "UserControl.Resources", resources));
        return Assert.IsType<UserControl>(XamlReader.Parse(document.ToString(SaveOptions.DisableFormatting)));
    }

    private static Rect BoundsWithin(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static Border ArrangeVideoResultCard(double availableWidth, bool exported)
    {
        UserControl view = LoadExportStatusViewForLayout();
        view.DataContext = new VideoResultLayoutModel(exported);
        view.Measure(new Size(availableWidth, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, availableWidth, view.DesiredSize.Height));
        view.UpdateLayout();
        return Assert.IsType<Border>(view.FindName("VideoResultCard"));
    }

    private static double CenterY(Rect bounds) => bounds.Top + (bounds.Height / 2);

    private static bool Overlaps(Rect first, Rect second) =>
        first.Left < second.Right - 0.1
        && first.Right > second.Left + 0.1
        && first.Top < second.Bottom - 0.1
        && first.Bottom > second.Top + 0.1;

    private static void RunSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF review test did not complete.");
        failure?.Throw();
    }

    private sealed class PositionModel : INotifyPropertyChanged
    {
        private double _position;
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Writes { get; private set; }
        public double Position
        {
            get => _position;
            set { _position = value; Writes++; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position))); }
        }
    }

    private sealed class VideoResultLayoutModel(bool exported)
    {
        private static readonly ICommand Command = new EnabledCommand();

        public bool IsProcessingResultPanelVisible => true;
        public bool IsExportStatusVisible => false;
        public bool ShowImageResultPanel => false;
        public bool ShowVideoResultPanel => true;
        public bool IsVideoResultSuccess => true;
        public bool IsVideoResultWarning => false;
        public bool IsVideoResultFailure => false;
        public bool HasVideoExportDestination => exported;
        public bool HasVideoArtifact => true;
        public bool ShowVideoRenderMetrics => true;
        public bool CanOpenExportFolder => exported;
        public string VideoResultTitle => exported ? "Video Exported" : "Video Ready";
        public string VideoResultSummary => exported
            ? "Ready for review and another export."
            : "Ready for review and export.";
        public string VideoExportDestinationText => "C:\\Дуже довга тека\\відео-日本語-测试\\готове символізоване відео.mp4";
        public string VideoRenderedFrameCountText => "284";
        public string VideoOutputDimensionsText => "1024 × 576";
        public string VideoAudioOutcomeText => "Original";
        public string VideoAudioOutcomeDescription => "Original audio preserved";
        public string VideoExportActionLabel => exported ? "Export Again" : "Export Video";
        public ICommand ExportVideoCommand => Command;
        public ICommand OpenExportFolderCommand => Command;
    }

    private sealed class EnabledCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
