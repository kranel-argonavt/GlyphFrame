using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.Views;
using GlyphFrame.Core.Enums;


namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class ReviewPaneLayoutTests
{
    private sealed class LayoutClipboard : IClipboardService
    {
        public bool Success { get; set; } = true;
        public string? Text { get; private set; }
        public ClipboardWriteResult SetText(string text)
        {
            Text = text;
            return Success ? ClipboardWriteResult.Completed() : ClipboardWriteResult.Failed();
        }
    }

    [Theory]
    [InlineData(1024, false)]
    [InlineData(1280, false)]
    [InlineData(1760, false)]
    [InlineData(1024, true)]
    [InlineData(1280, true)]
    [InlineData(1760, true)]
    public void CopyPath_ActualButtonFeedbackPreservesGeometryAndAccessibleAction(double width, bool video)
    {
        RunSta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow(realMediaComponents: true);
            TransitionData data = new(video, "Fit");
            window.Width = width; window.Height = 1080; window.Left = -10000;
            window.ShowActivated = false; window.DataContext = data;
            try
            {
                window.Show(); data.Set("Ready"); Settle(window);
                Button copy = Assert.Single(Descendants(window).OfType<Button>(), b => b.Name == (video ? "CopyVideoExportPathButton" : "CopyImageExportPathButton"));
                Assert.False(copy.IsVisible);
                data.Set("Exported"); Settle(window);
                ScrollViewer left = (ScrollViewer)window.FindName("LeftPaneScrollViewer");
                FrameworkElement pane = (FrameworkElement)left.Content;
                Border shell = Assert.Single(Descendants(pane).OfType<Border>(), b => b.Name == "StatusShell");
                FrameworkElement preview = (FrameworkElement)VisualTreeHelper.GetChild(pane, 0);
                AssertScenario(pane, preview, data);
                Rect shellBounds = Bounds(shell, pane), previewBounds = Bounds(preview, pane), copyBounds = Bounds(copy, pane);
                Assert.Equal("Copy export path", AutomationProperties.GetName(copy));
                Assert.NotNull(copy.FocusVisualStyle); Assert.True(copy.Focusable && copy.IsTabStop);
                foreach (bool success in new[] { true, false })
                {
                    data.Clipboard.Success = success;
                    Task feedback = data.ExportPathCopy.Command.ExecuteAsync(null);
                    Settle(window);
                    Assert.Equal(success ? "Copied!" : "Copy failed", copy.Content);
                    Assert.Equal(data.ExportPathCopy.Path, data.Clipboard.Text);
                    Assert.Contains(data.ExportPathCopy.Path, (string)copy.ToolTip);
                    AssertBounds(copyBounds, Bounds(copy, pane));
                    AssertBounds(shellBounds, Bounds(shell, pane));
                    AssertBounds(previewBounds, Bounds(preview, pane));
                    foreach (Button button in Descendants(shell).OfType<Button>().Where(b => b.IsVisible)) { AssertContained(button, shell); }
                    data.ExportPathCopy.ClearFeedback();
                    Settle(window);
                    // Cancellation schedules the async continuation; it need not
                    // have completed by the first dispatcher drain under load.
                    DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                    while (!feedback.IsCompleted && DateTime.UtcNow < deadline)
                    { Settle(window); Thread.Sleep(1); }
                    Assert.True(feedback.IsCompleted);
                    feedback.GetAwaiter().GetResult();
                    Assert.Equal("Copy path", copy.Content);
                }
            }
            finally { data.ExportPathCopy.Dispose(); window.Close(); }
        });
    }
    // Layout rounding may differ by one device pixel. These are DIP geometry
    // assertions on real components, not audiovisual or monitor-DPI verification.
    private const double Tolerance = 1;

    [Fact]
    public void ActualTabButtons_RepeatInvocationRetainsSelectionDrivenStyle()
    {
        RunSta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow(realMediaComponents: true);
            TransitionData data = new(true, "Fit");
            window.DataContext = data;
            window.Left = -10000;
            window.ShowActivated = false;
            try
            {
                window.Show();
                data.Set("Ready");
                Settle(window);
                foreach (string name in new[] { "OriginalPreviewTab", "SymbolizedPreviewTab" })
                {
                    Button selected = Assert.Single(Descendants(window).OfType<Button>(), button => button.Name == name);
                    IInvokeProvider invoke = (IInvokeProvider)new ButtonAutomationPeer(selected).GetPattern(PatternInterface.Invoke);
                    for (int i = 0; i < 4; i++)
                    {
                        invoke.Invoke();
                        Settle(window);
                        Assert.Same(selected.FindResource("AccentBrush"), selected.BorderBrush);
                        Assert.Same(selected.FindResource("AccentSubtleBrush"), selected.Background);
                        Assert.Equal(name == "OriginalPreviewTab", data.IsOriginalPreviewSelected);
                        Assert.Equal(name == "SymbolizedPreviewTab", data.IsSymbolizedPreviewSelected);
                    }
                }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void StatusTransition_OnlyContentChangesAnimateAndReducedMotionOrUnloadStopsClock()
    {
        RunSta(() =>
        {
            Border card = new() { Child = new TextBlock { Text = "Rendering" } };
            Window window = new() { Content = card, Width = 300, Height = 200, Left = -10000, ShowActivated = false };
            StatusContentTransition.SetContentKey(card, "Rendering");
            try
            {
                window.Show();
                Settle(window);
                Assert.False(card.HasAnimatedProperties); // Reduced motion / no preference opt-in.
                StatusContentTransition.SetAnimationsEnabled(card, true);
                StatusContentTransition.SetContentKey(card, "Ready");
                var animation = StatusContentTransition.ActiveAnimation(card);
                Assert.NotNull(animation);
                Assert.Equal(TimeSpan.FromMilliseconds(180), animation.Duration.TimeSpan);
                Assert.True(card.HasAnimatedProperties);
                StatusContentTransition.SetContentKey(card, "Ready");
                Assert.Same(animation, StatusContentTransition.ActiveAnimation(card));
                StatusContentTransition.SetAnimationsEnabled(card, false);
                Assert.False(card.HasAnimatedProperties);
                Assert.Equal(1, card.Opacity);
                StatusContentTransition.SetAnimationsEnabled(card, true);
                StatusContentTransition.SetContentKey(card, "Exported");
                Assert.True(card.HasAnimatedProperties);
                window.Content = null;
                Settle(window);
                Assert.False(card.HasAnimatedProperties);
                Assert.Null(StatusContentTransition.ActiveAnimation(card));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void NoMedia_DoesNotReserveOrDisplayPretendResult()
    {
        RunSta(() =>
        {
            ReviewPanePanel panel = new() { ViewportHeight = 600 };
            Border preview = new() { MinHeight = 360 };
            Border status = new() { Visibility = Visibility.Collapsed };
            panel.Children.Add(preview);
            panel.Children.Add(status);
            panel.Measure(new Size(700, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, 700, panel.DesiredSize.Height));
            Assert.Equal(600, preview.ActualHeight);
            Assert.Equal(600, panel.DesiredSize.Height);
        });
    }

    [Theory]
    [InlineData(1024, 720, false, "Fit")]
    [InlineData(1024, 720, true, "Fit")]
    [InlineData(1280, 820, false, "Fit")]
    [InlineData(1280, 820, true, "Fit")]
    [InlineData(1440, 1080, false, "100%")]
    [InlineData(1440, 1080, true, "Fill")]
    [InlineData(1760, 1080, false, "Fit")]
    [InlineData(1760, 1080, true, "Fit")]
    public void ActualMediaComponents_AllocateMeasuredStatusAndReturnSpaceToPreview(double width, double height, bool video, string scale)
    {
        RunSta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow(realMediaComponents: true);
            window.Width = width; window.Height = height; window.Left = -10000; window.ShowActivated = false;
            TransitionData data = new(video, scale); window.DataContext = data;
            try
            {
                window.Show(); Settle(window);
                ScrollViewer left = (ScrollViewer)window.FindName("LeftPaneScrollViewer");
                ReviewPanePanel pane = Assert.IsType<ReviewPanePanel>(left.Content);
                FrameworkElement preview = (FrameworkElement)VisualTreeHelper.GetChild(pane, 0);
                Border shell = Assert.Single(Descendants(pane).OfType<Border>(), b => b.Name == "StatusShell");
                Dictionary<string, Size> sizes = [];
                double plannedHeight = shell.ActualHeight;
                foreach (double hostWidth in new[] { width, 1024, 1280, width })
                {
                    window.Width = hostWidth; Settle(window);
                    foreach (string state in new[] { "Loaded", "Rendering", "Completing", "Ready", "Original", "Ready", "Exporting", "Exported", "Original", "Exported", "Stale", "Failed", "Cancelled", "Unavailable", "Ready" })
                    {
                        data.Set(state); Settle(window);
                        AssertScenario(pane, preview, data); AssertFilenameGeometry(preview, data);
                        Border active = Assert.Single(Descendants(shell).OfType<Border>(), b => b.IsVisible && b.Name is "ReadyToRenderCard" or "ProcessingContent" or "ImageResultCard" or "VideoResultCard");
                        AssertClose(active.DesiredSize.Height + 34, shell.ActualHeight);
                        Assert.Equal(new Thickness(16), shell.Padding);
                        AssertClose(Math.Max(preview.MinHeight, left.ViewportHeight - shell.ActualHeight - ReviewPanePanel.CardGap), preview.ActualHeight);
                        AssertClose(Bounds(preview, pane).Bottom + ReviewPanePanel.CardGap, Bounds(shell, pane).Top);
                        AssertClose(Math.Max(left.ViewportHeight, preview.MinHeight + shell.ActualHeight + ReviewPanePanel.CardGap), pane.ActualHeight);
                        Assert.Empty(Descendants(shell).OfType<ScrollViewer>());
                        AdaptiveResultPanel composition = Assert.IsType<AdaptiveResultPanel>(active.Child);
                        FrameworkElement? actionGroup = composition.Children.Cast<FrameworkElement>().FirstOrDefault(e => e.IsVisible && AdaptiveResultPanel.GetRole(e) == AdaptiveResultPanelRole.Actions);
                        if (actionGroup is not null && (composition.Family == StatusLayoutFamily.Working && composition.LayoutMode == StatusLayoutMode.CompactWide
                            || actionGroup is ExportActionsPanel exports && exports.HasSecondaryActions && composition.LayoutMode != StatusLayoutMode.Narrow))
                        {
                            Rect body = Rect.Empty;
                            foreach (FrameworkElement group in composition.Children.Cast<FrameworkElement>().Where(e => e.IsVisible && !ReferenceEquals(e, actionGroup)))
                            { body.Union(Bounds(group, composition)); }
                            Rect actionBounds = Bounds(actionGroup, composition);
                            // One physical pixel permits independent device rounding of
                            // the two centers; unrounded component tests use 0.11 DIP.
                            double pixel = 1 / VisualTreeHelper.GetDpi(composition).DpiScaleY;
                            Assert.InRange(Math.Abs(body.Top + body.Height / 2 - actionBounds.Top - actionBounds.Height / 2), 0, pixel);
                        }
                        CaptureEvidence(window, shell, active, video, state);
                        Assert.Equal(0, left.ScrollableWidth);
                        foreach (FrameworkElement control in Descendants(shell).OfType<FrameworkElement>().Where(e => e.IsVisible && e is Button or ComboBox or TextBlock)) { AssertContained(control, shell); }
                        foreach (VideoReviewSurface surface in Descendants(preview).OfType<VideoReviewSurface>().Where(v => v.IsVisible))
                        {
                            AssertContained(surface, preview); Assert.True(surface.ActualHeight > 100);
                            ReviewViewport viewport = Assert.Single(Descendants(surface).OfType<ReviewViewport>());
                            Assert.Equal(scale, viewport.ScaleMode); AssertContained(viewport, surface);
                        }
                        if (state == "Ready")
                        {
                            string key = $"{left.ViewportWidth:F1}";
                            Size geometry = new(shell.ActualWidth, shell.ActualHeight);
                            if (sizes.TryGetValue(key, out Size previous)) { AssertClose(previous.Height, geometry.Height); }
                            sizes[key] = geometry;
                        }
                        if (left.ScrollableHeight > 0)
                        {
                            left.ScrollToEnd(); Settle(window);
                            foreach (Button button in Descendants(shell).OfType<Button>().Where(b => b.IsVisible)) { AssertContained(button, left); }
                        }
                    }
                }
                data.Set("Exported"); Settle(window);
                Assert.True(plannedHeight < shell.ActualHeight, $"Planned {plannedHeight}; exported {shell.ActualHeight}");
                window.Height = 720; window.Width = 1024; data.Set("Unavailable"); Settle(window);
                left.ScrollToEnd(); Settle(window);
                foreach (Button button in Descendants(shell).OfType<Button>().Where(b => b.IsVisible)) { AssertContained(button, left); }
            }
            finally { data.ExportPathCopy.Dispose(); window.Close(); }
        });
    }
    [Fact]
    public void RetainedCanvas_DoesNotContributeIntrinsicViewportSizeToParent()
    {
        RunSta(() =>
        {
            ReviewViewport viewport = new() { PixelWidth = 4096, PixelHeight = 2160, ScaleMode = "100%" };
            viewport.Measure(new Size(600, 400));
            viewport.Arrange(new Rect(0, 0, 600, 400));
            viewport.UpdateLayout();
            viewport.InvalidateMeasure();
            viewport.Measure(new Size(600, double.PositiveInfinity));
            Assert.Equal(0, viewport.DesiredSize.Height);
            Assert.Equal(0, viewport.DesiredSize.Width);
        });
    }

    private static void Settle(Window window)
    {
        // Drain binding/layout callbacks without sleeps or a decoder dependency.
        for (int i = 0; i < 3; i++)
        {
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }
    }

    private static void AssertScenario(FrameworkElement content, FrameworkElement preview, TransitionData data)
    {
        DependencyObject[] all = Descendants(content).ToArray();
        ReviewViewport image = Assert.Single(Descendants(preview).OfType<ReviewViewport>(),
            viewport => BindingOperations.GetBinding(viewport, ReviewViewport.SourceProperty)?.Path.Path == nameof(data.CurrentPreviewImageSource));
        Assert.Equal(data.ShowImagePreview, image.IsVisible);
        if (data.ShowImagePreview) { Assert.Same(data.CurrentPreviewImageSource, image.Source); Assert.NotNull(image.Source); }
        foreach (VideoReviewSurface surface in all.OfType<VideoReviewSurface>())
        {
            bool original = surface.PlaybackKind == VideoPlaybackSurfaceKind.Original;
            Assert.Equal(original ? data.ShowOriginalVideoPlayer : data.ShowRenderedVideoPlayer, surface.IsVisible);
            Assert.Same(original ? data.OriginalVideoPlaybackSource : data.RenderedVideoPlaybackSource, surface.PlaybackSource);
            if (surface.IsVisible)
            {
                Assert.NotNull(surface.PlaybackSource);
                Assert.Same(surface.PlaybackSource, Assert.Single(Descendants(surface).OfType<ReviewViewport>()).Source);
                Assert.Same(original ? data.ToggleOriginalVideoPlaybackCommand : data.ToggleRenderedVideoPlaybackCommand, surface.TogglePlaybackCommand);
            }
        }

        foreach (Button folder in all.OfType<Button>().Where(button => button.Name is "OpenVideoExportFolderButton" or "OpenImageExportFolderButton"))
        {
            bool relevant = folder.Name == (data.IsVideoSelected ? "OpenVideoExportFolderButton" : "OpenImageExportFolderButton");
            Assert.Equal(relevant && data.CanOpenExportFolder && !data.IsExportStatusVisible, folder.IsVisible);
            Assert.Equal(data.CanOpenExportFolder, folder.Command!.CanExecute(null));
        }
        foreach (Button copy in all.OfType<Button>().Where(button => button.Name is "CopyVideoExportPathButton" or "CopyImageExportPathButton"))
        {
            bool relevant = copy.Name == (data.IsVideoSelected ? "CopyVideoExportPathButton" : "CopyImageExportPathButton");
            Assert.Equal(relevant && data.ShowCopyExportPath && !data.IsExportStatusVisible, copy.IsVisible);
            Assert.Same(data.ExportPathCopy.Command, copy.Command);
            Assert.Equal(data.ExportPathCopy.ToolTip, copy.ToolTip);
            if (copy.IsVisible) { Assert.True(copy.IsEnabled); AssertContained(copy, content); }
        }

        Border reviewError = Assert.Single(all.OfType<Border>(), border => BindingOperations.GetBinding(border, UIElement.VisibilityProperty)?.Path.Path == nameof(data.ShowRenderedVideoReviewError));
        Assert.Equal(data.ShowRenderedVideoReviewError, reviewError.IsVisible);
        TextBlock guidance = Assert.Single(Descendants(reviewError).OfType<TextBlock>(), text => BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path == "VideoReviewErrorPresentation.UserMessage");
        Button details = Assert.Single(Descendants(reviewError).OfType<Button>());
        Assert.Equal(data.ShowVideoReviewErrorDetails, details.IsVisible);
        Assert.Same(data.ShowVideoReviewErrorDetailsCommand, details.Command);
        Assert.Equal(data.ShowVideoReviewErrorDetails, details.IsEnabled);
        if (data.ShowRenderedVideoReviewError)
        {
            Assert.NotNull(data.VideoReviewErrorPresentation);
            Assert.Equal(ErrorPresentationContext.SymbolizedPlayback, data.VideoReviewErrorPresentation.Context);
            Assert.Equal(data.VideoReviewErrorPresentation.UserMessage, guidance.Text);
            Assert.NotEmpty(data.VideoReviewErrorPresentation.TechnicalDetail!);
            Assert.False(data.IsVideoResultSuccess);
        }
        Assert.Equal(data.ShowVideoResultPanel && !data.IsExportStatusVisible, Assert.Single(all.OfType<Border>(), element => element.Name == "VideoResultCard").IsVisible);
        Assert.Equal(data.ShowImageResultPanel && !data.IsExportStatusVisible, Assert.Single(all.OfType<Border>(), element => element.Name == "ImageResultCard").IsVisible);
        Border ready = Assert.Single(all.OfType<Border>(), element => element.Name == "ReadyToRenderCard");
        Assert.Equal(data.ShowReadyToRender, ready.IsVisible);
        if (ready.IsVisible)
        {
            Assert.Contains(Descendants(ready).OfType<TextBlock>(), text => text.Text == "Ready to render");
            Assert.Empty(Descendants(ready).OfType<Button>());
            Assert.False(data.ShowImageRenderMetrics || data.ShowVideoRenderMetrics);
            Assert.Contains(Descendants(ready).OfType<TextBlock>(), text => text.Text == data.PlannedOutputDimensionsText);
            Assert.Equal(data.IsVideoSelected, Descendants(ready).OfType<TextBlock>().Any(text => text.IsVisible && text.Text == data.PlannedAudioText));
            Assert.Empty(Descendants(ready).OfType<ExportActivityIndicator>());
        }
        ExportActivityIndicator[] indicators = all.OfType<ExportActivityIndicator>().Where(indicator => indicator.IsVisible).ToArray();
        Assert.Equal(data.IsExportActive ? 2 : data.IsExportCompletionVisible ? 1 : 0, indicators.Length);
        if (data.IsExportCompletionVisible) { Assert.True(Assert.Single(indicators).IsCompleted); }
        if (data.IsExportActive)
        {
            Assert.All(indicators, indicator => Assert.True(indicator.IsActive));
            Assert.Equal(data.ExportPercentageText, Assert.Single(indicators, indicator => indicator.ShowPercentageText).PercentageText);
            Assert.Same(data.CancelExportCommand, Assert.Single(all.OfType<Button>(), button => button.IsVisible && button.Content as string == "Cancel").Command);
        }

        // Inspect actual bindings on the media/result subtree, including hidden
        // controls and the role bindings installed by VideoReviewSurface. Missing
        // fake properties must not silently leave default-visible/empty controls.
        foreach (DependencyObject element in all.Append(content))
        {
            LocalValueEnumerator values = element.GetLocalValueEnumerator();
            while (values.MoveNext())
            {
                BindingExpression? expression = BindingOperations.GetBindingExpression(element, values.Current.Property);
                if (expression?.DataItem != data) { continue; }
                string path = expression.ParentBinding.Path.Path;
                object? owner = data;
                foreach (string member in path.Split('.'))
                {
                    if (owner is null) { break; } // Optional, cleared error presentation.
                    System.Reflection.PropertyInfo? property = owner.GetType().GetProperty(member);
                    Assert.True(property is not null, $"Missing fixture property {path} on {element.GetType().Name}.{values.Current.Property.Name}");
                    owner = property.GetValue(owner);
                }
                Assert.False(expression.HasError, $"Binding {path} failed on {element.GetType().Name}.{values.Current.Property.Name}: {expression.Status}");
            }
        }
    }

    private static void AssertFilenameGeometry(FrameworkElement preview, TransitionData data)
    {
        TextBlock filename = Assert.Single(Descendants(preview).OfType<TextBlock>(), text => BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path == nameof(data.SelectedFileName) && BindingOperations.GetBinding(text, FrameworkElement.ToolTipProperty)?.Path.Path == nameof(data.SelectedFileName));
        Panel row = Assert.IsAssignableFrom<Panel>(filename.Parent);
        System.Windows.Shapes.Path icon = Assert.Single(row.Children.OfType<System.Windows.Shapes.Path>());
        Assert.DoesNotContain(Descendants(preview).OfType<TextBlock>(), text =>
            BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path == nameof(data.MediaTypeText));
        FrameworkElement metadata = (FrameworkElement)row.Parent;
        Grid information = Assert.IsType<Grid>(metadata.Parent);
        FrameworkElement actions = Assert.Single(information.Children.Cast<FrameworkElement>(), element => Grid.GetColumn(element) == 1);
        Assert.Equal(data.SelectedFileName, filename.Text);
        Assert.Equal(data.SelectedFileName, filename.ToolTip);
        Assert.True(filename.ActualWidth > 40);
        Assert.True(new FormattedText(filename.Text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(filename.FontFamily, filename.FontStyle, filename.FontWeight, filename.FontStretch), filename.FontSize,
            Brushes.White, VisualTreeHelper.GetDpi(filename).PixelsPerDip).Width > filename.ActualWidth);
        foreach (FrameworkElement text in new FrameworkElement[] { filename, icon })
        {
            Assert.True(text.IsVisible && text.ActualWidth > 0);
            AssertContained(text, metadata);
            Assert.False(Bounds(text, information).IntersectsWith(Bounds(actions, information)), $"{text.GetType().Name} overlaps media actions.");
        }
        FrameworkElement[] parts = [icon, filename];
        for (int i = 0; i < parts.Length - 1; i++)
        {
            Assert.True(Bounds(parts[i], row).Right <= Bounds(parts[i + 1], row).Left + Tolerance);
        }
        AssertContained(metadata, information);
        AssertContained(actions, information);
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static void CaptureEvidence(Window window, Border shell, Border active, bool video, string state)
    {
        string? directory = Environment.GetEnvironmentVariable("GLYPHFRAME_STATUS_VIEWPORT_CAPTURES");
        if (directory is null) { return; }
        Directory.CreateDirectory(directory);
        string id = $"wpf-viewport-{(video ? "video" : "image")}-{state}-{window.ActualWidth:F0}x{window.ActualHeight:F0}";
        string path = Path.Combine(directory, id);
        if (File.Exists(path + ".png")) { return; }
        FrameworkElement content = (FrameworkElement)window.Content;
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        DrawingVisual backdrop = new();
        using (DrawingContext drawing = backdrop.RenderOpen())
        {
            Rect area = new(0, 0, content.ActualWidth, content.ActualHeight);
            drawing.DrawRectangle((Brush)window.FindResource("MainBackgroundBrush"), null, area);
            drawing.DrawRectangle(new VisualBrush(content), null, area);
        }
        bitmap.Render(backdrop);
        PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(path + ".png")) { encoder.Save(stream); }
        Rect bounds = Bounds(active, shell);
        File.WriteAllText(path + ".json", JsonSerializer.Serialize(new
        {
            Kind = "WPF fixed viewport fixture; test data, production XAML and controls; not native application acceptance",
            Window = new { window.ActualWidth, window.ActualHeight },
            Panel = new { shell.ActualWidth, shell.ActualHeight },
            Composition = ((AdaptiveResultPanel)active.Child).LayoutMode.ToString(),
            Dpi = VisualTreeHelper.GetDpi(window).DpiScaleX,
            InsetsFromOuterBorder = new { Left = bounds.Left, Top = bounds.Top, Right = shell.ActualWidth - bounds.Right, Bottom = shell.ActualHeight - bounds.Bottom }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void AssertClose(double expected, double actual) => Assert.InRange(actual, expected - Tolerance, expected + Tolerance);
    private static void AssertBounds(Rect expected, Rect actual)
    {
        AssertClose(expected.X, actual.X); AssertClose(expected.Y, actual.Y);
        AssertClose(expected.Width, actual.Width); AssertClose(expected.Height, actual.Height);
    }
    private static void AssertContained(FrameworkElement child, FrameworkElement parent)
    {
        Rect bounds = Bounds(child, parent);
        Assert.True(bounds.Left >= -Tolerance && bounds.Right <= parent.ActualWidth + Tolerance, $"{child.Name}: {bounds}");
        Assert.True(bounds.Top >= -Tolerance && bounds.Bottom <= parent.ActualHeight + Tolerance, $"{child.Name}: {bounds}");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) { yield return descendant; }
        }
    }
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Review layout did not settle.");
        failure?.Throw();
    }

    private sealed class TransitionData : Phase13UiTests.MainLayoutData, INotifyPropertyChanged
    {
        public GlyphFrame.App.Presentation.ImageNavigationState ImageNavigation { get; } = new();
        public GlyphFrame.App.Presentation.ImagePreviewFrame? CurrentImageFrame => !video && CurrentPreviewImageSource is { } source
            ? new(source, CurrentPreviewPixelWidth, CurrentPreviewPixelHeight) : null;
        private readonly bool video;
        public TransitionData(bool isVideo, string scale) : base(isVideo)
        {
            ExportPathCopy = new(Clipboard);
            video = isVideo;
            SelectedFitMode = scale;
            RenderImageCommand = new RelayCommand(() => { }, () => !video && !IsExportActive);
            RenderVideoCommand = new RelayCommand(() => { }, () => video && !IsExportActive);
            PreviewFrameCommand = new RelayCommand(() => { }, () => video && !HasVideoArtifact && !IsExportActive);
            ToggleOriginalVideoPlaybackCommand = new RelayCommand(() => { }, () => ShowOriginalVideoPlayer && !IsExportActive);
            ToggleRenderedVideoPlaybackCommand = new RelayCommand(() => { }, () => ShowRenderedVideoPlayer && !IsExportActive);
            OriginalFullscreenCommand = new RelayCommand(() => { }, () => ShowOriginalVideoPlayer && !IsExportActive);
            FullscreenCommand = new RelayCommand(() => { }, () => ShowRenderedVideoPlayer && !IsExportActive);
            SelectOriginalCommand = new RelayCommand(() => { if (!IsOriginalPreviewSelected) { Set("Original"); } });
            SelectSymbolizedCommand = new RelayCommand(() => { if (!IsSymbolizedPreviewSelected) { Set("Ready"); } });
        }
        private string _state = "Loaded";
        private bool _hasResult;
        private readonly ErrorPresentation _reviewError = ErrorPresentation.Create(
            ApplicationErrorCode.SymbolizedPlaybackUnavailable, ErrorPresentationContext.SymbolizedPlayback,
            "Deterministic retained-review failure; no media or codec dependency.");
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Set(string state)
        {
            _state = state;
            ExportPathCopy.SetDestination(this, ShowCopyExportPath
                ? video ? VideoExportDestinationText : @"C:\Медіа\very long Unicode destination\зображення-日本語.png" : null);
            if (state is "Ready" or "Completing") { _hasResult = true; }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            foreach (IRelayCommand command in new[] { ExportImageCommand, ExportVideoCommand, OpenExportFolderCommand, ShowVideoReviewErrorDetailsCommand,
                RenderImageCommand, RenderVideoCommand, PreviewFrameCommand, ToggleOriginalVideoPlaybackCommand,
                ToggleRenderedVideoPlaybackCommand, OriginalFullscreenCommand, FullscreenCommand })
            {
                command.NotifyCanExecuteChanged();
            }
        }
        private bool Result => _hasResult;
        public new bool IsProcessingResultPanelVisible => true;
        public new bool ShowReadyToRender => !IsExportActive && !ShowImageResultPanel && !ShowVideoResultPanel;
        public new string ReadyToRenderMessage => "Review your settings, then render.";
        public new string PlannedOutputDimensionsText { get; private set; } = "1280 × 720";
        public new string PlannedAudioText { get; private set; } = "Keep audio";
        public void ChangePlannedSettings(string target, string audio)
        {
            PlannedOutputDimensionsText = target;
            PlannedAudioText = audio;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
        public new bool IsExportStatusVisible => IsExportActive || IsExportCompletionVisible;
        public bool IsExportActive => _state is "Rendering" or "Exporting";
        public new bool ShowImageResultPanel => !video && Result && !IsExportActive;
        public new bool ShowVideoResultPanel => video && Result && !IsExportActive;
        public new bool HasVideoArtifact => video && Result;
        public bool ShowOriginalVideoPlayer => video && _state is "Original" or "Loaded" or "Rendering";
        public bool ShowRenderedVideoPlayer => video && !ShowOriginalVideoPlayer && Result && _state != "Unavailable";
        public bool ShowImagePreview => !video;
        public new string SelectedFitMode { get; set; }
        public ImageSource CurrentPreviewImageSource { get; } = new DrawingImage(new GeometryDrawing(Brushes.Purple, null, new RectangleGeometry(new Rect(0, 0, 1280, 720))));
        public ImageSource OriginalVideoPlaybackSource { get; } = new DrawingImage(new GeometryDrawing(Brushes.SlateBlue, null, new RectangleGeometry(new Rect(0, 0, 1920, 1080))));
        public ImageSource? RenderedVideoPlaybackSource => _state == "Unavailable" ? null : CurrentPreviewImageSource;
        public double CurrentPreviewPixelWidth => Result ? 640 : 1280;
        public double CurrentPreviewPixelHeight => Result ? 360 : 720;
        public double OriginalVideoPixelWidth => 1920;
        public double OriginalVideoPixelHeight => 1080;
        public double RenderedVideoPixelWidth => 1280;
        public double RenderedVideoPixelHeight => 720;
        public new string VideoOutputDimensionsText => "1280 × 720";
        public new string SelectedFileName => $"Український оригінал — Кириллица 漢字 — {new string('Ж', 80)} — a substantially long original media filename.{(video ? "mp4" : "png")}";
        public new string InputDisplayName => SelectedFileName;
        public new string SelectedFilePath => @"C:\Медіа\" + SelectedFileName;
        public new string InputCardSummary => MetadataPrimaryText;
        public bool HasVideoExportDestination => video && _state == "Exported";
        public new bool CanOpenExportFolder => _state == "Exported" && !IsExportActive;
        public new bool ShowCopyExportPath => _state == "Exported" && !IsExportStatusVisible;
        public LayoutClipboard Clipboard { get; } = new();
        public new ExportPathCopyPresentation ExportPathCopy { get; }
        public string VideoExportDestinationText => HasVideoExportDestination ? @"C:\Медіа\very long destination folder\another long folder\rendered output відео з довгим ім’ям.mp4" : string.Empty;
        public new string ImageResultSummary => _state switch
        {
            "Failed" => "The replacement could not be rendered. The previously accepted image remains available for review and another attempt.",
            "Exported" => "Ready for review and another export.",
            "Stale" => "Settings changed. Render Again before exporting the retained image.",
            "Cancelled" => "Rendering was cancelled. The previous image remains available.",
            _ => "Ready for review and export."
        };
        public new string VideoResultSummary => _state switch
        {
            "Failed" => "The replacement could not be rendered. The previously accepted video remains available for review and another attempt.",
            "Unavailable" => _reviewError.UserMessage,
            "Stale" => "Settings changed. Render Again before exporting the retained video.",
            "Cancelled" => "Rendering was cancelled. The previous video remains available.",
            "Exported" => "Ready for review and another export.",
            _ => "Ready for review and export."
        };
        public new string VideoResultTitle => _state switch
        {
            "Exported" => "Video Exported",
            "Failed" => "Video Render Failed",
            "Cancelled" => "Video Rendering Cancelled",
            "Stale" => "Render Stale",
            "Unavailable" => "Video Review Unavailable",
            _ => "Video Ready"
        };
        public new string ImageResultTitle => _state switch
        {
            "Exported" => "Image Exported",
            "Failed" => "Image Render Failed",
            "Cancelled" => "Image Rendering Cancelled",
            "Stale" => "Render Stale",
            _ => "Image Ready"
        };
        public new bool ShowImageRenderWarning => !video && _state == "Failed";
        public string ImageRenderWarningText => "The selected font was unavailable; a supported fallback font was used for this retained result.";
        public new bool ShowVideoResultErrorDetails => video && _state == "Unavailable";
        public ErrorPresentation? VideoReviewErrorPresentation => ShowRenderedVideoReviewError ? _reviewError : null;
        public bool ShowRenderedVideoReviewError => video && _state == "Unavailable";
        public bool ShowVideoReviewErrorDetails => ShowRenderedVideoReviewError;
        public IRelayCommand ShowVideoReviewErrorDetailsCommand => _reviewDetailsCommand ??= new RelayCommand(() => { }, () => ShowVideoReviewErrorDetails);
        public IRelayCommand ShowVideoResultErrorDetailsCommand => ShowVideoReviewErrorDetailsCommand;
        private IRelayCommand? _reviewDetailsCommand;
        public new bool IsImageResultSuccess => !video && Result && _state is "Ready" or "Exported" or "Original";
        public new bool IsVideoResultSuccess => video && Result && _state is "Ready" or "Exported" or "Original";
        public bool IsImageResultWarning => !video && _state is "Stale" or "Cancelled";
        public bool IsVideoResultWarning => video && _state is "Stale" or "Cancelled";
        public bool IsImageResultFailure => !video && _state == "Failed";
        public bool IsVideoResultFailure => video && _state is "Failed" or "Unavailable";
        public new bool ShowVideoRenderMetrics => HasVideoArtifact;
        public new bool ShowPreviewFrameAction => video && !HasVideoArtifact;
        public new bool HasImageResultForRerender => !video && Result;
        public new bool ShowImageRenderMetrics => !video && Result;
        public new bool ShowImageExportControls => !video && Result;
        public new string ImageRenderActionLabel => Result ? "Render Again" : "Render Image";
        public new string VideoRenderActionLabel => Result ? "Render Again" : "Render Video";
        public new string VideoExportActionLabel => _state == "Exported" ? "Export Again" : "Export Video";
        public new string ImageExportActionLabel => _state == "Exported" ? "Export Again" : "Export Image";
        public string ExportOperationTitle => _state == "Exporting" ? video ? "Exporting Video" : "Exporting Image" : video ? "Rendering Video" : "Rendering Image";
        public string ExportProgressText => _state == "Exporting" ? "Copying the reviewed video" : "Processing frames";
        public string ExportPercentageText => "50%";
        public double ExportProgress => 50;
        public VideoExportStage ExportStage => _state == "Exporting" ? VideoExportStage.CopyingArtifact : VideoExportStage.ProcessingFrames;
        public string ExportFrameText => "6 / 12 frames";
        public string ElapsedText => "Elapsed 0:01";
        public string RemainingText => "Remaining 0:01";
        public long ExportPresentationGeneration => _state == "Exporting" ? 2 : 1;
        public bool IsExportCompletionVisible => _state == "Completing";
        public bool IsExportProgressIndeterminate => false;
        public new bool AreExportAnimationsEnabled => false;
        private IRelayCommand? _exportImageCommand, _exportVideoCommand, _openFolderCommand;
        public IRelayCommand ExportImageCommand => _exportImageCommand ??= new RelayCommand(() => { }, () => !video && Result && !IsExportActive && _state != "Stale");
        public IRelayCommand ExportVideoCommand => _exportVideoCommand ??= new RelayCommand(() => { }, () => video && Result && !IsExportActive && _state != "Stale");
        public IRelayCommand OpenExportFolderCommand => _openFolderCommand ??= new RelayCommand(() => { }, () => CanOpenExportFolder);
        public IRelayCommand CancelExportCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand RenderImageCommand { get; }
        public IRelayCommand RenderVideoCommand { get; }
        public IRelayCommand PreviewFrameCommand { get; }
        public IRelayCommand ToggleOriginalVideoPlaybackCommand { get; }
        public IRelayCommand ToggleRenderedVideoPlaybackCommand { get; }
        public IRelayCommand OriginalFullscreenCommand { get; }
        public IRelayCommand FullscreenCommand { get; }
        public bool IsOriginalPreviewSelected => _state is "Loaded" or "Rendering" or "Original";
        public bool IsSymbolizedPreviewSelected => !IsOriginalPreviewSelected;
        public bool ShowOriginalPlaybackOpening => false;
        public bool ShowOriginalPlaybackFallback => false;
        public bool ShowOriginalPlaybackErrorDetails => false;
        public bool ShowPreviewStatusBadge => _state == "Stale";
        public string PreviewStaleMessage => "Settings changed. Render Again to update the result.";
        public bool ShowEmptyPreview => false;
        public bool ShowLoadingPreview => false;
        public bool ShowErrorPreview => false;
        public bool ShowMediaPlaceholder => false;
        public bool ShowVideoFrameLoading => false;
        public bool ShowVideoFrameError => false;
        public bool ShowVideoFrameErrorDetails => false;
        public bool ShowVideoFrameControls => false;
        public bool ShowResolutionWarning => false;
        public bool ShowMediaInputErrorDetails => false;
        public new bool ShowImageResultErrorDetails => false;
        public bool IsVideoSeekEnabled => false;
        public double TimelinePosition { get; set; }
        public string TimelineText => "0:02 / 0:09";
        public string VideoFrameStatusMessage => string.Empty;
        public new string InputMetadataSummary => MetadataPrimaryText;
        public string OriginalVideoPlaybackLimitationMessage => string.Empty;
        public IRelayCommand ShowOriginalPlaybackErrorDetailsCommand { get; } = new RelayCommand(() => { }, () => false);
        public IRelayCommand ShowVideoFrameErrorDetailsCommand { get; } = new RelayCommand(() => { }, () => false);
        public IRelayCommand ShowImageResultErrorDetailsCommand { get; } = new RelayCommand(() => { }, () => false);
        public IRelayCommand SelectOriginalCommand { get; }
        public IRelayCommand SelectSymbolizedCommand { get; }
        public IRelayCommand BrowseMediaCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand DropMediaCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand RestartOriginalVideoCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand RestartRenderedVideoCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand ToggleOriginalVideoMuteCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand ToggleRenderedVideoMuteCommand { get; } = new RelayCommand(() => { });
        public double OriginalVideoRotationDegrees => 0;
        public string OriginalVideoPlaybackActionLabel => "Play Original video";
        public string RenderedVideoPlaybackActionLabel => "Play rendered video";
        public string OriginalVideoMuteActionLabel => "Mute Original video";
        public string RenderedVideoMuteActionLabel => "Mute rendered video";
        public bool IsOriginalVideoPlaying => false;
        public bool IsRenderedVideoPlaying => false;
        public bool IsOriginalVideoMuted => false;
        public bool IsRenderedVideoMuted => false;
        public bool ShowOriginalVideoReplay => false;
        public bool ShowRenderedVideoReplay => false;
        public string OriginalVideoCurrentTimeText => "0:02";
        public string RenderedVideoCurrentTimeText => "0:02";
        public string OriginalVideoTotalTimeText => "0:09";
        public string RenderedVideoTotalTimeText => "0:09";
        public double OriginalVideoPositionSeconds { get; set; } = 2;
        public double RenderedVideoPositionSeconds { get; set; } = 2;
        public double OriginalVideoDurationSeconds => 9;
        public double RenderedVideoDurationSeconds => 9;
        public double OriginalVideoVolume { get; set; } = 0.4;
        public double RenderedVideoVolume { get; set; } = 0.7;
    }
}
