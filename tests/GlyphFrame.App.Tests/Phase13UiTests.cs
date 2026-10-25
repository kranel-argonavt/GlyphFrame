using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class Phase13UiTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    public void StartupWindows_FitTheMonitorWorkAreaAtRepresentativeDpi(double dpi)
    {
        Rect workArea = WindowWorkAreaPlacement.ConvertWorkArea(
            new Rect(1920, 0, 1920, 1152), new Matrix(1 / dpi, 0, 0, 1 / dpi, 0, 0));
        Rect main = WindowWorkAreaPlacement.CalculateBounds(workArea,
            new Size(1320, 820), new Size(1024, 720));
        Rect settings = WindowWorkAreaPlacement.CalculateBounds(workArea,
            new Size(720, 760), new Size(620, 620));

        AssertWithinWorkArea(main, workArea);
        AssertWithinWorkArea(settings, workArea);
        Assert.True(main.Width >= 1024 && main.Height >= 720);
        Assert.True(settings.Width >= 620 && settings.Height >= 620);
        if (dpi == 1)
        {
            Assert.Equal(1320, main.Width);
            Assert.Equal(820, main.Height);
            Assert.Equal(720, settings.Width);
            Assert.Equal(760, settings.Height);
        }
        if (dpi == 1.5)
        {
            Assert.True(main.Height < 820);
            Assert.True(settings.Height < 760);
        }
    }

    [Theory]
    [InlineData(1024d, 720d)]
    [InlineData(1280d, 820d)]
    public void SettingsPlacement_UsesOwnerMonitorAtMinimumAndOrdinarySize(double width, double height)
    {
        RunSta(() =>
        {
            Window owner = new()
            {
                Width = width,
                Height = height,
                Left = -10000,
                Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false
            };
            try
            {
                owner.Show();
                Window settings = new() { Owner = owner, Width = 720, Height = 760 };
                Assert.Same(owner, WindowWorkAreaPlacement.MonitorAnchor(settings));
                Assert.Same(owner, WindowWorkAreaPlacement.MonitorAnchor(owner));
            }
            finally
            {
                owner.Close();
            }
        });
    }

    private static void AssertWithinWorkArea(Rect bounds, Rect workArea)
    {
        Assert.True(bounds.Left >= workArea.Left && bounds.Top >= workArea.Top);
        Assert.True(bounds.Right <= workArea.Right && bounds.Bottom <= workArea.Bottom);
    }

    [Fact]
    public void AboutVersion_ComesFromApplicationAssembly()
    {
        Version? assemblyVersion = typeof(GlyphFrame.App.App).Assembly.GetName().Version;
        Assert.NotNull(assemblyVersion);
        Assert.Equal($"v{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}",
            AboutDialogService.FormatVersion(typeof(GlyphFrame.App.App).Assembly));
    }

    [Fact]
    public void AboutOwner_PrefersActiveWindowAndFallsBackToMain()
    {
        RunSta(() =>
        {
            Window main = new();
            Window active = new();
            Assert.Same(active, AboutDialogService.ResolveOwner([main, active], main,
                window => ReferenceEquals(window, active)));
            Assert.Same(main, AboutDialogService.ResolveOwner([main], main, _ => false));
        });
    }

    [Fact]
    public void AboutDialog_HasThemedAccessibleCloseAndNoAutomaticTelemetryAction()
    {
        XDocument document = ReadView("AboutWindow.xaml");
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace automation = "clr-namespace:System.Windows.Automation;assembly=PresentationCore";
        XElement root = Assert.IsType<XElement>(document.Root);
        Assert.Equal("CenterOwner", root.Attribute("WindowStartupLocation")?.Value);
        Assert.Equal("False", root.Attribute("ShowInTaskbar")?.Value);
        Assert.Equal("NoResize", root.Attribute("ResizeMode")?.Value);
        Assert.Contains(document.Descendants(wpf + "TextBlock"), element =>
            element.Attribute("Text")?.Value.Contains("does not upload your media or send telemetry", StringComparison.Ordinal) == true);
        XElement close = Assert.Single(document.Descendants(wpf + "Button"), element =>
            element.Attribute("Content")?.Value == "Close");
        Assert.Equal("True", close.Attribute("IsCancel")?.Value);
        Assert.Equal("Close About dialog", close.Attribute(automation + "AutomationProperties.Name")?.Value);
    }

    [Fact]
    public void SharedControls_GateHoverWhenDisabledAndKeepKeyboardFocus()
    {
        XDocument controls = ReadResource("Controls.xaml");
        XDocument player = ReadResource("PlayerControls.xaml");
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        foreach (string key in new[] { "SegmentRadioButtonStyle", "PreviewTabStyle" })
        {
            XElement style = Assert.Single(controls.Descendants(wpf + "Style"), element =>
                element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == key));
            Assert.Contains(style.Descendants(wpf + "MultiTrigger"), HasEnabledHoverConditions);
            Assert.Contains(style.Descendants(wpf + "Setter"), setter =>
                setter.Attribute("Property")?.Value == "FocusVisualStyle");
        }
        XElement iconStyle = Assert.Single(player.Descendants(wpf + "Style"), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "PlayerIconButtonStyle"));
        Assert.Contains(iconStyle.Descendants(wpf + "MultiTrigger"), HasEnabledHoverConditions);
        Assert.Contains(iconStyle.Descendants(wpf + "Setter"), setter =>
            setter.Attribute("Property")?.Value == "FocusVisualStyle");

        static bool HasEnabledHoverConditions(XElement trigger) =>
            trigger.Descendants().Any(element => element.Name.LocalName == "Condition"
                && element.Attribute("Property")?.Value == "IsMouseOver"
                && element.Attribute("Value")?.Value == "True")
            && trigger.Descendants().Any(element => element.Name.LocalName == "Condition"
                && element.Attribute("Property")?.Value == "IsEnabled"
                && element.Attribute("Value")?.Value == "True");
    }

    [Fact]
    public void MutedText_RemainsReadableOnCards()
    {
        XDocument colors = ReadResource("Colors.xaml");
        Color muted = ColorValue("MutedTextColor");
        Assert.True(Contrast(muted, ColorValue("CardBackgroundColor")) >= 4.5);
        Assert.True(Contrast(muted, ColorValue("ElevatedBackgroundColor")) >= 4.5);

        Color ColorValue(string key) => (Color)ColorConverter.ConvertFromString(
            Assert.Single(colors.Descendants(), element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" && attribute.Value == key)).Value);
    }

    [Theory]
    [InlineData("AboutWindow.xaml", 470d, 310d)]
    [InlineData("SettingsWindow.xaml", 620d, 620d)]
    [InlineData("SettingsWindow.xaml", 720d, 760d)]
    public void Dialogs_MeasureAndArrangeWithoutHorizontalOverflow(string view, double width, double height)
    {
        RunSta(() =>
        {
            Window window = LoadLooseWindow(view);
            window.Width = width;
            window.Height = height;
            window.Left = -10000;
            window.Top = -10000;
            window.ShowActivated = false;
            window.DataContext = new LayoutData();
            try
            {
                window.Show();
                window.UpdateLayout();
                FrameworkElement content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                Assert.True(content.ActualWidth > 0);
                Assert.True(content.ActualWidth <= width + 1);
                foreach (Button button in Descendants(content).OfType<Button>().Where(button => button.IsVisible))
                {
                    Rect bounds = button.TransformToAncestor(content).TransformBounds(new Rect(button.RenderSize));
                    Assert.True(bounds.Left >= -1 && bounds.Right <= content.ActualWidth + 1,
                        $"{view}: {button.Content} extends outside the dialog at {width} DIPs.");
                }
                if (view == "SettingsWindow.xaml")
                {
                    ScrollViewer scroll = Assert.Single(Descendants(content).OfType<ScrollViewer>(), viewer =>
                        Grid.GetRow(viewer) == 1 && viewer.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);
                    Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                    Assert.True(scroll.ActualWidth <= content.ActualWidth);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MainWindow_LeftPaneKeepsPreviewActionsAndResultReachable()
    {
        RunSta(() =>
        {
            foreach ((double width, double height, bool video) in new[]
            {
                (1024d, 720d, false), (1280d, 820d, false),
                (1024d, 720d, true), (1280d, 820d, true)
            })
            {
                AssertMainWindowLayout(width, height, video);
            }
            AssertRetainedInputDetailsLayout();
        });
    }

    private static void AssertMainWindowLayout(double width, double height, bool video)
    {
        Window window = LoadLooseMainWindow();
        window.Width = width;
        window.Height = height;
        window.Left = -10000;
        window.Top = -10000;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowActivated = false;
        window.DataContext = new MainLayoutData(video);
        try
        {
            window.Show();
            window.UpdateLayout();
            ScrollViewer[] scrolls = Descendants(window).OfType<ScrollViewer>()
                .Where(scroll => scroll.Name != "StatusContentScroller"
                    && scroll.Name != "RenderParameterScroller"
                    && scroll.VerticalScrollBarVisibility is ScrollBarVisibility.Auto or ScrollBarVisibility.Visible).ToArray();
            Assert.Equal(2, scrolls.Length);
            ScrollViewer left = Assert.Single(scrolls, scroll => Grid.GetColumn(scroll) == 0);
            ScrollViewer right = Assert.Single(scrolls, scroll => Grid.GetColumn(scroll) == 2);
            Assert.Equal(ScrollBarVisibility.Disabled, left.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Visible, left.VerticalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Disabled, right.HorizontalScrollBarVisibility);

            Button render = Assert.Single(Descendants(left).OfType<Button>(), button =>
                button.IsVisible && button.Content as string == "Render Again");
            Assert.DoesNotContain(Descendants(left).OfType<Button>(), button =>
                button.IsVisible && button.Content as string == "Preview Frame");
            Border result = Assert.Single(Descendants(left).OfType<Border>(), border =>
                border.Name == (video ? "VideoResultCard" : "ImageResultCard"));
            Assert.True(result.ActualHeight > 0);
            Assert.True(render.ActualWidth >= (video ? 148 : 188));
            Assert.True(render.IsEnabled);

            left.ScrollToEnd();
            window.UpdateLayout();
            foreach (FrameworkElement element in new FrameworkElement[] { render, result })
            {
                Rect bounds = element.TransformToAncestor(left).TransformBounds(new Rect(element.RenderSize));
                Assert.True(bounds.Top >= -1 && bounds.Bottom <= left.ViewportHeight + 1,
                    $"{element.Name} is unreachable in the left pane at {width}×{height}: {bounds}");
            }
            Assert.True(left.ExtentHeight >= left.ViewportHeight);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertRetainedInputDetailsLayout()
    {
        Window window = LoadLooseMainWindow();
        window.Width = 1024;
        window.Height = 720;
        window.Left = -10000;
        window.Top = -10000;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowActivated = false;
        window.DataContext = new MainLayoutData(false, retainedInputError: true);
        try
        {
            window.Show();
            window.UpdateLayout();
            ScrollViewer settings = Assert.Single(Descendants(window).OfType<ScrollViewer>(), scroll =>
                Grid.GetColumn(scroll) == 2 && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Visible);
            Button details = Assert.Single(Descendants(settings).OfType<Button>(), button =>
                button.IsVisible && button.Content as string == "Show Details");
            Rect bounds = details.TransformToAncestor(settings).TransformBounds(new Rect(details.RenderSize));
            Assert.True(details.IsEnabled);
            Assert.True(bounds.Left >= 0 && bounds.Right <= settings.ViewportWidth);
            Assert.True(bounds.Top >= 0 && bounds.Bottom <= settings.ViewportHeight);
        }
        finally
        {
            window.Close();
        }
    }

    internal static Window LoadLooseMainWindow(bool realMediaComponents = false)
    {
        XDocument document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "GlyphFrame.App", "MainWindow.xaml"));
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace views = "clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App";
        XElement root = Assert.IsType<XElement>(document.Root);
        root.Attribute(x + "Class")?.Remove();
        root.Attribute("Icon")?.Remove();
        root.Attribute("Background")?.Remove();
        foreach (XElement element in root.Descendants().Where(element =>
                     element.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views"
                     && element.Name.LocalName is "HeaderView" or "FooterView" or "PreviewPanelView"
                         or "ExportStatusView" or "SettingsPanelView").ToArray())
        {
            XElement replacement = Assert.IsType<XElement>(ReadView($"{element.Name.LocalName}.xaml").Root);
            replacement.Attribute(x + "Class")?.Remove();
            foreach (XAttribute attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            {
                replacement.SetAttributeValue(attribute.Name, attribute.Value);
            }
            foreach (XAttribute handler in replacement.DescendantsAndSelf().SelectMany(view => view.Attributes())
                         .Where(attribute => attribute.Name.LocalName is "Click" or "MouseLeftButtonDown"
                             or "Loaded" or "Unloaded" or "SizeChanged").ToArray())
            {
                handler.Remove();
            }
            foreach (XElement nested in replacement.Descendants().Where(view => !realMediaComponents &&
                         view.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views"
                         && view.Name.LocalName is "ReviewViewport" or "VideoReviewSurface"
                             or "ExportActivityIndicator").ToArray())
            {
                XElement placeholder = new(wpf + "Border");
                foreach (XAttribute attribute in nested.Attributes().Where(attribute =>
                             attribute.Name.LocalName is "Width" or "Height" or "Visibility" or "Grid.Row"
                                 or "Grid.Column"))
                {
                    placeholder.SetAttributeValue(attribute.Name, attribute.Value);
                }
                nested.ReplaceWith(placeholder);
            }
            element.ReplaceWith(replacement);
        }
        foreach (XElement element in root.DescendantsAndSelf())
        {
            if (element.Name.NamespaceName.StartsWith("clr-namespace:GlyphFrame.App.", StringComparison.Ordinal)
                && !element.Name.NamespaceName.Contains(";assembly=", StringComparison.Ordinal))
            {
                element.Name = XNamespace.Get($"{element.Name.NamespaceName};assembly=GlyphFrame.App")
                    + element.Name.LocalName;
            }
            foreach (XAttribute attribute in element.Attributes().ToArray())
            {
                if (attribute.IsNamespaceDeclaration
                    && attribute.Value.StartsWith("clr-namespace:GlyphFrame.App.", StringComparison.Ordinal)
                    && !attribute.Value.Contains(";assembly=", StringComparison.Ordinal))
                {
                    attribute.Value += ";assembly=GlyphFrame.App";
                }
                else if (attribute.Name.NamespaceName.StartsWith("clr-namespace:GlyphFrame.App.", StringComparison.Ordinal)
                         && !attribute.Name.NamespaceName.Contains(";assembly=", StringComparison.Ordinal))
                {
                    XName name = XNamespace.Get($"{attribute.Name.NamespaceName};assembly=GlyphFrame.App")
                        + attribute.Name.LocalName;
                    string value = attribute.Value;
                    attribute.Remove();
                    element.SetAttributeValue(name, value);
                }
            }
        }
        root.SetAttributeValue(XNamespace.Xmlns + "views", views.NamespaceName);
        XElement dictionary = new(wpf + "ResourceDictionary");
        foreach (string file in new[] { "Colors.xaml", "Typography.xaml", "Controls.xaml", "PlayerControls.xaml", "Components.xaml" })
        {
            foreach (XElement resource in ReadResource(file).Root!.Elements())
            {
                dictionary.Add(new XElement(resource));
            }
        }
        root.AddFirst(new XElement(wpf + "Window.Resources", dictionary));
        // Loose XAML needs the assembly qualifier for the real decorative WPF type.
        XAttribute? viewNamespace = root.Attribute(XNamespace.Xmlns + "views");
        if (viewNamespace is not null) { viewNamespace.Value = "clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App"; }
        foreach (XElement element in root.Descendants().Where(e => e.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views").ToArray())
        { element.Name = XNamespace.Get("clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App") + element.Name.LocalName; }
        foreach (XElement element in root.Descendants().Where(e => e.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views").ToArray())
        { element.Name = XNamespace.Get("clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App") + element.Name.LocalName; }
        return Assert.IsType<Window>(XamlReader.Parse(document.ToString(SaveOptions.DisableFormatting)));
    }

    internal static Window LoadLooseWindow(string view)
    {
        XDocument document = ReadView(view);
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement root = Assert.IsType<XElement>(document.Root);
        root.Attribute(x + "Class")?.Remove();
        root.Attribute("Icon")?.Remove(); // Root resources are not available until after loose-XAML construction.
        root.Attribute("Background")?.Remove();
        root.SetAttributeValue(XNamespace.Xmlns + "views", "clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App");
        foreach (XAttribute attribute in root.DescendantsAndSelf().SelectMany(element => element.Attributes())
                     .Where(attribute => attribute.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views").ToArray())
        {
            XName name = XNamespace.Get("clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App") + attribute.Name.LocalName;
            string value = attribute.Value; XElement owner = attribute.Parent!;
            attribute.Remove(); owner.SetAttributeValue(name, value);
        }
        foreach (XElement element in root.Descendants().Where(e => e.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views").ToArray())
        { element.Name = XNamespace.Get("clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App") + element.Name.LocalName; }
        foreach (XAttribute handler in root.DescendantsAndSelf().SelectMany(element => element.Attributes())
                     .Where(attribute => attribute.Name.LocalName is "Click" or "MouseLeftButtonDown").ToArray())
        {
            handler.Remove();
        }
        XElement dictionary = new(wpf + "ResourceDictionary");
        foreach (string file in new[] { "Colors.xaml", "Typography.xaml", "Controls.xaml", "Components.xaml" })
        {
            foreach (XElement resource in ReadResource(file).Root!.Elements())
            {
                dictionary.Add(new XElement(resource));
            }
        }
        XElement? localResources = root.Element(wpf + "Window.Resources");
        if (localResources is not null)
        {
            dictionary.Add(localResources.Elements().Select(resource => new XElement(resource)));
            localResources.Remove();
        }
        root.AddFirst(new XElement(wpf + "Window.Resources", dictionary));
        foreach (XElement element in root.Descendants().Where(e => e.Name.NamespaceName == "clr-namespace:GlyphFrame.App.Views").ToArray())
        { element.Name = XNamespace.Get("clr-namespace:GlyphFrame.App.Views;assembly=GlyphFrame.App") + element.Name.LocalName; }
        return Assert.IsType<Window>(XamlReader.Parse(document.ToString(SaveOptions.DisableFormatting)));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static double Contrast(Color first, Color second)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte value)
            {
                double normalized = value / 255d;
                return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }
        double light = Math.Max(Luminance(first), Luminance(second));
        double dark = Math.Min(Luminance(first), Luminance(second));
        return (light + 0.05) / (dark + 0.05);
    }

    private static XDocument ReadView(string name) => XDocument.Load(Path.Combine(RepositoryRoot(),
        "src", "GlyphFrame.App", "Views", name));

    private static XDocument ReadResource(string name) => XDocument.Load(Path.Combine(RepositoryRoot(),
        "src", "GlyphFrame.App", "Resources", name));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GlyphFrame.sln")))
        {
            directory = directory.Parent;
        }
        return Assert.IsType<DirectoryInfo>(directory).FullName;
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF dialog layout did not complete.");
        failure?.Throw();
    }

    private sealed class LayoutData
    {
        public string Name => "GlyphFrame";
        public string VersionText => "v1.0.0";
        public string StatusMessage => "The selected defaults have been saved and the bundled video tools are available.";
        public string FfmpegStatusMessage => "The bundled tools were validated locally and are ready for video processing.";
        public string BundledToolsDirectory => @"C:\Users\Example\A long mixed-language folder\Медіа\Tools\ffmpeg";
        public string FfmpegVersion => "9.0-ffmpeg-test-long-version-value";
        public string FfprobeVersion => "9.0-ffprobe-test-long-version-value";
        public string ApplicationDataDirectory => @"C:\Users\Example\AppData\Local\GlyphFrame";
        public string LogsDirectory => @"C:\Users\Example\AppData\Local\GlyphFrame\logs";
    }

    private sealed class UnusedClipboard : IClipboardService
    {
        public ClipboardWriteResult SetText(string text) => ClipboardWriteResult.Failed();
    }

    internal class MainLayoutData(bool video, bool retainedInputError = false)
    {
        public ExportPathCopyPresentation ExportPathCopy { get; } = new(new UnusedClipboard());
        public bool ShowCopyExportPath => false;
        public bool CanOpenExportFolder => false;
        public bool ShowImageRenderWarning => false;
        public bool ShowImageResultErrorDetails => false;
        public bool ShowVideoResultErrorDetails => false;
        public bool AreExportAnimationsEnabled => false;
        public bool IsBrowseMediaAvailable => true;
        public bool ShowPreviewFrameAction => video && !HasVideoArtifact;
        public bool HasImageResultForRerender => !video;
        public string InputDisplayName => SelectedFileName;
        public bool IsInputReady => !retainedInputError;
        public string InputCardSummary => MetadataPrimaryText;
        public string SelectedFilePath => @"C:\Медіа\sample.png";
        public string BrowseMediaActionLabel => "Change file";
        public string InputDropText => "Drop another file to replace";
        public string InputReadinessText => retainedInputError ? string.Empty : "Source accepted";
        public string InputMetadataSummary => MetadataPrimaryText;
        public bool HasMediaInputError => retainedInputError;
        public bool AreSettingsEnabled => true;
        public bool AreVideoAudioSettingsEnabled => video;
        public bool IsKeepAudioSelected { get; set; } = true;
        public bool IsRemoveAudioSelected { get; set; }
        public IReadOnlyList<GlyphFrame.Core.Models.CharacterPresetOption> Presets => GlyphFrame.Core.Models.CharacterPresetOption.BuiltIn;
        public GlyphFrame.Core.Models.CharacterPresetOption SelectedPreset { get; set; } = GlyphFrame.Core.Models.CharacterPresetOption.Default;
        public string CharacterRamp => " .:-=+*#%@";
        public string[] FontFamilies => ["Cascadia Mono", "Consolas"];
        public string SelectedFont { get; set; } = "Cascadia Mono";
        public int[] FontSizes => [9, 10, 12];
        public int SelectedFontSize { get; set; } = 10;
        public string[] OutputResolutions => ["Same as source", "720p", "1080p"];
        public string SelectedOutputResolution { get; set; } = "Same as source";
        public object[] SymbolPolarities => [new { DisplayName = "Light on Dark", Value = 0 }];
        public int SelectedSymbolPolarity { get; set; }
        public bool ShowRetainedMediaInputErrorDetails => retainedInputError;
        public string InputStatusMessage => retainedInputError ? "This file could not be opened." : "Image ready.";
        public IRelayCommand ShowMediaInputErrorDetailsCommand { get; } = new RelayCommand(() => { });
        public bool IsProcessingResultPanelVisible => true;
        public bool IsMediaSelected => true;
        public bool ShowReadyToRender => false;
        public string ReadyToRenderMessage => "Render the selected source.";
        public string PlannedOutputDimensionsText => "1280 × 720";
        public bool ShowVideoAudioUnavailableOverlay => !IsVideoSelected;
        public string VideoAudioUnavailableReason => "Audio does not apply to images";
        public string PlannedAudioText => "Keep audio";
        public bool IsExportStatusVisible => false;
        public bool ShowImageResultPanel => !video;
        public bool ShowVideoResultPanel => video;
        public bool IsImageSelected => !video;
        public bool IsVideoSelected => video;
        public bool ShowImageExportControls => !video;
        public bool ShowImageRenderMetrics => !video;
        public bool HasVideoArtifact => video;
        public bool ShowVideoRenderMetrics => video;
        public bool IsImageResultSuccess => !video;
        public bool IsVideoResultSuccess => video;
        public string ImageResultTitle => "Image Ready";
        public string ImageResultSummary => "Ready for review and export.";
        public string VideoResultTitle => "Video Ready";
        public string VideoResultSummary => "Ready for review and export.";
        public string ImageGridDimensionsText => "80 × 22";
        public string ImageOutputDimensionsText => "640 × 360";
        public string VideoRenderedFrameCountText => "12";
        public string VideoFileSizeText => "1.2 MB";
        public string VideoOutputDimensionsText => "64 × 48";
        public string VideoAudioOutcomeText => "Original";
        public string VideoAudioOutcomeDescription => "Original audio preserved";
        public string ImageRenderActionLabel => "Render Again";
        public string VideoRenderActionLabel => "Render Again";
        public string PreviewFrameActionLabel => "Preview Frame";
        public string ImageExportActionLabel => "Export Image";
        public string VideoExportActionLabel => "Export Video";
        public string[] FitModes => ["Fit", "Fill", "100%"];
        public string SelectedFitMode { get; set; } = "Fit";
        public string SelectedFileName => video ? "sample.mp4" : "sample.png";
        public string MediaTypeText => video ? "Video" : "Image";
        public string MetadataPrimaryText => video ? "64 × 48 - MP4" : "640 × 360 - PNG";
        public string MetadataSecondaryText => "Local test media";
    }
}
