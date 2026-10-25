using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using GlyphFrame.App.Services;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Tests;

[Collection("WPF layout")]
public sealed class ReviewSeekSliderTests
{
    [Theory]
    [InlineData(9, false)]
    [InlineData(20, false)]
    [InlineData(9, true)]
    [InlineData(20, true)]
    public void InitializationOrder_SynchronizesValueWithoutWritingPosition(double position, bool rangeFirst)
    {
        RunSta(() =>
        {
            PositionModel model = new(position);
            ReviewSeekSlider slider = new();
            if (rangeFirst) slider.Maximum = 20;
            slider.SetBinding(ReviewSeekSlider.PositionProperty, new Binding(nameof(model.Position))
            { Source = model, Mode = BindingMode.TwoWay });
            if (!rangeFirst) slider.Maximum = 20;
            Assert.Equal(position, slider.Value);
            Assert.Equal(position, slider.Position);
            Assert.Equal(0, model.Writes);
            slider.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, 7d);
            Assert.Equal(7, model.Position);
            Assert.NotNull(BindingOperations.GetBindingExpression(slider, ReviewSeekSlider.PositionProperty));
            model.Position = 8;
            Assert.Equal(8, slider.Value);
            Assert.Equal(2, model.Writes); // One user seek, one clock notification; no feedback.
        });
    }

    [Fact]
    public void RangeChanges_ResynchronizeUnchangedPositionWithoutPublishingCoercion()
    {
        RunSta(() =>
        {
            PositionModel model = new(9);
            ReviewSeekSlider slider = new();
            slider.SetBinding(ReviewSeekSlider.PositionProperty, new Binding(nameof(model.Position))
            { Source = model, Mode = BindingMode.TwoWay });
            foreach ((double maximum, double expected) in new[] { (0d, 0d), (20d, 9d), (5d, 5d), (0d, 0d), (30d, 9d) })
            {
                slider.Maximum = maximum;
                Assert.Equal(expected, slider.Value);
                Assert.Equal(9, slider.Position);
                Assert.Equal(9, model.Position);
                Assert.Equal(0, model.Writes);
            }
            slider.Minimum = 12;
            Assert.Equal(12, slider.Value);
            slider.Minimum = 0;
            Assert.Equal(9, slider.Value);
            slider.Minimum = 35; // RangeBase also coerces Maximum in this notification.
            Assert.Equal(35, slider.Maximum);
            Assert.Equal(35, slider.Value);
            slider.Minimum = 0;
            Assert.Equal(9, slider.Value);
            Assert.Equal(0, model.Writes);
        });
    }

    [Fact]
    public void RangeAndClockChanges_DuringDragPreserveThumbAndFinalSeek()
    {
        RunSta(() =>
        {
            PositionModel model = new(9);
            ReviewSeekSlider slider = new() { Maximum = 20 };
            slider.SetBinding(ReviewSeekSlider.PositionProperty, new Binding(nameof(model.Position))
            { Source = model, Mode = BindingMode.TwoWay });
            slider.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
            slider.Value = 12;
            model.Position = 10;
            int writes = model.Writes;
            slider.Maximum = 30;
            Assert.Equal(12, slider.Value);
            Assert.Equal(writes, model.Writes);
            slider.Value = 14;
            model.Position = 11;
            slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            Assert.Equal(14, model.Position);
            Assert.NotNull(BindingOperations.GetBindingExpression(slider, ReviewSeekSlider.PositionProperty));
            model.Position = 15;
            Assert.Equal(15, slider.Value);
        });
    }

    [Theory]
    [InlineData(VideoPlaybackSurfaceKind.Original, 9, false)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered, 9, false)]
    [InlineData(VideoPlaybackSurfaceKind.Original, 20, false)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered, 20, false)]
    [InlineData(VideoPlaybackSurfaceKind.Original, 9, true)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered, 9, true)]
    public void RealSurfaceRoleBindings_InitializePausedTimelineAndTrack(
        VideoPlaybackSurfaceKind role, double position, bool durationFirst)
    {
        RunSta(() =>
        {
            SurfaceModel model = new(position, durationFirst ? 20 : 0);
            VideoReviewSurface surface = new() { DataContext = model, IsFullscreen = true, PlaybackKind = role };
            Window window = new()
            {
                Content = surface,
                Width = 750,
                Height = 450,
                Left = -10000,
                Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                Settle(window);
                Grid root = Assert.IsType<Grid>(surface.Content);
                Grid row = Assert.Single(root.Children.OfType<Grid>(), child => Grid.GetRow(child) == 1);
                ReviewSeekSlider slider = Assert.Single(row.Children.OfType<ReviewSeekSlider>());
                Assert.Equal(position, slider.Position); // Arrives with zero range in the reproducing case.
                model.Duration = 20;
                Settle(window);
                Assert.Equal(20, surface.DurationSeconds);
                Assert.Equal(position, surface.PositionSeconds);
                Assert.Equal(position, slider.Value);
                Assert.Equal(0, model.Writes);
                Track track = Assert.IsType<Track>(slider.Template.FindName("PART_Track", slider));
                Assert.Equal(position, track.Value);
                Assert.True(track.DecreaseRepeatButton.ActualWidth > 0); // The actual accent fill.
                Assert.True(track.Thumb.TranslatePoint(new Point(), track).X > 0);
                Assert.False(surface.IsPlaying);
                string path = role == VideoPlaybackSurfaceKind.Original ? "OriginalVideoPositionSeconds" : "RenderedVideoPositionSeconds";
                Assert.Equal(path, BindingOperations.GetBinding(surface, VideoReviewSurface.PositionSecondsProperty)!.Path.Path);
                model.Duration = 5;
                Settle(window);
                Assert.Equal(5, slider.Value);
                Assert.Equal(position, surface.PositionSeconds);
                Assert.Equal(0, model.Writes);
                model.Duration = 20;
                Settle(window);
                Assert.Equal(position, slider.Value);
                slider.Value = 7;
                Settle(window);
                Assert.Equal(7, model.Position);
                Assert.Equal(1, model.Writes);
                Assert.False(BindingOperations.GetBindingExpression(slider, ReviewSeekSlider.PositionProperty)!.HasError);
                Assert.NotNull(BindingOperations.GetBindingExpression(surface, VideoReviewSurface.PositionSecondsProperty));
                model.Position = 8;
                Settle(window);
                Assert.Equal(8, slider.Value);
                Assert.Equal(2, model.Writes);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(VideoPlaybackSurfaceKind.Original)]
    [InlineData(VideoPlaybackSurfaceKind.Rendered)]
    public void FullscreenHost_RepeatedEntryAtPausedMiddleAndEndUsesCurrentBoundPosition(VideoPlaybackSurfaceKind role)
    {
        RunSta(() =>
        {
            SurfaceModel model = new(9, 20);
            Window owner = new()
            {
                DataContext = model,
                Width = 750,
                Height = 450,
                Left = -10000,
                Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false,
                ShowInTaskbar = false
            };
            using WpfVideoPlaybackFullscreenHost host = new(role);
            try
            {
                owner.Show();
                host.SetOwner(owner);
                foreach (double position in new[] { 9d, 9d, 20d })
                {
                    model.Position = position;
                    int writes = model.Writes;
                    host.Show("Fit");
                    Window fullscreen = Assert.Single(owner.OwnedWindows.Cast<Window>());
                    Settle(fullscreen);
                    VideoReviewSurface surface = Assert.IsType<VideoReviewSurface>(fullscreen.Content);
                    Grid row = Assert.Single(((Grid)surface.Content).Children.OfType<Grid>(), child => Grid.GetRow(child) == 1);
                    ReviewSeekSlider slider = Assert.Single(row.Children.OfType<ReviewSeekSlider>());
                    Assert.Equal(role, surface.PlaybackKind);
                    Assert.Equal(position, slider.Position);
                    Assert.Equal(position, slider.Value);
                    Assert.Equal(20, slider.Maximum);
                    Assert.False(surface.IsPlaying);
                    Assert.Equal(writes, model.Writes);
                    host.Close();
                    Assert.Empty(owner.OwnedWindows.Cast<Window>());
                    Assert.Equal(position, model.Position);
                }
            }
            finally { host.Close(); owner.Close(); }
        });
    }

    private static void Settle(Window window)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static void RunSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Seek bindings did not settle.");
        failure?.Throw();
    }

    private class PositionModel(double position) : INotifyPropertyChanged
    {
        private double _position = position;
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Writes { get; private set; }
        public double Position
        {
            get => _position;
            set { _position = value; Writes++; Notify(nameof(Position)); }
        }
        protected virtual void Notify(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    private sealed class SurfaceModel(double position, double duration) : PositionModel(position)
    {
        private double _duration = duration;
        protected override void Notify(string property)
        {
            base.Notify(property);
            if (property == nameof(Position))
            {
                base.Notify(nameof(OriginalVideoPositionSeconds));
                base.Notify(nameof(RenderedVideoPositionSeconds));
            }
        }
        public double Duration
        {
            get => _duration;
            set { _duration = value; Notify(nameof(OriginalVideoDurationSeconds)); Notify(nameof(RenderedVideoDurationSeconds)); }
        }
        public double OriginalVideoPositionSeconds { get => Position; set => Position = value; }
        public double RenderedVideoPositionSeconds { get => Position; set => Position = value; }
        public double OriginalVideoDurationSeconds => Duration;
        public double RenderedVideoDurationSeconds => Duration;
        public bool IsOriginalVideoPlaying => false;
        public bool IsRenderedVideoPlaying => false;
        public bool ShowOriginalVideoReplay => Position == 20;
        public bool ShowRenderedVideoReplay => Position == 20;
        public bool AreExportAnimationsEnabled => false;
    }
}
