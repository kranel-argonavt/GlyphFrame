using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Tests;

public sealed class ReviewPaneLayoutTests
{
    // Layout rounding may differ by one device pixel. These are DIP geometry
    // assertions on real components, not audiovisual or monitor-DPI verification.
    private const double Tolerance = 1;

    [Theory]
    [InlineData(1024, 720, true, "Fit")]
    [InlineData(1280, 820, true, "Fit")]
    [InlineData(1440, 1080, true, "Fit")]
    [InlineData(1024, 720, false, "Fit")]
    [InlineData(1280, 820, false, "Fit")]
    [InlineData(1440, 1080, false, "Fit")]
    [InlineData(1024, 720, true, "Fill")]
    [InlineData(1280, 820, true, "100%")]
    [InlineData(1024, 720, false, "100%")]
    public void ActualMediaComponents_KeepAllocationAcrossTransitions(
        double width, double height, bool video, string scale)
    {
        RunSta(() =>
        {
            Window window = Phase13UiTests.LoadLooseMainWindow(realMediaComponents: true);
            window.Width = width;
            window.Height = height;
            window.Left = -10000;
            window.Top = -10000;
            window.ShowActivated = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            TransitionData data = new(video, scale);
            window.DataContext = data;
            try
            {
                window.Show();
                Settle(window);
                ScrollViewer left = (ScrollViewer)window.FindName("LeftPaneScrollViewer");
                FrameworkElement content = (FrameworkElement)left.Content;
                FrameworkElement preview = (FrameworkElement)VisualTreeHelper.GetChild(content, 0);
                Rect allocation = Bounds(preview, content);
                Assert.True(allocation.Width > 500 && allocation.Height >= 420);
                double contentWidth = left.ViewportWidth;
                bool initiallyScrolls = left.ScrollableHeight > 0;
                List<Size> playerSizes = [];
                List<Size> viewportSizes = [];
                AssertScenario(content, preview, data);
                AssertFilenameGeometry(preview, data);

                foreach (string state in new[]
                {
                    "Rendering", "Ready", "Original", "Ready", "Original", "Ready",
                    "Exporting", "Exported", "Original", "Exported", "Stale", "Failed", "Cancelled", "Unavailable", "Exported"
                })
                {
                    data.Set(state);
                    Settle(window);
                    AssertScenario(content, preview, data);
                    AssertFilenameGeometry(preview, data);
                    AssertBounds(allocation, Bounds(preview, content));
                    AssertClose(contentWidth, left.ViewportWidth);
                    Assert.Equal(0, left.ScrollableWidth);
                    Assert.Equal(ScrollBarVisibility.Visible, left.VerticalScrollBarVisibility);
                    foreach (ReviewViewport viewport in Descendants(preview).OfType<ReviewViewport>().Where(v => v.IsVisible))
                    {
                        viewportSizes.Add(viewport.RenderSize);
                        AssertContained(viewport, preview);
                    }
                    foreach (VideoReviewSurface surface in Descendants(preview).OfType<VideoReviewSurface>().Where(v => v.IsVisible))
                    {
                        Assert.True(surface.ActualHeight > 100);
                        playerSizes.Add(surface.RenderSize);
                        ReviewViewport viewport = Assert.Single(Descendants(surface).OfType<ReviewViewport>());
                        Assert.True(viewport.ActualHeight > 50);
                        Assert.Equal(scale, viewport.ScaleMode);
                        AssertContained(surface, preview);
                        foreach (Button button in Descendants(surface).OfType<Button>().Where(b => b.IsVisible))
                        {
                            AssertContained(button, surface);
                        }
                    }
                    foreach (Button button in Descendants(left).OfType<Button>().Where(b => b.IsVisible))
                    {
                        Rect bounds = Bounds(button, content);
                        Assert.True(bounds.Left >= -Tolerance && bounds.Right <= contentWidth + Tolerance,
                            $"{state}: {button.Content} exceeds content width: {bounds}");
                    }
                    foreach (AdaptiveResultPanel result in Descendants(left).OfType<AdaptiveResultPanel>().Where(p => p.IsVisible))
                    {
                        FrameworkElement[] groups = result.Children.Cast<FrameworkElement>().Where(e => e.IsVisible).ToArray();
                        foreach (FrameworkElement group in groups)
                        {
                            AssertContained(group, result);
                            if (AdaptiveResultPanel.GetRole(group) == AdaptiveResultPanelRole.Text)
                            {
                                Assert.True(group.ActualWidth >= 180, $"{state}: result text has no practical reading width.");
                            }
                        }
                        for (int i = 0; i < groups.Length; i++)
                        {
                            for (int j = i + 1; j < groups.Length; j++)
                            {
                                Assert.False(Bounds(groups[i], result).IntersectsWith(Bounds(groups[j], result)),
                                    $"{state}: {groups[i].Name} overlaps {groups[j].Name}");
                            }
                        }
                    }
                }
                Assert.False(initiallyScrolls);
                Assert.True(left.ScrollableHeight > 0); // Result growth scrolls below, not into, the preview.
                Assert.NotEmpty(viewportSizes);
                foreach (Size size in viewportSizes)
                {
                    AssertClose(viewportSizes[0].Width, size.Width);
                    AssertClose(viewportSizes[0].Height, size.Height);
                }
                if (video)
                {
                    Assert.NotEmpty(playerSizes);
                    foreach (Size size in playerSizes) { AssertClose(playerSizes[0].Width, size.Width); AssertClose(playerSizes[0].Height, size.Height); }
                }

                left.ScrollToVerticalOffset(30);
                Settle(window);
                double offset = left.VerticalOffset;
                data.Set("Failed");
                Settle(window);
                AssertClose(offset, left.VerticalOffset);
                left.ScrollToEnd();
                Settle(window);
                data.Set("Ready");
                Settle(window);
                Button export = Assert.Single(Descendants(left).OfType<Button>(), b => b.IsVisible && b.Content as string == (video ? "Export Video" : "Export Image"));
                AssertContained(export, left);
                Assert.True(export.IsEnabled);

                window.Width = width == 1440 ? width - 160 : width + 160;
                window.Height = height == 1080 ? height - 120 : height + 120;
                Settle(window);
                Rect resized = Bounds(preview, content);
                Assert.True(Math.Abs(resized.Width - allocation.Width) > 1 && Math.Abs(resized.Height - allocation.Height) > 1);
                window.Width = width;
                window.Height = height;
                Settle(window);
                AssertBounds(allocation, Bounds(preview, content));
                AssertScenario(content, preview, data);
                AssertFilenameGeometry(preview, data);
            }
            finally { window.Close(); }
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
            Assert.Equal(relevant && data.CanOpenExportFolder, folder.IsVisible);
            Assert.Equal(data.CanOpenExportFolder, folder.Command!.CanExecute(null));
        }
        TextBlock destination = Assert.Single(all.OfType<TextBlock>(), text => text.Name == "VideoExportDestinationTextBlock");
        Assert.Equal(data.HasVideoExportDestination && data.ShowVideoResultPanel, destination.IsVisible);
        Assert.Equal(data.VideoExportDestinationText, destination.Text);
        Assert.Equal(data.VideoExportDestinationText, destination.ToolTip);
        Assert.Equal(TextTrimming.CharacterEllipsis, destination.TextTrimming);

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
        Assert.Equal(data.ShowVideoResultPanel, Assert.Single(all.OfType<Border>(), element => element.Name == "VideoResultCard").IsVisible);
        Assert.Equal(data.ShowImageResultPanel, Assert.Single(all.OfType<Border>(), element => element.Name == "ImageResultCard").IsVisible);
        ExportActivityIndicator[] indicators = all.OfType<ExportActivityIndicator>().Where(indicator => indicator.IsVisible).ToArray();
        Assert.Equal(data.IsExportActive ? 2 : 0, indicators.Length);
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
        TextBlock icon = Assert.Single(row.Children.OfType<TextBlock>(), text => text.Text == "▣");
        TextBlock kind = Assert.Single(row.Children.OfType<TextBlock>(), text => text.Text == data.MediaTypeText);
        FrameworkElement metadata = (FrameworkElement)row.Parent;
        Grid information = Assert.IsType<Grid>(metadata.Parent);
        FrameworkElement actions = Assert.Single(information.Children.Cast<FrameworkElement>(), element => Grid.GetColumn(element) == 1);
        Assert.Equal(data.SelectedFileName, filename.Text);
        Assert.Equal(data.SelectedFileName, filename.ToolTip);
        Assert.True(filename.ActualWidth > 40);
        Assert.True(new FormattedText(filename.Text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(filename.FontFamily, filename.FontStyle, filename.FontWeight, filename.FontStretch), filename.FontSize,
            Brushes.White, VisualTreeHelper.GetDpi(filename).PixelsPerDip).Width > filename.ActualWidth);
        foreach (TextBlock text in new[] { filename, icon, kind })
        {
            Assert.True(text.IsVisible && text.ActualWidth > 0);
            AssertContained(text, metadata);
            Assert.False(Bounds(text, information).IntersectsWith(Bounds(actions, information)), $"{text.Text} overlaps media actions.");
        }
        TextBlock[] parts = [icon, filename, kind];
        for (int i = 0; i < parts.Length - 1; i++)
        {
            Assert.True(Bounds(parts[i], row).Right <= Bounds(parts[i + 1], row).Left + Tolerance);
        }
        AssertContained(metadata, information);
        AssertContained(actions, information);
    }

    private static Rect Bounds(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
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
        private readonly bool video;
        public TransitionData(bool isVideo, string scale) : base(isVideo)
        {
            video = isVideo;
            SelectedFitMode = scale;
            RenderImageCommand = new RelayCommand(() => { }, () => !video && !IsExportActive);
            RenderVideoCommand = new RelayCommand(() => { }, () => video && !IsExportActive);
            PreviewFrameCommand = new RelayCommand(() => { }, () => video && !HasVideoArtifact && !IsExportActive);
            ToggleOriginalVideoPlaybackCommand = new RelayCommand(() => { }, () => ShowOriginalVideoPlayer && !IsExportActive);
            ToggleRenderedVideoPlaybackCommand = new RelayCommand(() => { }, () => ShowRenderedVideoPlayer && !IsExportActive);
            OriginalFullscreenCommand = new RelayCommand(() => { }, () => ShowOriginalVideoPlayer && !IsExportActive);
            FullscreenCommand = new RelayCommand(() => { }, () => ShowRenderedVideoPlayer && !IsExportActive);
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
            if (state == "Ready") { _hasResult = true; }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            foreach (IRelayCommand command in new[] { ExportImageCommand, ExportVideoCommand, OpenExportFolderCommand, ShowVideoReviewErrorDetailsCommand,
                RenderImageCommand, RenderVideoCommand, PreviewFrameCommand, ToggleOriginalVideoPlaybackCommand,
                ToggleRenderedVideoPlaybackCommand, OriginalFullscreenCommand, FullscreenCommand })
            {
                command.NotifyCanExecuteChanged();
            }
        }
        private bool Result => _hasResult;
        public new bool IsProcessingResultPanelVisible => _state != "Loaded";
        public new bool IsExportStatusVisible => IsExportActive;
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
        public bool HasVideoExportDestination => video && _state == "Exported";
        public bool CanOpenExportFolder => _state == "Exported" && !IsExportActive;
        public string VideoExportDestinationText => HasVideoExportDestination ? @"C:\Медіа\very long destination folder\another long folder\rendered output відео з довгим ім’ям.mp4" : string.Empty;
        public new string ImageResultSummary => _state switch
        {
            "Failed" => "The replacement could not be rendered. The previously accepted image remains available for review and another attempt.",
            "Exported" => @"Saved to C:\Медіа\long destination folder\rendered output зображення.png. The image remains available for another export.",
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
        public bool ShowImageRenderWarning => !video && _state == "Failed";
        public string ImageRenderWarningText => "The selected font was unavailable; a supported fallback font was used for this retained result.";
        public bool ShowVideoResultErrorDetails => video && _state == "Unavailable";
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
        public bool IsExportCompletionVisible => false;
        public bool IsExportProgressIndeterminate => false;
        public bool AreExportAnimationsEnabled => false;
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
        public bool ShowImageResultErrorDetails => false;
        public bool IsVideoSeekEnabled => false;
        public double TimelinePosition { get; set; }
        public string TimelineText => "0:02 / 0:09";
        public string VideoFrameStatusMessage => string.Empty;
        public string InputMetadataSummary => MetadataPrimaryText;
        public string OriginalVideoPlaybackLimitationMessage => string.Empty;
        public IRelayCommand ShowOriginalPlaybackErrorDetailsCommand { get; } = new RelayCommand(() => { }, () => false);
        public IRelayCommand ShowVideoFrameErrorDetailsCommand { get; } = new RelayCommand(() => { }, () => false);
        public IRelayCommand ShowImageResultErrorDetailsCommand { get; } = new RelayCommand(() => { }, () => false);
        public IRelayCommand SelectOriginalCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand SelectSymbolizedCommand { get; } = new RelayCommand(() => { });
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
