using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using GlyphFrame.App.ViewModels;

namespace GlyphFrame.App.Views;

/// <summary>Both surfaces borrow the owner's ViewModel and playback session. Unloading
/// this decorative surface never closes playback (the preview owner owns that lifetime).</summary>
public partial class VideoReviewSurface : UserControl
{
    private static readonly TimeSpan ReplayFadeDuration = TimeSpan.FromMilliseconds(160);
    public static readonly DependencyProperty IsFullscreenProperty = DependencyProperty.Register(nameof(IsFullscreen), typeof(bool), typeof(VideoReviewSurface),
        new PropertyMetadata(false, (d, e) => d.SetValue(FullscreenActionLabelPropertyKey, (bool)e.NewValue ? "Exit fullscreen" : "Enter fullscreen")));
    private static readonly DependencyPropertyKey FullscreenActionLabelPropertyKey = DependencyProperty.RegisterReadOnly(nameof(FullscreenActionLabel), typeof(string), typeof(VideoReviewSurface), new PropertyMetadata("Enter fullscreen"));
    public static readonly DependencyProperty FullscreenActionLabelProperty = FullscreenActionLabelPropertyKey.DependencyProperty;
    public static readonly DependencyProperty ShowReplayProperty = DependencyProperty.Register(nameof(ShowReplay), typeof(bool), typeof(VideoReviewSurface), new PropertyMetadata(false, ReplayChanged));
    public static readonly DependencyProperty AnimationsEnabledProperty = DependencyProperty.Register(nameof(AnimationsEnabled), typeof(bool), typeof(VideoReviewSurface), new PropertyMetadata(true, ReplayChanged));
    public bool IsFullscreen { get => (bool)GetValue(IsFullscreenProperty); set => SetValue(IsFullscreenProperty, value); }
    public bool ShowReplay { get => (bool)GetValue(ShowReplayProperty); set => SetValue(ShowReplayProperty, value); }
    public bool AnimationsEnabled { get => (bool)GetValue(AnimationsEnabledProperty); set => SetValue(AnimationsEnabledProperty, value); }
    public string FullscreenActionLabel => (string)GetValue(FullscreenActionLabelProperty);
    public VideoReviewSurface()
    {
        InitializeComponent();
        SetBinding(ShowReplayProperty, nameof(MainWindowViewModel.ShowRenderedVideoReplay));
        SetBinding(AnimationsEnabledProperty, nameof(MainWindowViewModel.AreExportAnimationsEnabled));
        Loaded += (_, _) => UpdateReplay();
        Unloaded += (_, _) => ReplayOverlay.BeginAnimation(OpacityProperty, null);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) UpdateReplay();
            else ReplayOverlay.BeginAnimation(OpacityProperty, null);
        };
    }
    private static void ReplayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((VideoReviewSurface)d).UpdateReplay();
    private void UpdateReplay()
    {
        if (ReplayOverlay is null) return;
        ReplayOverlay.BeginAnimation(OpacityProperty, null);
        if (!ShowReplay && ReplayButton.IsKeyboardFocusWithin) PlayButton.Focus();
        ReplayOverlay.Visibility = ShowReplay ? Visibility.Visible : Visibility.Collapsed;
        ReplayOverlay.Opacity = 1;
        if (ShowReplay && IsLoaded && AnimationsEnabled && SystemParameters.ClientAreaAnimation)
        {
            ReplayOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, ReplayFadeDuration) { FillBehavior = FillBehavior.Stop });
        }
    }
    private void OnFullscreenClick(object sender, RoutedEventArgs e)
    {
        if (IsFullscreen) Window.GetWindow(this)?.Close();
        else if (DataContext is MainWindowViewModel vm && vm.FullscreenCommand.CanExecute(null)) vm.FullscreenCommand.Execute(null);
    }
}
