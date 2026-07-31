using System.Windows;
using GlyphFrame.App.ViewModels;

namespace GlyphFrame.App;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
