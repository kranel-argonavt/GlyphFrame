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
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class ReviewSurfaceTests
{
    [Theory]
    [InlineData(404, false)]
    [InlineData(590, false)]
    [InlineData(1100, false)]
    [InlineData(404, true)]
    [InlineData(590, true)]
    [InlineData(1100, true)]
    public void ProcessingAndCancelling_UseSharedModeWithReachableActions(double width, bool cancelling)
    {
        RunSta(() =>
        {
            UserControl view = LoadExportStatusViewForLayout();
            view.Width = width; view.DataContext = new ProcessingLayoutModel(cancelling);
            StatusLayoutMode mode = width >= 476 ? StatusLayoutMode.CompactWide : StatusLayoutMode.Narrow;
            Window host = new() { Content = view, SizeToContent = SizeToContent.WidthAndHeight, Left = -10000, ShowActivated = false };
            try
            {
                host.Show(); host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); host.UpdateLayout();
                Border shell = (Border)view.FindName("StatusShell"), active = (Border)view.FindName("ProcessingContent");
                AdaptiveResultPanel panel = Assert.IsType<AdaptiveResultPanel>(active.Child);
                Assert.True(active.IsVisible); Assert.Equal(mode, panel.LayoutMode);
                Assert.Equal(active.DesiredSize.Height + 34, shell.ActualHeight, 4);
                Assert.True(Assert.Single(VisualDescendants(active).OfType<ExportActivityIndicator>()).IsActive);
                Button cancel = Assert.Single(VisualDescendants(active).OfType<Button>());
                Assert.True(cancel.IsVisible); Assert.Equal(!cancelling, cancel.IsEnabled);
                Assert.Equal("CancelExportCommand", BindingOperations.GetBinding(cancel, Button.CommandProperty)!.Path.Path);
                foreach (TextBlock field in VisualDescendants(active).OfType<TextBlock>())
                {
                    Assert.False(string.IsNullOrWhiteSpace(field.Text));
                    Rect bounds = BoundsWithin(field, shell);
                    Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= shell.ActualWidth && bounds.Bottom <= shell.ActualHeight);
                }
                Rect button = BoundsWithin(cancel, shell);
                Assert.True(button.Right <= shell.ActualWidth && button.Bottom <= shell.ActualHeight);
                Assert.Empty(VisualDescendants(shell).OfType<ScrollViewer>());
                if (mode == StatusLayoutMode.CompactWide)
                {
                    TextBlock title = VisualDescendants(active).OfType<TextBlock>().First(t => t.Text == "Rendering Video");
                    TextBlock remaining = VisualDescendants(active).OfType<TextBlock>().First(t => t.Text.StartsWith("Remaining", StringComparison.Ordinal));
                    Assert.InRange(BoundsWithin(title, shell).Top - shell.BorderThickness.Top, 16, 17);
                    Assert.InRange(shell.ActualHeight - shell.BorderThickness.Bottom - BoundsWithin(remaining, shell).Bottom, 15.99, 17.01);
                }
            }
            finally { host.Close(); }
        });
    }

    [Theory]
    [InlineData(320, StatusLayoutMode.Narrow)]
    [InlineData(404, StatusLayoutMode.Narrow)]
    [InlineData(612, StatusLayoutMode.Intermediate)]
    [InlineData(736, StatusLayoutMode.CompactWide)]
    [InlineData(1100, StatusLayoutMode.Spacious)]
    public void ResponsiveResult_UsesApprovedCompositionWithFiniteMetrics(double width, StatusLayoutMode expected)
    {
        RunSta(() =>
        {
            UserControl view = LoadExportStatusViewForLayout();
            VideoResultLayoutModel model = new(true);
            view.DataContext = model;

            view.Width = width;
            Window host = new() { Content = view, SizeToContent = SizeToContent.WidthAndHeight, Left = -10000, ShowActivated = false };
            try
            {
                host.Show();
                view.Measure(new Size(width, double.PositiveInfinity));
                view.Arrange(new Rect(0, 0, width, view.DesiredSize.Height)); view.UpdateLayout();
                AdaptiveResultPanel result = (AdaptiveResultPanel)view.FindName("VideoResultLayout");
                FrameworkElement text = (FrameworkElement)view.FindName("VideoResultText");
                MetricCardsPanel metrics = (MetricCardsPanel)view.FindName("VideoResultMetrics");
                VideoExportActionsPanel actions = (VideoExportActionsPanel)view.FindName("VideoResultActions");
                Button export = (Button)view.FindName("VideoExportButton"), folder = (Button)view.FindName("OpenVideoExportFolderButton"), copy = (Button)view.FindName("CopyVideoExportPathButton");
                Assert.Equal(expected, result.LayoutMode);
                Assert.True(result.IsVisible && metrics.IsVisible && text.IsVisible);
                Assert.True(export.IsEnabled && folder.IsEnabled && copy.IsEnabled);
                Assert.Equal(model.VideoFileSizeText, ((StackPanel)((Border)metrics.Children[0]).Child).Children.OfType<TextBlock>().Last().Text);
                Assert.Equal(model.ExportPathCopy.Path, copy.ToolTip);
                Assert.NotNull(BindingOperations.GetBindingExpression(copy, Button.CommandProperty));
                Assert.InRange(text.ActualWidth, 120, width);
                Rect t = BoundsWithin(text, result), m = BoundsWithin(metrics, result), a = BoundsWithin(actions, result);
                Assert.False(Overlaps(t, m)); Assert.False(Overlaps(t, a)); Assert.False(Overlaps(m, a));
                if (expected == StatusLayoutMode.Spacious)
                {
                    Assert.True(t.Right < m.Left && m.Right < a.Left);
                    Assert.Equal(CenterY(t), CenterY(m), 4); Assert.Equal(CenterY(m), CenterY(a), 4);
                    Assert.True(text.ActualWidth >= 180);
                }
                else if (expected == StatusLayoutMode.CompactWide) { Assert.True(t.Bottom < m.Top && m.Right < a.Left); }
                else if (expected == StatusLayoutMode.Intermediate) { Assert.True(t.Bottom < m.Top && Math.Max(t.Right, m.Right) < a.Left); }
                else { Assert.True(t.Bottom < m.Top && m.Bottom < a.Top); Assert.Equal((result.ActualWidth - a.Width) / 2, a.Left, 4); Assert.InRange(a.Width, 1, 360); }
                Rect e = BoundsWithin(export, actions), f = BoundsWithin(folder, actions), c = BoundsWithin(copy, actions);
                if (expected == StatusLayoutMode.Intermediate)
                {
                    Assert.Equal(e.Width, f.Width); Assert.Equal(f.Width, c.Width);
                    Assert.Equal(e.Left, f.Left); Assert.Equal(f.Left, c.Left);
                    Assert.Equal(8, f.Top - e.Bottom, 6); Assert.Equal(8, c.Top - f.Bottom, 6);
                }
                else
                {
                    Assert.Equal(e.Left, f.Left); Assert.Equal(e.Right, c.Right); Assert.Equal(f.Top, c.Top);
                    if (expected == StatusLayoutMode.Narrow) { Assert.Equal(f.Width, c.Width); }
                    else { Assert.Equal(f.Width, c.Width); }
                    Assert.Equal(e.Width, f.Width + ExportActionsPanel.Gap + c.Width, 6);
                    Assert.True(e.Bottom < f.Top);
                }
                FrameworkElement[] controls = [text, export, folder, copy, .. metrics.Children.Cast<FrameworkElement>()];
                foreach (FrameworkElement control in controls)
                {
                    Rect bounds = BoundsWithin(control, result);
                    Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= result.ActualWidth + 0.1 && bounds.Bottom <= result.ActualHeight + 0.1);
                }
                foreach (FrameworkElement card in metrics.Children)
                {
                    Assert.InRange(card.ActualWidth, MetricCardsPanel.MinimumCardWidth, MetricCardsPanel.MaximumCardWidth);
                    foreach (TextBlock value in VisualDescendants(card).OfType<TextBlock>().Skip(1))
                    { Assert.Equal(TextWrapping.Wrap, value.TextWrapping); Assert.Equal(TextTrimming.None, value.TextTrimming); }
                }
                Assert.DoesNotContain(VisualDescendants(view).OfType<ScrollViewer>(), scroll => scroll.ComputedHorizontalScrollBarVisibility == Visibility.Visible);
                string? captureDirectory = Environment.GetEnvironmentVariable("GLYPHFRAME_STATUS_CAPTURE_DIRECTORY");
                if (captureDirectory is not null)
                {
                    Directory.CreateDirectory(captureDirectory);
                    SaveComponent(RenderComponent(view), Path.Combine(captureDirectory, $"component-{expected}-{width}.png"));
                }
            }
            finally { host.Close(); }
        });
    }

    [Theory]
    [InlineData(180)]
    [InlineData(290)]
    [InlineData(352)]
    [InlineData(900)]
    public void Metrics_BoundedGrowthAndLongValues_ReflowWithoutOverlap(double width)
    {
        RunSta(() =>
        {
            MetricCardsPanel panel = new();
            foreach (string value in new[] { "123.45 GB", "16384 × 16384", "Unavailable_日本語_Кириллица" })
            {
                panel.Children.Add(new Border
                {
                    Padding = new Thickness(10, 8, 10, 8),
                    Child = new TextBlock
                    { Text = value, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None, FontSize = 12 }
                });
            }
            foreach (double available in new[] { width, width + 20, width, width + 1, width })
            {
                panel.Measure(new Size(available, double.PositiveInfinity));
                panel.Arrange(new Rect(0, 0, available, panel.DesiredSize.Height)); panel.UpdateLayout();
                Rect[] bounds = panel.Children.Cast<FrameworkElement>().Select(c => BoundsWithin(c, panel)).ToArray();
                Assert.All(bounds, b => { Assert.InRange(b.Width, 88, 112); Assert.True(b.Left >= 0 && b.Right <= available + 0.1 && b.Bottom <= panel.ActualHeight + 0.1); });
                for (int i = 0; i < bounds.Length; i++)
                { for (int j = i + 1; j < bounds.Length; j++) { Assert.False(Overlaps(bounds[i], bounds[j])); } }
                Assert.Equal(available < 280, bounds[0].Top != bounds[^1].Top);
                Assert.All(panel.Children.Cast<Border>(), card => Assert.True(((TextBlock)card.Child).ActualWidth <= card.ActualWidth - 20 + 0.1));
            }
        });
    }

    [Theory]
    [InlineData(VideoPlaybackSurfaceKind.Original, false)]
    [InlineData(VideoPlaybackSurfaceKind.Original, true)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered, false)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered, true)]
    public void SharedPlayerTemplate_CentersAllocation_AndOnlyPlayHasOpticalCorrection(VideoPlaybackSurfaceKind role, bool fullscreen)
    {
        RunSta(() =>
        {
            VideoReviewSurface surface = new() { PlaybackKind = role, IsFullscreen = fullscreen, ShowReplay = true };
            PositionModel model = new();
            surface.SetBinding(VideoReviewSurface.IsPlayingProperty, new Binding(nameof(model.IsPlaying)) { Source = model });
            Window window = new() { Content = surface, Width = 650, Height = 400, Left = -10000, ShowActivated = false };
            try
            {
                window.Show(); window.UpdateLayout();
                Button play = (Button)surface.FindName("PlayButton");
                Grid allocation = Assert.IsType<Grid>(play.Content);
                System.Windows.Shapes.Path icon = Assert.IsType<System.Windows.Shapes.Path>(Assert.Single(allocation.Children.Cast<UIElement>()));
                Grid row = Assert.IsType<Grid>(play.Parent);
                Rect rowBounds = BoundsWithin(row, surface), buttonBounds = BoundsWithin(play, surface);
                Rect[] surrounding = row.Children.OfType<FrameworkElement>().Select(c => BoundsWithin(c, surface)).ToArray();
                foreach (Button button in VisualDescendants(surface).OfType<Button>().Where(b => b.IsVisible && (b.Content is System.Windows.Shapes.Path || b == play)))
                {
                    button.ApplyTemplate();
                    ContentPresenter presenter = Assert.IsType<ContentPresenter>(button.Template.FindName("IconContent", button));
                    Rect slot = BoundsWithin(presenter, button);
                    Assert.Equal(HorizontalAlignment.Center, presenter.HorizontalAlignment);
                    Assert.Equal(VerticalAlignment.Center, presenter.VerticalAlignment);
                    Assert.InRange(Math.Abs(slot.Left + slot.Width / 2 - button.ActualWidth / 2), 0, 0.01);
                    Assert.InRange(Math.Abs(slot.Top + slot.Height / 2 - button.ActualHeight / 2), 0, 0.01);
                    Assert.True(slot.Left >= 0 && slot.Top >= 0 && slot.Right <= button.ActualWidth && slot.Bottom <= button.ActualHeight);
                    if (button != play) { Assert.True(((System.Windows.Shapes.Path)button.Content).RenderTransform.Value.IsIdentity); }
                }
                Assert.Equal(32, play.ActualWidth); Assert.Equal(32, play.ActualHeight);
                Assert.Equal(16, allocation.ActualWidth); Assert.Equal(16, allocation.ActualHeight);
                Assert.Equal(16d * 14 / 18, icon.ActualWidth, 6); Assert.Equal(16, icon.ActualHeight, 6);
                Assert.Equal(1, icon.RenderTransform.Value.OffsetX);
                // Uniform-fit triangle: 14/18 aspect; centroid lies one sixth
                // of its ink width left of its bounds center. The 1-DIP offset
                // reduces that imbalance without changing the 16-DIP slot.
                double centroidDeficit = 16d * 14 / 18 / 6;
                Assert.True(Math.Abs(centroidDeficit - icon.RenderTransform.Value.OffsetX) < centroidDeficit);
                for (int i = 0; i < 4; i++)
                {
                    model.IsPlaying = !model.IsPlaying; window.UpdateLayout();
                    Assert.Equal(model.IsPlaying, surface.IsPlaying);
                    Assert.Equal(model.IsPlaying ? 0 : 1, icon.RenderTransform.Value.OffsetX);
                    Assert.Equal(((Geometry)surface.FindResource(model.IsPlaying ? "PlayerPauseIcon" : "PlayerPlayIcon")).ToString(), icon.Data.ToString());
                    Assert.Equal(buttonBounds, BoundsWithin(play, surface)); Assert.Equal(rowBounds, BoundsWithin(row, surface));
                    Assert.Equal(surrounding, row.Children.OfType<FrameworkElement>().Select(c => BoundsWithin(c, surface)).ToArray());
                }
                Assert.NotNull(BindingOperations.GetBindingExpression(surface, VideoReviewSurface.IsPlayingProperty));
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(VideoPlaybackSurfaceKind.Original, false)]
    [InlineData(VideoPlaybackSurfaceKind.Original, true)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered, false)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered, true)]
    public void SharedSoundMute_UsesOutlinedMatchingGeometryInBothRolesAndHosts(VideoPlaybackSurfaceKind role, bool fullscreen)
    {
        RunSta(() =>
        {
            VideoReviewSurface surface = new() { PlaybackKind = role, IsFullscreen = fullscreen };
            PositionModel model = new();
            // IsMuted is the view's presentation input; the controller/role bindings
            // are separately covered by the existing synchronization/lifetime tests.
            surface.SetBinding(VideoReviewSurface.IsMutedProperty, new Binding(nameof(model.IsMuted)) { Source = model });
            Window window = new() { Content = surface, Width = 610, Height = 400, Left = -10000, ShowActivated = false };
            try
            {
                window.Show(); window.UpdateLayout();
                Grid row = Assert.Single(((Grid)surface.Content).Children.OfType<Grid>(), grid => Grid.GetRow(grid) == 1);
                Button mute = Assert.Single(row.Children.OfType<Button>(), button => Grid.GetColumn(button) == 4);
                System.Windows.Shapes.Path icon = Assert.IsType<System.Windows.Shapes.Path>(mute.Content);
                string sound = icon.Data.ToString();
                Rect bounds = BoundsWithin(icon, mute);
                Assert.Null(icon.Fill); Assert.Equal(1.6, icon.StrokeThickness);
                Assert.Equal(PenLineCap.Round, icon.StrokeStartLineCap); Assert.Equal(PenLineCap.Round, icon.StrokeEndLineCap);
                Assert.False(icon.Focusable); Assert.InRange(icon.ActualWidth, 14, 20);
                model.IsMuted = true; window.UpdateLayout();
                Assert.True(surface.IsMuted); Assert.StartsWith(sound, icon.Data.ToString()); Assert.NotEqual(sound, icon.Data.ToString());
                Assert.Equal(bounds, BoundsWithin(icon, mute));
                model.IsMuted = false; window.UpdateLayout(); Assert.Equal(sound, icon.Data.ToString());
                Assert.NotNull(BindingOperations.GetBindingExpression(surface, VideoReviewSurface.IsMutedProperty));
            }
            finally { window.Close(); }
        });
    }

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
                ScrollViewer scroll = Assert.IsAssignableFrom<ScrollViewer>(Assert.Single(viewport.Children.Cast<UIElement>()));
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
                ScrollViewer scroll = Assert.IsAssignableFrom<ScrollViewer>(Assert.Single(viewport.Children.Cast<UIElement>()));
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

    [Theory]
    [InlineData(0, 640, 360)]
    [InlineData(90, 360, 640)]
    [InlineData(180, 640, 360)]
    [InlineData(270, 360, 640)]
    [InlineData(-90, 360, 640)]
    [InlineData(450, 360, 640)]
    public void OriginalPlaybackRotation_UsesDisplayBoundsAndSingleQuarterTurn(
        double rotation,
        double displayWidth,
        double displayHeight)
    {
        RunSta(() =>
        {
            ReviewViewport viewport = new()
            {
                Width = 400,
                Height = 300,
                PixelWidth = displayWidth,
                PixelHeight = displayHeight,
                RotationDegrees = rotation,
                ScaleMode = "100%",
                Source = new DrawingImage(new GeometryDrawing(
                    Brushes.MediumPurple,
                    null,
                    new RectangleGeometry(new Rect(0, 0, 640, 360))))
            };
            Window window = new() { Content = viewport, Width = 420, Height = 340, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                ScrollViewer scroll = Assert.IsAssignableFrom<ScrollViewer>(Assert.Single(viewport.Children.Cast<UIElement>()));
                Canvas canvas = Assert.IsType<Canvas>(scroll.Content);
                Image image = Assert.IsType<Image>(Assert.Single(canvas.Children.Cast<UIElement>()));
                int normalized = ((int)rotation % 360 + 360) % 360;
                if (normalized is not (90 or 180 or 270)) normalized = 0;
                if (normalized == 0)
                {
                    Assert.Same(Transform.Identity, image.RenderTransform);
                }
                else
                {
                    Assert.Equal(normalized, Assert.IsType<RotateTransform>(image.RenderTransform).Angle);
                }
                Rect displayed = image.TransformToAncestor(canvas).TransformBounds(new Rect(image.RenderSize));
                DpiScale dpi = VisualTreeHelper.GetDpi(viewport);
                Assert.Equal(displayWidth, displayed.Width * dpi.DpiScaleX, 3);
                Assert.Equal(displayHeight, displayed.Height * dpi.DpiScaleY, 3);
                Assert.True(canvas.Width * dpi.DpiScaleX >= displayWidth);
                Assert.True(canvas.Height * dpi.DpiScaleY >= displayHeight);
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
    [InlineData(404, true, true)]
    [InlineData(590, true, false)] // Conservative editor-pane width at MainWindow.MinWidth=1024.
    [InlineData(404, false, true)]
    [InlineData(590, false, false)]
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
            MetricCardsPanel metrics = Assert.IsType<MetricCardsPanel>(view.FindName("VideoResultMetrics"));
            VideoExportActionsPanel actions = Assert.IsType<VideoExportActionsPanel>(view.FindName("VideoResultActions"));
            TextBlock title = Assert.IsType<TextBlock>(view.FindName("VideoResultTitleText"));
            TextBlock summary = Assert.IsType<TextBlock>(view.FindName("VideoResultSummaryText"));
            Button copy = Assert.IsType<Button>(view.FindName("CopyVideoExportPathButton"));
            Button export = Assert.IsType<Button>(view.FindName("VideoExportButton"));
            Button openFolder = Assert.IsType<Button>(view.FindName("OpenVideoExportFolderButton"));
            Border[] metricCards =
            [
                Assert.IsType<Border>(view.FindName("VideoFileSizeMetricCard")),
                Assert.IsType<Border>(view.FindName("VideoOutputMetricCard")),
                Assert.IsType<Border>(view.FindName("VideoAudioMetricCard"))
            ];

            Assert.Equal(Visibility.Visible, card.Visibility);
            Assert.Equal(Visibility.Visible, metrics.Visibility);
            Assert.Equal(Visibility.Visible, actions.Visibility);
            Assert.Equal(expectedCompact, layout.IsCompact);
            Assert.True(text.ActualWidth >= 120, $"Result text width at {availableWidth}: {text.ActualWidth}");
            Assert.Equal(TextTrimming.None, title.TextTrimming);
            Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
            Assert.True(summary.ActualWidth > 0 && summary.ActualWidth <= text.ActualWidth);
            Assert.Equal(exported ? Visibility.Visible : Visibility.Collapsed, copy.Visibility);
            Assert.Equal(exported ? new VideoResultLayoutModel(true).ExportPathCopy.Path : string.Empty, copy.ToolTip);
            Assert.True(copy.Focusable);
            Assert.Equal(112, copy.MinWidth);
            Assert.True(double.IsNaN(copy.Width));
            Assert.True(export.IsEnabled);
            Assert.True(export.ActualWidth >= 112);
            Assert.Equal(exported ? Visibility.Visible : Visibility.Collapsed, openFolder.Visibility);
            if (exported)
            {
                Assert.True(openFolder.IsEnabled);
                Assert.True(openFolder.ActualWidth >= 112);
            }

            FrameworkElement[] visibleElements = exported
                ? [title, summary, copy, export, openFolder, .. metricCards]
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
                Assert.True(metricsBounds.Bottom <= actionBounds.Top + 0.1);
            }
            else if (layout.LayoutMode == StatusLayoutMode.Spacious)
            {
                Assert.True(textBounds.Right < metricsBounds.Left && metricsBounds.Right < actionBounds.Left);
                Assert.Equal(CenterY(textBounds), CenterY(metricsBounds), 4);
                Assert.Equal(CenterY(metricsBounds), CenterY(actionBounds), 4);
            }
            else if (layout.LayoutMode == StatusLayoutMode.Intermediate)
            {
                Assert.True(textBounds.Bottom <= metricsBounds.Top + 0.1);
                Assert.True(Math.Max(textBounds.Right, metricsBounds.Right) <= actionBounds.Left + 0.1);
            }
            else
            {
                Assert.True(textBounds.Bottom <= metricsBounds.Top + 0.1);
                if (layout.LayoutMode == StatusLayoutMode.Below) { Assert.True(metricsBounds.Bottom < actionBounds.Top); }
                else { Assert.True(metricsBounds.Right <= actionBounds.Left + 0.1); }
                if (!exported && layout.LayoutMode == StatusLayoutMode.CompactWide) { Assert.True(Math.Abs(CenterY(metricsBounds) - CenterY(actionBounds)) <= 0.1); }
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
            Border compact = ArrangeVideoResultCard(480, exported: false);
            Border wide = ArrangeVideoResultCard(1100, exported: false);

            Assert.True(compact.ActualHeight - wide.ActualHeight >= 30,
                $"Compact={compact.ActualHeight}; wide={wide.ActualHeight}");
        });
    }



    [Theory]
    [InlineData(590, "ready", false)]
    [InlineData(750, "ready", false)]
    [InlineData(1100, "ready", false)]
    [InlineData(1100, "exported", false)]
    [InlineData(404, "ready", true)]
    [InlineData(404, "exported", true)]
    [InlineData(590, "stale", false)]
    [InlineData(750, "stale", false)]
    [InlineData(590, "exported", false)]
    [InlineData(750, "exported", false)]
    [InlineData(590, "failed", false)]
    [InlineData(590, "cancelled", false)]
    [InlineData(590, "export-failure", false)]
    [InlineData(590, "export-cancellation", false)]
    [InlineData(590, "fallback", false)]
    [InlineData(750, "fallback", false)]
    [InlineData(590, "initial-failure", false)]
    [InlineData(590, "initial-cancellation", false)]
    public void ImageResultPanel_MeasuresAndArrangesAllStatesWithinTheCard(
        double availableWidth,
        string state,
        bool expectedCompact)
    {
        RunSta(() =>
        {
            UserControl view = LoadExportStatusViewForLayout();
            ImageResultLayoutModel model = new(state);
            view.DataContext = model;

            view.Measure(new Size(availableWidth, double.PositiveInfinity));
            view.Arrange(new Rect(0, 0, availableWidth, view.DesiredSize.Height));
            view.UpdateLayout();

            Border card = Assert.IsType<Border>(view.FindName("ImageResultCard"));
            AdaptiveResultPanel layout = Assert.IsType<AdaptiveResultPanel>(view.FindName("ImageResultLayout"));
            StackPanel text = Assert.IsType<StackPanel>(view.FindName("ImageResultText"));
            MetricCardsPanel metrics = Assert.IsType<MetricCardsPanel>(view.FindName("ImageResultMetrics"));
            ImageExportActionsPanel actions = Assert.IsType<ImageExportActionsPanel>(view.FindName("ImageResultActions"));
            TextBlock title = Assert.IsType<TextBlock>(view.FindName("ImageResultTitleText"));
            TextBlock summary = Assert.IsType<TextBlock>(view.FindName("ImageResultSummaryText"));
            TextBlock warning = Assert.IsType<TextBlock>(view.FindName("ImageRenderWarningTextBlock"));
            Button details = Assert.IsType<Button>(view.FindName("ImageResultDetailsButton"));
            Assert.Null(view.FindName("ImageFormatSelector"));
            Assert.Empty(VisualDescendants(card).OfType<ComboBox>());
            Button export = Assert.IsType<Button>(view.FindName("ImageExportButton"));
            Button folder = Assert.IsType<Button>(view.FindName("OpenImageExportFolderButton"));
            Border[] metricCards =
            [
                Assert.IsType<Border>(view.FindName("ImageGridMetricCard")),
                Assert.IsType<Border>(view.FindName("ImageOutputMetricCard"))
            ];

            Assert.Equal(expectedCompact || !model.ShowImageRenderMetrics, layout.IsCompact);
            Assert.Equal(Visibility.Visible, card.Visibility);
            Assert.True(text.ActualWidth >= 120, $"Image text width at {availableWidth} in {state}: {text.ActualWidth}");
            Assert.True(title.ActualWidth >= 120);
            Assert.True(summary.ActualWidth > 0 && summary.ActualWidth <= text.ActualWidth);
            Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
            double titleLeft = BoundsWithin(title, text).Left;
            Assert.True(Math.Abs(BoundsWithin(summary, text).Left - titleLeft) <= 0.1,
                $"Image summary does not align with title at {availableWidth} in {state}.");
            if (model.ShowImageRenderWarning)
            {
                Assert.True(Math.Abs(BoundsWithin(warning, text).Left - titleLeft) <= 0.1,
                    $"Image warning does not align with title at {availableWidth} in {state}.");
            }
            Assert.Equal(model.ShowImageRenderWarning ? Visibility.Visible : Visibility.Collapsed, warning.Visibility);
            Assert.Equal(model.ShowImageResultErrorDetails ? Visibility.Visible : Visibility.Collapsed, details.Visibility);
            Assert.Equal(model.ShowImageRenderMetrics ? Visibility.Visible : Visibility.Collapsed, metrics.Visibility);
            Assert.Equal(model.ShowImageExportControls ? Visibility.Visible : Visibility.Collapsed, actions.Visibility);
            Assert.Equal(model.CanOpenExportFolder ? Visibility.Visible : Visibility.Collapsed, folder.Visibility);

            FrameworkElement[] visibleElements =
            [
                title, summary,
                .. model.ShowImageRenderWarning ? [warning] : Array.Empty<FrameworkElement>(),
                .. model.ShowImageResultErrorDetails ? [details] : Array.Empty<FrameworkElement>(),
                .. model.ShowImageRenderMetrics ? metricCards : Array.Empty<FrameworkElement>(),
                .. model.ShowImageExportControls ? [export] : Array.Empty<FrameworkElement>(),
                .. model.CanOpenExportFolder ? [folder] : Array.Empty<FrameworkElement>()
            ];
            foreach (FrameworkElement element in visibleElements)
            {
                Rect bounds = BoundsWithin(element, card);
                Assert.True(bounds.Left >= -0.1 && bounds.Top >= -0.1,
                    $"{element.Name} starts outside the image card: {bounds}");
                Assert.True(bounds.Right <= card.ActualWidth + 0.1 && bounds.Bottom <= card.ActualHeight + 0.1,
                    $"{element.Name} extends outside the image card: {bounds}; card={card.RenderSize}");
            }

            if (model.ShowImageRenderMetrics)
            {
                Assert.False(Overlaps(BoundsWithin(metricCards[0], card), BoundsWithin(metricCards[1], card)));
            }

            if (model.ShowImageExportControls)
            {
                Assert.True(export.ActualWidth >= 150);
                Assert.Equal(state != "stale", export.IsEnabled);
                Rect textBounds = BoundsWithin(text, layout);
                Rect metricsBounds = BoundsWithin(metrics, layout);
                Rect actionBounds = BoundsWithin(actions, layout);
                Assert.False(Overlaps(textBounds, actionBounds));
                Assert.False(Overlaps(metricsBounds, actionBounds));
                if (expectedCompact)
                {
                    Assert.True(textBounds.Bottom <= metricsBounds.Top + 0.1);
                }
                else if (layout.LayoutMode == StatusLayoutMode.Spacious)
                {
                    Assert.True(textBounds.Right < metricsBounds.Left && metricsBounds.Right < actionBounds.Left);
                    Assert.True(text.ActualWidth >= 180);
                    Assert.Equal(CenterY(textBounds), CenterY(metricsBounds), 4);
                    Assert.Equal(CenterY(metricsBounds), CenterY(actionBounds), 4);
                }
                else if (layout.LayoutMode == StatusLayoutMode.Intermediate)
                {
                    Assert.True(textBounds.Bottom <= metricsBounds.Top + 0.1);
                    Assert.True(Math.Max(textBounds.Right, metricsBounds.Right) <= actionBounds.Left + 0.1);
                }
                else
                {
                    Assert.True(textBounds.Bottom <= metricsBounds.Top + 0.1);
                    Assert.True(metricsBounds.Right <= actionBounds.Left + 0.1);
                }
            }

            Assert.DoesNotContain(VisualDescendants(view).OfType<ScrollViewer>(), scroll =>
                scroll.ComputedHorizontalScrollBarVisibility == Visibility.Visible);
        });
    }



    [Fact]
    public void ImageResultPanel_WideReadyStateRemainsCompact()
    {
        RunSta(() =>
        {
            Border compact = ArrangeImageResultCard(480, "ready");
            Border wide = ArrangeImageResultCard(1100, "ready");
            Assert.True(compact.ActualHeight - wide.ActualHeight >= 20,
                $"Compact={compact.ActualHeight}; wide={wide.ActualHeight}");
        });
    }

    [Fact]
    public void SharedViewContract_HasOnePrimaryPlayControl_OverlayOnlyReplay_AndAccessibleActions()
    {
        XDocument document = ReadView("VideoReviewSurface.xaml");
        XElement play = Assert.Single(document.Descendants(), e => e.Attribute("Command")?.Value == "{Binding TogglePlaybackCommand, ElementName=Surface}");
        Assert.Equal("{Binding PlaybackActionLabel, ElementName=Surface}", play.Attribute("AutomationProperties.Name")?.Value);
        XElement replay = Assert.Single(document.Descendants(), e => e.Attribute("Command")?.Value == "{Binding ReplayCommand, ElementName=Surface}");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        Assert.Equal("ReplayOverlay", replay.Parent?.Attribute(x + "Name")?.Value);
        Assert.Single(document.Descendants(), e => e.Attribute("Command")?.Value == "{Binding ToggleMuteCommand, ElementName=Surface}");
        Assert.Single(document.Descendants(), e => e.Attribute("SelectedItem")?.Value == "{Binding SelectedScaleMode, ElementName=Surface, Mode=TwoWay}");
        foreach (XElement control in document.Descendants().Where(e => e.Name.LocalName is "Button" or "Slider" or "ReviewSeekSlider" or "ComboBox"))
        {
            Assert.NotNull(control.Attribute("ToolTip"));
            Assert.NotNull(control.Attribute("AutomationProperties.Name"));
        }
        XDocument preview = ReadView("PreviewPanelView.xaml");
        Assert.DoesNotContain(preview.Descendants(), e => e.Name.LocalName == "Viewbox");
        Assert.Equal(2, preview.Descendants().Count(e => e.Name.LocalName == "VideoReviewSurface"));
        Assert.Single(preview.Descendants(), e => e.Name.LocalName == "VideoReviewSurface"
            && e.Attribute("PlaybackKind")?.Value == "Original");
        Assert.Single(document.Descendants(), e => e.Attribute("RotationDegrees")?.Value == "{Binding RotationDegrees, ElementName=Surface}");
        Assert.Contains(
            "OriginalVideoRotationDegrees",
            File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(ReadViewPath("VideoReviewSurface.xaml"))!,
                "VideoReviewSurface.xaml.cs")),
            StringComparison.Ordinal);
        XElement frame = Assert.Single(preview.Descendants(), e => e.Attribute("Visibility")?.Value.Contains("ShowVideoFrameControls", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(frame.Descendants(), e => e.Name.LocalName == "Button");
        Assert.Contains(frame.Descendants(), e => e.Attribute("Text")?.Value == "Frame position");
        XElement media = Assert.Single(preview.Descendants(), e => e.Name.LocalName == "ReviewViewport");
        Assert.Equal("{Binding SelectedFitMode}", media.Attribute("ScaleMode")?.Value);
        Assert.Equal("{Binding CurrentPreviewPixelWidth}", media.Attribute("PixelWidth")?.Value);
        Assert.Equal("{Binding CurrentImageFrame}", media.Attribute("ImageFrame")?.Value);
        Assert.Equal("{Binding ImageNavigation}", media.Attribute("ImageNavigation")?.Value);
        Assert.DoesNotContain(preview.Descendants(), e => e.Attributes().Any(a =>
            a.Value.StartsWith("{Binding ImageNavigation.", StringComparison.Ordinal)));
        Assert.DoesNotContain(preview.Descendants(), e => e.Name.LocalName == "ComboBox"
            && e.Attributes().Any(a => a.Value == "Image review scale"));
    }

    [Fact]
    public void RenderedReviewError_BindsGuidanceAndDetailsToSameIndependentContext()
    {
        XDocument preview = ReadView("PreviewPanelView.xaml");
        XElement review = Assert.Single(preview.Descendants(), e =>
            e.Attribute("Visibility")?.Value.Contains("ShowRenderedVideoReviewError", StringComparison.Ordinal) == true);
        Assert.Single(review.Descendants(), e => e.Attribute("Text")?.Value == "{Binding VideoReviewErrorPresentation.UserMessage}");
        Assert.DoesNotContain(review.Descendants(), e => e.Attribute("Text")?.Value == "{Binding StatusMessage}");
        Assert.Single(review.Descendants(), e => e.Attribute("Command")?.Value == "{Binding ShowVideoReviewErrorDetailsCommand}");
    }

    private static XDocument ReadView(string name)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GlyphFrame.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(directory.FullName, "src", "GlyphFrame.App", "Views", name));
    }

    private static string ReadViewPath(string name)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GlyphFrame.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "GlyphFrame.App", "Views", name);
    }

    internal static UserControl LoadExportStatusViewForLayout()
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
        XElement? localResources = root.Element(presentation + "UserControl.Resources");
        if (localResources is not null)
        {
            foreach (XElement local in localResources.Elements().ToArray()) { resources.Add(local); }
            localResources.Remove();
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

    private static Border ArrangeImageResultCard(double availableWidth, string state)
    {
        UserControl view = LoadExportStatusViewForLayout();
        view.DataContext = new ImageResultLayoutModel(state);

        view.Measure(new Size(availableWidth, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, availableWidth, view.DesiredSize.Height));
        view.UpdateLayout();
        return Assert.IsType<Border>(view.FindName("ImageResultCard"));
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
        private bool _playing;
        public bool IsPlaying { get => _playing; set { _playing = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaying))); } }
        private bool _muted;
        public bool IsMuted { get => _muted; set { _muted = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMuted))); } }
        private double _position;
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Writes { get; private set; }
        public double Position
        {
            get => _position;
            set { _position = value; Writes++; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position))); }
        }
    }

    private sealed class ProcessingLayoutModel(bool cancelling) : VideoResultLayoutModel(false)
    {
        public new bool IsExportStatusVisible => true;
        public new bool ShowVideoResultPanel => false;
        public string ExportOperationTitle => "Rendering Video";
        public string ExportProgressText => cancelling ? "Stopping the current render" : "Processing frames";
        public string ExportFrameText => "Frame 119 / 284";
        public string ElapsedText => "Elapsed 0:04";
        public string RemainingText => "Remaining about 0:06";
        public string ExportPercentageText => "42%";
        public double ExportProgress => 42;
        public long ExportPresentationGeneration => 1;
        public bool IsExportActive => true;
        public bool IsExportCompletionVisible => false;
        public bool IsExportProgressIndeterminate => cancelling;
        public GlyphFrame.Core.Enums.VideoExportStage ExportStage => cancelling ? GlyphFrame.Core.Enums.VideoExportStage.Cancelling : GlyphFrame.Core.Enums.VideoExportStage.ProcessingFrames;
        public ICommand CancelExportCommand { get; } = cancelling ? new DisabledCommand() : new EnabledCommand();
    }

    private class VideoResultLayoutModel(bool exported)
    {
        public bool ShowCopyExportPath => exported;
        public ExportPathCopyPresentation ExportPathCopy { get; } = LayoutCopy(exported ? @"C:\Дуже довга тека\відео-日本語-测试\готове символізоване відео.mp4" : null);
        private static readonly ICommand Command = new EnabledCommand();

        public bool ShowReadyToRender => false;
        public bool IsVideoSelected => true;
        public string ReadyToRenderMessage => "Review your settings, then render.";
        public string PlannedOutputDimensionsText => "1024 × 576";
        public string PlannedAudioText => "Keep audio";
        public bool AreExportAnimationsEnabled => false;
        public bool ShowVideoResultErrorDetails => false;
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
        public string VideoFileSizeText => "1.2 MB";
        public string VideoOutputDimensionsText => "1024 × 576";
        public string VideoAudioOutcomeText => "Original";
        public string VideoAudioOutcomeDescription => "Original audio preserved";
        public string VideoExportActionLabel => exported ? "Export Again" : "Export Video";
        public ICommand ExportVideoCommand => Command;
        public ICommand OpenExportFolderCommand => Command;
    }

    private sealed class ImageResultLayoutModel(string state)
    {
        public bool ShowCopyExportPath => state == "exported";
        public ExportPathCopyPresentation ExportPathCopy { get; } = LayoutCopy(state == "exported" ? @"C:\Дуже довга тека\зображення-日本語-测试.png" : null);
        private static readonly ICommand Enabled = new EnabledCommand();
        private static readonly ICommand Disabled = new DisabledCommand();

        public bool ShowReadyToRender => false;
        public bool IsVideoSelected => false;
        public string ReadyToRenderMessage => "Review your settings, then render.";
        public string PlannedOutputDimensionsText => "1448 × 1086";
        public string PlannedAudioText => "Keep audio";
        public bool AreExportAnimationsEnabled => false;
        public bool IsProcessingResultPanelVisible => true;
        public bool IsExportStatusVisible => false;
        public bool ShowImageResultPanel => true;
        public bool ShowVideoResultPanel => false;
        public bool IsImageResultSuccess => state is "ready" or "exported" or "fallback";
        public bool IsImageResultWarning => state is "stale" or "cancelled" or "initial-cancellation" or "export-cancellation";
        public bool IsImageResultFailure => state is "failed" or "initial-failure" or "export-failure";
        public bool ShowImageRenderMetrics => state is not ("initial-failure" or "initial-cancellation");
        public bool ShowImageExportControls => ShowImageRenderMetrics;
        public bool CanOpenExportFolder => state == "exported";
        public bool ShowImageRenderWarning => state == "fallback";
        public bool ShowImageResultErrorDetails => state is "failed" or "initial-failure" or "export-failure";
        public string ImageResultTitle => state switch
        {
            "stale" => "Render Stale",
            "exported" => "Image Exported",
            "failed" or "initial-failure" => "Image Render Failed",
            "cancelled" or "initial-cancellation" => "Image Rendering Cancelled",
            "export-failure" => "Image Export Failed",
            "export-cancellation" => "Image Export Cancelled",
            _ => "Image Ready"
        };
        public string ImageResultSummary => state switch
        {
            "stale" => "Render settings changed. Review the previous result or render again before exporting.",
            "exported" => "Ready for review and another export.",
            "failed" => "GlyphFrame could not render the image. Your previous rendered image is still available.",
            "initial-failure" => "GlyphFrame could not render this image. You can try again.",
            "cancelled" => "Image rendering was cancelled. Your previous rendered image is still available.",
            "initial-cancellation" => "Rendering was cancelled. The original image remains available.",
            "export-failure" => "GlyphFrame could not export the image. The rendered image remains ready to try again.",
            "export-cancellation" => "Image export was cancelled. The rendered image remains ready for another export.",
            _ => "Ready for review and export."
        };
        public string ImageRenderWarningText => "The selected font was unavailable; a fallback was used for this retained result.";
        public string ImageGridDimensionsText => "181 × 67";
        public string ImageOutputDimensionsText => "1448 × 1086";
        public string ImageExportActionLabel => state == "exported" ? "Export Again" : "Export Image";
        public ICommand ShowImageResultErrorDetailsCommand => Enabled;
        public ICommand ExportImageCommand => state == "stale" ? Disabled : Enabled;
        public ICommand OpenExportFolderCommand => Enabled;
    }

    private static ExportPathCopyPresentation LayoutCopy(string? path)
    {
        ExportPathCopyPresentation copy = new(new LayoutClipboard());
        copy.SetDestination(new object(), path);
        return copy;
    }

    private sealed class LayoutClipboard : IClipboardService
    {
        public ClipboardWriteResult SetText(string text) => ClipboardWriteResult.Completed();
    }

    private sealed class DisabledCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => false;
        public void Execute(object? parameter) { }
    }

    private sealed class EnabledCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
