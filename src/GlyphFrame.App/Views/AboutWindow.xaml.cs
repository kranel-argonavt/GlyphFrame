using System.Windows;
using System.Windows.Input;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Views;

public partial class AboutWindow : Window
{
    private readonly ThirdPartyNoticesService _notices = new();
    public AboutWindow(string name, string versionText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionText);
        InitializeComponent();
        DataContext = new AboutContent(name, versionText);
        Loaded += OnLoaded;
        SourceInitialized += (_, _) => WindowWorkAreaPlacement.ConstrainContentBounds(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        WindowWorkAreaPlacement.ConstrainContentBounds(this);
        CloseButton.Focus();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ThirdPartyNotices_Click(object sender, RoutedEventArgs e)
    {
        string? error = _notices.Show(this);
        NoticesFeedback.Text = error ?? string.Empty;
        NoticesFeedback.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        ThirdPartyNoticesButton.Focus();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private sealed record AboutContent(string Name, string VersionText);
}
