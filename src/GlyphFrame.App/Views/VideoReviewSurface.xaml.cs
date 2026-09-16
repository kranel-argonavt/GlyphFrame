using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Views;

/// <summary>A reusable presentation-only surface. Original and Rendered roles
/// bind to separate facade state; this control never owns a media transport.</summary>
public partial class VideoReviewSurface : UserControl
{
    private static readonly TimeSpan ReplayFadeDuration = TimeSpan.FromMilliseconds(160);

    public static readonly DependencyProperty IsFullscreenProperty = DependencyProperty.Register(
        nameof(IsFullscreen), typeof(bool), typeof(VideoReviewSurface),
        new PropertyMetadata(false, (d, e) => d.SetValue(
            FullscreenActionLabelPropertyKey,
            (bool)e.NewValue ? "Exit fullscreen" : "Enter fullscreen")));
    private static readonly DependencyPropertyKey FullscreenActionLabelPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(FullscreenActionLabel), typeof(string), typeof(VideoReviewSurface),
            new PropertyMetadata("Enter fullscreen"));
    public static readonly DependencyProperty FullscreenActionLabelProperty =
        FullscreenActionLabelPropertyKey.DependencyProperty;
    public static readonly DependencyProperty ShowReplayProperty = Register(nameof(ShowReplay), typeof(bool), false, ReplayChanged);
    public static readonly DependencyProperty AnimationsEnabledProperty = Register(nameof(AnimationsEnabled), typeof(bool), true, ReplayChanged);
    public static readonly DependencyProperty PlaybackKindProperty = Register(
        nameof(PlaybackKind), typeof(VideoPlaybackSurfaceKind), VideoPlaybackSurfaceKind.Rendered, PlaybackKindChanged);
    public static readonly DependencyProperty PlaybackSourceProperty = Register(nameof(PlaybackSource), typeof(ImageSource), null);
    public static readonly DependencyProperty PixelWidthProperty = Register(nameof(PixelWidth), typeof(double), 0d);
    public static readonly DependencyProperty PixelHeightProperty = Register(nameof(PixelHeight), typeof(double), 0d);
    public static readonly DependencyProperty RotationDegreesProperty = Register(nameof(RotationDegrees), typeof(double), 0d);
    public static readonly DependencyProperty SelectedScaleModeProperty = RegisterTwoWay(nameof(SelectedScaleMode), typeof(string), "Fit");
    public static readonly DependencyProperty TogglePlaybackCommandProperty = Register(nameof(TogglePlaybackCommand), typeof(ICommand), null);
    public static readonly DependencyProperty ReplayCommandProperty = Register(nameof(ReplayCommand), typeof(ICommand), null);
    public static readonly DependencyProperty ToggleMuteCommandProperty = Register(nameof(ToggleMuteCommand), typeof(ICommand), null);
    public static readonly DependencyProperty FullscreenCommandProperty = Register(nameof(FullscreenCommand), typeof(ICommand), null);
    public static readonly DependencyProperty PlaybackActionLabelProperty = Register(nameof(PlaybackActionLabel), typeof(string), "Play");
    public static readonly DependencyProperty MuteActionLabelProperty = Register(nameof(MuteActionLabel), typeof(string), "Mute");
    public static readonly DependencyProperty PositionAutomationNameProperty = Register(nameof(PositionAutomationName), typeof(string), "Rendered video position");
    public static readonly DependencyProperty VolumeAutomationNameProperty = Register(nameof(VolumeAutomationName), typeof(string), "Rendered video volume");
    public static readonly DependencyProperty IsPlayingProperty = Register(nameof(IsPlaying), typeof(bool), false);
    public static readonly DependencyProperty IsMutedProperty = Register(nameof(IsMuted), typeof(bool), false);
    public static readonly DependencyProperty CurrentTimeTextProperty = Register(nameof(CurrentTimeText), typeof(string), "0:00");
    public static readonly DependencyProperty TotalTimeTextProperty = Register(nameof(TotalTimeText), typeof(string), "0:00");
    public static readonly DependencyProperty PositionSecondsProperty = RegisterTwoWay(nameof(PositionSeconds), typeof(double), 0d);
    public static readonly DependencyProperty DurationSecondsProperty = Register(nameof(DurationSeconds), typeof(double), 0d);
    public static readonly DependencyProperty VolumeProperty = RegisterTwoWay(nameof(Volume), typeof(double), 0.75d);
    public static readonly DependencyProperty FitModesProperty = Register(nameof(FitModes), typeof(IEnumerable<string>), null);

    public bool IsFullscreen { get => (bool)GetValue(IsFullscreenProperty); set => SetValue(IsFullscreenProperty, value); }
    public bool ShowReplay { get => (bool)GetValue(ShowReplayProperty); set => SetValue(ShowReplayProperty, value); }
    public bool AnimationsEnabled { get => (bool)GetValue(AnimationsEnabledProperty); set => SetValue(AnimationsEnabledProperty, value); }
    public VideoPlaybackSurfaceKind PlaybackKind { get => (VideoPlaybackSurfaceKind)GetValue(PlaybackKindProperty); set => SetValue(PlaybackKindProperty, value); }
    public ImageSource? PlaybackSource { get => (ImageSource?)GetValue(PlaybackSourceProperty); set => SetValue(PlaybackSourceProperty, value); }
    public double PixelWidth { get => (double)GetValue(PixelWidthProperty); set => SetValue(PixelWidthProperty, value); }
    public double PixelHeight { get => (double)GetValue(PixelHeightProperty); set => SetValue(PixelHeightProperty, value); }
    public double RotationDegrees { get => (double)GetValue(RotationDegreesProperty); set => SetValue(RotationDegreesProperty, value); }
    public string SelectedScaleMode { get => (string)GetValue(SelectedScaleModeProperty); set => SetValue(SelectedScaleModeProperty, value); }
    public ICommand? TogglePlaybackCommand { get => (ICommand?)GetValue(TogglePlaybackCommandProperty); set => SetValue(TogglePlaybackCommandProperty, value); }
    public ICommand? ReplayCommand { get => (ICommand?)GetValue(ReplayCommandProperty); set => SetValue(ReplayCommandProperty, value); }
    public ICommand? ToggleMuteCommand { get => (ICommand?)GetValue(ToggleMuteCommandProperty); set => SetValue(ToggleMuteCommandProperty, value); }
    public ICommand? FullscreenCommand { get => (ICommand?)GetValue(FullscreenCommandProperty); set => SetValue(FullscreenCommandProperty, value); }
    public string PlaybackActionLabel { get => (string)GetValue(PlaybackActionLabelProperty); set => SetValue(PlaybackActionLabelProperty, value); }
    public string MuteActionLabel { get => (string)GetValue(MuteActionLabelProperty); set => SetValue(MuteActionLabelProperty, value); }
    public string PositionAutomationName { get => (string)GetValue(PositionAutomationNameProperty); set => SetValue(PositionAutomationNameProperty, value); }
    public string VolumeAutomationName { get => (string)GetValue(VolumeAutomationNameProperty); set => SetValue(VolumeAutomationNameProperty, value); }
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public bool IsMuted { get => (bool)GetValue(IsMutedProperty); set => SetValue(IsMutedProperty, value); }
    public string CurrentTimeText { get => (string)GetValue(CurrentTimeTextProperty); set => SetValue(CurrentTimeTextProperty, value); }
    public string TotalTimeText { get => (string)GetValue(TotalTimeTextProperty); set => SetValue(TotalTimeTextProperty, value); }
    public double PositionSeconds { get => (double)GetValue(PositionSecondsProperty); set => SetValue(PositionSecondsProperty, value); }
    public double DurationSeconds { get => (double)GetValue(DurationSecondsProperty); set => SetValue(DurationSecondsProperty, value); }
    public double Volume { get => (double)GetValue(VolumeProperty); set => SetValue(VolumeProperty, value); }
    public IEnumerable<string>? FitModes { get => (IEnumerable<string>?)GetValue(FitModesProperty); set => SetValue(FitModesProperty, value); }
    public string FullscreenActionLabel => (string)GetValue(FullscreenActionLabelProperty);

    public VideoReviewSurface()
    {
        InitializeComponent();
        ApplyRoleBindings();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private static DependencyProperty Register(
        string name,
        Type propertyType,
        object? defaultValue,
        PropertyChangedCallback? callback = null) =>
        DependencyProperty.Register(name, propertyType, typeof(VideoReviewSurface), new PropertyMetadata(defaultValue, callback));

    private static DependencyProperty RegisterTwoWay(string name, Type propertyType, object defaultValue) =>
        DependencyProperty.Register(name, propertyType, typeof(VideoReviewSurface),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private static void ReplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((VideoReviewSurface)d).UpdateReplay();

    private static void PlaybackKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((VideoReviewSurface)d).ApplyRoleBindings();

    private void ApplyRoleBindings()
    {
        bool original = PlaybackKind == VideoPlaybackSurfaceKind.Original;
        Bind(PlaybackSourceProperty, original ? "OriginalVideoPlaybackSource" : "RenderedVideoPlaybackSource");
        Bind(PixelWidthProperty, original ? "OriginalVideoPixelWidth" : "RenderedVideoPixelWidth");
        Bind(PixelHeightProperty, original ? "OriginalVideoPixelHeight" : "RenderedVideoPixelHeight");
        Bind(RotationDegreesProperty, original ? "OriginalVideoRotationDegrees" : null);
        Bind(SelectedScaleModeProperty, "SelectedFitMode", BindingMode.TwoWay);
        Bind(TogglePlaybackCommandProperty, original ? "ToggleOriginalVideoPlaybackCommand" : "ToggleRenderedVideoPlaybackCommand");
        Bind(ReplayCommandProperty, original ? "RestartOriginalVideoCommand" : "RestartRenderedVideoCommand");
        Bind(ToggleMuteCommandProperty, original ? "ToggleOriginalVideoMuteCommand" : "ToggleRenderedVideoMuteCommand");
        Bind(FullscreenCommandProperty, original ? "OriginalFullscreenCommand" : "FullscreenCommand");
        Bind(PlaybackActionLabelProperty, original ? "OriginalVideoPlaybackActionLabel" : "RenderedVideoPlaybackActionLabel");
        Bind(MuteActionLabelProperty, original ? "OriginalVideoMuteActionLabel" : "RenderedVideoMuteActionLabel");
        Bind(IsPlayingProperty, original ? "IsOriginalVideoPlaying" : "IsRenderedVideoPlaying");
        Bind(IsMutedProperty, original ? "IsOriginalVideoMuted" : "IsRenderedVideoMuted");
        Bind(ShowReplayProperty, original ? "ShowOriginalVideoReplay" : "ShowRenderedVideoReplay");
        Bind(CurrentTimeTextProperty, original ? "OriginalVideoCurrentTimeText" : "RenderedVideoCurrentTimeText");
        Bind(TotalTimeTextProperty, original ? "OriginalVideoTotalTimeText" : "RenderedVideoTotalTimeText");
        Bind(PositionSecondsProperty, original ? "OriginalVideoPositionSeconds" : "RenderedVideoPositionSeconds", BindingMode.TwoWay);
        Bind(DurationSecondsProperty, original ? "OriginalVideoDurationSeconds" : "RenderedVideoDurationSeconds");
        Bind(VolumeProperty, original ? "OriginalVideoVolume" : "RenderedVideoVolume", BindingMode.TwoWay);
        Bind(FitModesProperty, "FitModes");
        Bind(AnimationsEnabledProperty, "AreExportAnimationsEnabled");
        PositionAutomationName = original ? "Original video position" : "Rendered video position";
        VolumeAutomationName = original ? "Original video volume" : "Rendered video volume";
    }

    private void Bind(DependencyProperty property, string? path, BindingMode mode = BindingMode.OneWay)
    {
        BindingOperations.ClearBinding(this, property);
        if (path is not null)
        {
            SetBinding(property, new Binding(path) { Mode = mode });
        }
        else
        {
            SetCurrentValue(property, 0d);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateReplay();

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        ReplayOverlay.BeginAnimation(OpacityProperty, null);

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            UpdateReplay();
        }
        else
        {
            ReplayOverlay.BeginAnimation(OpacityProperty, null);
        }
    }

    private void UpdateReplay()
    {
        if (ReplayOverlay is null)
        {
            return;
        }

        ReplayOverlay.BeginAnimation(OpacityProperty, null);
        if (!ShowReplay && ReplayButton.IsKeyboardFocusWithin)
        {
            PlayButton.Focus();
        }

        ReplayOverlay.Visibility = ShowReplay ? Visibility.Visible : Visibility.Collapsed;
        ReplayOverlay.Opacity = 1;
        if (ShowReplay && IsLoaded && AnimationsEnabled && SystemParameters.ClientAreaAnimation)
        {
            ReplayOverlay.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, ReplayFadeDuration) { FillBehavior = FillBehavior.Stop });
        }
    }

    private void OnFullscreenClick(object sender, RoutedEventArgs e)
    {
        if (IsFullscreen)
        {
            Window.GetWindow(this)?.Close();
        }
        else if (FullscreenCommand?.CanExecute(null) == true)
        {
            FullscreenCommand.Execute(null);
        }
    }
}
