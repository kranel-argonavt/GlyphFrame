using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.Views;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class ExportStatusAnimationTests
{
    [Fact]
    public void ReducedMotion_AppliesProgressAndStageTextImmediately()
    {
        RunOnStaThread(() =>
        {
            ProgressBar progressBar = new();
            ExportStatusAnimation.SetAnimationsEnabled(progressBar, false);
            ExportStatusAnimation.SetProgressGeneration(progressBar, 1);
            ExportStatusAnimation.SetProgressStage(progressBar, VideoExportStage.ProcessingFrames);
            ExportStatusAnimation.SetProgressIsIndeterminate(progressBar, false);
            ExportStatusAnimation.SetProgressValue(progressBar, 48.5);

            Assert.False(progressBar.IsIndeterminate);
            Assert.Equal(48.5, progressBar.Value);
            Assert.False(progressBar.HasAnimatedProperties);

            TextBlock stageText = new();
            ExportStatusAnimation.SetAnimationsEnabled(stageText, false);
            ExportStatusAnimation.SetStage(stageText, VideoExportStage.Preparing);
            ExportStatusAnimation.SetStageText(stageText, "Preparing export...");
            ExportStatusAnimation.SetStage(stageText, VideoExportStage.ProcessingFrames);
            ExportStatusAnimation.SetStageText(stageText, "Decoding, rendering, and encoding frames...");

            Assert.Equal("Decoding, rendering, and encoding frames...", stageText.Text);
            Assert.Equal(1d, stageText.Opacity);
            Assert.False(stageText.HasAnimatedProperties);
        });
    }

    [Fact]
    public void ReducedMotion_DisablesRingMotionAndShowsCompletionImmediately()
    {
        RunOnStaThread(() =>
        {
            ExportActivityIndicator indicator = new()
            {
                AnimationsEnabled = false,
                IsActive = true,
                PercentageText = "64%"
            };
            Path ring = Assert.IsType<Path>(indicator.FindName("RingSegments"));
            RotateTransform rotation = Assert.IsType<RotateTransform>(indicator.FindName("RingRotation"));
            Viewbox completion = Assert.IsType<Viewbox>(indicator.FindName("CompletionPresenter"));
            TextBlock percentage = Assert.IsType<TextBlock>(indicator.FindName("PercentagePresenter"));

            Assert.Equal(0d, rotation.Angle);
            Assert.False(rotation.HasAnimatedProperties);
            Assert.False(ring.HasAnimatedProperties);
            Assert.Equal("64%", percentage.Text);

            indicator.IsCompleted = true;

            Assert.Equal(Visibility.Collapsed, ring.Visibility);
            Assert.Equal(Visibility.Collapsed, percentage.Visibility);
            Assert.Equal(Visibility.Visible, completion.Visibility);
            Assert.Equal(1d, completion.Opacity);
            Assert.False(completion.HasAnimatedProperties);
        });
    }

    [Fact]
    public void SpinnerOnlyOverlay_HidesPercentageWithoutChangingDetailedIndicatorOrReducedMotion()
    {
        RunOnStaThread(() =>
        {
            ExportActivityIndicator overlay = new()
            {
                ShowPercentageText = false,
                AnimationsEnabled = false,
                IsActive = true,
                PercentageText = "64%"
            };
            TextBlock overlayPercentage = Assert.IsType<TextBlock>(overlay.FindName("PercentagePresenter"));
            Path overlayRing = Assert.IsType<Path>(overlay.FindName("RingSegments"));
            RotateTransform rotation = Assert.IsType<RotateTransform>(overlay.FindName("RingRotation"));
            Assert.Equal(Visibility.Collapsed, overlayPercentage.Visibility);
            Assert.Equal(string.Empty, overlayPercentage.Text);
            Assert.Equal(Visibility.Visible, overlayRing.Visibility);
            Assert.False(rotation.HasAnimatedProperties);

            ExportActivityIndicator detailed = new() { AnimationsEnabled = false, PercentageText = "64%" };
            TextBlock detailedPercentage = Assert.IsType<TextBlock>(detailed.FindName("PercentagePresenter"));
            Assert.Equal(Visibility.Visible, detailedPercentage.Visibility);
            Assert.Equal("64%", detailedPercentage.Text);

            overlay.ShowPercentageText = true;
            Assert.Equal(Visibility.Visible, overlayPercentage.Visibility);
            Assert.Equal("64%", overlayPercentage.Text);
        });
    }

    [Theory]
    [InlineData(54)]
    [InlineData(64)]
    [InlineData(72)]
    public void RingSegments_HaveEqualAngularLengthAndSpacingAtBothSizes(int diameter)
    {
        RunOnStaThread(() =>
        {
            ExportActivityIndicator indicator = new() { Width = diameter, Height = diameter };
            indicator.ApplyTemplate();
            indicator.Measure(new Size(diameter, diameter));
            indicator.Arrange(new Rect(0d, 0d, diameter, diameter));
            indicator.UpdateLayout();

            Path ring = Assert.IsType<Path>(indicator.FindName("RingSegments"));
            Assert.True(ring.ActualWidth > 0d, "The ring must be arranged before its geometry is inspected.");
            PathGeometry geometry = Assert.IsType<PathGeometry>(ring.Data);
            Assert.True(geometry.IsFrozen);
            Assert.Equal(4, geometry.Figures.Count);

            Point center = new(ring.ActualWidth / 2d, ring.ActualHeight / 2d);
            double radius = (ring.ActualWidth - ring.StrokeThickness) / 2d;
            double? previousStartAngle = null;
            foreach (PathFigure figure in geometry.Figures)
            {
                ArcSegment arc = Assert.IsType<ArcSegment>(Assert.Single(figure.Segments));
                Assert.Equal(radius, arc.Size.Width, 6);
                Assert.Equal(radius, arc.Size.Height, 6);
                Assert.Equal(SweepDirection.Clockwise, arc.SweepDirection);

                double startAngle = AngleFromCenter(center, figure.StartPoint);
                double endAngle = AngleFromCenter(center, arc.Point);
                Assert.Equal(70d, (endAngle - startAngle + 360d) % 360d, 6);
                if (previousStartAngle is double previous)
                {
                    Assert.Equal(90d, (startAngle - previous + 360d) % 360d, 6);
                }

                previousStartAngle = startAngle;
            }
        });
    }

    private static double AngleFromCenter(Point center, Point point) =>
        Math.Atan2(point.Y - center.Y, point.X - center.X) * 180d / Math.PI;

    [Fact]
    public void WorkingRing_LoadPercentageCompletionReducedMotionHideAndUnloadReleaseClocks()
    {
        RunOnStaThread(() =>
        {
            ExportActivityIndicator indicator = new() { Width = 54, Height = 54, IsActive = true, AnimationsEnabled = true };
            Window host = new() { Content = indicator, SizeToContent = SizeToContent.WidthAndHeight, Left = -10000, ShowActivated = false };
            try
            {
                host.Show(); host.UpdateLayout();
                Path ring = (Path)indicator.FindName("RingSegments");
                RotateTransform rotation = (RotateTransform)indicator.FindName("RingRotation");
                Viewbox completion = (Viewbox)indicator.FindName("CompletionPresenter");
                Assert.True(rotation.HasAnimatedProperties); Assert.True(ring.HasAnimatedProperties);
                foreach (string label in new[] { "0%", "1%", "9%", "10%", "76%", "99%", "100%" })
                {
                    indicator.PercentageText = label; host.UpdateLayout(); Assert.True(rotation.HasAnimatedProperties);
                    Assert.Equal(54, indicator.RenderSize.Width, 6); Assert.Equal(54, indicator.RenderSize.Height, 6);
                }
                indicator.AnimationsEnabled = false;
                Assert.False(rotation.HasAnimatedProperties); Assert.False(ring.HasAnimatedProperties);
                indicator.IsCompleted = true;
                Assert.Equal(Visibility.Visible, completion.Visibility); Assert.Equal(1, completion.Opacity); Assert.False(completion.HasAnimatedProperties);
                indicator.IsCompleted = false; indicator.AnimationsEnabled = true;
                Assert.True(rotation.HasAnimatedProperties);
                host.Hide(); Assert.False(rotation.HasAnimatedProperties); Assert.False(ring.HasAnimatedProperties);
                host.Show(); Assert.True(rotation.HasAnimatedProperties);
                indicator.IsCompleted = true; Assert.False(rotation.HasAnimatedProperties); Assert.True(completion.HasAnimatedProperties);
                host.Content = null;
                Assert.False(rotation.HasAnimatedProperties); Assert.False(ring.HasAnimatedProperties); Assert.False(completion.HasAnimatedProperties);
            }
            finally { host.Close(); }
        });
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The WPF presentation assertion did not complete.");
        failure?.Throw();
    }
}
