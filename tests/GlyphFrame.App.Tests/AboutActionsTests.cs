using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class AboutActionsTests
{
    [Theory]
    [InlineData(520, 800, 13)]
    [InlineData(420, 350, 22)]
    public void About_ContentAndBoundedScrollingKeepCloseReachable(double width, double maximumHeight, double textSize) => InputSettingsPolishTests.Sta(() =>
    {
        Window window = Phase13UiTests.LoadLooseWindow("AboutWindow.xaml");
        string version = new AboutDialogService().VersionText;
        window.DataContext = new { Name = "GlyphFrame", VersionText = version };
        window.Width = width; window.MaxHeight = maximumHeight; window.Left = -10000; window.ShowActivated = false;
        try
        {
            window.Show(); InputSettingsPolishTests.Settle(window);
            foreach (TextBlock text in InputSettingsPolishTests.Descendants(window).OfType<TextBlock>()) { text.FontSize = textSize; }
            foreach (Button button in InputSettingsPolishTests.Descendants(window).OfType<Button>())
            { button.FontSize = textSize; }
            InputSettingsPolishTests.Settle(window);
            string[] contents = InputSettingsPolishTests.Descendants(window).OfType<TextBlock>().Select(t => t.Text).ToArray();
            foreach (string expected in new[] { "GlyphFrame", version, "Turn videos and images into symbol art.",
                "Render, preview, and export your results in one workspace.", "Privacy",
                "Processing runs locally on your computer. GlyphFrame does not upload your media or send telemetry.",
                "Third-party software", "Includes FFmpeg and SkiaSharp. See the third-party notices for details." })
            { Assert.Contains(expected, contents); }
            Button close = InputSettingsPolishTests.Descendants(window).OfType<Button>().Single(b => b.Content as string == "Close");
            Button notices = InputSettingsPolishTests.Descendants(window).OfType<Button>().Single(b => b.Content as string == "Third-party notices");
            ScrollViewer scroll = InputSettingsPolishTests.Descendants(window).OfType<ScrollViewer>().Single();
            Assert.True(close.IsCancel); Assert.True(close.Focus()); Assert.Same(close, System.Windows.Input.Keyboard.FocusedElement);
            Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
            Assert.Equal(0, scroll.ScrollableWidth); Assert.True(window.ActualHeight <= maximumHeight + 1);
            Rect closeBounds = InputSettingsPolishTests.Bounds(close, window);
            Assert.True(closeBounds.Bottom <= window.ActualHeight + 1);
            if (maximumHeight == 350)
            {
                Assert.True(scroll.ScrollableHeight > 0);
                notices.BringIntoView(); InputSettingsPolishTests.Settle(window);
                Rect bounds = InputSettingsPolishTests.Bounds(notices, scroll);
                Assert.InRange(bounds.Top, -1, scroll.ViewportHeight); Assert.True(bounds.Bottom <= scroll.ViewportHeight + 1);
            }
            else { Assert.Equal(0, scroll.ScrollableHeight); Assert.True(window.ActualHeight < maximumHeight - 100); }
            InputSettingsPolishTests.Capture(window, $"about-{width}-{textSize}");
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Notices_ExplicitActivationReadsCompleteInstalledDocumentAndHandlesFailures() => InputSettingsPolishTests.Sta(() =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "GlyphFrame-notices-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "THIRD_PARTY_NOTICES.md");
        string shipped = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.md"));
        int opens = 0; Window owner = new();
        ThirdPartyNoticesService service = new(directory, (actualOwner, text) =>
        { Assert.Same(owner, actualOwner); Assert.Equal(shipped, text); opens++; });
        try
        {
            Assert.Equal(0, opens);
            Assert.Contains("could not be read", service.Show(owner)); Assert.Equal(0, opens);
            File.WriteAllText(path, shipped);
            Assert.Null(service.Show(owner)); Assert.Equal(1, opens);
            using (FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            { Assert.Contains("present and readable beside GlyphFrame", service.Show(owner)); Assert.Equal(1, opens); }
            Assert.Null(service.Show(owner)); Assert.Equal(2, opens);
        }
        finally { owner.Close(); Directory.Delete(directory, recursive: true); }
    });

    [Fact]
    public void Notices_ViewerIsReadOnlyCompleteSelectableWrappedAndScrollable() => InputSettingsPolishTests.Sta(() =>
    {
        string content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.md"));
        Window window = Phase13UiTests.LoadLooseWindow("NoticesWindow.xaml");
        window.DataContext = new { Text = content }; window.Width = 420; window.Height = 350; window.Left = -10000; window.ShowActivated = false;
        try
        {
            window.Show(); InputSettingsPolishTests.Settle(window);
            TextBox document = InputSettingsPolishTests.Descendants(window).OfType<TextBox>().Single();
            Assert.Equal(content, document.Text); Assert.True(document.IsReadOnly); Assert.True(document.Focusable);
            Assert.Equal("Complete third-party notices", AutomationProperties.GetName(document));
            Assert.Equal(TextWrapping.Wrap, document.TextWrapping);
            Assert.Equal(ScrollBarVisibility.Disabled, document.HorizontalScrollBarVisibility);
            document.SelectAll(); Assert.Equal(content, document.SelectedText);
            document.ScrollToEnd(); InputSettingsPolishTests.Settle(window);
            Assert.True(document.VerticalOffset > 0); Assert.Equal(0, document.HorizontalOffset);
            Button close = InputSettingsPolishTests.Descendants(window).OfType<Button>().Single();
            Assert.True(close.IsCancel); Assert.True(InputSettingsPolishTests.Bounds(close, window).Bottom <= window.ActualHeight + 1);
            InputSettingsPolishTests.Capture(window, "notices-text-fixture");
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(620, 13)]
    [InlineData(620, 24)]
    [InlineData(620, 32)]
    [InlineData(720, 18)]
    public void Settings_PersistentActionsAndFeedbackFollowDraftState(double width, double textSize) => InputSettingsPolishTests.Sta(() =>
    {
        SettingsDraftTests.Harness h = SettingsDraftTests.Harness.Create().GetAwaiter().GetResult();
        Window window = Phase13UiTests.LoadLooseWindow("SettingsWindow.xaml");
        window.DataContext = h.ViewModel; window.Width = width; window.Height = 620; window.Left = -10000; window.ShowActivated = false;
        try
        {
            window.Show(); InputSettingsPolishTests.Settle(window);
            SettingsActionsPanel panel = InputSettingsPolishTests.Descendants(window).OfType<SettingsActionsPanel>().Single();
            Button[] buttons = panel.Children.Cast<Button>().ToArray();
            foreach (Button button in buttons) { button.FontSize = textSize; }
            InputSettingsPolishTests.Settle(window);
            Assert.Equal(3, buttons.Length);
            Assert.Same(h.ViewModel.ResetCommand, buttons[0].Command);
            Assert.Same(h.ViewModel.CancelCommand, buttons[1].Command); Assert.Same(h.ViewModel.SaveCommand, buttons[2].Command);
            Assert.Single(InputSettingsPolishTests.Descendants(window).OfType<Button>(), b => b.Command == h.ViewModel.ResetCommand);
            TextBlock feedback = (TextBlock)window.FindName("SettingsFeedback");
            feedback.FontSize = textSize;
            Assert.False(h.ViewModel.HasStatusMessage); Assert.Equal(Visibility.Collapsed, feedback.Visibility);
            Assert.Equal(0, feedback.DesiredSize.Height);
            double initial = panel.TransformToAncestor(window).Transform(new Point()).Y;
            ScrollViewer main = InputSettingsPolishTests.Descendants(window).OfType<ScrollViewer>().Single(s => Grid.GetRow(s) == 1);
            main.ScrollToEnd(); InputSettingsPolishTests.Settle(window);
            Assert.Equal(initial, panel.TransformToAncestor(window).Transform(new Point()).Y);
            AssertActions();
            h.ViewModel.ResetCommand.ExecuteAsync(null).GetAwaiter().GetResult(); InputSettingsPolishTests.Settle(window);
            Assert.True(h.ViewModel.HasStatusMessage); Assert.Equal(Visibility.Visible, feedback.Visibility);
            Assert.Contains("Defaults loaded", feedback.Text); Assert.True(feedback.ActualHeight > 0);
            Assert.Equal(0, h.Store.Saves); Assert.Equal(0, h.Published); AssertActions();
            h.Store.FailSave = true; h.ViewModel.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); InputSettingsPolishTests.Settle(window);
            Assert.Contains("could not be saved", feedback.Text); Assert.Equal(Visibility.Visible, feedback.Visibility); AssertActions();
            Rect feedbackBounds = InputSettingsPolishTests.Bounds(feedback, window);
            Assert.True(feedbackBounds.Top >= 0 && feedbackBounds.Bottom <= InputSettingsPolishTests.Bounds(panel, window).Top);
            InputSettingsPolishTests.Capture(window, $"settings-actions-{width}-{textSize}");
            h.ViewModel.BeginEdit(); InputSettingsPolishTests.Settle(window);
            Assert.Equal(Visibility.Collapsed, feedback.Visibility); Assert.Equal(0, feedback.DesiredSize.Height);

            void AssertActions()
            {
                foreach (Button button in buttons)
                {
                    Rect bounds = InputSettingsPolishTests.Bounds(button, panel);
                    Assert.True(bounds.Left >= -1 && bounds.Right <= panel.ActualWidth + 1);
                    Assert.True(bounds.Top >= -1 && bounds.Bottom <= panel.ActualHeight + 1);
                    Assert.True(InputSettingsPolishTests.Bounds(button, window).Bottom <= window.ActualHeight + 1);
                    TextBlock label = InputSettingsPolishTests.Descendants(button).OfType<TextBlock>().Single();
                    Assert.True(label.DesiredSize.Width <= button.ActualWidth - button.Padding.Left - button.Padding.Right + 1);
                    Assert.True(label.DesiredSize.Height <= button.ActualHeight - button.Padding.Top - button.Padding.Bottom + 1);
                }
                Rect reset = InputSettingsPolishTests.Bounds(buttons[0], panel), cancel = InputSettingsPolishTests.Bounds(buttons[1], panel), save = InputSettingsPolishTests.Bounds(buttons[2], panel);
                Assert.InRange(reset.Left, 0, 1); Assert.InRange(panel.ActualWidth - save.Right, 0, 1);
                Assert.True(cancel.Right + 9 <= save.Left);
                Assert.True(reset.Right + 15 <= cancel.Left || reset.Bottom + 11 <= cancel.Top);
            }
        }
        finally { window.Close(); h.ViewModel.EndEdit(); }
    });

    [Fact]
    public void Settings_ActionBarReflowsAtMeasuredBoundariesInBothDirections() => InputSettingsPolishTests.Sta(() =>
    {
        Window window = Phase13UiTests.LoadLooseWindow("SettingsWindow.xaml");
        window.DataContext = SettingsDraftTests.Harness.Create().GetAwaiter().GetResult().ViewModel;
        window.Left = -10000; window.ShowActivated = false;
        try
        {
            window.Show(); InputSettingsPolishTests.Settle(window);
            SettingsActionsPanel panel = InputSettingsPolishTests.Descendants(window).OfType<SettingsActionsPanel>().Single();
            Button[] buttons = panel.Children.Cast<Button>().ToArray();
            double pair = buttons[1].DesiredSize.Width + 10 + buttons[2].DesiredSize.Width;
            double single = buttons[0].DesiredSize.Width + 16 + pair;
            double[] widths = [single + 1, single - 1, pair + 1, pair - 1, pair + 1, single - 1, single + 1];
            foreach (double width in widths)
            {
                panel.Width = width; InputSettingsPolishTests.Settle(window);
                Rect[] bounds = buttons.Select(b => InputSettingsPolishTests.Bounds(b, panel)).ToArray();
                Assert.All(bounds, b => Assert.True(b.Left >= -1 && b.Right <= panel.ActualWidth + 1));
                if (width >= single) { Assert.InRange(Math.Abs(bounds[0].Top - bounds[1].Top), 0, 1); }
                else { Assert.True(bounds[0].Bottom + 11 <= bounds[1].Top); }
                if (width >= pair) { Assert.True(bounds[1].Right + 9 <= bounds[2].Left); }
                else { Assert.True(bounds[1].Bottom + 11 <= bounds[2].Top); }
            }
        }
        finally { window.Close(); }
    });
}
