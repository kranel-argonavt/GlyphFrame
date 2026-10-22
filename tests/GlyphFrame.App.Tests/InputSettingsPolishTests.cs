using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class InputSettingsPolishTests
{
    [Theory]
    [InlineData(1024, 12)]
    [InlineData(1280, 12)]
    [InlineData(1024, 18)]
    public void Input_ReadinessPrecedesMetadataAndReflowsWithoutRightReservation(double width, double textSize) => Sta(() =>
    {
        Window window = Phase13UiTests.LoadLooseMainWindow();
        InputData data = new(); window.DataContext = data;
        window.Width = width; window.Height = 720; window.Left = -10000; window.ShowActivated = false;
        try
        {
            window.Show(); Settle(window);
            Border input = Descendants(window).OfType<Border>().Single(b => b.Name == "InputCard");
            foreach (TextBlock label in Descendants(input).OfType<TextBlock>()) { label.FontSize = textSize; }
            Assert.DoesNotContain(Descendants(input).OfType<TextBlock>(), t => t.Text == "Formats");
            Assert.DoesNotContain(Descendants(input).OfType<System.Windows.Shapes.Path>(), p => p.Data.ToString() == ((Geometry)window.FindResource("InfoIcon")).ToString());
            Button browse = Descendants(input).OfType<Button>().Single(b => b.Command == data.BrowseMediaCommand);
            Assert.Contains("PNG", AutomationProperties.GetHelpText(browse)); Assert.Contains("AVI", AutomationProperties.GetHelpText(browse));
            StackPanel ready = Descendants(input).OfType<StackPanel>().Single(p => p.Name == "SourceReady");
            WrapPanel line = Descendants(input).OfType<WrapPanel>().Single(p => p.Name == "InputSummaryLine");
            TextBlock metadata = Descendants(input).OfType<TextBlock>().Single(t => t.Name == "InputMetadata");
            TextBlock filename = Descendants(input).OfType<TextBlock>().Single(t => t.Name == "InputFileName");
            bool wrapped = false;
            foreach (string state in new[] { "Image", "Video", "Loading", "Error", "CancelledReplacement", "Empty", "Video" })
            {
                data.Set(state); Settle(window);
                Assert.Equal(data.IsInputReady, ready.IsVisible);
                Assert.Equal(data.InputCardSummary, metadata.Text);
                Assert.Equal(data.SelectedFilePath, filename.ToolTip);
                Assert.Equal(data.SelectedFilePath, AutomationProperties.GetHelpText(filename));
                Assert.True(Bounds(filename, input).Bottom <= Bounds(line, input).Top + 1);
                AssertContained(line, input); AssertContained(metadata, line);
                Assert.Equal(TextWrapping.Wrap, metadata.TextWrapping);
                Assert.Equal(TextTrimming.None, metadata.TextTrimming);
                if (ready.IsVisible)
                {
                    Rect r = Bounds(ready, line), m = Bounds(metadata, line);
                    Assert.InRange(r.Left, 0, 1); AssertContained(ready, line);
                    Rect overlap = Rect.Intersect(r, m);
                    Assert.True(overlap.IsEmpty || overlap.Width <= 0.5 || overlap.Height <= 0.5, $"Ready {r}, metadata {m}");
                    if (m.Top >= r.Bottom - 1) { wrapped = true; Assert.InRange(m.Left, 0, 1); }
                    else { Assert.True(m.Left >= r.Right); }
                    FrameworkElement dot = Assert.Single(ready.Children.OfType<System.Windows.Shapes.Ellipse>());
                    TextBlock label = ready.Children.OfType<TextBlock>().First();
                    Assert.True(Bounds(dot, ready).Right <= Bounds(label, ready).Left);
                }
                else { Assert.InRange(Bounds(metadata, line).Left, 0, 1); }
                if (state == "Error")
                {
                    Button details = Descendants(input).OfType<Button>().Single(b => b.Content as string == "Show Details");
                    Assert.True(details.IsVisible); AssertContained(details, input);
                }
            }
            if (textSize == 18) { Assert.True(wrapped); }
            Capture(window, $"main-{width}-{textSize}");
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(620, 620, 12)]
    [InlineData(720, 760, 12)]
    [InlineData(620, 620, 18)]
    public void Settings_PathsAreReadOnlySelectableCopyableAndBounded(double width, double height, double textSize) => Sta(() =>
    {
        SettingsDraftTests.Harness h = SettingsDraftTests.Harness.Create().GetAwaiter().GetResult();
        Window window = Phase13UiTests.LoadLooseWindow("SettingsWindow.xaml");
        window.DataContext = h.ViewModel; window.Width = width; window.Height = height;
        window.Left = -10000; window.ShowActivated = false;
        try
        {
            window.Show(); Settle(window);
            foreach (TextBlock text in Descendants(window).OfType<TextBlock>()) { if (text.FontSize < textSize) { text.FontSize = textSize; } }
            foreach (Control control in Descendants(window).OfType<Control>()) { control.FontSize = textSize; }
            Settle(window);
            FrameworkElement content = (FrameworkElement)window.Content;
            ScrollViewer scroll = Descendants(window).OfType<ScrollViewer>().Single(s => Grid.GetRow(s) == 1 && s.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);
            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
            foreach ((string name, string value, string accessibleName) in new[]
            {
                ("ApplicationDataPath", h.ViewModel.ApplicationDataDirectory, "Application-data folder path"),
                ("LogsFolderPath", h.ViewModel.LogsDirectory, "Logs folder path")
            })
            {
                TextBox path = (TextBox)window.FindName(name);
                path.Focus(); path.BringIntoView(); Settle(window);
                Assert.True(path.IsReadOnly); Assert.True(path.IsEnabled); Assert.True(path.Focusable);
                Assert.Equal(value, path.Text); Assert.Equal(value, path.ToolTip);
                Assert.Equal(accessibleName, AutomationProperties.GetName(path));
                IValueProvider provider = (IValueProvider)new TextBoxAutomationPeer(path).GetPattern(PatternInterface.Value);
                Assert.True(provider.IsReadOnly); Assert.Equal(value, provider.Value);
                ApplicationCommands.SelectAll.Execute(null, path);
                Assert.Equal(value, path.SelectedText);
                string? copied = null;
                DataObjectCopyingEventHandler copying = (_, e) =>
                {
                    copied = e.DataObject.GetData(DataFormats.UnicodeText) as string;
                    e.CancelCommand(); // Exercise the routed copy command without replacing the user's clipboard.
                };
                DataObject.AddCopyingHandler(path, copying);
                try { Assert.True(ApplicationCommands.Copy.CanExecute(null, path)); ApplicationCommands.Copy.Execute(null, path); }
                finally { DataObject.RemoveCopyingHandler(path, copying); }
                Assert.Equal(value, copied);
                EditingCommands.Backspace.Execute(null, path);
                Assert.Equal(value, path.Text);
                AssertContained(path, content);
                ScrollContentPresenter viewport = Descendants(scroll).OfType<ScrollContentPresenter>().First();
                Rect visiblePath = Bounds(path, viewport);
                Assert.True(visiblePath.Top >= -1 && visiblePath.Bottom <= viewport.ActualHeight + 1,
                    $"{name}: {visiblePath}, visible viewport {viewport.RenderSize}, offset {scroll.VerticalOffset}");
            }
            Assert.Contains(ApplicationCommands.Copy.InputGestures.OfType<KeyGesture>(), k => k.Key == Key.C && k.Modifiers == ModifierKeys.Control);
            ComboBox format = (ComboBox)window.FindName("ImageExportFormatComboBox");
            Assert.Equal(2, format.Items.Count);
            Assert.Equal("Default image export format", AutomationProperties.GetName(format));
            Assert.Contains("image Save dialog", AutomationProperties.GetHelpText(format));
            Assert.Contains("change it there", AutomationProperties.GetHelpText(format));
            Button cancel = Descendants(window).OfType<Button>().Single(b => b.Content as string == "Cancel");
            Assert.True(cancel.IsCancel); Assert.Same(h.ViewModel.CancelCommand, cancel.Command);
            Assert.Same(h.ViewModel.SaveCommand, Descendants(window).OfType<Button>().Single(b => b.Content as string == "Save & Close").Command);
            Assert.DoesNotContain(Descendants(window).OfType<TextBlock>(), t => t.Text.Contains("approved", StringComparison.OrdinalIgnoreCase));
            Assert.All(Descendants(window).OfType<Button>(), b => AssertContained(b, content));
            Assert.All(Descendants(window).OfType<ComboBox>(), c => AssertContained(c, content));
            format.BringIntoView(); Settle(window); Capture(window, $"settings-format-{width}-{textSize}");
            scroll.ScrollToEnd(); Settle(window); Capture(window, $"settings-paths-{width}-{textSize}");
            scroll.ScrollToHome(); Settle(window); Capture(window, $"settings-defaults-{width}-{textSize}");
        }
        finally { window.Close(); h.ViewModel.EndEdit(); }
    });

    [Theory]
    [InlineData(1024, 720)]
    [InlineData(1280, 820)]
    public void Footer_PreservesOperationalContentAndWorkingActionsAtIncreasedText(double width, double height) => Sta(() =>
    {
        Window window = Phase13UiTests.LoadLooseMainWindow(); InputData data = new();
        window.DataContext = data; window.Width = width; window.Height = height; window.Left = -10000; window.ShowActivated = false;
        try
        {
            window.Show(); Settle(window);
            Button about = Descendants(window).OfType<Button>().Single(b => b.Content as string == "About");
            Button settings = Descendants(window).OfType<Button>().Single(b => b.Content as string == "Settings");
            DependencyObject ancestor = VisualTreeHelper.GetParent(about);
            while (ancestor is not UserControl) { ancestor = VisualTreeHelper.GetParent(ancestor); }
            UserControl footer = (UserControl)ancestor;
            foreach (TextBlock label in Descendants(footer).OfType<TextBlock>()) { label.FontSize = 18; }
            about.FontSize = 18; settings.FontSize = 18; Settle(window);
            Assert.Same(data.AboutCommand, about.Command); Assert.Same(data.SettingsCommand, settings.Command);
            Assert.True(about.IsEnabled); Assert.True(settings.IsEnabled);
            Assert.True(about.Focusable); Assert.True(settings.Focusable);
            Assert.NotNull(about.FocusVisualStyle); Assert.NotNull(settings.FocusVisualStyle);
            TextBlock version = Descendants(footer).OfType<TextBlock>().Single(t => t.Text == data.ApplicationVersion);
            TextBlock status = Descendants(footer).OfType<TextBlock>().Single(t => t.Text == data.StatusMessage);
            Assert.DoesNotContain(Descendants(footer).OfType<TextBlock>(), t => t.Text.Contains("No telemetry", StringComparison.Ordinal));
            foreach (FrameworkElement element in new FrameworkElement[] { version, status, about, settings })
            {
                AssertContained(element, footer);
                Rect bounds = Bounds(element, footer);
                Assert.True(bounds.Top >= -1 && bounds.Bottom <= footer.ActualHeight + 1);
            }
            Assert.True(Bounds(version, footer).Right < Bounds(status, footer).Left);
            Assert.True(Bounds(status, footer).Right < Bounds(about, footer).Left);
            about.Command.Execute(null); settings.Command.Execute(null);
            Assert.Equal(1, data.AboutCalls); Assert.Equal(1, data.SettingsCalls);
            Capture(window, $"footer-{width}-18");
        }
        finally { window.Close(); }
    });

    private sealed class InputData : Phase13UiTests.MainLayoutData, INotifyPropertyChanged
    {
        internal InputData() : base(false)
        {
            AboutCommand = new RelayCommand(() => AboutCalls++);
            SettingsCommand = new RelayCommand(() => SettingsCalls++);
        }
        private string _state = "Image";
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Set(string state) { _state = state; PropertyChanged?.Invoke(this, new(null)); }
        public new bool IsMediaSelected => _state != "Empty";
        public new bool IsVideoSelected => _state is "Video" or "Loading" or "Error" or "CancelledReplacement";
        public new bool HasMediaInputError => _state == "Error";
        public new bool IsInputReady => IsMediaSelected && _state != "Loading" && !HasMediaInputError;
        public new string InputDisplayName => IsMediaSelected ? "Медіа_日本語_" + new string('A', 100) + (_state == "Image" ? ".png" : ".mp4") : "No media selected";
        public new string SelectedFilePath => @"C:\Local Media\" + InputDisplayName;
        public new string InputCardSummary => IsMediaSelected ? $"{(IsVideoSelected ? "Video" : "Image")} · 1024 × 576 · {105.92.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} KB" : "Choose an image or video.";
        public new string InputReadinessText => _state == "Loading" ? "Reading local media..." : HasMediaInputError ? "" : "Source accepted";
        public new bool ShowRetainedMediaInputErrorDetails => HasMediaInputError;
        public new string InputStatusMessage => HasMediaInputError ? "This file could not be opened. The previous result is still available." : "";
        public IRelayCommand BrowseMediaCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand DropMediaCommand { get; } = new RelayCommand(() => { });
        public string ApplicationVersion => new AboutDialogService().VersionText;
        public string StatusMessage => "Image exported successfully.";
        public int AboutCalls { get; private set; }
        public int SettingsCalls { get; private set; }
        public IRelayCommand AboutCommand { get; }
        public IRelayCommand SettingsCommand { get; }
    }

    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (DependencyObject nested in Descendants(child)) { yield return nested; }
        }
    }
    internal static Rect Bounds(FrameworkElement child, Visual parent) => child.TransformToAncestor(parent).TransformBounds(new Rect(child.RenderSize));
    private static void AssertContained(FrameworkElement child, FrameworkElement parent)
    {
        Rect bounds = Bounds(child, parent);
        Assert.True(bounds.Left >= -1 && bounds.Right <= parent.ActualWidth + 1, $"{child.Name}: horizontal {bounds}, parent {parent.RenderSize}");
        // Scrollable content may lie outside the viewport vertically until brought into view.
    }
    internal static void Settle(Window window)
    { for (int i = 0; i < 3; i++) { window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout(); } }
    internal static void Capture(Window window, string name)
    {
        string? folder = Environment.GetEnvironmentVariable("GLYPHFRAME_POLISH_EVIDENCE");
        if (folder is null) { return; }
        Directory.CreateDirectory(folder);
        FrameworkElement content = (FrameworkElement)window.Content;
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content); PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(stream);
        File.WriteAllText(Path.Combine(folder, name + ".json"), System.Text.Json.JsonSerializer.Serialize(new
        { Kind = "Populated finite-viewport WPF fixture, not native visual acceptance", Width = content.ActualWidth, Height = content.ActualHeight }));
    }
    internal static void Sta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        { try { action(); } catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); } finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF polish fixture timed out."); failure?.Throw();
    }
}
