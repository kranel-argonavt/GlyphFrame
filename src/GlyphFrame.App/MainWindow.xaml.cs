using System.Windows;
using GlyphFrame.App.ViewModels;
using GlyphFrame.App.Views;

namespace GlyphFrame.App;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) =>
        {
            WindowsCaptionTheme.Apply(this);
            WindowWorkAreaPlacement.FitInitialBounds(this);
            WindowState = WindowState.Maximized;
        };
    }
}
