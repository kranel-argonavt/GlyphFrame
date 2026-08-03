using GlyphFrame.App.ViewModels;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Services;

public sealed class SettingsDialogService : ISettingsDialogService
{
    private readonly SettingsViewModel _viewModel;

    public SettingsDialogService(SettingsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public void Show()
    {
        _viewModel.BeginEdit();

        SettingsWindow window = new(_viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        try
        {
            window.ShowDialog();
        }
        finally
        {
            _viewModel.EndEdit();
        }
    }
}
