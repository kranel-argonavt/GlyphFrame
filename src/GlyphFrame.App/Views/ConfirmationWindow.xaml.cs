using System.Windows;
using System.Windows.Input;

namespace GlyphFrame.App.Views;

public partial class ConfirmationWindow : Window
{
    public ConfirmationWindow(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmButtonText);
        ArgumentException.ThrowIfNullOrWhiteSpace(cancelButtonText);

        InitializeComponent();
        DataContext = new ConfirmationContent(
            title,
            message,
            confirmButtonText,
            cancelButtonText);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ConfirmButton.Focus();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private sealed record ConfirmationContent(
        string Title,
        string Message,
        string ConfirmButtonText,
        string CancelButtonText);
}
