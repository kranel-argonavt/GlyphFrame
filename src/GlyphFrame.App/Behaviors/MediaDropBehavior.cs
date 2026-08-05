using System.Windows;
using System.Windows.Input;

namespace GlyphFrame.App.Behaviors;

public static class MediaDropBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(MediaDropBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(MediaDropBehavior));

    private static readonly DependencyPropertyKey IsDragOverPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsDragOver",
            typeof(bool),
            typeof(MediaDropBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsDragOverProperty = IsDragOverPropertyKey.DependencyProperty;

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static bool GetIsDragOver(DependencyObject element) =>
        (bool)element.GetValue(IsDragOverProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not UIElement element)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            element.AllowDrop = true;
            element.PreviewDragOver += OnPreviewDragOver;
            element.DragLeave += OnDragLeave;
            element.Drop += OnDrop;
        }
        else
        {
            element.PreviewDragOver -= OnPreviewDragOver;
            element.DragLeave -= OnDragLeave;
            element.Drop -= OnDrop;
            element.ClearValue(UIElement.AllowDropProperty);
            SetDragOver(element, false);
        }
    }

    private static void OnPreviewDragOver(object sender, DragEventArgs args)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        bool hasFiles = args.Data.GetDataPresent(DataFormats.FileDrop);
        args.Effects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        args.Handled = true;
        SetDragOver(element, hasFiles);
    }

    private static void OnDragLeave(object sender, DragEventArgs args)
    {
        if (sender is UIElement element)
        {
            SetDragOver(element, false);
        }
    }

    private static void OnDrop(object sender, DragEventArgs args)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        SetDragOver(element, false);
        args.Handled = true;

        string[] paths = args.Data.GetDataPresent(DataFormats.FileDrop)
            ? (args.Data.GetData(DataFormats.FileDrop) as string[] ?? [])
            : [];
        ICommand? command = GetCommand(element);
        if (command?.CanExecute(paths) == true)
        {
            command.Execute(paths);
        }
    }

    private static void SetDragOver(UIElement element, bool value) =>
        element.SetValue(IsDragOverPropertyKey, value);
}
