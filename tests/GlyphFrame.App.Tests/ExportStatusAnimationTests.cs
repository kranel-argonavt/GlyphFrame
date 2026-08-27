using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GlyphFrame.App.Presentation;
using GlyphFrame.App.Views;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Tests;

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
            Ellipse ring = Assert.IsType<Ellipse>(indicator.FindName("RingSegments"));
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
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The WPF presentation assertion did not complete.");
        failure?.Throw();
    }
}
