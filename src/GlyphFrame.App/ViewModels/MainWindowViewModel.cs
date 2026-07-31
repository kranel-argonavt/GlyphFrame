using CommunityToolkit.Mvvm.ComponentModel;
using GlyphFrame.Core.Models;

namespace GlyphFrame.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusMessage;

    public MainWindowViewModel(ApplicationInfo applicationInfo)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);

        ApplicationInfo = applicationInfo;
        _statusMessage = applicationInfo.Status;
    }

    public ApplicationInfo ApplicationInfo { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
}
