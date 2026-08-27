using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Presentation;

public static class ExportStatusAnimation
{
    private static readonly ConditionalWeakTable<ProgressBar, ProgressBarAnimationState> ProgressStates = new();
    private static readonly ConditionalWeakTable<TextBlock, StageTextAnimationState> StageTextStates = new();

    public static readonly DependencyProperty AnimationsEnabledProperty = DependencyProperty.RegisterAttached(
        "AnimationsEnabled",
        typeof(bool),
        typeof(ExportStatusAnimation),
        new PropertyMetadata(true, OnAnimationsEnabledChanged));

    public static readonly DependencyProperty ProgressValueProperty = DependencyProperty.RegisterAttached(
        "ProgressValue",
        typeof(double),
        typeof(ExportStatusAnimation),
        new PropertyMetadata(0d, OnProgressPresentationChanged));

    public static readonly DependencyProperty ProgressGenerationProperty = DependencyProperty.RegisterAttached(
        "ProgressGeneration",
        typeof(long),
        typeof(ExportStatusAnimation),
        new PropertyMetadata(0L, OnProgressPresentationChanged));

    public static readonly DependencyProperty ProgressStageProperty = DependencyProperty.RegisterAttached(
        "ProgressStage",
        typeof(VideoExportStage),
        typeof(ExportStatusAnimation),
        new PropertyMetadata(VideoExportStage.Preparing, OnProgressPresentationChanged));

    public static readonly DependencyProperty ProgressIsIndeterminateProperty = DependencyProperty.RegisterAttached(
        "ProgressIsIndeterminate",
        typeof(bool),
        typeof(ExportStatusAnimation),
        new PropertyMetadata(true, OnProgressPresentationChanged));

    public static readonly DependencyProperty StageTextProperty = DependencyProperty.RegisterAttached(
        "StageText",
        typeof(string),
        typeof(ExportStatusAnimation),
        new PropertyMetadata(string.Empty, OnStagePresentationChanged));

    public static readonly DependencyProperty StageProperty = DependencyProperty.RegisterAttached(
        "Stage",
        typeof(VideoExportStage),
        typeof(ExportStatusAnimation),
        new PropertyMetadata(VideoExportStage.Preparing, OnStagePresentationChanged));

    public static bool GetAnimationsEnabled(DependencyObject element) =>
        (bool)element.GetValue(AnimationsEnabledProperty);

    public static void SetAnimationsEnabled(DependencyObject element, bool value) =>
        element.SetValue(AnimationsEnabledProperty, value);

    public static double GetProgressValue(DependencyObject element) =>
        (double)element.GetValue(ProgressValueProperty);

    public static void SetProgressValue(DependencyObject element, double value) =>
        element.SetValue(ProgressValueProperty, value);

    public static long GetProgressGeneration(DependencyObject element) =>
        (long)element.GetValue(ProgressGenerationProperty);

    public static void SetProgressGeneration(DependencyObject element, long value) =>
        element.SetValue(ProgressGenerationProperty, value);

    public static VideoExportStage GetProgressStage(DependencyObject element) =>
        (VideoExportStage)element.GetValue(ProgressStageProperty);

    public static void SetProgressStage(DependencyObject element, VideoExportStage value) =>
        element.SetValue(ProgressStageProperty, value);

    public static bool GetProgressIsIndeterminate(DependencyObject element) =>
        (bool)element.GetValue(ProgressIsIndeterminateProperty);

    public static void SetProgressIsIndeterminate(DependencyObject element, bool value) =>
        element.SetValue(ProgressIsIndeterminateProperty, value);

    public static string GetStageText(DependencyObject element) =>
        (string)element.GetValue(StageTextProperty);

    public static void SetStageText(DependencyObject element, string value) =>
        element.SetValue(StageTextProperty, value);

    public static VideoExportStage GetStage(DependencyObject element) =>
        (VideoExportStage)element.GetValue(StageProperty);

    public static void SetStage(DependencyObject element, VideoExportStage value) =>
        element.SetValue(StageProperty, value);

    private static void OnProgressPresentationChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ProgressBar progressBar)
        {
            return;
        }

        ProgressBarAnimationState state = ProgressStates.GetValue(progressBar, CreateProgressState);
        state.Update(
            GetProgressGeneration(progressBar),
            GetProgressValue(progressBar),
            GetProgressIsIndeterminate(progressBar),
            GetProgressStage(progressBar),
            GetAnimationsEnabled(progressBar));
    }

    private static void OnAnimationsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        OnProgressPresentationChanged(element, args);
        OnStagePresentationChanged(element, args);
    }

    private static ProgressBarAnimationState CreateProgressState(ProgressBar progressBar)
    {
        ProgressBarAnimationState state = new(progressBar);
        progressBar.Unloaded += OnProgressBarUnloaded;
        return state;
    }

    private static void OnProgressBarUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is ProgressBar progressBar && ProgressStates.TryGetValue(progressBar, out ProgressBarAnimationState? state))
        {
            state.Stop();
        }
    }

    private static void OnStagePresentationChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBlock textBlock)
        {
            return;
        }

        StageTextAnimationState state = StageTextStates.GetValue(textBlock, CreateStageTextState);
        state.Schedule(
            GetStageText(textBlock),
            GetStage(textBlock),
            GetAnimationsEnabled(textBlock));
    }

    private static StageTextAnimationState CreateStageTextState(TextBlock textBlock)
    {
        StageTextAnimationState state = new(textBlock);
        textBlock.Unloaded += OnStageTextUnloaded;
        return state;
    }

    private static void OnStageTextUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is TextBlock textBlock && StageTextStates.TryGetValue(textBlock, out StageTextAnimationState? state))
        {
            state.Stop();
        }
    }

    private sealed class ProgressBarAnimationState(ProgressBar progressBar)
    {
        private readonly ExportProgressAnimationState _progress = new();
        private long _animationVersion;

        public void Update(
            long generation,
            double sourceValue,
            bool isIndeterminate,
            VideoExportStage stage,
            bool animationsEnabled)
        {
            double previousTarget = _progress.TargetValue;
            bool wasIndeterminate = _progress.IsIndeterminate;
            _progress.Update(generation, sourceValue, isIndeterminate, stage);
            progressBar.IsIndeterminate = _progress.IsIndeterminate;

            if (_progress.IsIndeterminate)
            {
                SetImmediately(_progress.TargetValue);
                return;
            }

            if (!animationsEnabled || previousTarget > _progress.TargetValue || !progressBar.IsLoaded)
            {
                SetImmediately(_progress.TargetValue);
                return;
            }

            if (wasIndeterminate || !AreClose(progressBar.Value, _progress.TargetValue))
            {
                AnimateTo(_progress.TargetValue);
            }
        }

        public void Stop()
        {
            _animationVersion++;
            progressBar.BeginAnimation(ProgressBar.ValueProperty, null);
            progressBar.SetCurrentValue(ProgressBar.ValueProperty, _progress.TargetValue);
        }

        private void AnimateTo(double target)
        {
            long version = ++_animationVersion;
            DoubleAnimation animation = new()
            {
                From = progressBar.Value,
                To = target,
                Duration = new Duration(ExportAnimationTimings.ProgressTransition),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            animation.Completed += (_, _) =>
            {
                if (version == _animationVersion)
                {
                    SetImmediately(target);
                }
            };
            progressBar.BeginAnimation(ProgressBar.ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void SetImmediately(double value)
        {
            _animationVersion++;
            progressBar.BeginAnimation(ProgressBar.ValueProperty, null);
            progressBar.SetCurrentValue(ProgressBar.ValueProperty, value);
        }

        private static bool AreClose(double first, double second) => Math.Abs(first - second) < 0.001d;
    }

    private sealed class StageTextAnimationState(TextBlock textBlock)
    {
        private DispatcherOperation? _pendingUpdate;
        private long _animationVersion;
        private bool _hasStage;
        private VideoExportStage _stage;
        private string _text = string.Empty;

        public void Schedule(string text, VideoExportStage stage, bool animationsEnabled)
        {
            _pendingUpdate?.Abort();
            if (!animationsEnabled || stage == VideoExportStage.Cancelling)
            {
                _pendingUpdate = null;
                Apply(text, stage, animationsEnabled: false);
                return;
            }

            _pendingUpdate = textBlock.Dispatcher.BeginInvoke(
                DispatcherPriority.DataBind,
                () => Apply(text, stage, animationsEnabled));
        }

        public void Stop()
        {
            _pendingUpdate?.Abort();
            _pendingUpdate = null;
            _animationVersion++;
            textBlock.BeginAnimation(UIElement.OpacityProperty, null);
            textBlock.BeginAnimation(TextBlock.TextProperty, null);
            textBlock.SetCurrentValue(TextBlock.TextProperty, _text);
            textBlock.SetCurrentValue(UIElement.OpacityProperty, 1d);
        }

        private void Apply(string text, VideoExportStage stage, bool animationsEnabled)
        {
            _pendingUpdate = null;
            bool stageChanged = _hasStage && stage != _stage;
            _hasStage = true;
            _stage = stage;
            _text = text ?? string.Empty;

            if (!stageChanged
                || !animationsEnabled
                || stage == VideoExportStage.Cancelling
                || !textBlock.IsLoaded)
            {
                SetImmediately(_text);
                return;
            }

            AnimateTo(_text);
        }

        private void AnimateTo(string text)
        {
            long version = ++_animationVersion;
            TimeSpan halfDuration = TimeSpan.FromTicks(ExportAnimationTimings.StageCrossfade.Ticks / 2);
            DoubleAnimationUsingKeyFrames opacityAnimation = new()
            {
                Duration = new Duration(ExportAnimationTimings.StageCrossfade),
                FillBehavior = FillBehavior.Stop
            };
            opacityAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0d, KeyTime.FromTimeSpan(halfDuration)));
            opacityAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1d, KeyTime.FromTimeSpan(ExportAnimationTimings.StageCrossfade)));

            StringAnimationUsingKeyFrames textAnimation = new()
            {
                Duration = new Duration(ExportAnimationTimings.StageCrossfade),
                FillBehavior = FillBehavior.Stop
            };
            textAnimation.KeyFrames.Add(new DiscreteStringKeyFrame(text, KeyTime.FromTimeSpan(halfDuration)));
            opacityAnimation.Completed += (_, _) =>
            {
                if (version == _animationVersion)
                {
                    SetImmediately(text);
                }
            };

            textBlock.BeginAnimation(TextBlock.TextProperty, textAnimation, HandoffBehavior.SnapshotAndReplace);
            textBlock.BeginAnimation(UIElement.OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
        }

        private void SetImmediately(string text)
        {
            _animationVersion++;
            textBlock.BeginAnimation(TextBlock.TextProperty, null);
            textBlock.BeginAnimation(UIElement.OpacityProperty, null);
            textBlock.SetCurrentValue(TextBlock.TextProperty, text);
            textBlock.SetCurrentValue(UIElement.OpacityProperty, 1d);
        }
    }
}
