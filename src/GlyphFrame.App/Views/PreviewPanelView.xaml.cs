using System.Windows.Controls;
using System.Windows;
using GlyphFrame.App.ViewModels;

namespace GlyphFrame.App.Views;

public partial class PreviewPanelView : UserControl
{
    public PreviewPanelView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.LoadReviewViewAsync(Window.GetWindow(this));
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.UnloadReviewView();
        }
    }
}
