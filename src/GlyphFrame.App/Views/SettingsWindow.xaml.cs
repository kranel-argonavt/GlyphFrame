using System.Windows;
using GlyphFrame.App.ViewModels;

namespace GlyphFrame.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowsCaptionTheme.Apply(this);
        WindowWorkAreaPlacement.FitInitialBounds(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        AccentPresetComboBox.Focus();
    }

    private void OnRequestClose(object? sender, EventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        SourceInitialized -= OnSourceInitialized;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
    }
}
