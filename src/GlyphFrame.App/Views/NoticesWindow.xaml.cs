using System.Windows;

namespace GlyphFrame.App.Views;

public partial class NoticesWindow : Window
{
    public NoticesWindow(string text)
    {
        InitializeComponent();
        DataContext = new NoticeContent(text);
        SourceInitialized += (_, _) =>
        {
            WindowsCaptionTheme.Apply(this);
            WindowWorkAreaPlacement.ConstrainContentBounds(this);
        };
        Loaded += (_, _) =>
        {
            WindowWorkAreaPlacement.ConstrainContentBounds(this);
            DocumentText.Focus();
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record NoticeContent(string Text);
}
