using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Text;
using GlyphFrame.App.Services;
using GlyphFrame.App.ViewModels;
using GlyphFrame.App.Views;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class LogoAccentTests
{
    [Fact]
    public void SharedNativeIcon_PrivateArtwork_RenderAllAccentsAndDraftLifecycle() => InputSettingsPolishTests.Sta(() =>
    {
        Window main = Phase13UiTests.LoadLooseMainWindow();
        main.DataContext = new Phase13UiTests.MainLayoutData(false);
        main.Left = -10000; main.ShowActivated = false;
        DrawingImage native = (DrawingImage)main.Resources["GlyphFrameWindowIcon"];
        main.Icon = native;
        AccentThemeService theme = new(main.Resources);
        SettingsDraftTests.Store store = new();
        ApplicationSettingsManager manager = new(store, theme);
        manager.InitializeAsync().GetAwaiter().GetResult();
        SettingsViewModel vm = new(manager, new SettingsDraftTests.Paths(), theme, new SettingsDraftTests.Folders(),
            new SettingsDraftTests.Dialogs(), new Validation(), new ErrorPresentationService(), new Details());
        List<Window> dialogs = [];
        try
        {
            main.Show(); InputSettingsPolishTests.Settle(main);
            foreach (string file in new[] { "AboutWindow.xaml", "ConfirmationWindow.xaml", "ErrorDetailsWindow.xaml" })
            {
                Window dialog = Phase13UiTests.LoadLooseWindow(file);
                dialog.Resources = main.Resources; dialog.Owner = main; dialog.Left = -10000; dialog.ShowActivated = false;
                dialog.Show(); InputSettingsPolishTests.Settle(dialog); dialogs.Add(dialog);
            }
            Image[] artwork = new[] { main }.Concat(dialogs).Select(w =>
                Assert.Single(InputSettingsPolishTests.Descendants(w).OfType<DecorativeImage>())).ToArray();
            BrandLogo headerLogo = Assert.Single(InputSettingsPolishTests.Descendants(main).OfType<BrandLogo>());
            BrandLogo aboutLogo = Assert.Single(InputSettingsPolishTests.Descendants(dialogs[0]).OfType<BrandLogo>());
            Assert.Equal(headerLogo.Template.TargetType, aboutLogo.Template.TargetType);
            Assert.Equal(headerLogo.RenderSize, aboutLogo.RenderSize);
            // WPF rounds DIPs to physical pixels at the host DPI; allow one device pixel.
            double pixel = 1 / VisualTreeHelper.GetDpi(headerLogo).DpiScaleX;
            Assert.InRange(Math.Abs(headerLogo.ActualWidth - 54), 0, pixel);
            Assert.InRange(Math.Abs(headerLogo.ActualHeight - 54), 0, pixel);
            Assert.Equal(artwork[0].RenderSize, artwork[1].RenderSize);
            Assert.InRange(Math.Abs(artwork[0].ActualWidth - 38), 0, pixel);
            Assert.InRange(Math.Abs(artwork[0].ActualHeight - 38), 0, pixel);
            Border headerFrame = Assert.Single(InputSettingsPolishTests.Descendants(headerLogo).OfType<Border>());
            Border aboutFrame = Assert.Single(InputSettingsPolishTests.Descendants(aboutLogo).OfType<Border>());
            Assert.Equal(headerFrame.CornerRadius, aboutFrame.CornerRadius);
            Assert.Equal(headerFrame.BorderThickness, aboutFrame.BorderThickness);
            Assert.False(headerLogo.Focusable); Assert.False(aboutLogo.Focusable);
            ImageSource[] sources = artwork.Select(i => i.Source).ToArray();
            Assert.All(sources, source => Assert.NotSame(native, source));
            Assert.Equal(4, sources.Distinct().Count());
            DrawingGroup original = (DrawingGroup)native.Drawing;
            foreach (DrawingImage source in sources)
            {
                DrawingGroup drawing = (DrawingGroup)source.Drawing;
                Assert.Equal(original.Bounds, drawing.Bounds);
                Assert.Equal(3, drawing.Children.Count);
                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(((GeometryDrawing)original.Children[i]).Geometry.ToString(), ((GeometryDrawing)drawing.Children[i]).Geometry.ToString());
                }
                Assert.Equal(2, ((GeometryDrawing)drawing.Children[1]).Pen.Thickness);
            }
            Color neutral = ((SolidColorBrush)main.Resources["PrimaryTextBrush"]).Color;
            int[] neutralCounts = artwork.Select(i => NeutralCount(Pixels(i), neutral)).ToArray();
            vm.BeginEdit();
            foreach (AccentPreset accent in Enum.GetValues<AccentPreset>().Concat(Enum.GetValues<AccentPreset>().Reverse()))
            {
                vm.SelectedAccentPreset = accent;
                Check(accent);
                Assert.Same(native, main.Icon);
                Assert.Equal(SettingsDraftTests.Harness.Persisted, manager.Current);
                InputSettingsPolishTests.Capture(main, $"logo-{accent}");
            }
            vm.CancelCommand.Execute(null); Check(SettingsDraftTests.Harness.Persisted.AccentPreset);
            vm.BeginEdit(); vm.SelectedAccentPreset = AccentPreset.Cyan; Check(AccentPreset.Cyan);
            vm.EndEdit(); Check(SettingsDraftTests.Harness.Persisted.AccentPreset);
            vm.BeginEdit(); vm.SelectedAccentPreset = AccentPreset.Blue;
            vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Check(AccentPreset.Blue);
            Assert.Equal(AccentPreset.Blue, store.Saved.AccentPreset); Assert.Equal(1, store.Saves);
            vm.BeginEdit(); Assert.Equal(AccentPreset.Blue, vm.SelectedAccentPreset);
            vm.SelectedAccentPreset = AccentPreset.Emerald; Check(AccentPreset.Emerald);
            vm.CancelCommand.Execute(null); Check(AccentPreset.Blue);
            ApplicationSettingsManager restarted = new(store, theme);
            restarted.InitializeAsync().GetAwaiter().GetResult(); Check(AccentPreset.Blue);
            Assert.Equal(AccentPreset.Blue, restarted.Current.AccentPreset);

            void Check(AccentPreset accent)
            {
                InputSettingsPolishTests.Settle(main);
                foreach (Window dialog in dialogs) { InputSettingsPolishTests.Settle(dialog); }
                Color expected = (Color)ColorConverter.ConvertFromString(theme.GetPrimaryColorHex(accent));
                Color background = ((SolidColorBrush)main.Resources["AccentSubtleBrush"]).Color;
                for (int i = 0; i < artwork.Length; i++)
                {
                    BitmapSource pixels = Pixels(artwork[i]);
                    Assert.Equal(expected, At(pixels, 2, 5));
                    Assert.Equal(neutral, At(pixels, 8, 12));
                    Assert.Equal(background, At(pixels, 16, 27));
                    Assert.Equal(neutralCounts[i], NeutralCount(pixels, neutral));
                    Assert.Same(sources[i], artwork[i].Source);
                    Assert.False(artwork[i].Focusable); Assert.Null(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(artwork[i]));
                }
            }
        }
        finally { foreach (Window dialog in dialogs) { dialog.Close(); } main.Close(); }
    });

    [Fact]
    public void NativeWindowIcons_RenderAllAccentsAndPreviewRollbackSaveWithoutRestart()
    {
        // Application resources reproduce the actual icon lookup lifecycle.
        // WPF permits only one Application per process, so isolate this test
        // from other STA layout fixtures rather than leaving global state.
        if (Environment.GetEnvironmentVariable("GLYPHFRAME_ICON_TEST_CHILD") != "1")
        { RunIconTestInOwnProcess(); return; }
        InputSettingsPolishTests.Sta(() =>
        {
            Application application = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Window main = Phase13UiTests.LoadLooseMainWindow();
            main.DataContext = new Phase13UiTests.MainLayoutData(false);
            main.Left = -10000; main.ShowActivated = false;
            ResourceDictionary resources = main.Resources;
            application.Resources = resources; main.Resources = new ResourceDictionary();
            DrawingImage factory = (DrawingImage)resources["GlyphFrameWindowIcon"];
            // The loose-XAML helper strips the old root Icon attribute. Reproduce
            // that production assignment until the window owns its own source.
            main.Icon ??= factory;
            Window settings = Phase13UiTests.LoadLooseWindow("SettingsWindow.xaml");
            settings.Resources = new ResourceDictionary(); settings.Left = -10000; settings.ShowActivated = false;
            settings.Icon ??= factory;
            Window reference = new() { Left = -10000, Top = -10000, Width = 200, Height = 100, ShowActivated = false, ShowInTaskbar = false };
            AccentThemeService theme = new();
            SettingsDraftTests.Store store = new(); ApplicationSettingsManager manager = new(store, theme);
            manager.InitializeAsync().GetAwaiter().GetResult();
            SettingsViewModel vm = new(manager, new SettingsDraftTests.Paths(), theme, new SettingsDraftTests.Folders(),
                new SettingsDraftTests.Dialogs(), new Validation(), new ErrorPresentationService(), new Details());
            List<object> measurements = []; int check = 0;
            List<Window> iconWindows = [main, settings];
            ImageSource mainIcon = main.Icon, settingsIcon = settings.Icon;
            try
            {
                main.Show(); settings.Owner = main; settings.Show(); reference.Show();
                Assert.Same(factory, ThemedWindowIcon.GetSource(main)); Assert.Same(factory, ThemedWindowIcon.GetSource(settings));
                Assert.NotSame(factory, mainIcon); Assert.NotSame(factory, settingsIcon); Assert.NotSame(mainIcon, settingsIcon);
                Check("initial", SettingsDraftTests.Harness.Persisted.AccentPreset);
                vm.BeginEdit();
                foreach (AccentPreset preset in Enum.GetValues<AccentPreset>().Concat(Enum.GetValues<AccentPreset>().Reverse()))
                {
                    vm.SelectedAccentPreset = preset; Check("preview", preset);
                    Assert.Equal(SettingsDraftTests.Harness.Persisted, manager.Current); Assert.Equal(0, store.Saves);
                }
                vm.CancelCommand.Execute(null); Check("cancel", SettingsDraftTests.Harness.Persisted.AccentPreset);
                vm.BeginEdit(); vm.SelectedAccentPreset = AccentPreset.Cyan; Check("preview-before-dismiss", AccentPreset.Cyan);
                vm.EndEdit(); Check("end-edit", SettingsDraftTests.Harness.Persisted.AccentPreset);
                vm.BeginEdit(); vm.SelectedAccentPreset = AccentPreset.Blue;
                vm.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Check("save", AccentPreset.Blue);
                Assert.Equal(AccentPreset.Blue, store.Saved.AccentPreset); Assert.Equal(1, store.Saves);
                vm.BeginEdit(); Assert.Equal(AccentPreset.Blue, vm.SelectedAccentPreset);
                vm.SelectedAccentPreset = AccentPreset.Rose; Check("reopened-preview", AccentPreset.Rose);
                vm.CancelCommand.Execute(null); Check("reopened-cancel", AccentPreset.Blue);
                ApplicationSettingsManager reloaded = new(store, theme);
                reloaded.InitializeAsync().GetAwaiter().GetResult(); Check("reload", AccentPreset.Blue);
                Assert.Equal(AccentPreset.Blue, reloaded.Current.AccentPreset);
                Window reopened = Phase13UiTests.LoadLooseWindow("SettingsWindow.xaml");
                reopened.Resources = new ResourceDictionary(); reopened.Owner = main; reopened.Left = -10000; reopened.ShowActivated = false;
                iconWindows.Add(reopened); reopened.Show();
                Assert.NotSame(factory, reopened.Icon); Assert.NotSame(mainIcon, reopened.Icon);
                Check("new-window-after-save", AccentPreset.Blue);
            }
            finally
            {
                foreach (Window window in iconWindows.Skip(1)) { window.Close(); }
                reference.Close(); main.Close();
                theme.Apply(AccentPreset.Violet); // No stale window/native-handle callbacks after closure.
                application.Shutdown();
            }

            void Check(string phase, AccentPreset preset)
            {
                InputSettingsPolishTests.Settle(main); InputSettingsPolishTests.Settle(settings);
                Assert.Same(mainIcon, main.Icon); Assert.Same(settingsIcon, settings.Icon);
                DrawingImage expected = factory.CloneCurrentValue();
                DrawingGroup group = (DrawingGroup)expected.Drawing;
                ((GeometryDrawing)group.Children[0]).Brush = ((Brush)resources["AccentSubtleBrush"]).CloneCurrentValue();
                ((GeometryDrawing)group.Children[1]).Pen.Brush = ((Brush)resources["AccentBrush"]).CloneCurrentValue();
                ((GeometryDrawing)group.Children[2]).Brush = ((Brush)resources["PrimaryTextBrush"]).CloneCurrentValue();
                expected.Freeze(); reference.Icon = expected; InputSettingsPolishTests.Settle(reference);
                string? folder = Environment.GetEnvironmentVariable("GLYPHFRAME_NATIVE_ICON_EVIDENCE");
                foreach (Window window in iconWindows)
                {
                    foreach (int kind in new[] { 0, 1 }) // Both actual small and large WM_GETICON images.
                    {
                        BitmapSource actual = NativeIconPixels(window, kind), wanted = NativeIconPixels(reference, kind);
                        bool same = actual.PixelWidth == wanted.PixelWidth && actual.PixelHeight == wanted.PixelHeight
                            && PixelBytes(actual).SequenceEqual(PixelBytes(wanted));
                        string name = ReferenceEquals(window, main) ? "main" : ReferenceEquals(window, settings) ? "settings" : "reopened-settings";
                        Color drawingColor = ((SolidColorBrush)((GeometryDrawing)((DrawingGroup)((DrawingImage)window.Icon).Drawing).Children[1]).Pen.Brush).Color;
                        measurements.Add(new { Phase = phase, Preset = preset.ToString(), Window = name, Kind = kind, DrawingFrame = drawingColor.ToString(), ExpectedFrame = theme.GetPrimaryColorHex(preset), actual.PixelWidth, actual.PixelHeight, Match = same });
                        if (folder is not null)
                        {
                            Save(actual, Path.Combine(folder, $"{check}-{phase}-{preset}-{name}-{kind}-actual.png"));
                            Save(wanted, Path.Combine(folder, $"{check}-{phase}-{preset}-{name}-{kind}-expected.png"));
                            File.WriteAllText(Path.Combine(folder, "native-icon-readback.json"), System.Text.Json.JsonSerializer.Serialize(measurements, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        }
                        Assert.True(same, $"{phase}/{preset}/{name}/icon-{kind}: native pixels differ; drawing={drawingColor}");
                    }
                }
                check++;
            }
        });
    }

    private static void RunIconTestInOwnProcess()
    {
        string folder = Environment.GetEnvironmentVariable("GLYPHFRAME_NATIVE_ICON_EVIDENCE")
            ?? Path.Combine(Path.GetTempPath(), "GlyphFrame.NativeIconTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        ProcessStartInfo start = new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add("vstest"); start.ArgumentList.Add(typeof(LogoAccentTests).Assembly.Location);
        start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName=GlyphFrame.App.Tests.LogoAccentTests.NativeWindowIcons_RenderAllAccentsAndPreviewRollbackSaveWithoutRestart");
        start.ArgumentList.Add("/Logger:trx;LogFileName=isolated-icon-" + Guid.NewGuid().ToString("N") + ".trx");
        start.ArgumentList.Add("/ResultsDirectory:" + folder);
        start.Environment["GLYPHFRAME_ICON_TEST_CHILD"] = "1";
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(), errors = process.StandardError.ReadToEndAsync();
        try
        {
            Assert.True(process.WaitForExit(45000), "Isolated application/icon test did not finish.");
            string log = output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(folder, "isolated-icon.log"), log);
            Assert.True(process.ExitCode == 0, log);
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
    }

    private static BitmapSource NativeIconPixels(Window window, int kind)
    {
        // Read WPF-owned HICONs, never destroy them. Copy pixels before the next
        // accent update, which may legitimately dispose/replace those handles.
        IntPtr handle = SendMessage(new WindowInteropHelper(window).Handle, 0x007F, new IntPtr(kind), IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, handle);
        BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        return new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);
    }
    private static byte[] PixelBytes(BitmapSource bitmap)
    { byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0); return pixels; }
    private static void Save(BitmapSource bitmap, string path)
    { PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using FileStream file = File.Create(path); encoder.Save(file); }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static BitmapSource Pixels(Image artwork)
    {
        // Render the actual Image visual at its populated view's arranged size.
        RenderTargetBitmap bitmap = new((int)Math.Round(artwork.ActualWidth), (int)Math.Round(artwork.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        DrawingVisual visual = new();
        using (DrawingContext drawing = visual.RenderOpen())
        { drawing.DrawRectangle(new VisualBrush(artwork), null, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)); }
        bitmap.Render(visual); return bitmap;
    }
    private static Color At(BitmapSource bitmap, double x, double y)
    {
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect((int)(x * bitmap.PixelWidth / 32), (int)(y * bitmap.PixelHeight / 32), 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
    private static int NeutralCount(BitmapSource bitmap, Color neutral)
    {
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        int count = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        { if (pixels[i] == neutral.B && pixels[i + 1] == neutral.G && pixels[i + 2] == neutral.R && pixels[i + 3] == 255) { count++; } }
        return count;
    }
    private sealed class Validation : IFfmpegValidationService
    {
        public Task<FfmpegValidationResult> ValidateAsync(bool forceRefresh = false, CancellationToken cancellationToken = default) => Task.FromResult(
            new FfmpegValidationResult("tools", new(FfmpegToolKind.Ffmpeg, FfmpegToolValidationStatus.Available, "ffmpeg", "test", "Available", "diagnostic"),
                new(FfmpegToolKind.Ffprobe, FfmpegToolValidationStatus.Available, "ffprobe", "test", "Available", "diagnostic"), DateTimeOffset.UtcNow));
    }
    private sealed class Details : IErrorDetailsDialogService { public void Show(ErrorPresentation presentation) { } }
}
