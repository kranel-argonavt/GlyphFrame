using System.Windows;
using GlyphFrame.App.Services;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Media;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Infrastructure.Paths;
using GlyphFrame.Infrastructure.Settings;
using GlyphFrame.Infrastructure.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace GlyphFrame.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        IApplicationSettingsManager settingsManager =
            _serviceProvider.GetRequiredService<IApplicationSettingsManager>();
        await settingsManager.InitializeAsync();

        MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(ApplicationInfo.Phase5);
        services.AddSingleton<IApplicationPaths, ApplicationPaths>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IFolderLauncher, WindowsFolderLauncher>();
        services.AddSingleton<IMediaTypeDetector, MediaTypeDetector>();
        services.AddSingleton<IImageMetadataReader, MetadataExtractorImageMetadataReader>();
        services.AddSingleton<IVideoMetadataReader, MetadataExtractorVideoMetadataReader>();
        services.AddSingleton<IMediaInputService, MediaInputService>();
        services.AddSingleton<IAccentThemeService, AccentThemeService>();
        services.AddSingleton<IApplicationSettingsManager, ApplicationSettingsManager>();
        services.AddSingleton<IUserDialogService, UserDialogService>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ISettingsDialogService, SettingsDialogService>();
        services.AddSingleton<IMediaFileDialogService, MediaFileDialogService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
