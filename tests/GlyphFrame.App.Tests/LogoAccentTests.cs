using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
