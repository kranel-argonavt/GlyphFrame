using System.Windows;
using System.Windows.Input;

namespace GlyphFrame.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(string name, string versionText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionText);
        InitializeComponent();
        DataContext = new AboutContent(name, versionText);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        CloseButton.Focus();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private sealed record AboutContent(string Name, string VersionText);
}
