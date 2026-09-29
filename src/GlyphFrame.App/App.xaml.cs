using System.IO;
using System.Windows;
using GlyphFrame.App.Services;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Media;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Presets;
using GlyphFrame.Core.Policies;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Infrastructure.Ffmpeg;
using GlyphFrame.Infrastructure.Paths;
using GlyphFrame.Infrastructure.Settings;
using GlyphFrame.Infrastructure.Shell;
using GlyphFrame.Rendering.Conversion;
using GlyphFrame.Rendering.Grid;
using GlyphFrame.Rendering.Mapping;
using GlyphFrame.Rendering.Skia;
using Microsoft.Extensions.DependencyInjection;

namespace GlyphFrame.App;

public partial class App : Application
{
    private readonly ApplicationShutdownCoordinator _shutdownCoordinator = new(
        message => System.Diagnostics.Debug.WriteLine(message));
    private ServiceProvider? _serviceProvider;
    private CancellationTokenSource? _artifactRecoveryCancellation;
    private Task? _artifactRecoveryTask;

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

        _ = WarmFfmpegValidationAsync(
            _serviceProvider.GetRequiredService<IFfmpegValidationService>());
        _artifactRecoveryCancellation = new CancellationTokenSource();
        _artifactRecoveryTask = RecoverManagedArtifactsAsync(
            _serviceProvider.GetRequiredService<IManagedVideoArtifactSession>(),
            _artifactRecoveryCancellation.Token);

        MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            ServiceProvider? provider = Interlocked.Exchange(ref _serviceProvider, null);
            CancellationTokenSource? recoveryCancellation = Interlocked.Exchange(
                ref _artifactRecoveryCancellation,
                null);
            Task? recoveryTask = Interlocked.Exchange(ref _artifactRecoveryTask, null);
            _shutdownCoordinator.Shutdown(recoveryCancellation, recoveryTask, provider);
        }
        finally
        {
            base.OnExit(e);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(ApplicationInfo.Phase11B4);
        services.AddSingleton<IApplicationPaths, ApplicationPaths>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IFolderLauncher, WindowsFolderLauncher>();
        services.AddSingleton<IMediaTypeDetector, MediaTypeDetector>();
        services.AddSingleton<IImageMetadataReader, MetadataExtractorImageMetadataReader>();
        services.AddSingleton<IBundledFfmpegLocator, BundledFfmpegLocator>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IBinaryProcessRunner, BinaryProcessRunner>();
        services.AddSingleton<FfmpegValidationService>();
        services.AddSingleton<IFfmpegValidationService>(provider => new LoggedFfmpegValidationService(
            provider.GetRequiredService<FfmpegValidationService>(),
            provider.GetRequiredService<IErrorPresentationService>(),
            provider.GetRequiredService<IApplicationDiagnosticLogger>()));
        services.AddSingleton<MetadataExtractorVideoMetadataReader>();
        services.AddSingleton<IVideoMetadataReader, FfprobeVideoMetadataReader>();
        services.AddSingleton<IVideoFrameReader, FfmpegVideoFrameReader>();
        services.AddSingleton<ISequentialVideoFrameReader, FfmpegSequentialVideoFrameReader>();
        services.AddSingleton<IVideoEncoder, FfmpegVideoEncoder>();
        services.AddSingleton<IAudioStreamProbe, FfprobeAudioStreamProbe>();
        services.AddSingleton<IAudioCompatibilityPolicy, Mp4AudioCompatibilityPolicy>();
        services.AddSingleton<IAudioMuxer, FfmpegAudioMuxer>();
        services.AddSingleton<IMediaInputService, MediaInputService>();
        services.AddSingleton<ICharacterPresetProvider, CharacterPresetProvider>();
        services.AddSingleton<IImageDecoder, SkiaImageDecoder>();
        services.AddSingleton<IOutputDimensionResolver, OutputDimensionResolver>();
        services.AddSingleton<IGlyphMeasurer, SkiaGlyphMeasurer>();
        services.AddSingleton<ISymbolGridCalculator, SymbolGridCalculator>();
        services.AddSingleton<IBrightnessToSymbolMapper, BrightnessToSymbolMapper>();
        services.AddSingleton<IFrameToSymbolConverter, FrameToSymbolConverter>();
        services.AddSingleton<ISymbolFrameRenderer, SkiaSymbolFrameRenderer>();
        services.AddSingleton<IImagePipelineService, ImagePipelineService>();
        services.AddSingleton<IVideoExportService, StreamingVideoExportService>();
        services.AddSingleton<IVideoArtifactPublisher, VideoArtifactPublisher>();
        services.AddSingleton<IManagedVideoArtifactStore, ManagedVideoArtifactStore>();
        services.AddSingleton<IManagedVideoRenderService, ManagedVideoRenderService>();
        services.AddSingleton<IManagedVideoArtifactSession, ManagedVideoArtifactSession>();
        services.AddSingleton<IRenderedVideoPlayer, RenderedVideoPlayer>();
        services.AddSingleton<IOriginalVideoPlayer>(_ => new OriginalVideoPlayer(
            new WpfMediaPlaybackBackend(),
            new DispatcherMediaPlaybackPositionTimer(),
            fullscreenHost: new WpfVideoPlaybackFullscreenHost(VideoPlaybackSurfaceKind.Original)));
        services.AddSingleton<IVideoPlaybackCoordinator, VideoPlaybackCoordinator>();
        services.AddSingleton<IImageExporter, SkiaImageExporter>();
        services.AddSingleton<IAccentThemeService, AccentThemeService>();
        services.AddSingleton<IApplicationSettingsManager, ApplicationSettingsManager>();
        services.AddSingleton<IUserDialogService, UserDialogService>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ISettingsDialogService, SettingsDialogService>();
        services.AddSingleton<IAboutDialogService, AboutDialogService>();
        services.AddSingleton<IMediaFileDialogService, MediaFileDialogService>();
        services.AddSingleton<IImageSaveDialogService, ImageSaveDialogService>();
        services.AddSingleton<IImageSourceFactory, WpfImageSourceFactory>();
        services.AddSingleton<IExportStatusPresentationService, ExportStatusPresentationService>();
        services.AddSingleton<IErrorPresentationService, ErrorPresentationService>();
        services.AddSingleton<IApplicationDiagnosticLogger, LocalDiagnosticLogger>();
        services.AddSingleton<IClipboardService, WpfClipboardService>();
        services.AddSingleton<IErrorDetailsDialogService, ErrorDetailsDialogService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static async Task WarmFfmpegValidationAsync(IFfmpegValidationService validationService)
    {
        try
        {
            await validationService.ValidateAsync();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception
            or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"FFmpeg startup validation failed: {exception}");
        }
    }

    private static async Task RecoverManagedArtifactsAsync(
        IManagedVideoArtifactSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            await session.RecoverOrphansAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"Managed artifact recovery failed: {exception}");
        }
    }
}
