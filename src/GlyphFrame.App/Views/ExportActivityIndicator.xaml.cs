using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GlyphFrame.App.Presentation;

namespace GlyphFrame.App.Views;

public partial class ExportActivityIndicator : UserControl
{
    private const int RingSegmentCount = 4;
    private const double RingSegmentSweepDegrees = 70d;

    public static readonly DependencyProperty PercentageTextProperty = DependencyProperty.Register(
        nameof(PercentageText),
        typeof(string),
        typeof(ExportActivityIndicator),
        new PropertyMetadata("0%", OnPercentageTextChanged));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(ExportActivityIndicator),
        new PropertyMetadata(false, OnPresentationChanged));

    public static readonly DependencyProperty IsCompletedProperty = DependencyProperty.Register(
        nameof(IsCompleted),
        typeof(bool),
        typeof(ExportActivityIndicator),
        new PropertyMetadata(false, OnPresentationChanged));

    public static readonly DependencyProperty AnimationsEnabledProperty = DependencyProperty.Register(
        nameof(AnimationsEnabled),
        typeof(bool),
        typeof(ExportActivityIndicator),
        new PropertyMetadata(true, OnPresentationChanged));

    public static readonly DependencyProperty ShowPercentageTextProperty = DependencyProperty.Register(
        nameof(ShowPercentageText),
        typeof(bool),
        typeof(ExportActivityIndicator),
        new PropertyMetadata(true, OnPresentationChanged));

    private long _completionAnimationVersion;

    public ExportActivityIndicator()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public string PercentageText
    {
        get => (string)GetValue(PercentageTextProperty);
        set => SetValue(PercentageTextProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsCompleted
    {
        get => (bool)GetValue(IsCompletedProperty);
        set => SetValue(IsCompletedProperty, value);
    }

    public bool AnimationsEnabled
    {
        get => (bool)GetValue(AnimationsEnabledProperty);
        set => SetValue(AnimationsEnabledProperty, value);
    }

    public bool ShowPercentageText
    {
        get => (bool)GetValue(ShowPercentageTextProperty);
        set => SetValue(ShowPercentageTextProperty, value);
    }

    private static void OnPresentationChanged(DependencyObject element, DependencyPropertyChangedEventArgs args) =>
        ((ExportActivityIndicator)element).UpdatePresentation();

    private static void OnPercentageTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        ExportActivityIndicator indicator = (ExportActivityIndicator)element;
        indicator.PercentagePresenter.Text = indicator.ShowPercentageText ? (string)args.NewValue : string.Empty;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => UpdatePresentation();

    private void OnUnloaded(object sender, RoutedEventArgs args) => StopAllAnimations();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (IsVisible)
        {
            UpdatePresentation();
        }
        else
        {
            StopAllAnimations();
        }
    }

    private void OnRingSegmentsSizeChanged(object sender, SizeChangedEventArgs args)
    {
        Size ringSize = args.NewSize;
        double radius = (Math.Min(ringSize.Width, ringSize.Height) - RingSegments.StrokeThickness) / 2d;
        if (radius <= 0d)
        {
            RingSegments.Data = Geometry.Empty;
            return;
        }

        Point center = new(ringSize.Width / 2d, ringSize.Height / 2d);
        PathGeometry geometry = new();
        double segmentStepDegrees = 360d / RingSegmentCount;
        for (int index = 0; index < RingSegmentCount; index++)
        {
            double middleDegrees = -90d + index * segmentStepDegrees;
            Point start = PointOnRing(center, radius, middleDegrees - RingSegmentSweepDegrees / 2d);
            Point end = PointOnRing(center, radius, middleDegrees + RingSegmentSweepDegrees / 2d);
            PathFigure figure = new() { StartPoint = start, IsFilled = false };
            figure.Segments.Add(new ArcSegment(
                end,
                new Size(radius, radius),
                0d,
                false,
                SweepDirection.Clockwise,
                true));
            geometry.Figures.Add(figure);
        }

        geometry.Freeze();
        RingSegments.Data = geometry;
    }

    private static Point PointOnRing(Point center, double radius, double degrees)
    {
        double radians = degrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private void UpdatePresentation()
    {
        PercentagePresenter.Text = ShowPercentageText ? PercentageText : string.Empty;
        PercentagePresenter.Visibility = !IsCompleted && ShowPercentageText ? Visibility.Visible : Visibility.Collapsed;
        CompletionPresenter.Visibility = IsCompleted ? Visibility.Visible : Visibility.Collapsed;
        RingSegments.Visibility = IsCompleted ? Visibility.Collapsed : Visibility.Visible;

        if (IsCompleted)
        {
            StopActivityAnimations();
            ShowCompletion();
        }
        else
        {
            HideCompletion();
            if (IsActive && IsLoaded && IsVisible && AnimationsEnabled)
            {
                StartActivityAnimations();
            }
            else
            {
                StopActivityAnimations();
            }
        }
    }

    private void StartActivityAnimations()
    {
        DoubleAnimation rotation = new(0d, 360d, new Duration(ExportAnimationTimings.RingRotation))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        RingRotation.BeginAnimation(
            System.Windows.Media.RotateTransform.AngleProperty,
            rotation,
            HandoffBehavior.SnapshotAndReplace);

        DoubleAnimation pulse = new(0.78d, 1d, new Duration(ExportAnimationTimings.RingPulseHalfCycle))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        RingSegments.BeginAnimation(OpacityProperty, pulse, HandoffBehavior.SnapshotAndReplace);
    }

    private void StopActivityAnimations()
    {
        RingRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        RingRotation.Angle = 0d;
        RingSegments.BeginAnimation(OpacityProperty, null);
        RingSegments.Opacity = IsActive ? 0.9d : 0.78d;
    }

    private void ShowCompletion()
    {
        long version = ++_completionAnimationVersion;
        if (!AnimationsEnabled || !IsLoaded || !IsVisible)
        {
            SetCompletionImmediatelyVisible();
            return;
        }

        CompletionPresenter.Opacity = 0d;
        CompletionScale.ScaleX = 0.86d;
        CompletionScale.ScaleY = 0.86d;

        DoubleAnimation fade = new(0d, 1d, new Duration(ExportAnimationTimings.CompletionTransition))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        fade.Completed += (_, _) =>
        {
            if (version == _completionAnimationVersion && IsCompleted)
            {
                SetCompletionImmediatelyVisible();
            }
        };
        DoubleAnimation scale = new(0.86d, 1d, new Duration(ExportAnimationTimings.CompletionTransition))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };

        CompletionPresenter.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        CompletionScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scale, HandoffBehavior.SnapshotAndReplace);
        CompletionScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scale, HandoffBehavior.SnapshotAndReplace);
    }

    private void HideCompletion()
    {
        _completionAnimationVersion++;
        CompletionPresenter.BeginAnimation(OpacityProperty, null);
        CompletionScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
        CompletionScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
        CompletionPresenter.Opacity = 0d;
        CompletionScale.ScaleX = 0.86d;
        CompletionScale.ScaleY = 0.86d;
    }

    private void SetCompletionImmediatelyVisible()
    {
        CompletionPresenter.BeginAnimation(OpacityProperty, null);
        CompletionScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
        CompletionScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
        CompletionPresenter.Opacity = 1d;
        CompletionScale.ScaleX = 1d;
        CompletionScale.ScaleY = 1d;
    }

    private void StopAllAnimations()
    {
        StopActivityAnimations();
        HideCompletion();
    }
}
