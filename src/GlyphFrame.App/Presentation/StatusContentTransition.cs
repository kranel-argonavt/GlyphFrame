using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace GlyphFrame.App.Presentation;

/// <summary>Decorates meaningful status changes only; never animates layout or progress ticks.</summary>
public static class StatusContentTransition
{
    private static readonly ConditionalWeakTable<FrameworkElement, State> States = new();

    public static readonly DependencyProperty ContentKeyProperty = DependencyProperty.RegisterAttached(
        "ContentKey", typeof(object), typeof(StatusContentTransition), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty AnimationsEnabledProperty = DependencyProperty.RegisterAttached(
        "AnimationsEnabled", typeof(bool), typeof(StatusContentTransition), new PropertyMetadata(false, Changed));

    public static object? GetContentKey(DependencyObject element) => element.GetValue(ContentKeyProperty);
    public static void SetContentKey(DependencyObject element, object? value) => element.SetValue(ContentKeyProperty, value);
    public static bool GetAnimationsEnabled(DependencyObject element) => (bool)element.GetValue(AnimationsEnabledProperty);
    public static void SetAnimationsEnabled(DependencyObject element, bool value) => element.SetValue(AnimationsEnabledProperty, value);

    internal static AnimationTimeline? ActiveAnimation(FrameworkElement view) =>
        States.TryGetValue(view, out State? state) ? state.ActiveAnimation : null;

    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is FrameworkElement view)
        {
            States.GetValue(view, owner => new State(owner)).Refresh();
        }
    }

    private sealed class State
    {
        private readonly FrameworkElement _view;
        private object? _key;
        private bool _visible;
        private AnimationClock? _clock;
        public AnimationTimeline? ActiveAnimation => _clock?.Timeline;

        public State(FrameworkElement view)
        {
            _view = view;
            view.Loaded += Loaded;
            view.Unloaded += Unloaded;
            view.IsVisibleChanged += VisibilityChanged;
        }

        private void Loaded(object sender, RoutedEventArgs args) => Refresh();
        private void Unloaded(object sender, RoutedEventArgs args)
        {
            Stop();
            _visible = false;
        }
        private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) => Refresh();

        public void Refresh()
        {
            object? key = GetContentKey(_view);
            bool visible = _view.IsLoaded && _view.IsVisible;
            bool changed = !Equals(key, _key) || visible && !_visible;
            _key = key;
            _visible = visible;
            if (!visible || !GetAnimationsEnabled(_view)) { Stop(); }
            else if (changed)
            {
                Stop();
                DoubleAnimation fade = new(0.75, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop };
                _clock = fade.CreateClock();
                _clock.Completed += Completed;
                _view.ApplyAnimationClock(UIElement.OpacityProperty, _clock, HandoffBehavior.SnapshotAndReplace);
            }
        }

        private void Completed(object? sender, EventArgs args) => Stop();
        private void Stop()
        {
            if (_clock is not null) { _clock.Completed -= Completed; _clock = null; }
            _view.BeginAnimation(UIElement.OpacityProperty, null);
        }
    }
}
