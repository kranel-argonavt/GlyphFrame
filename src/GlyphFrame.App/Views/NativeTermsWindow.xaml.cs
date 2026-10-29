using System.IO;
using System.Security;
using System.Windows;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Views;

public partial class NativeTermsWindow : Window
{
    private readonly NativeTermsBundle? _bundle;
    private readonly Action _save;

    internal NativeTermsWindow(NativeTermsBundle? bundle, string? error, Action save, ResourceDictionary? resources = null)
    {
        _bundle = bundle;
        _save = save;
        if (resources is not null) { Resources = resources; }
        InitializeComponent();
        if (bundle is null) { Title = "Microsoft component verification failed — GlyphFrame"; }
        TermsText.Text = bundle?.Text ?? error ?? "The complete terms are unavailable.";
        ReferencesButton.IsEnabled = bundle is not null;
        NoticesButton.IsEnabled = bundle is not null;
        Acknowledgement.IsEnabled = bundle is not null;
        SourceInitialized += (_, _) =>
        {
            WindowsCaptionTheme.Apply(this);
            WindowWorkAreaPlacement.ConstrainContentBounds(this);
        };
        Loaded += (_, _) =>
        {
            WindowWorkAreaPlacement.ConstrainContentBounds(this);
            (bundle is null ? (IInputElement)DeclineButton : Acknowledgement).Focus();
        };
    }

    private void ChoiceChanged(object sender, RoutedEventArgs e)
    {
        if (ContinueButton is not null) { ContinueButton.IsEnabled = _bundle is not null && Acknowledgement.IsChecked == true; }
    }

    private void ContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (HeaderPanel is null || ActionArea is null || ContentLayout is null || TermsText is null) { return; }
        // Keep a readable terms viewport; on a small work area scroll the entire dialog instead of clipping actions.
        ContentLayout.MinHeight = Math.Max(400, HeaderPanel.ActualHeight + HeaderPanel.Margin.Top +
            HeaderPanel.Margin.Bottom + ActionArea.ActualHeight + TermsText.MinHeight +
            TermsText.Margin.Top + TermsText.Margin.Bottom);
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_bundle is null || Acknowledgement.IsChecked != true) { return; }
        try
        {
            _save();
            DialogResult = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            Feedback.Text = "The local acceptance record could not be saved. The workspace has not started. Check access to application data and retry, or choose Decline & Exit.";
            Feedback.Visibility = Visibility.Visible;
            ContinueButton.Focus();
        }
    }

    private void Decline_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void References_Click(object sender, RoutedEventArgs e) => ShowText(_bundle?.References, "Microsoft reference texts — GlyphFrame");
    private void Notices_Click(object sender, RoutedEventArgs e) => ShowText(_bundle?.Notices, "Licenses and source information — GlyphFrame");
    private void ShowText(string? text, string title)
    {
        if (text is not null) { new NoticesWindow(text) { Owner = this, Title = title }.ShowDialog(); }
    }
}
