using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Text.Json;
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
                "Render, preview, and export your results in one workspace.", "Created by Nazar Khozin", "Privacy",
                "GlyphFrame code: MIT License. Third-party components retain their own licenses.",
                "Processing runs locally on your computer. GlyphFrame does not upload your media or send telemetry.",
                "Third-party software", "Includes FFmpeg and SkiaSharp. See the third-party notices for details." })
            { Assert.Contains(expected, contents); }
            Button close = InputSettingsPolishTests.Descendants(window).OfType<Button>().Single(b => b.Content as string == "Close");
            Button notices = InputSettingsPolishTests.Descendants(window).OfType<Button>().Single(b => b.Content as string == "Third-party notices");
            Button author = InputSettingsPolishTests.Descendants(window).OfType<Button>().Single(b => b.Content as string == "GitHub: kranel-argonavt");
            Assert.Equal("https://github.com/kranel-argonavt", author.ToolTip);
            Assert.Equal("Open Nazar Khozin's GitHub profile in your browser", AutomationProperties.GetName(author));
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
        {
            Assert.Same(owner, actualOwner); Assert.StartsWith(shipped, text);
            Assert.Contains("Copyright (c) 2026 Nazar Khozin (kranel-argonavt)", text);
            Assert.Contains("===== licenses/SkiaSharp/THIRD-PARTY-NOTICES.txt =====", text);
            Assert.Contains("===== licenses/XmpCore/LICENSE.txt =====", text);
            Assert.Contains("Copyright (c) 2009, Adobe Systems Incorporated  All rights reserved.", text);
            Assert.Contains("Neither the name of Adobe Systems Incorporated", text);
            Assert.Contains("===== licenses/XmpCore/ATTRIBUTIONS.txt =====", text);
            Assert.Contains("===== licenses/FFmpeg/GCC-RUNTIME-EXCEPTION.txt =====", text);
            Assert.Contains("===== licenses/FFmpeg/IJG-NOTICES.txt =====", text);
            Assert.Contains("This software is based in part on the work of the Independent JPEG Group.", text);
            Assert.Contains("Version: 9.0-glyphframe.2", text);
            Assert.Contains("Initially ported by Yakov Danila and Nathanael Jones", text);
            Assert.Contains("XmpCore 6.1.10.1 - BSD-3-Clause license provenance", text);
            opens++;
        });
        try
        {
            Assert.Equal(0, opens);
            Assert.Contains("could not be read", service.Show(owner)); Assert.Equal(0, opens);
            CopyLegalDocuments(directory);
            Assert.Null(service.Show(owner)); Assert.Equal(1, opens);
            using (FileStream locked = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            { Assert.Contains("present and readable beside GlyphFrame", service.Show(owner)); Assert.Equal(1, opens); }
            Assert.Null(service.Show(owner)); Assert.Equal(2, opens);
        }
        finally { owner.Close(); Directory.Delete(directory, recursive: true); }
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("invalid-json")]
    [InlineData("traversal")]
    [InlineData("missing-xmp-license")]
    [InlineData("changed-xmp-license")]
    [InlineData("missing-xmp-attributions")]
    [InlineData("changed-xmp-attributions")]
    [InlineData("missing-ffmpeg-runtime")]
    [InlineData("changed-ffmpeg-runtime")]
    [InlineData("missing-ffmpeg-ijg")]
    [InlineData("changed-ffmpeg-ijg")]
    public void Notices_RejectIncompleteAlteredOrUnsafeLegalCollection(string fault) => InputSettingsPolishTests.Sta(() =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "GlyphFrame-notices-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Window owner = new();
        int opens = 0;
        ThirdPartyNoticesService service = new(directory, (_, _) => opens++);
        try
        {
            CopyLegalDocuments(directory);
            string license = Path.Combine(directory, "licenses", "SkiaSharp", "LICENSE.txt");
            if (fault.Contains("xmp", StringComparison.Ordinal))
            {
                license = Path.Combine(directory, "licenses", "XmpCore",
                    fault.EndsWith("attributions", StringComparison.Ordinal) ? "ATTRIBUTIONS.txt" : "LICENSE.txt");
            }
            string manifest = Path.Combine(directory, "licenses", "manifest.json");
            if (fault.Contains("ffmpeg", StringComparison.Ordinal))
            {
                license = Path.Combine(directory, "licenses", "FFmpeg",
                    fault.EndsWith("ijg", StringComparison.Ordinal) ? "IJG-NOTICES.txt" : "GCC-RUNTIME-EXCEPTION.txt");
            }
            switch (fault)
            {
                case "missing":
                case "missing-xmp-license":
                case "missing-xmp-attributions": File.Delete(license); break;
                case "missing-ffmpeg-runtime":
                case "missing-ffmpeg-ijg": File.Delete(license); break;
                case "changed":
                case "changed-xmp-license":
                case "changed-xmp-attributions": File.AppendAllText(license, "changed license"); break;
                case "changed-ffmpeg-runtime":
                case "changed-ffmpeg-ijg": File.AppendAllText(license, "changed license"); break;
                case "invalid-json": File.WriteAllText(manifest, "invalid"); break;
                case "traversal":
                    File.WriteAllText(manifest, File.ReadAllText(manifest)
                        .Replace("licenses/SkiaSharp/LICENSE.txt", "licenses/../LICENSE", StringComparison.Ordinal)); break;
            }
            Assert.Contains("could not be read", service.Show(owner));
            Assert.Equal(0, opens);
        }
        finally { owner.Close(); Directory.Delete(directory, recursive: true); }
    });

    [Fact]
    public void Notices_XmpBindingIncludesRecoveredTermsWithoutApprovingOtherDistributionGates()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "licenses", "manifest.json")));
        JsonElement root = manifest.RootElement;
        JsonElement xmp = root.GetProperty("Components").EnumerateArray()
            .Single(component => component.GetProperty("Dependency").GetString() == "XmpCore/6.1.10.1");
        string[] documents = xmp.GetProperty("Documents").EnumerateArray().Select(document => document.GetString()!).ToArray();
        Assert.Equal(new[] { "licenses/XmpCore/LICENSE.txt", "licenses/XmpCore/ATTRIBUTIONS.txt", "licenses/XmpCore/PROVENANCE.txt" }, documents);
        Assert.Contains("BSD-3-Clause", xmp.GetProperty("Status").GetString());
        Assert.False(root.GetProperty("PublicDistributionApproved").GetBoolean());
        string[] issues = root.GetProperty("OpenIssues").EnumerateArray().Select(issue => issue.GetString()!).ToArray();
        Assert.DoesNotContain(issues, issue => issue.Contains("XmpCore", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => issue.Contains("FFmpeg", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Contains("Windows-native", StringComparison.Ordinal));
    }

    [Fact]
    public void Notices_ControlledToolsBindExactPairAndCompleteAdditionalDocuments()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "licenses", "manifest.json")));
        JsonElement root = manifest.RootElement;
        JsonElement tool = root.GetProperty("ControlledFfmpeg");
        Assert.Equal("9.0-glyphframe.2", tool.GetProperty("Version").GetString());
        Assert.Equal("a01e45417cfa050c11151a2bf609e983408b8fa688e9d6ab66cb223eba9e7dbe", tool.GetProperty("FfmpegSha256").GetString());
        Assert.Equal("e9fde8cdd23dd3534d15ff49b6788f80328f916634ae81bf7f8b9331da4ee2a9", tool.GetProperty("FfprobeSha256").GetString());
        string[] documents = tool.GetProperty("Documents").EnumerateArray().Select(item => item.GetString()!).ToArray();
        Assert.Equal(12, documents.Length);
        Assert.Equal(documents.Length, documents.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("licenses/FFmpeg/IJG-NOTICES.txt", documents);
        Assert.Contains("licenses/FFmpeg/WINPTHREADS-COPYING.txt", documents);
        string[] indexed = root.GetProperty("Files").EnumerateArray().Select(item => item.GetProperty("Path").GetString()!).ToArray();
        Assert.All(documents, path => Assert.Contains(path, indexed));
        Assert.False(root.GetProperty("PublicDistributionApproved").GetBoolean());
    }

    private static void CopyLegalDocuments(string directory)
    {
        string root = AppContext.BaseDirectory;
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "licenses", "manifest.json")));
        foreach (string relative in manifest.RootElement.GetProperty("Files").EnumerateArray()
            .Select(file => file.GetProperty("Path").GetString()!).Append("licenses/manifest.json"))
        {
            string target = Path.Combine(directory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(root, relative), target, overwrite: true);
        }
    }

    [Fact]
    public void Notices_ViewerIsReadOnlyCompleteSelectableWrappedAndScrollable() => InputSettingsPolishTests.Sta(() =>
    {
        string content = string.Empty;
        Window owner = new();
        try { Assert.Null(new ThirdPartyNoticesService(AppContext.BaseDirectory, (_, text) => content = text).Show(owner)); }
        finally { owner.Close(); }
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
