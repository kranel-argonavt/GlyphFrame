using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class NativeTermsWindowTests
{
    [Fact]
    public void CompiledDialog_RequiresUncheckedExplicitChoiceAndKeepsWriteFailureRetryable() => InputSettingsPolishTests.Sta(() =>
    {
        using NativeTermsTests.Fixture fixture = new();
        NativeTermsBundle bundle = NativeTermsBundle.Load(fixture.Root)!;
        Window resourceHost = Phase13UiTests.LoadLooseWindow("AboutWindow.xaml");
        int writes = 0;
        NativeTermsWindow window = new(bundle, null, () => { writes++; throw new IOException("synthetic locked record"); }, resourceHost.Resources);
        CheckBox choice = (CheckBox)window.FindName("Acknowledgement");
        Button proceed = (Button)window.FindName("ContinueButton");
        Button decline = (Button)window.FindName("DeclineButton");
        Assert.NotEqual(true, choice.IsChecked); Assert.False(proceed.IsEnabled); Assert.True(decline.IsCancel);
        Assert.Equal(bundle.Text, ((TextBox)window.FindName("TermsText")).Text);
        Assert.Equal("Microsoft component terms — GlyphFrame", window.Title);
        Assert.Contains("version 1.0", ((TextBlock)choice.Content).Text);
        Assert.Equal("Agree & Continue", proceed.Content);
        Assert.DoesNotContain("nonoperative", ((TextBlock)choice.Content).Text, StringComparison.OrdinalIgnoreCase);
        window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            proceed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(0, writes);
            choice.IsChecked = true; Assert.True(proceed.IsEnabled);
            proceed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, writes); Assert.True(window.IsVisible);
            Assert.Equal(Visibility.Visible, ((TextBlock)window.FindName("Feedback")).Visibility);
            Assert.False(File.Exists(Path.Combine(fixture.Data, NativeTermsAcknowledgementStore.FileName)));
            choice.IsChecked = false; Assert.False(proceed.IsEnabled);
            decline.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        try { Assert.False(window.ShowDialog()); }
        finally { window.Close(); resourceHost.Close(); }
    });

    [Theory]
    [InlineData(760, 660, 12)]
    [InlineData(420, 360, 22)]
    public void SmallOrEnlargedDialog_KeepsTermsReadableAndActionsReachable(double width, double height, double textSize) => InputSettingsPolishTests.Sta(() =>
    {
        using NativeTermsTests.Fixture fixture = new();
        Window resourceHost = Phase13UiTests.LoadLooseWindow("AboutWindow.xaml");
        NativeTermsWindow window = new(NativeTermsBundle.Load(fixture.Root), null, () => { }, resourceHost.Resources)
        { Width = width, Height = height, ShowActivated = false };
        try
        {
            window.Show(); InputSettingsPolishTests.Settle(window);
            foreach (TextBlock label in InputSettingsPolishTests.Descendants(window).OfType<TextBlock>()) { label.FontSize = textSize; }
            foreach (Control control in InputSettingsPolishTests.Descendants(window).OfType<Control>()) { control.FontSize = textSize; }
            InputSettingsPolishTests.Settle(window);
            TextBox terms = (TextBox)window.FindName("TermsText");
            Assert.True(terms.IsReadOnly);
            Assert.True(terms.ActualHeight >= 140, $"Readable viewport height: {terms.ActualHeight:R}; required minimum: {terms.MinHeight:R}");
            ScrollViewer viewport = (ScrollViewer)window.FindName("RootViewport");
            Button decline = (Button)window.FindName("DeclineButton");
            decline.BringIntoView(); InputSettingsPolishTests.Settle(window);
            Rect bounds = InputSettingsPolishTests.Bounds(decline, viewport);
            Assert.InRange(bounds.Top, -1, viewport.ActualHeight);
            Assert.True(bounds.Bottom <= viewport.ActualHeight + 1);
        }
        finally { window.Close(); resourceHost.Close(); }
    });

    [Fact]
    public void InvalidScope_HasNoAffirmativePath() => InputSettingsPolishTests.Sta(() =>
    {
        Window resourceHost = Phase13UiTests.LoadLooseWindow("AboutWindow.xaml");
        NativeTermsWindow window = new(null, "Synthetic invalid scope", () => throw new Exception("Must never save"), resourceHost.Resources);
        try
        {
            Assert.Equal("Microsoft component verification failed — GlyphFrame", window.Title);
            Assert.False(((CheckBox)window.FindName("Acknowledgement")).IsEnabled);
            Assert.False(((Button)window.FindName("ContinueButton")).IsEnabled);
            Assert.False(((Button)window.FindName("ReferencesButton")).IsEnabled);
        }
        finally { window.Close(); resourceHost.Close(); }
    });
}
