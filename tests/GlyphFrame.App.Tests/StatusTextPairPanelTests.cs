using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class StatusTextPairPanelTests
{
    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public void MeasuredBoundary_ReflowsBothDirectionsWithoutTruncation(double fontSize) => Sta(() =>
    {
        TextBlock leading = new() { Text = "Rendering video...", FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
        TextBlock trailing = new() { Text = "Frame 2,000,000 / 2,000,000", FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
        StatusTextPairPanel panel = new(); panel.Children.Add(leading); panel.Children.Add(trailing);
        leading.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); trailing.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double boundary = leading.DesiredSize.Width + trailing.DesiredSize.Width + 12;
        foreach (double width in new[] { boundary + 1, boundary, boundary - 0.25, 100, boundary - 0.25, boundary, boundary + 1 })
        {
            panel.Measure(new Size(width, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height)); panel.UpdateLayout();
            Rect left = leading.TransformToAncestor(panel).TransformBounds(new Rect(leading.RenderSize));
            Rect right = trailing.TransformToAncestor(panel).TransformBounds(new Rect(trailing.RenderSize));
            Assert.InRange(right.Right, width - 0.01, width + 0.01);
            if (width >= boundary) { Assert.InRange(right.Top + right.Height / 2 - left.Top - left.Height / 2, -0.01, 0.01); Assert.True(left.Right + 11.99 <= right.Left); }
            else { Assert.InRange(right.Top - left.Bottom, 3.99, 4.01); }
            Assert.InRange(Math.Max(left.Bottom, right.Bottom), panel.ActualHeight - 0.01, panel.ActualHeight + 0.01);
            Assert.True(left.Left >= 0 && right.Left >= 0 && left.Right <= width + 0.01 && right.Right <= width + 0.01);
            Assert.Equal(TextTrimming.None, leading.TextTrimming); Assert.Equal(TextTrimming.None, trailing.TextTrimming);
        }
    });

    [Fact]
    public void CollapsedMetadata_RemovesItsGapAndReturnsFullWidthToGuidance() => Sta(() =>
    {
        TextBlock guidance = new() { Text = "Committing the managed review result (cannot cancel)...", TextWrapping = TextWrapping.Wrap };
        TextBlock metadata = new() { Text = "Remaining about 100:00:00", TextWrapping = TextWrapping.Wrap };
        StatusTextPairPanel panel = new(); panel.Children.Add(guidance); panel.Children.Add(metadata);
        foreach (double width in new[] { 100d, 300, 700, 100 })
        {
            metadata.Visibility = Visibility.Collapsed;
            panel.Measure(new Size(width, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
            Assert.Equal(guidance.DesiredSize.Height, panel.DesiredSize.Height); Assert.Equal(width, guidance.ActualWidth);
            metadata.Visibility = Visibility.Visible;
            panel.Measure(new Size(width, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
            Assert.True(metadata.ActualHeight > 0); Assert.Contains("cannot cancel", guidance.Text);
        }
    });

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public void Footer_ReservesMeasuredTimeAndWrapsOnlyGuidanceAcrossBoundary(double fontSize) => Sta(() =>
    {
        TextBlock stage = new() { Text = "Decoding, rendering, and encoding frames…", FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
        TextBlock time = new() { Text = "Remaining about 00:00:05", FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
        StatusTextPairPanel panel = new() { WrapLeadingText = true }; panel.Children.Add(stage); panel.Children.Add(time);
        stage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); time.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double boundary = stage.DesiredSize.Width + 12 + time.DesiredSize.Width;
        double lineHeight = stage.DesiredSize.Height, timeWidth = time.DesiredSize.Width;
        foreach (double width in new[] { boundary + 1, boundary - 1, boundary - 60, boundary - 1, boundary + 1 })
        {
            panel.Measure(new Size(width, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height)); panel.UpdateLayout();
            Rect left = stage.TransformToAncestor(panel).TransformBounds(new Rect(stage.RenderSize));
            Rect right = time.TransformToAncestor(panel).TransformBounds(new Rect(time.RenderSize));
            Assert.InRange(left.Top, -0.01, 0.01); Assert.InRange(right.Top, -0.01, 0.01);
            Assert.InRange(right.Right, width - 0.01, width + 0.01); Assert.InRange(right.Width, timeWidth - 0.01, timeWidth + 0.01);
            Assert.True(left.Right + 11.99 <= right.Left);
            if (width < boundary) { Assert.True(left.Height > lineHeight); }
            else { Assert.InRange(left.Height, lineHeight - 0.01, lineHeight + 0.01); }
            Assert.InRange(panel.ActualHeight, Math.Max(left.Height, right.Height) - 0.01, Math.Max(left.Height, right.Height) + 0.01);
        }
        time.Visibility = Visibility.Collapsed;
        panel.Measure(new Size(120, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, 120, panel.DesiredSize.Height));
        Assert.Equal(120, stage.ActualWidth); Assert.Equal(stage.ActualHeight, panel.ActualHeight);
    });

    [Fact]
    public void Footer_MetadataWiderThanAvailableSpaceRetainsReadableFullWidthFallback() => Sta(() =>
    {
        TextBlock stage = new() { Text = "Decoding, rendering, and encoding frames…", FontSize = 24, TextWrapping = TextWrapping.Wrap };
        TextBlock time = new() { Text = "Remaining about 100:00:00", FontSize = 24, TextWrapping = TextWrapping.Wrap };
        StatusTextPairPanel panel = new() { WrapLeadingText = true }; panel.Children.Add(stage); panel.Children.Add(time);
        panel.Measure(new Size(180, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, 180, panel.DesiredSize.Height));
        Rect right = time.TransformToAncestor(panel).TransformBounds(new Rect(time.RenderSize));
        Assert.InRange(right.Top - stage.ActualHeight, 3.99, 4.01);
        Assert.InRange(right.Right, 179.99, 180.01); Assert.True(right.Bottom <= panel.ActualHeight + 0.01);
        Assert.Equal(TextTrimming.None, stage.TextTrimming); Assert.Equal(TextTrimming.None, time.TextTrimming);
    });

    [Fact]
    public void Footer_PathologicalMetadataOnlyWidthsUseFullWidthWithoutHeightTower() => Sta(() =>
    {
        TextBlock stage = FooterText("Decoding, rendering, and encoding frames…", 24);
        TextBlock time = FooterText("Remaining about 00:00:05", 24);
        StatusTextPairPanel panel = new() { WrapLeadingText = true }; panel.Children.Add(stage); panel.Children.Add(time);
        time.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double oldBoundary = time.DesiredSize.Width + 12;
        List<object> measurements = []; List<string> failures = [];
        foreach (double width in new[] { 304d, oldBoundary + 0.01, oldBoundary, oldBoundary - 1, oldBoundary + 0.01, 304 })
        {
            panel.Measure(new Size(width, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height)); panel.UpdateLayout();
            Rect left = Bounds(stage, panel), right = Bounds(time, panel);
            TextBlock fullStage = FooterText(stage.Text, 24), fullTime = FooterText(time.Text, 24);
            fullStage.Measure(new Size(width, double.PositiveInfinity)); fullTime.Measure(new Size(width, double.PositiveInfinity));
            double stackHeight = fullStage.DesiredSize.Height + 4 + fullTime.DesiredSize.Height;
            measurements.Add(new { Width = width, Height = panel.ActualHeight, StackReferenceHeight = stackHeight, Leading = left.ToString(), Trailing = right.ToString(), Overlap = left.IntersectsWith(right) });
            if (left.IntersectsWith(right) || Math.Abs(left.Width - width) > 0.01 || Math.Abs(right.Top - left.Bottom - 4) > 0.01
                || Math.Abs(panel.ActualHeight - stackHeight) > 0.01 || right.Right > width + 0.01 || right.Bottom > panel.ActualHeight + 0.01)
            { failures.Add($"width={width}: leading={left}, trailing={right}, height={panel.ActualHeight}, full-width reference={stackHeight}"); }
        }
        string? folder = Environment.GetEnvironmentVariable("GLYPHFRAME_STATUS_LAYOUT_EVIDENCE");
        if (folder is not null) { File.WriteAllText(Path.Combine(folder, "pathological-widths.json"), System.Text.Json.JsonSerializer.Serialize(measurements, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); }
        Assert.Empty(failures);
    });

    [Theory]
    [InlineData(12, false)]
    [InlineData(24, false)]
    [InlineData(12, true)]
    [InlineData(24, true)]
    public void Footer_ReadableColumnBoundaryKeepsOrdinaryWrappingInlineAndStacksBeforeCrowding(double fontSize, bool longGuidance) => Sta(() =>
    {
        TextBlock stage = FooterText(longGuidance
            ? "Cancelling… Finishing the current frame and cleaning up the partial output. Committing the managed review result cannot be cancelled."
            : "Decoding, rendering, and encoding frames…", fontSize);
        TextBlock time = FooterText("Remaining about 00:00:05", fontSize);
        StatusTextPairPanel panel = new() { WrapLeadingText = true }; panel.Children.Add(stage); panel.Children.Add(time);
        foreach (string estimate in new[] { "Remaining about 00:00:05", "Remaining about 123456:59:59", "Remaining about 00:00:05" })
        {
            time.Text = estimate;
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double minimum = panel.MinimumReadableLeadingWidth, timeWidth = time.DesiredSize.Width;
            double boundary = minimum + 12 + timeWidth, oldBoundary = timeWidth + 12;
            Assert.True(minimum >= 5 * fontSize); // A reading column, not one glyph.
            double[] widths = [boundary + 1, boundary + 0.01, boundary, boundary - 0.01, boundary - 1, oldBoundary + 1, oldBoundary + 0.01, oldBoundary, oldBoundary - 1, 304];
            foreach (double width in widths.OrderDescending().Concat(widths.Order()))
            {
                panel.Measure(new Size(width, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height)); panel.UpdateLayout();
                Rect left = Bounds(stage, panel), right = Bounds(time, panel);
                bool inline = width >= boundary;
                Assert.InRange(left.Top, -0.01, 0.01); Assert.InRange(right.Right, width - 0.01, width + 0.01);
                Assert.False(left.IntersectsWith(right));
                Assert.True(left.Left >= 0 && right.Left >= 0 && left.Right <= width + 0.01 && right.Right <= width + 0.01);
                TextBlock stageReference = FooterText(stage.Text, fontSize), timeReference = FooterText(time.Text, fontSize);
                stageReference.Measure(new Size(inline ? width - timeWidth - 12 : width, double.PositiveInfinity));
                timeReference.Measure(new Size(width, double.PositiveInfinity));
                double requiredHeight = inline ? Math.Max(stageReference.DesiredSize.Height, timeReference.DesiredSize.Height)
                    : stageReference.DesiredSize.Height + 4 + timeReference.DesiredSize.Height;
                Assert.InRange(panel.ActualHeight, requiredHeight - 0.01, requiredHeight + 0.01);
                Assert.True(left.Bottom <= panel.ActualHeight + 0.01 && right.Bottom <= panel.ActualHeight + 0.01);
                if (inline)
                {
                    Assert.True(left.Width >= minimum - 0.01); Assert.True(left.Right + 11.99 <= right.Left);
                    Assert.InRange(right.Top, -0.01, 0.01);
                }
                else { Assert.InRange(left.Width, width - 0.01, width + 0.01); Assert.InRange(right.Top - left.Bottom, 3.99, 4.01); }
                Assert.Equal(TextTrimming.None, stage.TextTrimming); Assert.Equal(TextTrimming.None, time.TextTrimming);
            }
        }
        foreach (string absent in new[] { "", "   " })
        {
            time.Text = absent;
            panel.InvalidateMeasure(); // Unhosted probe: process the child change in the next explicit measure.
            panel.Measure(new Size(304, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, 304, panel.DesiredSize.Height));
            Assert.Equal(304, stage.ActualWidth); Assert.Equal(stage.ActualHeight, panel.ActualHeight);
        }
        time.Text = "Remaining about 00:00:05"; time.Visibility = Visibility.Collapsed;
        panel.Measure(new Size(304, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, 304, panel.DesiredSize.Height));
        Assert.Equal(304, stage.ActualWidth); Assert.Equal(stage.ActualHeight, panel.ActualHeight);
        time.Visibility = Visibility.Visible; stage.Text = "Done";
        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double shortWidth = stage.DesiredSize.Width;
        Assert.Equal(shortWidth, panel.MinimumReadableLeadingWidth);
        double shortBoundary = shortWidth + 12 + time.DesiredSize.Width;
        panel.Measure(new Size(shortBoundary, double.PositiveInfinity)); panel.Arrange(new Rect(0, 0, shortBoundary, panel.DesiredSize.Height));
        Assert.InRange(Bounds(time, panel).Top, -0.01, 0.01);
    });

    [Fact]
    public void Footer_ReadableBudgetTracksFontAndActualGlyphsIncludingTextChrome() => Sta(() =>
    {
        TextBlock stage = FooterText("ＷＭ漢字 Decoding, rendering, and encoding frames…", 12);
        stage.Padding = new Thickness(3); stage.Margin = new Thickness(2); stage.FontStyle = FontStyles.Italic;
        TextBlock time = FooterText("Remaining about 00:00:05", 12);
        StatusTextPairPanel panel = new() { WrapLeadingText = true }; panel.Children.Add(stage); panel.Children.Add(time);
        double previousMinimum = 0;
        foreach (double size in new[] { 12d, 24, 12 })
        {
            stage.FontSize = size;
            panel.InvalidateMeasure();
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double minimum = panel.MinimumReadableLeadingWidth;
            Assert.True(minimum >= 5 * size + 10);
            foreach (char letter in stage.Text.Where(c => !char.IsWhiteSpace(c)))
            {
                TextBlock glyph = FooterText(letter.ToString(), size); glyph.FontStyle = stage.FontStyle;
                glyph.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(minimum >= glyph.DesiredSize.Width + 10);
            }
            if (size == 24) { Assert.True(minimum > previousMinimum); }
            previousMinimum = minimum;
        }
    });

    private static TextBlock FooterText(string text, double fontSize) => new() { Text = text, FontFamily = new FontFamily("Segoe UI"), FontSize = fontSize, TextWrapping = TextWrapping.Wrap };
    private static Rect Bounds(FrameworkElement child, FrameworkElement parent) => child.TransformToAncestor(parent).TransformBounds(new Rect(child.RenderSize));

    private static void Sta(Action action)
    {
        ExceptionDispatchInfo? error = null;
        Thread thread = new(() => { try { action(); } catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); } finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(30))); error?.Throw();
    }
}
