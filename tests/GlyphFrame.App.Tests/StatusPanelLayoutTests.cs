using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class StatusPanelLayoutTests
{
    public static IEnumerable<object[]> States => from video in new[] { false, true }
                                                  from state in new[] { "Planned", "Rendering", "Cancelling", "Exporting", "Ready", "Exported", "Cancelled", "Error", "Retained", "Stale" }
                                                  select new object[] { video, state };

    [Theory]
    [MemberData(nameof(States))]
    public void PopulatedStates_UseContentHeightAndApprovedGeometry(bool video, string state) => Sta(() =>
    {
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout();
        using Model model = new(video) { State = state };
        view.DataContext = model;
        Window host = Host(view);
        try
        {
            host.Show();
            foreach (double width in new[] { 1100d, 900, 736, 612, 532, 480, 320, 480, 532, 612, 736, 900, 1100 })
            {
                Layout(view, host, width);
                Border shell = (Border)view.FindName("StatusShell");
                AdaptiveResultPanel panel = Elements<AdaptiveResultPanel>(shell).Single(p => p.IsVisible);
                Close(panel.DesiredSize.Height + 34, shell.ActualHeight);
                Assert.Equal(new Thickness(16), shell.Padding);
                Assert.Equal(new CornerRadius(10), shell.CornerRadius);
                Assert.Empty(Elements<ScrollViewer>(shell));
                foreach (FrameworkElement child in panel.Children.Cast<FrameworkElement>().Where(e => e.IsVisible)) { Contained(child, panel); }
                foreach (TextBlock field in Elements<TextBlock>(shell).Where(e => e.IsVisible)) { Contained(field, shell); }
                FrameworkElement badge = panel.Children.Cast<FrameworkElement>().Single(e => AdaptiveResultPanel.GetRole(e) == AdaptiveResultPanelRole.Status);
                double badgeSize = panel.Family == StatusLayoutFamily.Working ? 54 : 42;
                Close(badgeSize, badge.ActualWidth); Close(badgeSize, badge.ActualHeight);
                FrameworkElement text = panel.Children.Cast<FrameworkElement>().Single(e => AdaptiveResultPanel.GetRole(e) == AdaptiveResultPanelRole.Text);
                Close(12, Bounds(text, panel).Left - Bounds(badge, panel).Right);
                if (model.HasResult)
                {
                    MetricCardsPanel metrics = (MetricCardsPanel)view.FindName(video ? "VideoResultMetrics" : "ImageResultMetrics");
                    ExportActionsPanel actions = (ExportActionsPanel)view.FindName(video ? "VideoResultActions" : "ImageResultActions");
                    Button export = (Button)view.FindName(video ? "VideoExportButton" : "ImageExportButton");
                    Button folder = (Button)view.FindName(video ? "OpenVideoExportFolderButton" : "OpenImageExportFolderButton");
                    Button copy = (Button)view.FindName(video ? "CopyVideoExportPathButton" : "CopyImageExportPathButton");
                    Assert.Same(video ? model.ExportVideoCommand : model.ExportImageCommand, export.Command);
                    Assert.Equal(state != "Stale", export.IsEnabled);
                    Assert.Equal(model.CanOpenExportFolder, folder.IsVisible);
                    Assert.Equal(model.ShowCopyExportPath, copy.IsVisible);
                    Assert.Equal(model.ExportPathCopy.AccessibleName, AutomationProperties.GetName(copy));
                    Assert.NotNull(copy.FocusVisualStyle); Assert.True(copy.Focusable && copy.IsTabStop);
                    Rect m = Bounds(metrics, panel), a = Bounds(actions, panel), t = Bounds(text, panel);
                    if (panel.LayoutMode == StatusLayoutMode.Spacious)
                    {
                        Rect header = Bounds(badge, panel); header.Union(t);
                        Close(CenterY(header), CenterY(m)); Close(CenterY(m), CenterY(a));
                        Close(16, m.Left - header.Right); Close(16, a.Left - m.Right);
                        Assert.True(text.ActualWidth >= 180);
                        Close(Math.Max(header.Height, Math.Max(m.Height, a.Height)), panel.ActualHeight);
                    }
                    else { Assert.True(t.Bottom <= m.Top - 11.9); }
                    if (panel.LayoutMode == StatusLayoutMode.Narrow)
                    {
                        double limit = video ? 360 : 296;
                        Close(Math.Min(limit, panel.ActualWidth), m.Width);
                        Close(m.Left, a.Left); Close(m.Right, a.Right);
                        Close((panel.ActualWidth - m.Width) / 2, m.Left);
                        Close(m.Left, Bounds(badge, panel).Left);
                        FrameworkElement[] cards = metrics.Children.Cast<FrameworkElement>().Where(e => e.IsVisible).ToArray();
                        foreach (FrameworkElement card in cards) { Close(cards[0].ActualWidth, card.ActualWidth); Assert.InRange(card.ActualWidth, card.MinWidth, 112.1); }
                        foreach (var row in cards.GroupBy(c => Bounds(c, metrics).Top))
                        { Close(metrics.ActualWidth / 2, (row.Min(c => Bounds(c, metrics).Left) + row.Max(c => Bounds(c, metrics).Right)) / 2); }
                        if (video && Bounds(cards[2], metrics).Top > 0)
                        { Close(metrics.ActualWidth / 2, Bounds(cards[2], metrics).Left + cards[2].ActualWidth / 2); }
                        Close(12, a.Top - m.Bottom);
                    }
                    else
                    {
                        foreach (FrameworkElement card in metrics.Children) { Assert.InRange(card.ActualWidth, card.MinWidth, 112.1); }
                        if (model.CanOpenExportFolder) { Close(panel.LayoutMode == StatusLayoutMode.Intermediate ? 150 : 252, a.Width); }
                        else { Assert.InRange(a.Width, 150, 252); }
                        if (panel.LayoutMode != StatusLayoutMode.Spacious && (state == "Ready" || state == "Stale"))
                        {
                            if (panel.LayoutMode == StatusLayoutMode.CompactWide) { Close(m.Top + m.Height / 2, a.Top + a.Height / 2); }
                            else { Close(12, a.Top - m.Bottom); Close(panel.ActualWidth, a.Right); }
                        }
                    }
                    if (model.CanOpenExportFolder)
                    {
                        if (panel.LayoutMode != StatusLayoutMode.Narrow)
                        {
                            Rect left = Bounds(badge, panel); left.Union(t); left.Union(m);
                            Close(CenterY(left), CenterY(a));
                        }
                        Rect e = Bounds(export, actions), f = Bounds(folder, actions), c = Bounds(copy, actions);
                        Close(e.Left, f.Left); Close(f.Width, c.Width); Close(8, f.Top - e.Bottom);
                        if (panel.LayoutMode == StatusLayoutMode.Intermediate)
                        { Close(e.Width, f.Width); Close(c.Left, f.Left); Close(8, c.Top - f.Bottom); }
                        else { Close(e.Right, c.Right); Close(f.Top, c.Top); Close(8, c.Left - f.Right); }
                        Assert.True(export.Focus());
                        Assert.True(export.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                        Assert.Same(folder, Keyboard.FocusedElement);
                        Assert.True(folder.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                        Assert.Same(copy, Keyboard.FocusedElement);
                        Assert.NotNull(export.FocusVisualStyle); Assert.NotNull(folder.FocusVisualStyle);
                    }
                    else { Close(export.ActualHeight, actions.ActualHeight); }
                    if (!video)
                    {
                        Assert.Null(view.FindName("ImageFormatSelector"));
                        Assert.Empty(Elements<ComboBox>(panel));
                        Assert.Equal(3, actions.Children.Count);
                        // With no format control, the text uses the whole header
                        // budget, right up to the next group or the shared edge.
                        double right = panel.LayoutMode == StatusLayoutMode.Spacious ? m.Left - 16
                            : panel.LayoutMode == StatusLayoutMode.Intermediate || (model.CanOpenExportFolder && panel.LayoutMode == StatusLayoutMode.CompactWide) ? a.Left - 16
                            : panel.LayoutMode == StatusLayoutMode.Narrow ? m.Right : panel.ActualWidth;
                        Close(right, t.Right);
                    }
                }
                if (state == "Planned")
                {
                    Assert.Empty(Elements<Button>(panel));
                    MetricCardsPanel metrics = Elements<MetricCardsPanel>(panel).Single();
                    Assert.Equal(video ? 2 : 1, metrics.Children.Cast<FrameworkElement>().Count(c => c.IsVisible));
                    if (width == 736) { Assert.True(Bounds(metrics, panel).Left > Bounds(text, panel).Right); Assert.True(shell.ActualHeight < 100); }
                    if (width == 320) { Close(12, Bounds(metrics, panel).Top - Math.Max(Bounds(text, panel).Bottom, Bounds(badge, panel).Bottom)); }
                }
                if (state is "Rendering" or "Cancelling" or "Exporting")
                {
                    Button cancel = Elements<Button>(panel).Single();
                    Assert.Same(model.CancelExportCommand, cancel.Command);
                    Assert.Equal(state != "Cancelling", cancel.IsEnabled);
                    Close(CenterY(Bounds(text, panel)), CenterY(Bounds(badge, panel)));
                    Rect body = Bounds(text, panel); body.Union(Bounds(badge, panel));
                    if (panel.LayoutMode == StatusLayoutMode.CompactWide) { Close(CenterY(body), CenterY(Bounds(cancel, panel))); }
                    else { Close(12, Bounds(cancel, panel).Top - body.Bottom); }
                }
                if (state is "Error" or "Retained") { Assert.Contains(Elements<Button>(panel), b => (string?)b.Content == "Show Details" && b.IsVisible && b.IsEnabled); }
                Capture(view, panel, video, state, width);
            }
        }
        finally { host.Close(); }
    });

    [Theory]
    [InlineData(false, "Ready")]
    [InlineData(true, "Ready")]
    [InlineData(false, "Exported")]
    [InlineData(true, "Exported")]
    [InlineData(false, "Planned")]
    [InlineData(true, "Planned")]
    [InlineData(false, "Rendering")]
    [InlineData(true, "Rendering")]
    public void ContinuousRoundTrips_KeepCompositionUntilItsContentBudgetRequiresReflow(bool video, string state) => Sta(() =>
    {
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout();
        using Model model = new(video) { State = state }; view.DataContext = model;
        Window host = Host(view);
        try
        {
            host.Show(); Dictionary<int, (StatusLayoutMode Mode, double Height)> down = [];
            foreach (int width in Enumerable.Range(320, 781).Reverse().Concat(Enumerable.Range(320, 781)))
            {
                Layout(view, host, width);
                AdaptiveResultPanel panel = Elements<AdaptiveResultPanel>(view).Single(p => p.IsVisible);
                var geometry = (panel.LayoutMode, view.ActualHeight);
                if (down.TryGetValue(width, out var previous)) { Assert.Equal(previous.Mode, geometry.LayoutMode); Close(previous.Height, geometry.ActualHeight); }
                else { down[width] = geometry; }
                foreach (FrameworkElement child in panel.Children.Cast<FrameworkElement>().Where(e => e.IsVisible)) { Contained(child, panel); }
            }
            StatusLayoutMode[] sequence = down.OrderByDescending(x => x.Key).Select(x => x.Value.Mode).Distinct().ToArray();
            Assert.Equal(state == "Exported" ? new[] { StatusLayoutMode.Spacious, StatusLayoutMode.CompactWide, StatusLayoutMode.Intermediate, StatusLayoutMode.Narrow }
                : state == "Ready" ? video
                    ? new[] { StatusLayoutMode.Spacious, StatusLayoutMode.CompactWide, StatusLayoutMode.Below, StatusLayoutMode.Narrow }
                    : new[] { StatusLayoutMode.Spacious, StatusLayoutMode.CompactWide, StatusLayoutMode.Narrow }
                : new[] { StatusLayoutMode.CompactWide, StatusLayoutMode.Narrow }, sequence);
            if (state is "Ready" or "Exported")
            {
                int boundary = down.Where(x => x.Value.Mode == StatusLayoutMode.Spacious).Min(x => x.Key);
                foreach (int width in new[] { boundary + 1, boundary, boundary - 1, boundary, boundary + 1, boundary - 1 })
                {
                    Layout(view, host, width);
                    AdaptiveResultPanel result = Elements<AdaptiveResultPanel>(view).Single(p => p.IsVisible);
                    Assert.Equal(width >= boundary ? StatusLayoutMode.Spacious : StatusLayoutMode.CompactWide, result.LayoutMode);
                }
                Layout(view, host, boundary);
                MetricCardsPanel metrics = (MetricCardsPanel)view.FindName(video ? "VideoResultMetrics" : "ImageResultMetrics");
                Assert.Contains(metrics.Children.Cast<FrameworkElement>(), c => c.ActualWidth < 111.9);
                Assert.All(metrics.Children.Cast<FrameworkElement>(), c => Assert.InRange(c.ActualWidth, c.MinWidth, 112.1));
            }
            string? directory = Environment.GetEnvironmentVariable("GLYPHFRAME_STATUS_LAYOUT_EVIDENCE");
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, $"transitions-{(video ? "video" : "image")}-{state}.json"),
                    System.Text.Json.JsonSerializer.Serialize(down.OrderBy(x => x.Key).GroupBy(x => x.Value.Mode).Select(g => new
                    { Mode = g.Key.ToString(), MinimumPanelWidth = g.Min(x => x.Key), MaximumPanelWidth = g.Max(x => x.Key) }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally { host.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkingIndicator_PercentageRectangleHasRadialClearanceAtEveryLabel(bool rounding) => Sta(() =>
    {
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout();
        using Model model = new(true) { State = "Rendering" }; view.DataContext = model;
        Window host = Host(view); host.UseLayoutRounding = rounding;
        try
        {
            host.Show(); Layout(view, host, 612);
            ExportActivityIndicator indicator = Elements<ExportActivityIndicator>(view).Single(i => i.IsVisible);
            var ring = (System.Windows.Shapes.Path)indicator.FindName("RingSegments");
            TextBlock percentage = (TextBlock)indicator.FindName("PercentagePresenter");
            Size footprint = indicator.RenderSize;
            Assert.InRange(footprint.Width, 53.2, 54.8); Assert.Equal(footprint.Width, footprint.Height);
            List<object> clearances = [];
            foreach (int value in new[] { 0, 1, 9, 10, 45, 76, 99, 100 })
            {
                model.Percent = value; model.Notify(); Layout(view, host, 612);
                Assert.Equal(footprint, indicator.RenderSize);
                Assert.Equal(14, percentage.FontSize); Assert.Equal($"{value}%", percentage.Text);
                PathGeometry geometry = Assert.IsType<PathGeometry>(ring.Data);
                ArcSegment arc = Assert.IsType<ArcSegment>(geometry.Figures[0].Segments[0]);
                Point center = ring.TransformToAncestor(indicator).Transform(new Point(ring.ActualWidth / 2, ring.ActualHeight / 2));
                double interiorRadius = arc.Size.Width - ring.StrokeThickness / 2;
                Rect label = Bounds(percentage, indicator);
                double clearance = new[] { label.TopLeft, label.TopRight, label.BottomLeft, label.BottomRight }
                    .Min(corner => interiorRadius - (corner - center).Length);
                // Full WPF text rectangle (including line-box whitespace), not just
                // its origin/containment. The inner circle is safe at every rotation.
                Assert.True(clearance >= 2, $"{value}%: radial clearance {clearance:F3}, label {label}, radius {interiorRadius:F3}");
                clearances.Add(new
                {
                    Percentage = value,
                    Width = footprint.Width,
                    Height = footprint.Height,
                    FontSize = percentage.FontSize,
                    LabelWidth = label.Width,
                    LabelHeight = label.Height,
                    InteriorRadius = interiorRadius,
                    Clearance = clearance
                });
                Capture(view, Elements<AdaptiveResultPanel>(view).Single(p => p.IsVisible), true, $"Rendering-{value}", 612);
            }
            string? directory = Environment.GetEnvironmentVariable("GLYPHFRAME_STATUS_LAYOUT_EVIDENCE");
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, $"indicator-clearances-rounding-{rounding}.json"),
                    System.Text.Json.JsonSerializer.Serialize(clearances, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally { host.Close(); }
    });

    [Fact]
    public void WorkingIndicator_CentersOnInformationBodyWithSideAndStackedCancel() => Sta(() =>
    {
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout();
        using Model model = new(true) { State = "Rendering" }; view.DataContext = model;
        Window host = Host(view);
        try
        {
            host.Show();
            foreach (bool wrapped in new[] { false, true })
            {
                model.Long = wrapped;
                foreach (double width in new[] { 900d, 612, 480, 476, 475, 320, 475, 476, 612, 900 })
                {
                    Rect? indicatorBefore = null, labelBefore = null;
                    foreach (int percent in new[] { 0, 9, 45, 99, 100 })
                    {
                        model.Percent = percent; model.Notify(); Layout(view, host, width);
                        AdaptiveResultPanel panel = Elements<AdaptiveResultPanel>(view).Single(p => p.IsVisible);
                        FrameworkElement body = panel.Children.Cast<FrameworkElement>().Single(e => AdaptiveResultPanel.GetRole(e) == AdaptiveResultPanelRole.Text);
                        ExportActivityIndicator indicator = Elements<ExportActivityIndicator>(panel).Single();
                        Button cancel = Elements<Button>(panel).Single();
                        Rect circle = Bounds(indicator, panel), information = Bounds(body, panel), action = Bounds(cancel, panel);
                        Capture(view, panel, true, $"Working-center-{wrapped}-{percent}", width);
                        Close(CenterY(information), CenterY(circle));
                        if (panel.LayoutMode == StatusLayoutMode.CompactWide) { Close(CenterY(circle), CenterY(action)); }
                        else { Close(12, action.Top - Math.Max(information.Bottom, circle.Bottom)); Assert.True(CenterY(circle) < CenterY(action)); }
                        TextBlock percentage = (TextBlock)indicator.FindName("PercentagePresenter");
                        Rect label = Bounds(percentage, indicator);
                        Close(27, label.Left + label.Width / 2); Close(27, CenterY(label));
                        Close(54, circle.Width); Close(54, circle.Height); Contained(percentage, indicator);
                        if (indicatorBefore is not null) { Assert.Equal(indicatorBefore.Value, circle); Close(CenterY(labelBefore!.Value), CenterY(label)); }
                        indicatorBefore = circle; labelBefore = label;
                    }
                }
            }
        }
        finally { host.Close(); }
    });

    [Fact]
    public void RegularMetrics_UseMinimumPreferredMaximumBudgetsWithoutMakingMaximumMandatory() => Sta(() =>
    {
        MetricCardsPanel metrics = new();
        foreach (double minimum in new[] { 88d, 100, 88 })
        {
            metrics.Children.Add(new Border
            {
                MinWidth = minimum,
                Padding = new Thickness(10, 8, 10, 8),
                Child = new TextBlock { Text = "1024 × 576", FontSize = 12, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap }
            });
        }
        double low = metrics.MinimumRowWidth, preferred = metrics.PreferredRowWidth, high = metrics.MaximumRowWidth;
        Assert.InRange(preferred, low, high); Assert.True(low < high);
        foreach (double budget in new[] { high + 200, preferred, low, low - 1, low, high })
        {
            metrics.Measure(new Size(budget, double.PositiveInfinity));
            metrics.Arrange(new Rect(0, 0, budget, metrics.DesiredSize.Height)); metrics.UpdateLayout();
            FrameworkElement[] cards = metrics.Children.Cast<FrameworkElement>().ToArray();
            Assert.All(cards, c => { Assert.InRange(c.ActualWidth, c.MinWidth, 112.1); Contained(c, metrics); });
            if (budget >= low) { Assert.All(cards, c => Close(0, Bounds(c, metrics).Top)); }
            else { Assert.True(Bounds(cards[^1], metrics).Top > 0); }
            if (budget >= preferred) { Assert.All(cards, c => Close((preferred - 16) / 3, c.ActualWidth)); }
            if (budget > high) { Assert.True(metrics.DesiredSize.Width < budget - 50); }
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneExport_ContractsBeforeReflowWhilePublishedActionsKeepTheirOwnCompositions(bool video) => Sta(() =>
    {
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout();
        using Model model = new(video) { State = "Ready" }; view.DataContext = model;
        Window host = Host(view);
        try
        {
            host.Show(); Layout(view, host, 900);
            MetricCardsPanel metrics = (MetricCardsPanel)view.FindName(video ? "VideoResultMetrics" : "ImageResultMetrics");
            ExportActionsPanel actions = (ExportActionsPanel)view.FindName(video ? "VideoResultActions" : "ImageResultActions");
            AdaptiveResultPanel panel = (AdaptiveResultPanel)actions.Parent;
            double boundary = metrics.MinimumRowWidth + 16 + actions.MinimumStandaloneWidth + 34;
            List<object> measurements = [];
            foreach (double width in new[] { boundary + 103, boundary + 70, boundary + 1, boundary, boundary - 1, boundary, boundary + 1, boundary + 70, boundary + 103 })
            {
                Layout(view, host, width);
                Rect m = Bounds(metrics, panel), a = Bounds(actions, panel);
                measurements.Add(new { Width = width, Mode = panel.LayoutMode.ToString(), ActionWidth = a.Width, MetricWidth = m.Width });
                Assert.False(actions.HasSecondaryActions);
                if (width >= boundary)
                {
                    Assert.Equal(StatusLayoutMode.CompactWide, panel.LayoutMode);
                    Close(Math.Min(252, width - 34 - metrics.MinimumRowWidth - 16), a.Width);
                    Assert.InRange(a.Width, 150, 252); Close(CenterY(m), CenterY(a));
                    Assert.True(a.Left - m.Right >= 15.9);
                }
                else { Assert.Contains(panel.LayoutMode, new[] { StatusLayoutMode.Below, StatusLayoutMode.Narrow }); Assert.True(a.Top >= m.Bottom + 11.9); }
            }
            // Accessibility grows the actual minimum, including icon, template
            // padding and text. No hidden secondary feedback budget is involved.
            Button primary = (Button)view.FindName(video ? "VideoExportButton" : "ImageExportButton");
            primary.FontSize = 32; model.Notify(); Layout(view, host, 900);
            Assert.True(actions.MinimumStandaloneWidth > 150);
            double scaledBoundary = metrics.MinimumRowWidth + 16 + actions.MinimumStandaloneWidth + 34;
            Layout(view, host, scaledBoundary); Assert.Equal(StatusLayoutMode.CompactWide, panel.LayoutMode);
            Close(actions.MinimumStandaloneWidth, actions.ActualWidth);
            foreach (TextBlock label in Elements<TextBlock>(primary)) { Contained(label, primary); }
            primary.ClearValue(Control.FontSizeProperty);

            model.State = "Exported"; model.Notify();
            foreach (double width in new[] { 1100d, 736, 630, 629, 612, 532, 480, 404, 320, 260, 320, 480, 532, 612, 629, 630, 736, 1100 })
            {
                Layout(view, host, width);
                Assert.True(actions.HasSecondaryActions);
                FrameworkElement[] buttons = actions.Children.Cast<FrameworkElement>().Where(b => b.IsVisible).ToArray();
                if (panel.LayoutMode == StatusLayoutMode.Intermediate)
                {
                    Close(150, actions.ActualWidth); Assert.False(actions.IsHorizontal);
                    Assert.All(buttons, b => Close(150, b.ActualWidth));
                }
                else if (panel.LayoutMode != StatusLayoutMode.Narrow) { Close(252, actions.ActualWidth); Assert.True(actions.IsHorizontal); }
                if (actions.IsHorizontal)
                { Close(buttons[0].ActualWidth, buttons[1].ActualWidth + 8 + buttons[2].ActualWidth); Close(Bounds(buttons[1], actions).Top, Bounds(buttons[2], actions).Top); }
                else { Assert.All(buttons, b => Close(buttons[0].ActualWidth, b.ActualWidth)); }
                foreach (FrameworkElement button in buttons) { Contained(button, actions); foreach (TextBlock label in Elements<TextBlock>(button)) { Contained(label, button); } }
                if (width == 260) { Assert.False(actions.IsHorizontal); } // Pair genuinely cannot fit.
            }
            model.State = "Ready"; model.Notify(); Layout(view, host, 174);
            Assert.Equal(StatusLayoutMode.Narrow, panel.LayoutMode); Assert.True(actions.ActualWidth < 150);
            foreach (TextBlock label in Elements<TextBlock>(primary)) { Contained(label, primary); }
            string? folder = Environment.GetEnvironmentVariable("GLYPHFRAME_STATUS_LAYOUT_EVIDENCE");
            if (folder is not null) { File.WriteAllText(Path.Combine(folder, $"standalone-sizing-{video}.json"), System.Text.Json.JsonSerializer.Serialize(measurements, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); }
        }
        finally { host.Close(); }
    });

    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 12)]
    [InlineData(false, 24)]
    [InlineData(true, 24)]
    [InlineData(false, 160)]
    [InlineData(true, 160)]
    public void MetricCards_UseOneBoundedBudgetIncludingCompactRowsAndExceptionalGlyphs(bool equal, double fontSize) => Sta(() =>
    {
        MetricCardsPanel metrics = new() { EqualColumns = equal };
        foreach (string value in new[] { "16384 × 16384", "123456.78 GB", "Unavailable_日本語_Кириллица" })
        {
            metrics.Children.Add(new Border
            {
                MinWidth = 88,
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = value, FontSize = fontSize, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None }
            });
        }
        double minimum = (metrics.MinimumRowWidth - 16) / 3;
        if (fontSize == 160) { Assert.True(minimum > 112); } else { Assert.Equal(88, minimum); }
        double cap = Math.Max(112, minimum);
        foreach (double width in new[] { 900d, 360, 320, 296, 260, 296, 320, 360, 900 })
        {
            metrics.Measure(new Size(width, double.PositiveInfinity)); metrics.Arrange(new Rect(0, 0, width, metrics.DesiredSize.Height)); metrics.UpdateLayout();
            FrameworkElement[] cards = metrics.Children.Cast<FrameworkElement>().ToArray();
            foreach (FrameworkElement card in cards)
            {
                Assert.InRange(card.ActualWidth, equal ? minimum - 0.11 : 88, cap + 0.11); Contained(card, metrics);
                Assert.All(Elements<TextBlock>(card), t => { Contained(t, card); Assert.Equal(TextTrimming.None, t.TextTrimming); Assert.True(t.ActualHeight + 16 < card.ActualHeight + 0.11); });
            }
            if (equal)
            {
                Assert.All(cards, c => Close(cards[0].ActualWidth, c.ActualWidth));
                foreach (var row in cards.GroupBy(c => Bounds(c, metrics).Top))
                { Close(width / 2, (row.Min(c => Bounds(c, metrics).Left) + row.Max(c => Bounds(c, metrics).Right)) / 2); }
            }
            foreach (var row in cards.GroupBy(c => Bounds(c, metrics).Top)) { Assert.All(row, c => Close(row.First().ActualWidth, c.ActualWidth)); }
            if (fontSize == 160) { Assert.True(cards[^1].ActualWidth > 112); }
            if (width == 900) { Assert.True(cards.Sum(c => c.ActualWidth) + 16 < width); }
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongGuidanceAndValues_WrapWithoutReservationsOrClipping(bool video) => Sta(() =>
    {
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout();
        using Model model = new(video) { State = "Retained", Long = true }; view.DataContext = model;
        Window host = Host(view);
        try
        {
            host.Show();
            foreach (double width in new[] { 320d, 480, 612, 736, 900, 1100 })
            {
                Layout(view, host, width);
                Border shell = (Border)view.FindName("StatusShell");
                foreach (TextBlock text in Elements<TextBlock>(shell).Where(t => t.IsVisible)) { Contained(text, shell); }
                Assert.Empty(Elements<ScrollViewer>(shell));
                FrameworkElement active = Elements<AdaptiveResultPanel>(shell).Single(p => p.IsVisible);
                Close(active.ActualHeight + 34, shell.ActualHeight);
            }
        }
        finally { host.Close(); }
    });

    [Theory]
    [InlineData(320)]
    [InlineData(480)]
    [InlineData(612)]
    [InlineData(736)]
    [InlineData(900)]
    [InlineData(1100)]
    public void NumericProgressAndCopyFeedback_DoNotChangeGeometry(double width) => StaAsync(async () =>
    {
        UserControl view = ReviewSurfaceTests.LoadExportStatusViewForLayout();
        TaskCompletionSource? hold = null;
        CancellationToken feedbackToken = default;
        using Model model = new(true, token => { feedbackToken = token; return hold!.Task; }) { State = "Rendering" };
        view.DataContext = model;
        Window host = Host(view);
        try
        {
            host.Show(); Layout(view, host, width); double height = view.ActualHeight;
            Rect beforeCancel = Bounds(Elements<Button>((Border)view.FindName("ProcessingContent")).Single(), view);
            foreach (int percent in new[] { 0, 1, 9, 10, 76, 99, 100 })
            {
                model.Percent = percent; model.Notify(); Layout(view, host, width); Close(height, view.ActualHeight);
                Assert.Equal(beforeCancel, Bounds(Elements<Button>((Border)view.FindName("ProcessingContent")).Single(), view));
            }
            model.State = "Exported"; model.Notify(); Layout(view, host, width); height = view.ActualHeight;
            Button copy = (Button)view.FindName("CopyVideoExportPathButton"); Rect before = Bounds(copy, view);
            foreach (bool success in new[] { true, false })
            {
                hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
                model.Clipboard.Success = success; Task feedback = model.ExportPathCopy.Command.ExecuteAsync(null);
                Layout(view, host, width); Assert.Equal(success ? "Copied!" : "Copy failed", copy.Content);
                Assert.Equal(before, Bounds(copy, view)); Close(height, view.ActualHeight);
                model.ExportPathCopy.ClearFeedback(); Layout(view, host, width);
                Assert.True(feedbackToken.IsCancellationRequested);
                // Deliberately finish cancellation later: resetting feedback is
                // synchronous, but the command need not have completed yet.
                Assert.False(feedback.IsCompleted);
                Assert.Equal("Copy path", copy.Content);
                Assert.Equal(before, Bounds(copy, view)); Close(height, view.ActualHeight);
                hold.SetCanceled(feedbackToken);
                await feedback.WaitAsync(TimeSpan.FromSeconds(2));
                Layout(view, host, width);
                Assert.Equal("Copy path", copy.Content);
                Assert.Equal(before, Bounds(copy, view)); Close(height, view.ActualHeight);
            }
        }
        finally { host.Close(); }
    });

    private static Window Host(UserControl view) => new() { Content = view, SizeToContent = SizeToContent.WidthAndHeight, Left = -10000, ShowActivated = false };
    private static void Capture(UserControl view, AdaptiveResultPanel panel, bool video, string state, double width)
    {
        string? directory = Environment.GetEnvironmentVariable("GLYPHFRAME_STATUS_LAYOUT_EVIDENCE");
        if (directory is null) { return; }
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"component-{(video ? "video" : "image")}-{state}-{width:F0}");
        RenderTargetBitmap bitmap = new((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(path + ".png")) { encoder.Save(stream); }
        File.WriteAllText(path + ".json", System.Text.Json.JsonSerializer.Serialize(new
        {
            Kind = "WPF natural-height component fixture; not native visual acceptance",
            Media = video ? "video" : "image",
            State = state,
            PanelWidth = view.ActualWidth,
            PanelHeight = view.ActualHeight,
            Composition = panel.LayoutMode.ToString(),
            Groups = panel.Children.Cast<FrameworkElement>().Where(e => e.IsVisible).Select(e => new
            { Role = AdaptiveResultPanel.GetRole(e).ToString(), X = Bounds(e, view).X, Y = Bounds(e, view).Y, Width = e.ActualWidth, Height = e.ActualHeight })
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Layout(UserControl view, Window host, double width)
    { view.Width = width; host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); host.UpdateLayout(); }
    private static IEnumerable<T> Elements<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { DependencyObject child = VisualTreeHelper.GetChild(parent, i); if (child is T match) { yield return match; } foreach (T item in Elements<T>(child)) { yield return item; } }
    }
    private static Rect Bounds(FrameworkElement child, Visual parent) => child.TransformToAncestor(parent).TransformBounds(new Rect(child.RenderSize));
    private static double CenterY(Rect rect) => rect.Top + rect.Height / 2;
    private static void Close(double expected, double actual) => Assert.InRange(actual, expected - 0.11, expected + 0.11);
    private static void Contained(FrameworkElement child, FrameworkElement parent)
    { Rect b = Bounds(child, parent); Assert.True(b.Left >= -0.11 && b.Top >= -0.11 && b.Right <= parent.ActualWidth + 0.11 && b.Bottom <= parent.ActualHeight + 0.11, $"{child.Name} {b} in {parent.RenderSize}"); }
    private static void StaAsync(Func<Task> action) => Sta(() =>
    {
        ExceptionDispatchInfo? failure = null;
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.InvokeAsync(async () =>
        {
            try { await action(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
            finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
        });
        Dispatcher.Run();
        failure?.Throw();
    });

    private static void Sta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() => { try { action(); } catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); } finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(90))); failure?.Throw();
    }

    private sealed class Clipboard : IClipboardService
    { public bool Success = true; public ClipboardWriteResult SetText(string text) => Success ? ClipboardWriteResult.Completed() : ClipboardWriteResult.Failed(); }
    private sealed class Model(bool video, Func<CancellationToken, Task>? feedbackHold = null) : Phase13UiTests.MainLayoutData(video), INotifyPropertyChanged, IDisposable
    {
        public string State = "Ready";
        public bool Long;
        public int Percent = 42;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Notify() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); CancelExportCommand.NotifyCanExecuteChanged(); }
        public bool HasResult => State is "Ready" or "Exported" or "Retained" or "Stale";
        public new bool ShowReadyToRender => State == "Planned";
        public new bool IsExportStatusVisible => State is "Rendering" or "Cancelling" or "Exporting";
        public bool IsExportActive => IsExportStatusVisible;
        public new bool ShowImageResultPanel => !IsVideoSelected && !ShowReadyToRender && !IsExportStatusVisible;
        public new bool ShowVideoResultPanel => IsVideoSelected && !ShowReadyToRender && !IsExportStatusVisible;
        public new bool ShowImageExportControls => !IsVideoSelected && HasResult;
        public new bool HasVideoArtifact => IsVideoSelected && HasResult;
        public new bool ShowImageRenderMetrics => !IsVideoSelected && HasResult;
        public new bool ShowVideoRenderMetrics => IsVideoSelected && HasResult;
        public new bool IsImageResultSuccess => !IsVideoSelected && State is "Ready" or "Exported";
        public new bool IsVideoResultSuccess => IsVideoSelected && State is "Ready" or "Exported";
        public bool IsImageResultWarning => !IsVideoSelected && State is "Stale" or "Cancelled";
        public bool IsVideoResultWarning => IsVideoSelected && State is "Stale" or "Cancelled";
        public bool IsImageResultFailure => !IsVideoSelected && State is "Error" or "Retained";
        public bool IsVideoResultFailure => IsVideoSelected && State is "Error" or "Retained";
        public new bool CanOpenExportFolder => State is "Exported" or "Retained";
        public new bool ShowCopyExportPath => CanOpenExportFolder;
        public new bool ShowImageResultErrorDetails => !IsVideoSelected && State is "Error" or "Retained";
        public new bool ShowVideoResultErrorDetails => IsVideoSelected && State is "Error" or "Retained";
        public new string ReadyToRenderMessage => "Review your settings, then render.";
        public new string ImageResultTitle => Title("Image");
        public new string VideoResultTitle => Title("Video");
        private string Title(string media) => State is "Error" or "Retained" ? "Export failed" : State == "Cancelled" ? "Rendering cancelled" : $"{media} {(State == "Exported" ? "Exported" : "Ready")}";
        public new string ImageResultSummary => Summary;
        public new string VideoResultSummary => Summary;
        private string Summary => Long ? "Another export failed. The previously accepted rendered result and previous export remain available. Choose another destination and try again. 日本語 Кириллица " + new string('Ж', 80)
            : State == "Retained" ? "Another export failed. The rendered result and previous export remain available."
            : State == "Exported" ? "Ready for review and another export." : "Ready for review and export.";
        public new string VideoFileSizeText => Long ? "123456.78 GB" : "906,48 KB";
        public new string VideoOutputDimensionsText => Long ? "16384 × 16384" : "1024 × 576";
        public new string VideoAudioOutcomeText => Long ? "Unavailable_日本語_Кириллица" : "Original";
        public new string ImageOutputDimensionsText => Long ? "16384 × 16384" : "1945 × 1296";
        public new string ImageGridDimensionsText => "194 × 86";
        public new string VideoExportActionLabel => CanOpenExportFolder ? "Export Again" : "Export Video";
        public new string ImageExportActionLabel => CanOpenExportFolder ? "Export Again" : "Export Image";
        public string ExportOperationTitle => State == "Cancelling" ? "Cancelling…" : $"{(State == "Exporting" ? "Exporting" : "Rendering")} {(IsVideoSelected ? "Video" : "Image")}";
        public string ExportProgressText => Long ? "Decoding, rendering, and encoding frames. The selected output is being prepared with the current settings. Please wait while all frames are processed." : "Decoding, rendering, and encoding frames.";
        public string ExportFrameText => $"Frame {Percent * 2} / 284";
        public string ElapsedText => $"Elapsed 00:00:{Percent % 60:00}";
        public string RemainingText => $"Remaining about 00:00:{Percent % 60:00}";
        public string ExportPercentageText => $"{Percent}%";
        public double ExportProgress => Percent;
        public long ExportPresentationGeneration => 1;
        public bool IsExportCompletionVisible => false;
        public bool IsExportProgressIndeterminate => State == "Cancelling";
        public VideoExportStage ExportStage => State == "Cancelling" ? VideoExportStage.Cancelling : VideoExportStage.ProcessingFrames;
        private IRelayCommand? _cancel, _export;
        public IRelayCommand CancelExportCommand => _cancel ??= new RelayCommand(() => { }, () => State != "Cancelling");
        public IRelayCommand ExportVideoCommand => _export ??= new RelayCommand(() => { }, () => State != "Stale");
        public IRelayCommand ExportImageCommand => ExportVideoCommand;
        public IRelayCommand OpenExportFolderCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand ShowImageResultErrorDetailsCommand { get; } = new RelayCommand(() => { });
        public IRelayCommand ShowVideoResultErrorDetailsCommand { get; } = new RelayCommand(() => { });
        public Clipboard Clipboard { get; } = new();
        private ExportPathCopyPresentation? _copy;
        public new ExportPathCopyPresentation ExportPathCopy
        {
            get { if (_copy is null) { _copy = new(Clipboard, feedbackHold); _copy.SetDestination(this, @"C:\status-test\accepted.mp4"); } return _copy; }
        }
        public void Dispose() { _copy?.Dispose(); base.ExportPathCopy.Dispose(); }
    }
}

