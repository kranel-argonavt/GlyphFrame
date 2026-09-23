using System.Windows;
using System.Windows.Input;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Views;

public partial class ErrorDetailsWindow : Window
{
    public ErrorDetailsWindow(
        ErrorPresentation presentation,
        IClipboardService clipboardService)
    {
        InitializeComponent();
        DataContext = new ErrorDetailsDialogViewModel(presentation, clipboardService);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Rect workArea = SystemParameters.WorkArea;
        MaxWidth = Math.Max(MinWidth, workArea.Width * 0.9);
        MaxHeight = Math.Max(MinHeight, workArea.Height * 0.9);
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
        CopyButton.Focus();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
