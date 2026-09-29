using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Settings;
using ImageMetadata = GlyphFrame.Core.Models.ImageMetadata;

namespace GlyphFrame.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IApplicationSettingsManager _settingsManager;
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly IMediaFileDialogService _mediaFileDialogService;
    private readonly IMediaInputService _mediaInputService;
    private readonly IVideoFrameReader _videoFrameReader;
    private readonly IVideoArtifactPublisher _videoArtifactPublisher;
    private readonly IManagedVideoRenderService _managedVideoRenderService;
    private readonly IManagedVideoArtifactSession _managedVideoArtifactSession;
    private readonly IRenderedVideoPlayer _renderedVideoPlayer;
    private readonly IOriginalVideoPlayer _originalVideoPlayer;
    private readonly IVideoPlaybackCoordinator _videoPlaybackCoordinator;
    private readonly IImagePipelineService _imagePipelineService;
    private readonly IImageExporter _imageExporter;
    private readonly IImageSaveDialogService _imageSaveDialogService;
    private readonly IImageSourceFactory _imageSourceFactory;
    private readonly IUserDialogService _userDialogService;
    private readonly IFolderLauncher _folderLauncher;
    private readonly ICharacterPresetProvider _presetProvider;
    private readonly IExportStatusPresentationService _exportStatusPresentationService;
    private readonly IErrorPresentationService _errorPresentationService;
    private readonly IApplicationDiagnosticLogger? _diagnosticLogger;
    private readonly IErrorDetailsDialogService _errorDetailsDialogService;
    private readonly IAboutDialogService _aboutDialogService;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly DispatcherTimer _exportElapsedTimer;
    private readonly Stopwatch _exportStopwatch = new();
    private readonly AsyncRelayCommand _browseMediaCommand;
    private readonly AsyncRelayCommand<IReadOnlyList<string>> _dropMediaCommand;
    private readonly AsyncRelayCommand _renderImageCommand;
    private readonly AsyncRelayCommand _previewFrameCommand;
    private readonly AsyncRelayCommand _renderVideoCommand;
    private readonly AsyncRelayCommand _exportImageCommand;
    private readonly AsyncRelayCommand _exportVideoCommand;
    private readonly AsyncRelayCommand _openExportFolderCommand;
    private readonly RelayCommand _cancelExportCommand;
    private readonly AsyncRelayCommand _selectOriginalCommand;
    private readonly RelayCommand _selectSymbolizedCommand;
    private readonly RelayCommand _toggleRenderedVideoPlaybackCommand;
    private readonly RelayCommand _restartRenderedVideoCommand;
    private readonly RelayCommand _toggleRenderedVideoMuteCommand;
    private readonly RelayCommand _fullscreenCommand;
    private readonly RelayCommand _toggleOriginalVideoPlaybackCommand;
    private readonly RelayCommand _restartOriginalVideoCommand;
    private readonly RelayCommand _toggleOriginalVideoMuteCommand;
    private readonly RelayCommand _originalFullscreenCommand;
    private readonly RelayCommand _showMediaInputErrorDetailsCommand;
    private readonly RelayCommand _showVideoFrameErrorDetailsCommand;
    private readonly RelayCommand _showOriginalPlaybackErrorDetailsCommand;
    private readonly RelayCommand _showVideoReviewErrorDetailsCommand;
    private readonly RelayCommand _showImageResultErrorDetailsCommand;
    private readonly RelayCommand _showVideoResultErrorDetailsCommand;
    private UiPresentationState _presentationState = UiPresentationState.Empty;
    private MediaInputState _mediaInputState = MediaInputState.Empty;
    private RenderWorkflowState _renderWorkflowState = RenderWorkflowState.Empty;
    private CancellationTokenSource? _mediaLoadCancellation;
    private CancellationTokenSource? _videoFrameCancellation;
    private CancellationTokenSource? _imageOperationCancellation;
    private CancellationTokenSource? _exportCompletionCancellation;
    private CancellationTokenSource? _originalVideoOpenCancellation;
    private readonly SemaphoreSlim _videoReadGate = new(1, 1);
    private long _mediaRequestId;
    private long _videoFrameRequestId;
    private long _imageOperationId;
    private long _exportCompletionPresentationId;
    private long _exportPresentationGeneration;
    private long _originalVideoOpenGeneration;
    private long _comparisonGeneration;
    private string? _pendingFilePath;
    private DecodedImage? _decodedImage;
    private RenderedImage? _renderedImage;
    private ImageSource? _originalImageSource;
    private ImageSource? _symbolizedImageSource;
    private string? _lastVideoExportPath;
    private Guid? _lastVideoExportArtifactId;
    private string? _lastImageExportPath;
    private CharacterPresetOption _selectedPreset = CharacterPresetOption.Default;
    private string _selectedFont = "Cascadia Mono";
    private int _selectedFontSize = 10;
    private string _selectedOutputResolution = "Same as source";
    private SymbolPolarity _selectedSymbolPolarity = SymbolPolarity.LightOnDark;
    private string _selectedImageOutputFormat = "PNG";
    private AudioExportMode _selectedAudioMode = AudioExportMode.KeepAudio;
    private PreviewMode _previewMode = PreviewMode.Original;
    private string _selectedFitMode = "Fit";
    private string _statusMessage = "Ready - select an image or video to begin.";
    private string _activeOperationTitle = "Processing image...";
    private string _activeOperationText = "Preparing local image processing";
    private string _lastGridSummary = string.Empty;
    private string _lastGridDimensions = string.Empty;
    private string _lastOutputDimensions = string.Empty;
    private string? _imageRenderWarning;
    private double _timelinePosition;
    private double _resolvedVideoPosition;
    private TimeSpan? _resolvedVideoTimestamp;
    private TimeSpan? _resolvedVideoDuration;
    private long? _resolvedVideoFrameIndex;
    private PreviewIdentity? _resolvedVideoFrameIdentity;
    private double _exportProgress;
    private bool _isExportProgressIndeterminate = true;
    private string _exportFrameText = string.Empty;
    private string _elapsedText = "Elapsed 00:00:00";
    private TimeSpan _lastExportElapsed;
    private string _remainingText = string.Empty;
    private bool _isExportCancellationRequested;
    private bool _isExportCompletionVisible;
    private VideoExportStage _exportStage = VideoExportStage.Preparing;
    private bool _isImageProcessing;
    private bool _isVideoFrameLoading;
    private bool _suppressTimelineSeek;
    private bool _suppressRenderedVideoSeek;
    private bool _suppressOriginalVideoSeek;
    private bool _disposed;
    private string? _videoFrameError;
    private string? _videoFrameTechnicalDetail;
    private bool _videoReviewUnavailable;
    private ErrorPresentation? _mediaInputErrorPresentation;
    private ErrorPresentation? _videoFrameErrorPresentation;
    private ErrorPresentation? _imageRenderErrorPresentation;
    private ErrorPresentation? _imageExportErrorPresentation;
    private ErrorPresentation? _videoRenderErrorPresentation;
    private ErrorPresentation? _videoPublicationErrorPresentation;
    private ErrorPresentation? _videoReviewErrorPresentation;
    private ErrorPresentation? _originalPlaybackErrorPresentation;
    private readonly SemaphoreSlim _reviewRecoveryGate = new(1, 1);
    private CancellationTokenSource _reviewRecoveryCancellation = new();
    private bool _reviewRecoveryPending;
    private bool _reviewRecoveryActive;
    private bool _managedVideoCommitStarted;
    private bool _reviewTransitionActive;
    private bool _reviewViewUnloaded;
    private long _reviewViewGeneration;
    private TimeSpan _suspendedReviewPosition;
    private TimeSpan _suspendedOriginalPosition;
    private PendingOriginalComparison? _pendingOriginalComparison;
    private bool _comparisonFrameRequestActive;
    private readonly bool _videoRenderToolsAvailable;

    public MainWindowViewModel(
        ApplicationInfo applicationInfo,
        IApplicationSettingsManager settingsManager,
        ISettingsDialogService settingsDialogService,
        IMediaFileDialogService mediaFileDialogService,
        IMediaInputService mediaInputService,
        IVideoFrameReader videoFrameReader,
        IVideoArtifactPublisher videoArtifactPublisher,
        IManagedVideoRenderService managedVideoRenderService,
        IManagedVideoArtifactSession managedVideoArtifactSession,
        IRenderedVideoPlayer renderedVideoPlayer,
        IOriginalVideoPlayer originalVideoPlayer,
        IVideoPlaybackCoordinator videoPlaybackCoordinator,
        IBundledFfmpegLocator ffmpegLocator,
        IImagePipelineService imagePipelineService,
        IImageExporter imageExporter,
        IImageSaveDialogService imageSaveDialogService,
        IImageSourceFactory imageSourceFactory,
        IUserDialogService userDialogService,
        IFolderLauncher folderLauncher,
        ICharacterPresetProvider presetProvider,
        IExportStatusPresentationService exportStatusPresentationService,
        IErrorPresentationService errorPresentationService,
        IErrorDetailsDialogService errorDetailsDialogService,
        IApplicationDiagnosticLogger? diagnosticLogger = null,
        IAboutDialogService? aboutDialogService = null)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _settingsDialogService = settingsDialogService ?? throw new ArgumentNullException(nameof(settingsDialogService));
        _mediaFileDialogService = mediaFileDialogService ?? throw new ArgumentNullException(nameof(mediaFileDialogService));
        _mediaInputService = mediaInputService ?? throw new ArgumentNullException(nameof(mediaInputService));
        _videoFrameReader = videoFrameReader ?? throw new ArgumentNullException(nameof(videoFrameReader));
        _videoArtifactPublisher = videoArtifactPublisher ?? throw new ArgumentNullException(nameof(videoArtifactPublisher));
        _managedVideoRenderService = managedVideoRenderService
            ?? throw new ArgumentNullException(nameof(managedVideoRenderService));
        _managedVideoArtifactSession = managedVideoArtifactSession
            ?? throw new ArgumentNullException(nameof(managedVideoArtifactSession));
        _renderedVideoPlayer = renderedVideoPlayer ?? throw new ArgumentNullException(nameof(renderedVideoPlayer));
        _originalVideoPlayer = originalVideoPlayer ?? throw new ArgumentNullException(nameof(originalVideoPlayer));
        _videoPlaybackCoordinator = videoPlaybackCoordinator
            ?? throw new ArgumentNullException(nameof(videoPlaybackCoordinator));
        ArgumentNullException.ThrowIfNull(ffmpegLocator);
        _imagePipelineService = imagePipelineService ?? throw new ArgumentNullException(nameof(imagePipelineService));
        _imageExporter = imageExporter ?? throw new ArgumentNullException(nameof(imageExporter));
        _imageSaveDialogService = imageSaveDialogService ?? throw new ArgumentNullException(nameof(imageSaveDialogService));
        _imageSourceFactory = imageSourceFactory ?? throw new ArgumentNullException(nameof(imageSourceFactory));
        _userDialogService = userDialogService ?? throw new ArgumentNullException(nameof(userDialogService));
        _folderLauncher = folderLauncher ?? throw new ArgumentNullException(nameof(folderLauncher));
        _presetProvider = presetProvider ?? throw new ArgumentNullException(nameof(presetProvider));
        _exportStatusPresentationService = exportStatusPresentationService
            ?? throw new ArgumentNullException(nameof(exportStatusPresentationService));
        _errorPresentationService = errorPresentationService
            ?? throw new ArgumentNullException(nameof(errorPresentationService));
        _errorDetailsDialogService = errorDetailsDialogService
            ?? throw new ArgumentNullException(nameof(errorDetailsDialogService));
        _diagnosticLogger = diagnosticLogger;
        _aboutDialogService = aboutDialogService ?? throw new ArgumentNullException(nameof(aboutDialogService));
        _synchronizationContext = SynchronizationContext.Current;
        _exportElapsedTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _exportElapsedTimer.Tick += OnExportElapsedTimerTick;
        _renderedVideoPlayer.StateChanged += OnRenderedVideoPlayerStateChanged;
        _originalVideoPlayer.StateChanged += OnOriginalVideoPlayerStateChanged;
        BundledFfmpegLocation tools = ffmpegLocator.Locate();
        _videoRenderToolsAvailable = tools.Ffmpeg.Exists
            && !tools.Ffmpeg.IsDirectory
            && tools.Ffprobe.Exists
            && !tools.Ffprobe.IsDirectory;

        ApplicationInfo = applicationInfo;
        ApplyApplicationSettings(_settingsManager.Current, invalidatePreview: false);
        _settingsManager.SettingsChanged += OnSettingsChanged;

        if (!string.IsNullOrWhiteSpace(_settingsManager.StartupStatusMessage))
        {
            _statusMessage = _settingsManager.StartupStatusMessage;
        }

        _browseMediaCommand = new AsyncRelayCommand(
            BrowseMediaAsync,
            () => IsBrowseMediaAvailable,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        _dropMediaCommand = new AsyncRelayCommand<IReadOnlyList<string>>(
            HandleDroppedPathsAsync,
            paths => IsBrowseMediaAvailable && paths is not null,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        _renderImageCommand = new AsyncRelayCommand(RenderImageAsync, CanRenderImage);
        _previewFrameCommand = new AsyncRelayCommand(PreviewFrameAsync, CanPreviewFrame);
        _renderVideoCommand = new AsyncRelayCommand(RenderVideoAsync, CanRenderVideo);
        _exportImageCommand = new AsyncRelayCommand(ExportImageAsync, CanExportImage);
        _exportVideoCommand = new AsyncRelayCommand(ExportVideoAsync, CanExportVideo);
        _openExportFolderCommand = new AsyncRelayCommand(OpenExportFolderAsync, CanOpenExportFolderAction);
        _cancelExportCommand = new RelayCommand(CancelActiveImageOperation, () => CanCancelExport);
        _selectOriginalCommand = new AsyncRelayCommand(
            SelectOriginalPreviewAsync,
            () => IsMediaSelected && !IsLoadingMedia && !IsExportActive);
        _selectSymbolizedCommand = new RelayCommand(
            SelectSymbolizedPreview,
            () => HasSymbolizedReview && !IsLoadingMedia && !IsExportActive);
        _toggleRenderedVideoPlaybackCommand = new RelayCommand(
            ToggleRenderedVideoPlayback,
            () => ShowRenderedVideoPlayer && !IsExportActive);
        _restartRenderedVideoCommand = new RelayCommand(
            _renderedVideoPlayer.Restart,
            () => ShowRenderedVideoPlayer && !IsExportActive);
        _toggleRenderedVideoMuteCommand = new RelayCommand(
            _renderedVideoPlayer.ToggleMute,
            () => HasVideoArtifact);
        _fullscreenCommand = new RelayCommand(
            () => _renderedVideoPlayer.ShowFullscreen(SelectedFitMode),
            () => ShowRenderedVideoPlayer && !IsExportActive);
        _toggleOriginalVideoPlaybackCommand = new RelayCommand(
            ToggleOriginalVideoPlayback,
            CanControlOriginalVideo);
        _restartOriginalVideoCommand = new RelayCommand(
            () => _videoPlaybackCoordinator.RestartOriginal(),
            CanControlOriginalVideo);
        _toggleOriginalVideoMuteCommand = new RelayCommand(
            _originalVideoPlayer.ToggleMute,
            CanControlOriginalVideo);
        _originalFullscreenCommand = new RelayCommand(
            () => _originalVideoPlayer.ShowFullscreen(SelectedFitMode),
            CanControlOriginalVideo);
        _showMediaInputErrorDetailsCommand = CreateErrorDetailsCommand(() => _mediaInputErrorPresentation);
        _showVideoFrameErrorDetailsCommand = CreateErrorDetailsCommand(() => _videoFrameErrorPresentation);
        _showOriginalPlaybackErrorDetailsCommand = CreateErrorDetailsCommand(() => _originalPlaybackErrorPresentation);
        _showVideoReviewErrorDetailsCommand = CreateErrorDetailsCommand(() => _videoReviewErrorPresentation);
        _showImageResultErrorDetailsCommand = CreateErrorDetailsCommand(() => VisibleImageResultErrorPresentation);
        _showVideoResultErrorDetailsCommand = CreateErrorDetailsCommand(() => VisibleVideoResultErrorPresentation);

        AboutCommand = new RelayCommand(() => _aboutDialogService.Show(ApplicationInfo));
        SettingsCommand = new RelayCommand(
            () => _settingsDialogService.Show(),
            () => AreSettingsEnabled);
    }

    public ApplicationInfo ApplicationInfo { get; }

    public string ApplicationVersion => _aboutDialogService.VersionText;

    public IReadOnlyList<CharacterPresetOption> Presets => _presetProvider.GetPresets();

    public IReadOnlyList<string> FontFamilies => AppSettingsCatalog.FontFamilies;

    public IReadOnlyList<int> FontSizes => AppSettingsCatalog.FontSizes;

    public IReadOnlyList<string> OutputResolutions { get; } =
        AppSettingsCatalog.OutputResolutions.Select(option => option.DisplayName).ToArray();

    public IReadOnlyList<SettingOption<SymbolPolarity>> SymbolPolarities => AppSettingsCatalog.SymbolPolarities;

    public IReadOnlyList<string> ImageOutputFormats { get; } =
        AppSettingsCatalog.ImageExportFormats.Select(option => option.DisplayName).ToArray();

    public IReadOnlyList<string> ColorModes { get; } = ["Original Colors"];

    public IReadOnlyList<string> FitModes { get; } = ["Fit", "Fill", "100%"];

    public IAsyncRelayCommand BrowseMediaCommand => _browseMediaCommand;

    public IAsyncRelayCommand<IReadOnlyList<string>> DropMediaCommand => _dropMediaCommand;

    public IAsyncRelayCommand RenderImageCommand => _renderImageCommand;

    public IAsyncRelayCommand PreviewFrameCommand => _previewFrameCommand;

    public IAsyncRelayCommand RenderVideoCommand => _renderVideoCommand;

    public IAsyncRelayCommand ExportImageCommand => _exportImageCommand;

    public IAsyncRelayCommand ExportVideoCommand => _exportVideoCommand;

    public IAsyncRelayCommand OpenExportFolderCommand => _openExportFolderCommand;

    public IRelayCommand CancelExportCommand => _cancelExportCommand;

    public IAsyncRelayCommand SelectOriginalCommand => _selectOriginalCommand;

    public IRelayCommand SelectSymbolizedCommand => _selectSymbolizedCommand;

    public IRelayCommand ToggleRenderedVideoPlaybackCommand => _toggleRenderedVideoPlaybackCommand;

    public IRelayCommand RestartRenderedVideoCommand => _restartRenderedVideoCommand;

    public IRelayCommand ToggleRenderedVideoMuteCommand => _toggleRenderedVideoMuteCommand;

    public IRelayCommand FullscreenCommand => _fullscreenCommand;

    public IRelayCommand ToggleOriginalVideoPlaybackCommand => _toggleOriginalVideoPlaybackCommand;

    public IRelayCommand RestartOriginalVideoCommand => _restartOriginalVideoCommand;

    public IRelayCommand ToggleOriginalVideoMuteCommand => _toggleOriginalVideoMuteCommand;

    public IRelayCommand OriginalFullscreenCommand => _originalFullscreenCommand;

    public IRelayCommand ShowMediaInputErrorDetailsCommand => _showMediaInputErrorDetailsCommand;

    public IRelayCommand ShowVideoFrameErrorDetailsCommand => _showVideoFrameErrorDetailsCommand;

    public IRelayCommand ShowOriginalPlaybackErrorDetailsCommand => _showOriginalPlaybackErrorDetailsCommand;

    public IRelayCommand ShowVideoReviewErrorDetailsCommand => _showVideoReviewErrorDetailsCommand;

    public IRelayCommand ShowImageResultErrorDetailsCommand => _showImageResultErrorDetailsCommand;

    public IRelayCommand ShowVideoResultErrorDetailsCommand => _showVideoResultErrorDetailsCommand;

    public IRelayCommand AboutCommand { get; }

    public IRelayCommand SettingsCommand { get; }

    public UiPresentationState PresentationState => _presentationState;

    public MediaInputState MediaInputState => _mediaInputState;

    public MediaInputResult? CurrentMedia => _mediaInputState.CurrentSelection;

    public ImageMetadata? ImageMetadata => CurrentMedia?.ImageMetadata;

    public VideoMetadata? VideoMetadata => CurrentMedia?.VideoMetadata;

    public bool IsNoMediaSelected => !_mediaInputState.HasSelection;

    public bool IsMediaSelected => _mediaInputState.HasSelection;

    public bool IsImageSelected => _mediaInputState.SelectedMediaType == MediaSelectionKind.Image;

    public bool IsVideoSelected => _mediaInputState.SelectedMediaType == MediaSelectionKind.Video;

    public bool IsLoadingMedia => _mediaInputState.Status == MediaInputStatus.Loading;

    public bool HasMediaInputError => _mediaInputState.Status == MediaInputStatus.Failed;

    public ErrorPresentation? MediaInputErrorPresentation => _mediaInputErrorPresentation;

    public bool ShowMediaInputErrorDetails => HasTechnicalDetails(_mediaInputErrorPresentation);
    public bool ShowRetainedMediaInputErrorDetails => IsMediaSelected && HasMediaInputError && ShowMediaInputErrorDetails;

    public bool ShowEmptyPreview => IsNoMediaSelected && !IsLoadingMedia && !HasMediaInputError;

    public bool ShowLoadingPreview => IsLoadingMedia;

    public bool HasVideoFrame => IsVideoSelected && _decodedImage is not null && _originalImageSource is not null;

    public bool IsVideoFrameLoading
    {
        get => _isVideoFrameLoading;
        private set => SetProperty(ref _isVideoFrameLoading, value);
    }

    public bool HasVideoFrameError => IsVideoSelected && !string.IsNullOrWhiteSpace(_videoFrameError);

    public string VideoFrameStatusMessage => _videoFrameError
        ?? (IsVideoFrameLoading ? "Loading the selected video frame locally..." : "Video frame preview is ready.");

    public string? VideoFrameTechnicalDetail => _videoFrameTechnicalDetail;

    public ErrorPresentation? VideoFrameErrorPresentation => _videoFrameErrorPresentation;

    public bool ShowVideoFrameErrorDetails => HasTechnicalDetails(_videoFrameErrorPresentation);

    public bool ShowVideoFrameLoading => IsVideoSelected
        && IsVideoFrameLoading
        && !ShowOriginalVideoPlayer
        && (!IsSymbolizedPreviewSelected || !HasVideoArtifact);

    public bool ShowVideoFrameError => IsVideoSelected
        && HasVideoFrameError
        && !IsVideoFrameLoading
        && !ShowOriginalVideoPlayer
        && (!IsSymbolizedPreviewSelected || !HasVideoArtifact);

    public bool ShowMediaPlaceholder => IsVideoSelected
        && !IsLoadingMedia
        && !HasVideoFrame
        && !IsVideoFrameLoading
        && !HasVideoFrameError
        && !ShowOriginalVideoPlayer
        && !ShowRenderedVideoPlayer;

    public bool ShowImagePreview => IsMediaSelected
        && CurrentPreviewImageSource is not null
        && !IsLoadingMedia
        && !ShowOriginalVideoPlayer
        && !ShowRenderedVideoPlayer;

    public bool ShowErrorPreview => IsNoMediaSelected && HasMediaInputError;

    public bool IsBrowseMediaAvailable => !IsExportActive && !IsLoadingMedia;

    public bool IsExportActive => _presentationState.IsExportActive;

    public bool IsExportStatusVisible => IsExportActive || IsExportCompletionVisible;

    public bool IsProcessingResultPanelVisible => IsExportStatusVisible
        || ShowImageResultPanel
        || ShowVideoResultPanel;

    public bool AreExportAnimationsEnabled => _exportStatusPresentationService.AnimationsEnabled;

    public long ExportPresentationGeneration => _exportPresentationGeneration;

    public VideoExportStage ExportStage
    {
        get => _exportStage;
        private set => SetProperty(ref _exportStage, value);
    }

    public bool IsExportCompletionVisible
    {
        get => _isExportCompletionVisible;
        private set
        {
            if (SetProperty(ref _isExportCompletionVisible, value))
            {
                OnPropertyChanged(nameof(IsExportStatusVisible));
                OnPropertyChanged(nameof(IsProcessingResultPanelVisible));
            }
        }
    }

    public bool CanCancelExport => IsExportActive && !IsExportCancellationRequested
        && !_managedVideoCommitStarted;

    public bool IsExportCancellationRequested
    {
        get => _isExportCancellationRequested;
        private set
        {
            if (SetProperty(ref _isExportCancellationRequested, value))
            {
                OnPropertyChanged(nameof(CanCancelExport));
            }
        }
    }

    public bool IsImageProcessing
    {
        get => _isImageProcessing;
        private set => SetProperty(ref _isImageProcessing, value);
    }

    public bool CanUseMediaActions => CanRenderImage() || CanPreviewFrame() || CanRenderVideo();

    public RenderWorkflowStatus WorkflowStatus => _renderWorkflowState.Status;

    public bool HasSymbolizedPreview => _renderWorkflowState.HasSymbolizedPreview
        && _renderedImage is not null
        && _symbolizedImageSource is not null;

    public bool HasVideoArtifact => IsVideoSelected
        && _renderWorkflowState.HasVideoArtifact
        && _managedVideoArtifactSession.Current is not null;

    public bool HasSymbolizedReview => HasVideoArtifact || HasSymbolizedPreview;

    public bool IsSymbolizedPreviewStale => IsVideoSelected && HasVideoArtifact
        ? _renderWorkflowState.IsVideoArtifactStale
        : HasSymbolizedPreview && _renderWorkflowState.IsPreviewStale;

    public bool ShowRenderedVideoPlayer => IsVideoSelected
        && IsSymbolizedPreviewSelected
        && HasVideoArtifact
        && _renderedVideoPlayer.IsOpen
        && _managedVideoArtifactSession.Current?.FilePath.Equals(
            _renderedVideoPlayer.SourcePath,
            StringComparison.OrdinalIgnoreCase) == true;

    public ImageSource RenderedVideoPlaybackSource => _renderedVideoPlayer.PlaybackSource;

    public bool IsOriginalVideoPlaybackOpening => IsVideoSelected
        && _originalVideoPlayer.IsOpening
        && VideoMetadata?.FilePath.Equals(_originalVideoPlayer.SourcePath, StringComparison.OrdinalIgnoreCase) == true;

    public bool IsOriginalVideoPlaybackReady => IsVideoSelected
        && _originalVideoPlayer.IsPlaybackReady
        && VideoMetadata?.FilePath.Equals(_originalVideoPlayer.SourcePath, StringComparison.OrdinalIgnoreCase) == true;

    public bool IsOriginalVideoPlaybackFallback => IsVideoSelected
        && !IsOriginalVideoPlaybackOpening
        && _originalVideoPlayer.FallbackRequired
        && VideoMetadata?.FilePath.Equals(_originalVideoPlayer.SourcePath, StringComparison.OrdinalIgnoreCase) == true;

    public bool ShowOriginalVideoPlayer => IsOriginalPreviewSelected && IsOriginalVideoPlaybackReady;

    public bool ShowOriginalPlaybackOpening => IsOriginalPreviewSelected && IsOriginalVideoPlaybackOpening;

    public bool ShowOriginalPlaybackFallback => IsOriginalPreviewSelected && IsOriginalVideoPlaybackFallback;

    public ImageSource OriginalVideoPlaybackSource => _originalVideoPlayer.PlaybackSource;

    public string OriginalVideoPlaybackLimitationMessage => _originalPlaybackErrorPresentation?.UserMessage
        ?? OriginalVideoPlayer.PlaybackUnavailableMessage;

    public string? OriginalVideoPlaybackTechnicalDetail => _originalPlaybackErrorPresentation?.TechnicalDetail
        ?? _originalVideoPlayer.TechnicalDetail;

    public ErrorPresentation? OriginalPlaybackErrorPresentation => _originalPlaybackErrorPresentation;

    public bool ShowOriginalPlaybackErrorDetails => HasTechnicalDetails(_originalPlaybackErrorPresentation);

    public bool ShowRenderedVideoReviewError => IsVideoSelected
        && IsSymbolizedPreviewSelected
        && HasVideoArtifact
        && _videoReviewUnavailable;

    public bool ShowPreviewStatusBadge => IsSymbolizedPreviewSelected
        && (IsImageSelected || !HasVideoArtifact || _renderWorkflowState.IsVideoArtifactStale);

    public bool HasImageRender => IsImageSelected
        && _renderWorkflowState.HasImageRender
        && _renderedImage is not null
        && _symbolizedImageSource is not null;

    public bool IsImageRenderStale => HasImageRender && _renderWorkflowState.IsImageRenderStale;

    public bool ShowImageResultPanel => IsImageSelected
        && _renderWorkflowState.Status is RenderWorkflowStatus.RenderReady
            or RenderWorkflowStatus.RenderStale
            or RenderWorkflowStatus.Exported
            or RenderWorkflowStatus.Cancelled
            or RenderWorkflowStatus.Failed;

    private RenderWorkflowStatus VideoResultStatus =>
        _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportVideo
        && _renderWorkflowState.Status == RenderWorkflowStatus.Exported
        ? _renderWorkflowState.VideoReviewStatus
        : _renderWorkflowState.Status;

    public bool ShowVideoResultPanel => IsVideoSelected
        && !IsExportActive
        && (HasVideoArtifact
            || _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.RenderVideo)
        && VideoResultStatus is RenderWorkflowStatus.RenderReady
            or RenderWorkflowStatus.RenderStale
            or RenderWorkflowStatus.Cancelled
            or RenderWorkflowStatus.Failed;

    private bool IsAcceptedReviewOpen => !_videoReviewUnavailable
        && _renderedVideoPlayer.IsOpen
        && _managedVideoArtifactSession.Current is ManagedVideoArtifact artifact
        && _renderedVideoPlayer.SourcePath == artifact.FilePath;

    public bool IsVideoResultSuccess => VideoResultStatus == RenderWorkflowStatus.RenderReady
        && IsAcceptedReviewOpen;

    public bool IsVideoResultWarning => !IsVideoResultFailure && VideoResultStatus is
        RenderWorkflowStatus.RenderStale or RenderWorkflowStatus.Cancelled;

    public bool IsVideoResultFailure => _renderWorkflowState.Status == RenderWorkflowStatus.Failed
        || _videoReviewUnavailable;

    public string VideoRenderActionLabel => _managedVideoArtifactSession.Current is null
        ? "Render Video"
        : "Render Again";

    public string VideoResultTitle => _videoReviewUnavailable
        ? "Video Review Unavailable"
        : HasVideoArtifact && !IsAcceptedReviewOpen
            ? _reviewViewUnloaded ? "Video Review Suspended" : "Opening Video Review"
        : VideoResultStatus switch
        {
            RenderWorkflowStatus.Cancelled when _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportVideo
                => "Video Export Cancelled",
            RenderWorkflowStatus.Failed when _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportVideo
                => "Video Export Failed",
            RenderWorkflowStatus.RenderStale => "Render Stale",
            RenderWorkflowStatus.Cancelled when _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.PreviewVideoFrame
                => "Frame Preview Cancelled",
            RenderWorkflowStatus.Failed when _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.PreviewVideoFrame
                => "Frame Preview Failed",
            RenderWorkflowStatus.Cancelled => "Video Rendering Cancelled",
            RenderWorkflowStatus.Failed => "Video Render Failed",
            _ when HasVideoExportDestination => "Video Exported",
            _ => "Video Ready"
        };

    public string VideoResultSummary
    {
        get
        {
            if (_videoReviewUnavailable)
            {
                return "The artifact file is retained, but playback is unavailable. A failed or cancelled render retry does not restore playback.";
            }

            if (HasVideoArtifact && !IsAcceptedReviewOpen)
            {
                return "The artifact file is retained. Review resumes when the loaded view can open it successfully.";
            }

            if (_renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportVideo
                && _renderWorkflowState.Status is RenderWorkflowStatus.Failed or RenderWorkflowStatus.Cancelled)
            {
                string outcome = _renderWorkflowState.Status == RenderWorkflowStatus.Failed
                    ? _videoPublicationErrorPresentation?.UserMessage ?? "Video export did not complete."
                    : "Video export was cancelled.";
                return $"{outcome} The managed review is retained"
                    + (_renderWorkflowState.IsVideoArtifactStale ? " and remains stale." : " and remains current.");
            }

            if (VideoResultStatus == RenderWorkflowStatus.RenderStale)
            {
                return "Render settings or audio mode changed. The previous video remains playable. Use Render Again before exporting.";
            }

            if (_renderWorkflowState.Status == RenderWorkflowStatus.Cancelled)
            {
                return HasVideoArtifact
                    ? "Rendering was cancelled. The previous managed video remains available for review."
                    : "Rendering was cancelled. No managed review video was retained.";
            }

            if (_renderWorkflowState.Status == RenderWorkflowStatus.Failed)
            {
                string failure = _videoRenderErrorPresentation?.UserMessage
                    ?? "GlyphFrame could not render the complete video. You can try again.";
                return HasVideoArtifact
                    ? $"{failure} The previous managed video remains available for review."
                    : failure;
            }

            return _managedVideoArtifactSession.Current is not null
                ? HasVideoExportDestination
                    ? "Ready for review and another export."
                    : "Ready for review and export."
                : "The rendered video is ready for review.";
        }
    }

    public bool ShowVideoRenderMetrics => HasVideoArtifact;

    public string VideoRenderedFrameCountText => _managedVideoArtifactSession.Current is ManagedVideoArtifact artifact
        ? artifact.RenderedFrameCount.ToString("N0")
        : string.Empty;

    public string VideoOutputDimensionsText => _managedVideoArtifactSession.Current is ManagedVideoArtifact artifact
        ? $"{artifact.OutputDimensions.Width} × {artifact.OutputDimensions.Height}"
        : string.Empty;

    public string VideoAudioOutcomeText => _managedVideoArtifactSession.Current is ManagedVideoArtifact artifact
        ? FormatAudioOutcomeValue(artifact.AudioOutcome)
        : string.Empty;

    public string? VideoAudioOutcomeDescription => _managedVideoArtifactSession.Current is ManagedVideoArtifact artifact
        ? FormatAudioOutcomeDescription(artifact.AudioOutcome)
        : null;

    public ErrorPresentation? ImageRenderErrorPresentation => _imageRenderErrorPresentation;

    public ErrorPresentation? ImageExportErrorPresentation => _imageExportErrorPresentation;

    public ErrorPresentation? VideoRenderErrorPresentation => _videoRenderErrorPresentation;

    public ErrorPresentation? VideoPublicationErrorPresentation => _videoPublicationErrorPresentation;

    public ErrorPresentation? VideoReviewErrorPresentation => _videoReviewErrorPresentation;

    public bool ShowVideoReviewErrorDetails => HasTechnicalDetails(_videoReviewErrorPresentation);

    private ErrorPresentation? VisibleImageResultErrorPresentation =>
        _renderWorkflowState.Status == RenderWorkflowStatus.Failed
            ? _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportImage
                ? _imageExportErrorPresentation
                : _imageRenderErrorPresentation
            : null;

    private ErrorPresentation? VisibleVideoResultErrorPresentation => _videoReviewUnavailable
        ? _videoReviewErrorPresentation
        : _renderWorkflowState.Status == RenderWorkflowStatus.Failed
            ? _renderWorkflowState.OutcomeOperation switch
            {
                RenderWorkflowOperation.ExportVideo => _videoPublicationErrorPresentation,
                RenderWorkflowOperation.PreviewVideoFrame => _videoFrameErrorPresentation,
                _ => _videoRenderErrorPresentation
            }
            : null;

    public bool ShowImageResultErrorDetails => HasTechnicalDetails(VisibleImageResultErrorPresentation);

    public bool ShowVideoResultErrorDetails => HasTechnicalDetails(VisibleVideoResultErrorPresentation);

    private ErrorPresentation? CurrentVideoOperationError =>
        _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportVideo
            ? _videoPublicationErrorPresentation
            : _videoRenderErrorPresentation;

    public string? VideoRenderTechnicalDetail => _videoReviewErrorPresentation?.TechnicalDetail is string review
        && CurrentVideoOperationError?.TechnicalDetail is string operation
        ? $"Review: {review}\nLatest operation: {operation}"
        : _videoReviewErrorPresentation?.TechnicalDetail ?? CurrentVideoOperationError?.TechnicalDetail;

    public bool ShowImageExportControls => HasImageRender;

    public bool ShowImageRenderMetrics => HasImageRender
        && !string.IsNullOrWhiteSpace(_lastGridDimensions)
        && !string.IsNullOrWhiteSpace(_lastOutputDimensions);

    public string ImageGridDimensionsText => _lastGridDimensions;

    public string ImageOutputDimensionsText => _lastOutputDimensions;

    public string ImageRenderWarningText => _imageRenderWarning ?? string.Empty;

    public bool ShowImageRenderWarning => HasImageRender && !string.IsNullOrWhiteSpace(_imageRenderWarning);

    public bool IsImageResultSuccess => _renderWorkflowState.Status is
        RenderWorkflowStatus.RenderReady or RenderWorkflowStatus.Exported;

    public bool IsImageResultWarning => _renderWorkflowState.Status is
        RenderWorkflowStatus.RenderStale or RenderWorkflowStatus.Cancelled;

    public bool IsImageResultFailure => _renderWorkflowState.Status == RenderWorkflowStatus.Failed;

    public bool IsImageExported => _renderWorkflowState.Status == RenderWorkflowStatus.Exported;

    public bool HasVideoExportDestination => HasVideoArtifact
        && _lastVideoExportArtifactId == _managedVideoArtifactSession.Current?.ArtifactId
        && !string.IsNullOrWhiteSpace(_lastVideoExportPath);

    public string VideoExportDestinationText => HasVideoExportDestination ? _lastVideoExportPath! : string.Empty;

    public string VideoExportActionLabel => HasVideoExportDestination ? "Export Again" : "Export Video";

    public bool CanOpenExportFolder => !IsExportActive
        && (IsImageSelected ? IsImageExported && !string.IsNullOrWhiteSpace(_lastImageExportPath)
            : HasVideoExportDestination);

    public string ImageRenderActionLabel => HasImageRender ? "Render Again" : "Render Image";

    public string ImageExportActionLabel => IsImageExported ? "Export Again" : "Export Image";

    public string ImageResultTitle => _renderWorkflowState.Status switch
    {
        RenderWorkflowStatus.Exported => "Image Exported",
        RenderWorkflowStatus.RenderStale => "Render Stale",
        RenderWorkflowStatus.Cancelled
            when _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportImage
            => "Image Export Cancelled",
        RenderWorkflowStatus.Cancelled => "Image Rendering Cancelled",
        RenderWorkflowStatus.Failed
            when _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportImage
            => "Image Export Failed",
        RenderWorkflowStatus.Failed => "Image Render Failed",
        _ => "Image Ready"
    };

    public string ImageResultSummary => _renderWorkflowState.Status switch
    {
        RenderWorkflowStatus.Exported when !string.IsNullOrWhiteSpace(_lastImageExportPath)
            => $"Saved {Path.GetFileName(_lastImageExportPath)}. The rendered image remains ready for another export.",
        RenderWorkflowStatus.RenderStale
            => "Render settings changed. Review the previous result or render again before exporting.",
        RenderWorkflowStatus.Cancelled
            when _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportImage
            => "Image export was cancelled. The rendered image remains ready for another export.",
        RenderWorkflowStatus.Cancelled when HasImageRender
            => "Image rendering was cancelled. Your previous rendered image is still available.",
        RenderWorkflowStatus.Cancelled
            => "Rendering was cancelled. The original image remains available.",
        RenderWorkflowStatus.Failed
            when _renderWorkflowState.OutcomeOperation == RenderWorkflowOperation.ExportImage
            => $"{_imageExportErrorPresentation?.UserMessage ?? "GlyphFrame could not export the image."} The rendered image remains ready to try again.",
        RenderWorkflowStatus.Failed when HasImageRender
            => $"{_imageRenderErrorPresentation?.UserMessage ?? "GlyphFrame could not render the image."} Your previous rendered image is still available.",
        RenderWorkflowStatus.Failed
            => _imageRenderErrorPresentation?.UserMessage ?? "GlyphFrame could not render this image. You can try again.",
        _ => string.IsNullOrWhiteSpace(_lastGridSummary)
            ? "The symbolized image is ready for review and export."
            : "Ready for review and export."
    };


    public bool AreSettingsEnabled => !IsExportActive && !IsLoadingMedia && !IsVideoFrameLoading;

    public bool IsVideoSeekEnabled => IsVideoSelected
        && VideoMetadata is not null
        && !IsLoadingMedia
        && !IsExportActive;

    public bool ShowVideoFrameControls => IsVideoSelected
        && (!IsSymbolizedPreviewSelected || !HasVideoArtifact)
        && !(IsOriginalPreviewSelected && IsOriginalVideoPlaybackReady);

    public string RenderedVideoPlaybackActionLabel => _renderedVideoPlayer.HasEnded
        ? "Restart"
        : _renderedVideoPlayer.IsPlaying ? "Pause" : "Play";

    public string RenderedVideoMuteActionLabel => _renderedVideoPlayer.IsMuted ? "Unmute" : "Mute";

    public bool IsRenderedVideoPlaying => _renderedVideoPlayer.IsPlaying;
    public bool IsRenderedVideoMuted => _renderedVideoPlayer.IsMuted;
    public bool ShowRenderedVideoReplay => _renderedVideoPlayer.IsOpen && _renderedVideoPlayer.HasEnded;
    public double RenderedVideoPixelWidth => _managedVideoArtifactSession.Current?.OutputDimensions.Width ?? 0;
    public double RenderedVideoPixelHeight => _managedVideoArtifactSession.Current?.OutputDimensions.Height ?? 0;
    public string RenderedVideoCurrentTimeText => FormatDuration(_renderedVideoPlayer.Position);
    public string RenderedVideoTotalTimeText => FormatDuration(ResolveRenderedVideoDuration());
    public double CurrentPreviewPixelWidth => IsSymbolizedPreviewSelected
        ? _renderedImage?.Dimensions.Width ?? 0 : _decodedImage?.Width ?? 0;
    public double CurrentPreviewPixelHeight => IsSymbolizedPreviewSelected
        ? _renderedImage?.Dimensions.Height ?? 0 : _decodedImage?.Height ?? 0;

    public double RenderedVideoPositionSeconds
    {
        get => Math.Max(0d, _renderedVideoPlayer.Position.TotalSeconds);
        set
        {
            if (!_suppressRenderedVideoSeek && double.IsFinite(value) && HasVideoArtifact)
            {
                _renderedVideoPlayer.Seek(TimeSpan.FromSeconds(Math.Max(0d, value)));
            }
        }
    }

    public double RenderedVideoDurationSeconds => Math.Max(0d, ResolveRenderedVideoDuration().TotalSeconds);

    public double RenderedVideoVolume
    {
        get => _renderedVideoPlayer.Volume;
        set => _renderedVideoPlayer.SetVolume(value);
    }

    public string OriginalVideoPlaybackActionLabel => _originalVideoPlayer.HasEnded
        ? "Restart"
        : _originalVideoPlayer.IsPlaying ? "Pause" : "Play";

    public string OriginalVideoMuteActionLabel => _originalVideoPlayer.IsMuted ? "Unmute" : "Mute";

    public bool IsOriginalVideoPlaying => _originalVideoPlayer.IsPlaying;

    public bool IsOriginalVideoMuted => _originalVideoPlayer.IsMuted;

    public bool ShowOriginalVideoReplay => IsOriginalVideoPlaybackReady && _originalVideoPlayer.HasEnded;

    public double OriginalVideoPixelWidth => VideoMetadata?.DisplayWidth
        ?? (_originalVideoPlayer.NaturalVideoWidth > 0 ? _originalVideoPlayer.NaturalVideoWidth : 0);

    public double OriginalVideoPixelHeight => VideoMetadata?.DisplayHeight
        ?? (_originalVideoPlayer.NaturalVideoHeight > 0 ? _originalVideoPlayer.NaturalVideoHeight : 0);

    public double OriginalVideoRotationDegrees => NormalizeVideoRotation(VideoMetadata?.NormalizedRotationDegrees ?? 0);

    public string OriginalVideoCurrentTimeText => FormatDuration(_originalVideoPlayer.Position);

    public string OriginalVideoTotalTimeText => FormatDuration(ResolveOriginalVideoDuration());

    public double OriginalVideoPositionSeconds
    {
        get => Math.Max(0d, _originalVideoPlayer.Position.TotalSeconds);
        set
        {
            if (!_suppressOriginalVideoSeek && double.IsFinite(value) && IsOriginalVideoPlaybackReady)
            {
                _originalVideoPlayer.Seek(TimeSpan.FromSeconds(Math.Max(0d, value)));
            }
        }
    }

    public double OriginalVideoDurationSeconds => Math.Max(0d, ResolveOriginalVideoDuration().TotalSeconds);

    public double OriginalVideoVolume
    {
        get => _originalVideoPlayer.Volume;
        set => _originalVideoPlayer.SetVolume(value);
    }

    public bool ShowResolutionWarning => IsImageSelected
        && ImageMetadata is not null
        && ResolveOutputResolution() == OutputResolution.SameAsSource
        && IsAbove1080p(ImageMetadata.DisplayWidth, ImageMetadata.DisplayHeight);

    public ImageSource? OriginalImageSource => _originalImageSource;

    public ImageSource? SymbolizedImageSource => _symbolizedImageSource;

    public ImageSource? CurrentPreviewImageSource => PreviewMode == PreviewMode.Symbolized
        ? SymbolizedImageSource
        : OriginalImageSource;

    public string InputLabel => _mediaInputState.SelectedMediaType switch
    {
        MediaSelectionKind.Image => "Image File",
        MediaSelectionKind.Video => "Video File",
        _ => "Media File"
    };

    public string PreviewFrameActionLabel => "Preview Frame";


    public string SelectedFileName => IsLoadingMedia
        ? _mediaInputState.PendingFileName ?? "Reading media"
        : ImageMetadata?.FileName ?? VideoMetadata?.FileName ?? "No media selected";

    public string SelectedFilePath => IsLoadingMedia
        ? _pendingFilePath ?? "Reading selected media"
        : ImageMetadata?.FilePath ?? VideoMetadata?.FilePath ?? "Choose an image or video";

    public string MediaTypeText => _mediaInputState.SelectedMediaType switch
    {
        MediaSelectionKind.Image => "Image",
        MediaSelectionKind.Video => "Video",
        _ when IsLoadingMedia => "Loading",
        _ => "Not selected"
    };

    public string MetadataPrimaryText => ImageMetadata is not null
        ? $"{FormatDimensions(ImageMetadata.DisplayWidth, ImageMetadata.DisplayHeight)}  -  {ImageMetadata.Format}  -  {FormatFileSize(ImageMetadata.FileSizeBytes)}"
        : VideoMetadata is not null
            ? $"{FormattedDimensions}  -  {FormattedFps}  -  {FormattedDuration}"
            : "Resolution  -  Duration  -  Frame rate";

    public string MetadataSecondaryText => ImageMetadata is not null
        ? BuildImageSecondaryMetadata(ImageMetadata)
        : VideoMetadata is not null
            ? $"{FormattedCodecInformation}  -  {AudioInformation}  -  {FormatFileSize(VideoMetadata.FileSizeBytes)}"
            : IsLoadingMedia
                ? "Reading local metadata without uploading media"
                : HasMediaInputError
                    ? InputStatusMessage
                    : "No media metadata available";

    public string FormattedFileSize => ImageMetadata is not null
        ? FormatFileSize(ImageMetadata.FileSizeBytes)
        : VideoMetadata is not null
            ? FormatFileSize(VideoMetadata.FileSizeBytes)
            : "Unknown";

    public string FormattedDimensions => ImageMetadata is not null
        ? FormatDimensions(ImageMetadata.DisplayWidth, ImageMetadata.DisplayHeight)
        : VideoMetadata?.DisplayWidth is int width && VideoMetadata.DisplayHeight is int height
            ? FormatDimensions(width, height)
            : "Unknown resolution";

    public string FormattedDuration => VideoMetadata?.Duration is TimeSpan duration
        ? FormatDuration(duration)
        : "Unknown duration";

    public string FormattedFps => VideoMetadata?.Fps is double fps
        ? $"{fps:0.###} FPS"
        : "Unknown FPS";

    public string FormattedFrameCount => VideoMetadata?.FrameCount is long count
        ? $"{count:N0} frames"
        : "Frame count unknown";

    public string FormattedCodecInformation => string.IsNullOrWhiteSpace(VideoMetadata?.VideoCodec)
        ? "Video codec unknown"
        : VideoMetadata.VideoCodec;

    public string OrientationText => ImageMetadata is null
        ? "Orientation unavailable"
        : FormatOrientation(ImageMetadata);

    public string AudioInformation => VideoMetadata?.HasAudio switch
    {
        true => BuildAudioInformation(VideoMetadata),
        false => "No audio track",
        _ => "Audio information unknown"
    };

    public string InputStatusMessage => _mediaInputState.Status switch
    {
        MediaInputStatus.Loading => "Validating and decoding the selected media locally...",
        MediaInputStatus.ImageSelected => "Image decoded locally and ready for symbolization.",
        MediaInputStatus.VideoSelected when IsVideoFrameLoading => "Video metadata loaded. Decoding the selected preview frame...",
        MediaInputStatus.VideoSelected when HasVideoFrameError => VideoFrameStatusMessage,
        MediaInputStatus.VideoSelected when HasVideoFrame => "Video metadata and the current preview frame are ready.",
        MediaInputStatus.VideoSelected => "Video metadata loaded. Choose a position to retry the frame preview.",
        MediaInputStatus.Failed => _mediaInputErrorPresentation?.UserMessage
            ?? "GlyphFrame could not read this media. Choose another file and try again.",
        _ => "Browse or drop one supported image or video."
    };

    public string InputMetadataSummary => IsMediaSelected
        ? $"{MediaTypeText}  -  {FormattedDimensions}  -  {FormattedFileSize}"
        : InputStatusMessage;

    public string TimelineText
    {
        get
        {
            TimeSpan? current = IsVideoFrameLoading
                ? ResolveTimelineTimestamp(TimelinePosition, VideoMetadata?.Duration)
                : _resolvedVideoTimestamp
                    ?? ResolveTimelineTimestamp(_resolvedVideoPosition, VideoMetadata?.Duration);
            string currentText = current is TimeSpan currentTime ? FormatDuration(currentTime) : FormatNormalizedPosition(TimelinePosition);
            string totalText = (VideoMetadata?.Duration ?? _resolvedVideoDuration) is TimeSpan duration
                ? FormatDuration(duration)
                : "position";
            return $"{currentText} / {totalText}";
        }
    }

    public string VideoFramePositionText => _resolvedVideoFrameIndex is long frameIndex
        ? $"Frame {frameIndex + 1:N0}"
        : $"Position {_resolvedVideoPosition:P0}";


    public string ExportOperationTitle => _activeOperationTitle;

    public string ExportProgressText => _activeOperationText;

    public string ExportPercentageText => IsExportProgressIndeterminate && IsExportActive
        ? "..."
        : $"{ExportProgress:0}%";

    public string ExportFrameText => _exportFrameText;

    public string ElapsedText => _elapsedText;

    public string RemainingText => _remainingText;

    public string PreviewStaleMessage => IsSymbolizedPreviewStale
        ? IsVideoSelected
            ? HasVideoArtifact
                ? "Render settings or audio mode changed - the previous video remains playable."
                : "Frame position or settings changed - generate a new symbolized frame."
            : "Render settings changed - review the previous result or render again before export."
        : _lastGridSummary;

    public CharacterPresetOption SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (value is not null && SetProperty(ref _selectedPreset, value))
            {
                OnPropertyChanged(nameof(CharacterRamp));
                InvalidateSymbolizedPreview(ImageRenderSetting.CharacterPreset);
            }
        }
    }

    public string CharacterRamp => SelectedPreset.Characters;

    public string SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (SetProperty(ref _selectedFont, value))
            {
                InvalidateSymbolizedPreview(ImageRenderSetting.FontFamily);
            }
        }
    }

    public int SelectedFontSize
    {
        get => _selectedFontSize;
        set
        {
            if (SetProperty(ref _selectedFontSize, value))
            {
                InvalidateSymbolizedPreview(ImageRenderSetting.FontSize);
            }
        }
    }

    public string SelectedOutputResolution
    {
        get => _selectedOutputResolution;
        set
        {
            if (SetProperty(ref _selectedOutputResolution, value))
            {
                OnPropertyChanged(nameof(ShowResolutionWarning));
                InvalidateSymbolizedPreview(ImageRenderSetting.OutputResolution);
            }
        }
    }

    public SymbolPolarity SelectedSymbolPolarity
    {
        get => _selectedSymbolPolarity;
        set
        {
            if (SetProperty(ref _selectedSymbolPolarity, value))
            {
                InvalidateSymbolizedPreview(ImageRenderSetting.SymbolPolarity);
            }
        }
    }

    public string SelectedImageOutputFormat
    {
        get => _selectedImageOutputFormat;
        set
        {
            if (SetProperty(ref _selectedImageOutputFormat, value))
            {
                _renderWorkflowState = _renderWorkflowState.OutputFormatChanged();
                NotifyImageProperties();
            }
        }
    }

    public AudioExportMode SelectedAudioMode
    {
        get => _selectedAudioMode;
        set
        {
            if (SetProperty(ref _selectedAudioMode, value))
            {
                _renderWorkflowState = _renderWorkflowState.VideoAudioModeChanged(value);
                CancelInvalidatedPublication();
                OnPropertyChanged(nameof(IsKeepAudioSelected));
                OnPropertyChanged(nameof(IsRemoveAudioSelected));
                NotifyVideoArtifactProperties();
                NotifyCommandStates();
            }
        }
    }

    public bool IsKeepAudioSelected
    {
        get => SelectedAudioMode == AudioExportMode.KeepAudio;
        set
        {
            if (value)
            {
                SelectedAudioMode = AudioExportMode.KeepAudio;
            }
        }
    }

    public bool IsRemoveAudioSelected
    {
        get => SelectedAudioMode == AudioExportMode.RemoveAudio;
        set
        {
            if (value)
            {
                SelectedAudioMode = AudioExportMode.RemoveAudio;
            }
        }
    }

    public PreviewMode PreviewMode
    {
        get => _previewMode;
        private set
        {
            if (_previewMode != value)
            {
                // Every tab change is a paused comparison boundary. User-initiated video
                // switches resolve their timestamp through the playback coordinator first.
                _videoPlaybackCoordinator.PauseAll();
            }

            if (SetProperty(ref _previewMode, value))
            {
                OnPropertyChanged(nameof(IsOriginalPreviewSelected));
                OnPropertyChanged(nameof(IsSymbolizedPreviewSelected));
                OnPropertyChanged(nameof(CurrentPreviewImageSource));
                OnPropertyChanged(nameof(CurrentPreviewPixelWidth));
                OnPropertyChanged(nameof(CurrentPreviewPixelHeight));
                OnPropertyChanged(nameof(ShowImagePreview));
                OnPropertyChanged(nameof(ShowRenderedVideoPlayer));
                OnPropertyChanged(nameof(ShowRenderedVideoReviewError));
                OnPropertyChanged(nameof(ShowVideoFrameLoading));
                OnPropertyChanged(nameof(ShowVideoFrameError));
                OnPropertyChanged(nameof(ShowPreviewStatusBadge));
                OnPropertyChanged(nameof(ShowVideoFrameControls));
                OnPropertyChanged(nameof(ShowOriginalVideoPlayer));
                OnPropertyChanged(nameof(ShowOriginalPlaybackOpening));
                OnPropertyChanged(nameof(ShowOriginalPlaybackFallback));
                OnPropertyChanged(nameof(ShowMediaPlaceholder));
                NotifyCommandStates();
            }
        }
    }

    public bool IsOriginalPreviewSelected => IsMediaSelected && PreviewMode == PreviewMode.Original;

    public bool IsSymbolizedPreviewSelected => PreviewMode == PreviewMode.Symbolized;

    public string SelectedFitMode
    {
        get => _selectedFitMode;
        set
        {
            if (SetProperty(ref _selectedFitMode, value))
            {
                _renderedVideoPlayer.UpdateFitMode(value);
                _originalVideoPlayer.UpdateFitMode(value);
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public double TimelinePosition
    {
        get => _timelinePosition;
        set
        {
            double normalized = NormalizeVideoPosition(value);
            if (!SetProperty(ref _timelinePosition, normalized))
            {
                return;
            }

            OnPropertyChanged(nameof(TimelineText));
            if (!_suppressTimelineSeek
                && IsVideoSelected
                && VideoMetadata is not null
                && _renderWorkflowState.ActiveOperation is not RenderWorkflowOperation.RenderVideo
                    and not RenderWorkflowOperation.ExportVideo)
            {
                QueueVideoSeek(normalized);
            }
        }
    }

    public double ExportProgress
    {
        get => _exportProgress;
        private set
        {
            if (SetProperty(ref _exportProgress, Math.Clamp(value, 0, 100)))
            {
                OnPropertyChanged(nameof(ExportPercentageText));
            }
        }
    }

    public bool IsExportProgressIndeterminate
    {
        get => _isExportProgressIndeterminate;
        private set
        {
            if (SetProperty(ref _isExportProgressIndeterminate, value))
            {
                OnPropertyChanged(nameof(ExportPercentageText));
            }
        }
    }

    private async Task BrowseMediaAsync()
    {
        string? selectedPath = _mediaFileDialogService.SelectMediaFile();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            await LoadMediaAsync(selectedPath);
        }
    }

    private Task HandleDroppedPathsAsync(IReadOnlyList<string>? paths)
    {
        if (paths is not { Count: 1 })
        {
            ApplyImmediateFailure(MediaInputResult.Failure(
                MediaInputErrorCategory.MultipleFiles,
                "Drop one supported image or video at a time."));
            return Task.CompletedTask;
        }

        return LoadMediaAsync(paths[0]);
    }

    private async Task LoadMediaAsync(string path)
    {
        CancelVideoFrameRequestCore();
        CancelImageOperationCore();
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _mediaLoadCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();

        long requestId = Interlocked.Increment(ref _mediaRequestId);
        _pendingFilePath = path;
        _mediaInputErrorPresentation = null;
        ApplyMediaInputState(_mediaInputState.BeginLoading(requestId, GetSafeFileName(path)));
        StatusMessage = $"Validating {GetSafeFileName(path)} locally...";

        MediaInputResult result;
        try
        {
            result = await _mediaInputService.LoadAsync(path, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            result = MediaInputResult.Failure(
                MediaInputErrorCategory.Cancelled,
                "Media loading was cancelled.");
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.MediaInput,
            exception))
        {
            result = MediaInputResult.Failure(
                MediaInputErrorCategory.UnexpectedFailure,
                "GlyphFrame could not read the selected media.",
                exception.ToString());
        }
        DecodedImage? decodedImage = null;

        if (result.Success && result.ImageMetadata is not null)
        {
            try
            {
                StatusMessage = $"Decoding {result.ImageMetadata.FileName} locally...";
                decodedImage = await _imagePipelineService.DecodeAsync(
                    result.ImageMetadata.FilePath,
                    result.ImageMetadata.Orientation,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                result = MediaInputResult.Failure(
                    MediaInputErrorCategory.Cancelled,
                    "Image loading was cancelled.");
            }
            catch (ImageProcessingException exception)
            {
                result = MediaInputResult.Failure(
                    MapImageError(exception.Category),
                    exception.UserMessage,
                    exception.TechnicalDetail);
            }
            catch (Exception exception) when (_errorPresentationService.IsRecoverable(
                ErrorPresentationContext.MediaInput,
                exception))
            {
                result = MediaInputResult.Failure(
                    MediaInputErrorCategory.InvalidImage,
                    "GlyphFrame could not decode the selected image.",
                    exception.ToString());
            }
        }

        MediaInputState completed = _mediaInputState.Complete(requestId, result);
        if (_disposed || requestId != _mediaRequestId || ReferenceEquals(completed, _mediaInputState))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _mediaLoadCancellation, null, cancellation) == cancellation)
        {
            cancellation.Dispose();
        }

        _pendingFilePath = null;

        if (!result.Success)
        {
            _mediaInputErrorPresentation = _errorPresentationService.From(result);
            ApplyMediaInputState(completed);
            LogAcceptedFailure(_mediaInputErrorPresentation);
        }

        if (result.Success)
        {
            CancelComparisonSwitch();
            CancelReviewRecovery();
            _reviewRecoveryPending = false;
            CancelOriginalVideoOpen();
            _originalVideoPlayer.Close();
            _renderedVideoPlayer.Close();
            await _managedVideoArtifactSession.ClearAsync();
            if (_disposed || requestId != _mediaRequestId)
            {
                return;
            }

            ApplyMediaInputState(completed);
            ApplyPresentationState(UiPresentationState.ForMedia(result.MediaType));
            PreviewMode = PreviewMode.Original;
            _lastVideoExportPath = null;
            _lastVideoExportArtifactId = null;
            _mediaInputErrorPresentation = null;
            _imageRenderErrorPresentation = null;
            _imageExportErrorPresentation = null;
            _videoRenderErrorPresentation = null;
            _videoPublicationErrorPresentation = null;
            _videoReviewUnavailable = false;
            _videoReviewErrorPresentation = null;
            _originalPlaybackErrorPresentation = null;
            _renderWorkflowState = _renderWorkflowState.SelectMedia(
                result.MediaType,
                requestId,
                result.MediaType == MediaSelectionKind.Image && decodedImage is not null);
            if (result.MediaType == MediaSelectionKind.Video)
            {
                _renderWorkflowState = _renderWorkflowState.VideoAudioModeChanged(SelectedAudioMode);
            }

            if (result.MediaType == MediaSelectionKind.Image && decodedImage is not null)
            {
                ResetVideoFramePresentation();
                _decodedImage = decodedImage;
                _originalImageSource = _imageSourceFactory.Create(decodedImage);
                ClearSymbolizedPreview();
                StatusMessage = "Image decoded locally. Original preview is ready.";
            }
            else if (result.VideoMetadata is not null)
            {
                _decodedImage = null;
                _originalImageSource = null;
                _resolvedVideoPosition = VideoPreviewDefaults.MiddlePosition;
                _resolvedVideoTimestamp = ResolveTimelineTimestamp(
                    VideoPreviewDefaults.MiddlePosition,
                    result.VideoMetadata.Duration);
                _resolvedVideoDuration = result.VideoMetadata.Duration;
                _resolvedVideoFrameIndex = null;
                _videoFrameError = null;
                _videoFrameTechnicalDetail = null;
                SetTimelinePositionSilently(VideoPreviewDefaults.MiddlePosition);
                ClearSymbolizedPreview();
                StatusMessage = "Video metadata loaded locally. Loading a middle preview frame...";
                NotifyVideoFrameProperties();
                StartOriginalVideoOpen(result.VideoMetadata, requestId, TimeSpan.Zero);
                await LoadVideoFrameAsync(
                    result.VideoMetadata.FilePath,
                    VideoPreviewDefaults.MiddlePosition,
                    requestId,
                    debounce: false);
            }

            NotifyImageProperties();
        }
        else if (result.ErrorCategory != MediaInputErrorCategory.Cancelled)
        {
            StatusMessage = _mediaInputErrorPresentation?.UserMessage
                ?? "GlyphFrame could not read this media. Choose another file and try again.";
        }
    }

    private void StartOriginalVideoOpen(
        VideoMetadata metadata,
        long mediaVersion,
        TimeSpan resumePosition)
    {
        if (_disposed || _reviewViewUnloaded || !ReferenceEquals(metadata, VideoMetadata))
        {
            return;
        }

        CancelOriginalVideoOpen();
        CancellationTokenSource cancellation = new();
        _originalVideoOpenCancellation = cancellation;
        long generation = Interlocked.Increment(ref _originalVideoOpenGeneration);
        _ = OpenOriginalVideoAsync(metadata, mediaVersion, resumePosition, generation, cancellation);
    }

    private async Task OpenOriginalVideoAsync(
        VideoMetadata metadata,
        long mediaVersion,
        TimeSpan resumePosition,
        long generation,
        CancellationTokenSource cancellation)
    {
        try
        {
            OriginalVideoOpenResult result = await _originalVideoPlayer.OpenAsync(
                metadata.FilePath,
                cancellation.Token);
            if (!IsCurrentOriginalVideoOpen(metadata, mediaVersion, generation, cancellation))
            {
                return;
            }

            if (result.IsReady)
            {
                _originalPlaybackErrorPresentation = null;
                if (!TryApplyPendingOriginalComparison(metadata, mediaVersion, generation))
                {
                    _originalVideoPlayer.Seek(resumePosition);
                    StatusMessage = "Original video playback is ready.";
                }
            }
            else if (result.Outcome == OriginalVideoOpenOutcome.FallbackRequired)
            {
                _originalPlaybackErrorPresentation = _errorPresentationService.PlaybackFailure(
                    ErrorPresentationContext.OriginalPlayback,
                    result.UserMessage,
                    result.TechnicalDetail);
                LogAcceptedFailure(_originalPlaybackErrorPresentation);
                StatusMessage = _originalPlaybackErrorPresentation.UserMessage;
                await ApplyPendingOriginalFallbackAsync(metadata, mediaVersion, generation);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.OriginalPlayback,
            exception))
        {
            if (IsCurrentOriginalVideoOpen(metadata, mediaVersion, generation, cancellation))
            {
                _originalPlaybackErrorPresentation = _errorPresentationService.PlaybackFailure(
                    ErrorPresentationContext.OriginalPlayback,
                    OriginalVideoPlayer.PlaybackUnavailableMessage,
                    exception.ToString());
                LogAcceptedFailure(_originalPlaybackErrorPresentation);
                StatusMessage = _originalPlaybackErrorPresentation.UserMessage;
            }
        }
        finally
        {
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _originalVideoOpenCancellation, null, cancellation),
                    cancellation))
            {
                cancellation.Dispose();
            }

            if (!_disposed && generation == _originalVideoOpenGeneration)
            {
                NotifyOriginalVideoPlayerProperties();
            }
        }
    }

    private bool IsCurrentOriginalVideoOpen(
        VideoMetadata metadata,
        long mediaVersion,
        long generation,
        CancellationTokenSource cancellation) =>
        !_disposed
        && !_reviewViewUnloaded
        && !cancellation.IsCancellationRequested
        && generation == _originalVideoOpenGeneration
        && mediaVersion == _renderWorkflowState.MediaVersion
        && ReferenceEquals(metadata, VideoMetadata)
        && metadata.FilePath.Equals(_originalVideoPlayer.SourcePath, StringComparison.OrdinalIgnoreCase);

    private void CancelOriginalVideoOpen()
    {
        Interlocked.Increment(ref _originalVideoOpenGeneration);
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _originalVideoOpenCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private void QueueVideoSeek(double normalizedPosition)
    {
        if (_disposed || !IsVideoSelected || VideoMetadata is null)
        {
            return;
        }

        _ = LoadVideoFrameAsync(
            VideoMetadata.FilePath,
            normalizedPosition,
            _renderWorkflowState.MediaVersion,
            debounce: true);
    }

    private async Task<VideoFrameLoadResult> LoadVideoFrameAsync(
        string inputPath,
        double normalizedPosition,
        long mediaVersion,
        bool debounce)
    {
        CancelImageOperationCore();
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _videoFrameCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();

        long requestId = Interlocked.Increment(ref _videoFrameRequestId);
        _renderWorkflowState = _renderWorkflowState.SourceChanged();
        _videoFrameError = null;
        _videoFrameTechnicalDetail = null;
        _videoFrameErrorPresentation = null;
        IsVideoFrameLoading = true;
        PreviewMode = PreviewMode.Original;
        StatusMessage = $"Loading video frame at {normalizedPosition:P0}...";
        NotifyVideoFrameProperties();
        NotifyImageProperties();

        bool gateEntered = false;
        VideoFrameLoadResult loadResult = VideoFrameLoadResult.NotAccepted(
            requestId,
            mediaVersion,
            inputPath,
            normalizedPosition);
        try
        {
            if (debounce)
            {
                await Task.Delay(VideoPreviewDefaults.SeekDebounceMilliseconds, cancellation.Token);
            }

            await _videoReadGate.WaitAsync(cancellation.Token);
            gateEntered = true;

            if (!IsCurrentVideoFrameRequest(requestId, mediaVersion, inputPath))
            {
                return loadResult;
            }

            VideoFrameReadResult result = await _videoFrameReader.ReadAsync(
                inputPath,
                normalizedPosition,
                VideoMetadata,
                cancellation.Token);

            if (!IsCurrentVideoFrameRequest(requestId, mediaVersion, inputPath))
            {
                return loadResult;
            }

            if (result.Success && result.Frame is not null)
            {
                _decodedImage = result.Frame;
                _originalImageSource = _imageSourceFactory.Create(result.Frame);
                _renderWorkflowState = _renderWorkflowState.OriginalAvailable(mediaVersion);
                _resolvedVideoFrameIdentity = _renderWorkflowState.CurrentIdentity;
                _resolvedVideoPosition = result.ActualNormalizedPosition ?? normalizedPosition;
                _resolvedVideoTimestamp = result.ActualTimestamp
                    ?? ResolveTimelineTimestamp(_resolvedVideoPosition, result.Duration ?? VideoMetadata?.Duration);
                _resolvedVideoDuration = result.Duration ?? VideoMetadata?.Duration;
                _resolvedVideoFrameIndex = result.ActualFrameIndex;
                _videoFrameError = null;
                _videoFrameTechnicalDetail = null;
                _videoFrameErrorPresentation = null;
                SetTimelinePositionSilently(_resolvedVideoPosition);
                if (!IsExportActive)
                {
                    StatusMessage = $"Video frame ready at {FormatVideoPosition(_resolvedVideoTimestamp, _resolvedVideoPosition)}.";
                }
                NotifyImageProperties();
                loadResult = loadResult.Accept(result.Frame, _renderWorkflowState.CurrentIdentity);
            }
            else if (result.Status == VideoFrameReadStatus.Cancelled)
            {
                if (_resolvedVideoFrameIdentity is not null)
                {
                    _renderWorkflowState = _renderWorkflowState.RestoreVideoFrameSource(
                        _resolvedVideoFrameIdentity);
                }

                SetTimelinePositionSilently(_resolvedVideoPosition);
                if (!IsExportActive)
                {
                    StatusMessage = "Frame preview was cancelled. The previous original frame remains available.";
                }

                NotifyImageProperties();
            }
            else
            {
                ApplyVideoFrameFailure(_errorPresentationService.From(result));
                SetTimelinePositionSilently(_resolvedVideoPosition);
                if (!IsExportActive)
                {
                    StatusMessage = _videoFrameError
                        ?? "GlyphFrame could not load this video frame. Try another position.";
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A superseded seek is an expected state transition and is not presented as an error.
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.VideoFramePreview,
            exception))
        {
            if (IsCurrentVideoFrameRequest(requestId, mediaVersion, inputPath))
            {
                ApplyVideoFrameFailure(_errorPresentationService.FromException(
                    ErrorPresentationContext.VideoFramePreview,
                    exception));
                SetTimelinePositionSilently(_resolvedVideoPosition);
                if (!IsExportActive)
                {
                    StatusMessage = _videoFrameError!;
                }
            }
        }
        finally
        {
            if (gateEntered)
            {
                _videoReadGate.Release();
            }

            if (IsCurrentVideoFrameRequest(requestId, mediaVersion, inputPath))
            {
                if (Interlocked.CompareExchange(ref _videoFrameCancellation, null, cancellation) == cancellation)
                {
                    cancellation.Dispose();
                }

                IsVideoFrameLoading = false;
                NotifyVideoFrameProperties();
                NotifyCommandStates();
            }
        }

        return loadResult;
    }

    private bool IsCurrentVideoFrameRequest(long requestId, long mediaVersion, string inputPath) =>
        !_disposed
        && requestId == _videoFrameRequestId
        && mediaVersion == _renderWorkflowState.MediaVersion
        && IsVideoSelected
        && VideoMetadata?.FilePath.Equals(inputPath, StringComparison.OrdinalIgnoreCase) == true;

    private bool IsCurrentVideoFrameLoad(VideoFrameLoadResult result, double requestedNormalizedPosition) =>
        result.Success
        && result.Frame is not null
        && result.Identity is not null
        && result.RequestedNormalizedPosition == requestedNormalizedPosition
        && IsCurrentVideoFrameRequest(result.RequestId, result.MediaVersion, result.InputPath)
        && ReferenceEquals(result.Frame, _decodedImage)
        && result.Identity == _renderWorkflowState.CurrentIdentity;

    private void ApplyVideoFrameFailure(ErrorPresentation? presentation)
    {
        _videoFrameErrorPresentation = presentation ?? ErrorPresentation.Create(
            ApplicationErrorCode.FrameDecodeFailed,
            ErrorPresentationContext.VideoFramePreview);
        _videoFrameError = _videoFrameErrorPresentation.UserMessage;
        _videoFrameTechnicalDetail = _videoFrameErrorPresentation.TechnicalDetail;
        LogAcceptedFailure(_videoFrameErrorPresentation);
    }

    public void UnloadReviewView()
    {
        if (_disposed || _reviewViewUnloaded)
        {
            return;
        }

        _reviewViewUnloaded = true;
        _reviewViewGeneration++;
        CancelComparisonSwitch();
        _reviewRecoveryPending = true;
        CancelReviewRecovery();
        _suspendedReviewPosition = _renderedVideoPlayer.Position;
        _suspendedOriginalPosition = _originalVideoPlayer.Position;
        CancelOriginalVideoOpen();
        CancelActiveImageOperation();
        _originalVideoPlayer.Close();
        _originalVideoPlayer.SetOwner(null);
        _renderedVideoPlayer.Close();
        _renderedVideoPlayer.SetOwner(null);
    }

    public async Task LoadReviewViewAsync(System.Windows.Window? owner)
    {
        if (_disposed)
        {
            return;
        }

        _renderedVideoPlayer.SetOwner(owner);
        _originalVideoPlayer.SetOwner(owner);
        if (_reviewViewUnloaded)
        {
            _reviewViewGeneration++;
        }
        _reviewViewUnloaded = false;
        NotifyCommandStates();
        if (VideoMetadata is VideoMetadata metadata)
        {
            StartOriginalVideoOpen(metadata, _renderWorkflowState.MediaVersion, _suspendedOriginalPosition);
        }

        if (_reviewRecoveryPending && !IsExportActive)
        {
            await EnsureAcceptedReviewAsync();
        }
    }

    // All deferred reopening (load, post-commit, and restoration) shares this gate.
    // A superseded continuation may observe completion but never closes a newer transport.
    private async Task<bool> EnsureAcceptedReviewAsync(bool allowActiveOperation = false)
    {
        long generation = _reviewViewGeneration;
        long operationId = _imageOperationId;
        long mediaVersion = _renderWorkflowState.MediaVersion;
        ManagedVideoArtifact? artifact = _managedVideoArtifactSession.Current;
        if (_disposed || _reviewViewUnloaded || artifact is null || (IsExportActive && !allowActiveOperation))
        {
            return false;
        }
        CancellationToken token = _reviewRecoveryCancellation.Token;
        bool entered = false;
        bool Current() => !_disposed && !_reviewViewUnloaded && !token.IsCancellationRequested
            && generation == _reviewViewGeneration && operationId == _imageOperationId
            && mediaVersion == _renderWorkflowState.MediaVersion
            && _managedVideoArtifactSession.Current?.ArtifactId == artifact.ArtifactId;
        try
        {
            await _reviewRecoveryGate.WaitAsync(token);
            entered = true;
            if (!Current())
            {
                return false;
            }
            if (IsAcceptedReviewOpen)
            {
                _reviewRecoveryPending = false;
                return true;
            }
            if (!_reviewRecoveryPending)
            {
                return false; // Another queued request already attempted this recovery.
            }
            _reviewRecoveryActive = true;
            _reviewRecoveryPending = false;
            RenderedVideoOpenResult opened = await _renderedVideoPlayer.OpenAsync(artifact.FilePath, token);
            if (!Current())
            {
                return false;
            }
            if (!opened.Success || !_renderedVideoPlayer.IsOpen || _renderedVideoPlayer.SourcePath != artifact.FilePath)
            {
                SetReviewUnavailable(opened.TechnicalDetail ?? _renderedVideoPlayer.TechnicalDetail);
                LogAcceptedFailure(_videoReviewErrorPresentation);
                return false;
            }
            ClearReviewFailure();
            _renderedVideoPlayer.Seek(_suspendedReviewPosition);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.SymbolizedPlayback,
            exception))
        {
            if (Current())
            {
                _renderedVideoPlayer.Close();
                SetReviewUnavailable(exception.ToString());
                LogAcceptedFailure(_videoReviewErrorPresentation);
            }
            return false;
        }
        finally
        {
            if (entered)
            {
                _reviewRecoveryActive = false;
                _reviewRecoveryGate.Release();
            }
            if (Current())
            {
                NotifyVideoArtifactProperties();
            }
        }
    }

    private void CancelReviewRecovery()
    {
        CancellationTokenSource previous = _reviewRecoveryCancellation;
        _reviewRecoveryCancellation = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
    }

    private void SetReviewUnavailable(string? detail)
    {
        _videoReviewUnavailable = true;
        _videoReviewErrorPresentation = _errorPresentationService.PlaybackFailure(
            ErrorPresentationContext.SymbolizedPlayback,
            _renderedVideoPlayer.FailureMessage,
            detail);
    }

    private void ClearReviewFailure()
    {
        _videoReviewUnavailable = false;
        _videoReviewErrorPresentation = null;
    }

    private Task RenderImageAsync() => RenderCurrentSourceAsync(RenderWorkflowOperation.RenderImage);

    private async Task PreviewFrameAsync()
    {
        if (IsOriginalVideoPlaybackReady && VideoMetadata is VideoMetadata metadata)
        {
            _originalVideoPlayer.Pause();
            TimeSpan authoritativeDuration = metadata.Duration is TimeSpan duration && duration > TimeSpan.Zero
                ? duration
                : _originalVideoPlayer.Duration;
            double normalized = authoritativeDuration > TimeSpan.Zero
                ? Math.Clamp(_originalVideoPlayer.Position.TotalSeconds / authoritativeDuration.TotalSeconds, 0d, 1d)
                : 0d;
            SetTimelinePositionSilently(normalized);
            VideoFrameLoadResult loadResult = await LoadVideoFrameAsync(
                metadata.FilePath,
                normalized,
                _renderWorkflowState.MediaVersion,
                debounce: false);

            if (!IsCurrentVideoFrameLoad(loadResult, normalized))
            {
                return;
            }

            await RenderCurrentSourceAsync(
                RenderWorkflowOperation.PreviewVideoFrame,
                loadResult.Frame,
                loadResult.Identity);
            return;
        }

        await RenderCurrentSourceAsync(RenderWorkflowOperation.PreviewVideoFrame);
    }

    private async Task RenderVideoAsync()
    {
        if (VideoMetadata is not VideoMetadata metadata || !CanRenderVideo())
        {
            return;
        }

        if (!File.Exists(metadata.FilePath))
        {
            _videoRenderErrorPresentation = ErrorPresentation.Create(
                ApplicationErrorCode.InputNotFound,
                ErrorPresentationContext.VideoRendering);
            LogAcceptedFailure(_videoRenderErrorPresentation);
            StatusMessage = _videoRenderErrorPresentation.UserMessage;
            return;
        }

        ImageRenderSettings renderSettings = CaptureRenderSettings();
        CharacterPresetOption preset = SelectedPreset;
        AudioExportMode audioMode = SelectedAudioMode;
        FullVideoRenderIdentity identity = _renderWorkflowState.CreateFullVideoRenderIdentity(
            renderSettings,
            preset);
        ManagedVideoRenderJob job = new(
            metadata.FilePath,
            metadata,
            renderSettings,
            preset,
            audioMode,
            identity);
        (long operationId, CancellationToken token) = BeginManagedVideoRenderOperation(identity);
        ManagedVideoArtifact? candidate = null;
        bool showCompletionPresentation = false;
        try
        {
            CallbackProgress<VideoExportProgress> progress = new(update =>
                DispatchVideoExportProgress(operationId, metadata, update));
            ManagedVideoRenderResult result = await _managedVideoRenderService.RenderAsync(job, token, progress);
            candidate = result.Artifact;

            if (result.Success && candidate is not null)
            {
                if (!IsCurrentManagedVideoRender(operationId, metadata, identity))
                {
                    await RejectManagedVideoCandidateAsync(candidate, operationId, reportFailure: false);
                    candidate = null;
                    return;
                }

                ManagedVideoArtifact intended = candidate;
                candidate = null; // Finalization owns rejection/restoration until session acceptance.
                showCompletionPresentation = await FinalizeManagedVideoAsync(
                    operationId, metadata, identity, intended, token);
            }
            else if (result.Status == ManagedVideoArtifactStatus.Cancelled)
            {
                if (operationId == _imageOperationId)
                {
                    _renderWorkflowState = _renderWorkflowState.CancelActiveOperation();
                    StatusMessage = "Video rendering was cancelled. Any previous artifact file is retained.";
                }
            }
            else if (IsCurrentManagedVideoRender(operationId, metadata, identity))
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                _videoRenderErrorPresentation = _errorPresentationService.From(result)
                    ?? ErrorPresentation.Create(
                        ApplicationErrorCode.VideoRenderFailed,
                        ErrorPresentationContext.VideoRendering);
                LogAcceptedFailure(_videoRenderErrorPresentation);
                StatusMessage = _videoRenderErrorPresentation.UserMessage;
            }
        }
        catch (OperationCanceledException)
        {
            if (candidate is not null)
            {
                await RejectManagedVideoCandidateAsync(candidate, operationId);
            }

            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.CancelActiveOperation();
                StatusMessage = "Video rendering was cancelled. Any previous artifact file is retained.";
            }
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.ManagedArtifact,
            exception))
        {
            if (candidate is not null)
            {
                await RejectManagedVideoCandidateAsync(candidate, operationId);
            }

            if (IsCurrentManagedVideoRender(operationId, metadata, identity))
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                _videoRenderErrorPresentation = _errorPresentationService.FromException(
                    ErrorPresentationContext.ManagedArtifact,
                    exception) ?? ErrorPresentation.Create(
                        ApplicationErrorCode.VideoRenderFailed,
                        ErrorPresentationContext.VideoRendering);
                LogAcceptedFailure(_videoRenderErrorPresentation);
                StatusMessage = _videoRenderErrorPresentation.UserMessage;
            }
        }
        finally
        {
            await EndImageOperationAsync(operationId);
        }

        if (showCompletionPresentation)
        {
            StartExportCompletionPresentation(operationId);
        }
    }

    private async Task<bool> FinalizeManagedVideoAsync(
        long operationId,
        VideoMetadata metadata,
        FullVideoRenderIdentity identity,
        ManagedVideoArtifact intended,
        CancellationToken token)
    {
        ManagedVideoArtifact? previous = _managedVideoArtifactSession.Current;
        TimeSpan previousPosition = _renderedVideoPlayer.Position;
        bool previousWasPlaying = _renderedVideoPlayer.IsPlaying;
        long viewGeneration = _reviewViewGeneration;
        bool accepted = false;
        bool Current() => IsCurrentManagedVideoRender(operationId, metadata, identity)
            && !_reviewViewUnloaded && viewGeneration == _reviewViewGeneration;
        _reviewTransitionActive = true;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!Current() || intended.Identity != identity)
            {
                return false;
            }

            // Keep the old file owned until the candidate has opened successfully.
            // Playback itself borrows this completed candidate during preflight.
            _renderedVideoPlayer.Close();
            _activeOperationText = "Opening the rendered video for review (cancellable)...";
            ExportStage = VideoExportStage.Finalizing;
            IsExportProgressIndeterminate = true;
            _remainingText = string.Empty;
            NotifyExportProgressProperties();
            RenderedVideoOpenResult opened = await _renderedVideoPlayer.OpenAsync(intended.FilePath, token);
            token.ThrowIfCancellationRequested();
            if (!Current())
            {
                return false;
            }

            if (!opened.Success && previous is not null)
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                _videoRenderErrorPresentation = _errorPresentationService.PlaybackFailure(
                    ErrorPresentationContext.SymbolizedPlayback,
                    opened.UserMessage,
                    opened.TechnicalDetail);
                LogAcceptedFailure(_videoRenderErrorPresentation);
                StatusMessage = _videoRenderErrorPresentation.UserMessage;
                return false;
            }

            // Commit boundary: Cancel is disabled before the non-cancellable session
            // transaction. A view interruption may require deferred reopening afterward.
            _managedVideoCommitStarted = true;
            _activeOperationText = "Committing the managed review result (cannot cancel)...";
            OnPropertyChanged(nameof(CanCancelExport));
            OnPropertyChanged(nameof(ExportProgressText));
            NotifyCommandStates();
            await _managedVideoArtifactSession.AcceptAsync(intended, CancellationToken.None);
            accepted = _managedVideoArtifactSession.Current?.ArtifactId == intended.ArtifactId;
            if (!accepted)
            {
                throw new InvalidOperationException("The managed session did not retain the intended candidate.");
            }

            if (_disposed || !ReferenceEquals(metadata, VideoMetadata)
                || operationId != _imageOperationId)
            {
                // The newer media/view lifetime owns its transport; never close it here.
                return false; // Session/shutdown remains the physical owner.
            }

            bool valid = IsCurrentManagedVideoRender(operationId, metadata, identity);
            _renderWorkflowState = _renderWorkflowState.RecordAcceptedVideoArtifact(identity);
            if (_reviewViewUnloaded || viewGeneration != _reviewViewGeneration)
            {
                _reviewRecoveryPending = true;
                _suspendedReviewPosition = TimeSpan.Zero;
                if (_reviewViewUnloaded)
                {
                    return false;
                }
                if (!await EnsureAcceptedReviewAsync(allowActiveOperation: true))
                {
                    return false;
                }
            }

            if (_disposed || _reviewViewUnloaded || !ReferenceEquals(metadata, VideoMetadata)
                || operationId != _imageOperationId
                || _managedVideoArtifactSession.Current?.ArtifactId != intended.ArtifactId)
            {
                return false;
            }

            if (!_renderedVideoPlayer.IsOpen || _renderedVideoPlayer.SourcePath != intended.FilePath)
            {
                SetReviewUnavailable(_videoReviewErrorPresentation?.TechnicalDetail
                    ?? opened.TechnicalDetail
                    ?? _renderedVideoPlayer.TechnicalDetail);
                LogAcceptedFailure(_videoReviewErrorPresentation);
                StatusMessage = "GlyphFrame could not open the managed video for review.";
                return false;
            }

            ClearReviewFailure();
            _videoRenderErrorPresentation = null;
            if (!valid || _renderWorkflowState.IsVideoArtifactStale)
            {
                // Settings changed during an asynchronous commit/reopen, not the view lifetime.
                _renderWorkflowState = _renderWorkflowState.RecordAcceptedVideoArtifact(identity);
                StatusMessage = "The accepted review is stale. Render again before continuing.";
                return false;
            }
            PreviewMode = PreviewMode.Symbolized;
            ExportStage = VideoExportStage.Completed;
            ExportProgress = 100;
            IsExportProgressIndeterminate = false;
            _activeOperationTitle = "Video render complete";
            _activeOperationText = "Managed review video completed.";
            _exportFrameText = $"Frame {intended.RenderedFrameCount:N0} / {intended.RenderedFrameCount:N0}";
            _remainingText = string.Empty;
            StatusMessage = "The complete symbolized video is ready for in-app review.";
            return true;
        }
        finally
        {
            if (!accepted && _managedVideoArtifactSession.Current?.ArtifactId != intended.ArtifactId)
            {
                if (_renderedVideoPlayer.SourcePath == intended.FilePath)
                {
                    _renderedVideoPlayer.Close();
                }

                await RejectManagedVideoCandidateAsync(intended, operationId);
                if (!_disposed && !_reviewViewUnloaded && ReferenceEquals(metadata, VideoMetadata)
                    && operationId == _imageOperationId)
                {
                    await RestorePreviousVideoReviewAsync(previous, previousPosition, previousWasPlaying);
                }
            }

            _reviewTransitionActive = false;
        }
    }

    private async Task RenderCurrentSourceAsync(
        RenderWorkflowOperation operation,
        DecodedImage? expectedSource = null,
        PreviewIdentity? expectedIdentity = null)
    {
        bool imageRender = operation == RenderWorkflowOperation.RenderImage;
        if (_decodedImage is null
            || _renderWorkflowState.CurrentIdentity is not PreviewIdentity identity
            || imageRender != IsImageSelected)
        {
            return;
        }

        string? inputPath = imageRender ? ImageMetadata?.FilePath : VideoMetadata?.FilePath;
        ErrorPresentationContext errorContext = imageRender
            ? ErrorPresentationContext.ImageRendering
            : ErrorPresentationContext.FrameSymbolization;
        DecodedImage source = _decodedImage;
        if ((expectedSource is not null && !ReferenceEquals(source, expectedSource))
            || (expectedIdentity is not null && identity != expectedIdentity))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            ErrorPresentation missing = ErrorPresentation.Create(
                ApplicationErrorCode.InputNotFound,
                errorContext);
            if (imageRender)
            {
                _imageRenderErrorPresentation = missing;
                LogAcceptedFailure(missing);
            }
            else
            {
                ApplyVideoFrameFailure(missing);
            }
            StatusMessage = missing.UserMessage;
            return;
        }

        (long operationId, CancellationToken token) = BeginImageOperation(
            operation,
            imageRender ? "Rendering image..." : "Previewing frame...",
            "Measuring glyphs and rendering symbol art");
        if (imageRender)
        {
            _imageRenderErrorPresentation = null;
        }
        else
        {
            _videoFrameErrorPresentation = null;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        ImagePipelineResult? result = null;
        try
        {
            result = await _imagePipelineService.RenderAsync(
                source,
                CaptureRenderSettings(),
                SelectedPreset,
                token);

            if (!IsCurrentImageOperation(operationId, identity, source, operation))
            {
                result.RenderedImage.Dispose();
                return;
            }

            ApplyRenderedResult(result, identity, operation);
            if (imageRender)
            {
                _imageRenderErrorPresentation = null;
            }
            else
            {
                _videoFrameErrorPresentation = null;
            }
            result = null;
            PreviewMode = PreviewMode.Symbolized;
            ExportProgress = 100;
            StatusMessage = imageRender
                ? "Image ready for review and export."
                : $"Symbolized frame generated in {stopwatch.Elapsed.TotalSeconds:0.##} seconds.";
        }
        catch (OperationCanceledException)
        {
            result?.RenderedImage.Dispose();
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.CancelActiveOperation();
                StatusMessage = imageRender
                    ? "Image rendering was cancelled. Any previous rendered result remains available."
                    : "Frame preview was cancelled. The original frame remains available.";
            }
        }
        catch (ImageProcessingException exception)
            when (exception.Category == ImageProcessingErrorCategory.Cancelled)
        {
            result?.RenderedImage.Dispose();
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.CancelActiveOperation();
                StatusMessage = imageRender
                    ? "Image rendering was cancelled. Any previous rendered result remains available."
                    : "Frame preview was cancelled. The original frame remains available.";
            }
        }
        catch (ImageProcessingException exception)
        {
            result?.RenderedImage.Dispose();
            if (IsCurrentImageOperation(operationId, identity, source, operation))
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                ErrorPresentation presentation = _errorPresentationService.From(
                    exception,
                    errorContext)
                    ?? ErrorPresentation.Create(
                        imageRender ? ApplicationErrorCode.ImageRenderFailed : ApplicationErrorCode.FrameRenderFailed,
                        errorContext);
                if (imageRender)
                {
                    _imageRenderErrorPresentation = presentation;
                    LogAcceptedFailure(presentation);
                }
                else
                {
                    ApplyVideoFrameFailure(presentation);
                }
                StatusMessage = presentation.UserMessage;
                if (!HasSymbolizedPreview)
                {
                    PreviewMode = PreviewMode.Original;
                }
            }
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(errorContext, exception))
        {
            result?.RenderedImage.Dispose();
            if (IsCurrentImageOperation(operationId, identity, source, operation))
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                ErrorPresentation presentation = _errorPresentationService.FromException(
                    errorContext,
                    exception) ?? ErrorPresentation.Create(
                        imageRender ? ApplicationErrorCode.ImageRenderFailed : ApplicationErrorCode.FrameRenderFailed,
                        errorContext);
                if (imageRender)
                {
                    _imageRenderErrorPresentation = presentation;
                    LogAcceptedFailure(presentation);
                }
                else
                {
                    ApplyVideoFrameFailure(presentation);
                }
                StatusMessage = presentation.UserMessage;
                if (!HasSymbolizedPreview)
                {
                    PreviewMode = PreviewMode.Original;
                }
            }
        }
        finally
        {
            await EndImageOperationAsync(operationId);
        }
    }

    private async Task ExportImageAsync()
    {
        if (_renderedImage is not RenderedImage rendered
            || ImageMetadata is null
            || !_renderWorkflowState.CanExportImage)
        {
            return;
        }

        if (!File.Exists(ImageMetadata.FilePath))
        {
            _imageExportErrorPresentation = ErrorPresentation.Create(
                ApplicationErrorCode.InputNotFound,
                ErrorPresentationContext.ImageExport);
            LogAcceptedFailure(_imageExportErrorPresentation);
            StatusMessage = _imageExportErrorPresentation.UserMessage;
            return;
        }

        ImageExportFormat format = ResolveImageExportFormat();
        string? outputPath = SelectOutputPath(format);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        outputPath = NormalizeOutputExtension(outputPath, format);
        ImageExportDestinationRequirement destinationRequirement = ImageExportPolicy.Evaluate(
            ImageMetadata.FilePath,
            outputPath,
            File.Exists(outputPath));
        if (destinationRequirement == ImageExportDestinationRequirement.RejectSourcePath)
        {
            _imageExportErrorPresentation = ErrorPresentation.Create(
                ApplicationErrorCode.DestinationConflict,
                ErrorPresentationContext.ImageExport);
            LogAcceptedFailure(_imageExportErrorPresentation);
            StatusMessage = _imageExportErrorPresentation.UserMessage;
            return;
        }

        if (destinationRequirement == ImageExportDestinationRequirement.InvalidPath)
        {
            _imageExportErrorPresentation = ErrorPresentation.Create(
                ApplicationErrorCode.DestinationUnavailable,
                ErrorPresentationContext.ImageExport);
            LogAcceptedFailure(_imageExportErrorPresentation);
            StatusMessage = _imageExportErrorPresentation.UserMessage;
            return;
        }

        bool overwrite = destinationRequirement == ImageExportDestinationRequirement.ConfirmOverwrite;
        if (overwrite && !_userDialogService.Confirm(
                "Replace existing image?",
                $"A file named '{Path.GetFileName(outputPath)}' already exists. Replace it with the new symbolized image?",
                "Replace Image",
                "Cancel"))
        {
            StatusMessage = "Export cancelled. The existing file was not changed.";
            return;
        }

        (long operationId, CancellationToken token) = BeginImageOperation(
            RenderWorkflowOperation.ExportImage,
            "Exporting image...",
            $"Encoding {AppSettingsCatalog.GetDisplayName(format)} from the current rendered image");
        _imageExportErrorPresentation = null;

        try
        {
            await _imageExporter.ExportAsync(rendered, outputPath, format, overwrite, token);

            if (operationId != _imageOperationId
                || !ReferenceEquals(rendered, _renderedImage)
                || !_renderWorkflowState.CanAcceptResult(
                    operationId,
                    _renderWorkflowState.ImageRenderIdentity!,
                    RenderWorkflowOperation.ExportImage))
            {
                return;
            }

            _lastImageExportPath = outputPath;
            _imageExportErrorPresentation = null;
            _renderWorkflowState = _renderWorkflowState.CompleteExport(operationId);
            ExportProgress = 100;
            StatusMessage = $"Symbolized image exported to {Path.GetFileName(outputPath)}.";
        }
        catch (OperationCanceledException)
        {
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.CancelActiveOperation();
                StatusMessage = "Image export was cancelled. No partial output was kept.";
            }
        }
        catch (ImageProcessingException exception)
            when (exception.Category == ImageProcessingErrorCategory.Cancelled)
        {
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.CancelActiveOperation();
                StatusMessage = "Image export was cancelled. No partial output was kept.";
            }
        }
        catch (ImageProcessingException exception)
        {
            if (operationId == _imageOperationId)
            {
                bool currentExport = ReferenceEquals(rendered, _renderedImage)
                    && _renderWorkflowState.ImageRenderIdentity is PreviewIdentity renderIdentity
                    && _renderWorkflowState.CanAcceptResult(
                        operationId, renderIdentity, RenderWorkflowOperation.ExportImage);
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                _imageExportErrorPresentation = _errorPresentationService.From(
                    exception,
                    ErrorPresentationContext.ImageExport) ?? ErrorPresentation.Create(
                        ApplicationErrorCode.ImageExportFailed,
                        ErrorPresentationContext.ImageExport);
                if (currentExport)
                {
                    LogAcceptedFailure(_imageExportErrorPresentation);
                }
                StatusMessage = _imageExportErrorPresentation.UserMessage;
            }
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.ImageExport,
            exception))
        {
            if (operationId == _imageOperationId)
            {
                bool currentExport = ReferenceEquals(rendered, _renderedImage)
                    && _renderWorkflowState.ImageRenderIdentity is PreviewIdentity renderIdentity
                    && _renderWorkflowState.CanAcceptResult(
                        operationId, renderIdentity, RenderWorkflowOperation.ExportImage);
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                _imageExportErrorPresentation = _errorPresentationService.FromException(
                    ErrorPresentationContext.ImageExport,
                    exception) ?? ErrorPresentation.Create(
                        ApplicationErrorCode.ImageExportFailed,
                        ErrorPresentationContext.ImageExport);
                if (currentExport)
                {
                    LogAcceptedFailure(_imageExportErrorPresentation);
                }
                StatusMessage = _imageExportErrorPresentation.UserMessage;
            }
        }
        finally
        {
            await EndImageOperationAsync(operationId);
        }
    }

    private async Task ExportVideoAsync()
    {
        if (!CanExportVideo() || VideoMetadata is not VideoMetadata metadata
            || _managedVideoArtifactSession.Current is not ManagedVideoArtifact artifact)
        {
            return;
        }

        long mediaRequest = _mediaRequestId;
        string? selected = _imageSaveDialogService.SelectVideoOutputFile(metadata.FilePath);
        if (string.IsNullOrWhiteSpace(selected) || !IsCurrentPublication())
        {
            return;
        }

        RenderWorkflowState beforeExportState = _renderWorkflowState;
        // Destination selection is modal; capture and revalidate before beginning async work.
        (long operationId, CancellationToken token) = BeginImageOperation(
            RenderWorkflowOperation.ExportVideo, "Exporting reviewed video...", "Preparing a byte-identical MP4 copy");
        _videoPublicationErrorPresentation = null;
        bool completed = false;
        try
        {
            VideoPublicationDestination destination = await _videoArtifactPublisher.PrepareAsync(
                NormalizeVideoOutputExtension(selected), metadata.FilePath, token);
            if (!IsCurrentPublication() || token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
                return;
            }

            bool overwrite = destination.ExistingVersion is not null;
            if (overwrite && !_userDialogService.Confirm("Replace existing video?",
                    $"Replace '{Path.GetFileName(destination.FilePath)}' with the reviewed video?", "Replace Video", "Cancel"))
            {
                // Declining consent is not a failed export; restore the pre-dialog outcome.
                _renderWorkflowState = beforeExportState;
                return;
            }

            if (!IsCurrentPublication() || token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
                return;
            }

            VideoArtifactPublicationJob job = new(artifact, metadata.FilePath, destination, overwrite);
            CallbackProgress<VideoExportProgress> progress = new(update =>
                DispatchVideoExportProgress(operationId, metadata, update));
            VideoExportResult result = await _managedVideoArtifactSession.UseCurrentAsync(artifact,
                cancellation => _videoArtifactPublisher.PublishAsync(job, cancellation, progress), token);
            if (_disposed || operationId != _imageOperationId || mediaRequest != _mediaRequestId
                || !ReferenceEquals(artifact, _managedVideoArtifactSession.Current))
            {
                return;
            }

            if (result.Success)
            {
                // Atomic publication is the commit boundary. A later cancellation cannot undo success.
                _renderWorkflowState = _renderWorkflowState.CompleteExport(operationId);
                _lastVideoExportPath = destination.FilePath;
                _lastVideoExportArtifactId = artifact.ArtifactId;
                _videoPublicationErrorPresentation = null;
                ExportStage = VideoExportStage.Completed;
                ExportProgress = 100;
                IsExportProgressIndeterminate = false;
                _activeOperationTitle = "Video export complete";
                OnPropertyChanged(nameof(ExportOperationTitle));
                _activeOperationText = "Reviewed video exported.";
                _exportFrameText = string.Empty;
                _remainingText = string.Empty;
                NotifyExportProgressProperties();
                StatusMessage = $"Reviewed video saved to {destination.FilePath}.";
                completed = true;
            }
            else
            {
                bool currentPublication = IsCurrentPublication();
                _renderWorkflowState = result.Status == VideoExportStatus.Cancelled
                    ? _renderWorkflowState.CancelActiveOperation()
                    : _renderWorkflowState.FailActiveOperation(operationId);
                if (result.Status == VideoExportStatus.Cancelled)
                {
                    StatusMessage = "Video export cancelled. The reviewed video is retained.";
                }
                else
                {
                    _videoPublicationErrorPresentation = _errorPresentationService.From(
                        result,
                        ErrorPresentationContext.VideoPublication) ?? ErrorPresentation.Create(
                            ApplicationErrorCode.AtomicPublicationFailed,
                            ErrorPresentationContext.VideoPublication);
                    if (currentPublication)
                    {
                        LogAcceptedFailure(_videoPublicationErrorPresentation);
                    }
                    StatusMessage = _videoPublicationErrorPresentation.UserMessage;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!_disposed && operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.CancelActiveOperation();
                StatusMessage = "Video export cancelled. The reviewed video is retained.";
            }
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.VideoPublication,
            exception))
        {
            if (!_disposed && operationId == _imageOperationId)
            {
                bool currentPublication = IsCurrentPublication();
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                _videoPublicationErrorPresentation = _errorPresentationService.FromException(
                    ErrorPresentationContext.VideoPublication,
                    exception) ?? ErrorPresentation.Create(
                        ApplicationErrorCode.AtomicPublicationFailed,
                        ErrorPresentationContext.VideoPublication);
                if (currentPublication)
                {
                    LogAcceptedFailure(_videoPublicationErrorPresentation);
                }
                StatusMessage = _videoPublicationErrorPresentation.UserMessage;
            }
        }
        finally
        {
            await EndImageOperationAsync(operationId);
        }

        if (completed)
        {
            StartExportCompletionPresentation(operationId);
        }

        bool IsCurrentPublication() => !_disposed && mediaRequest == _mediaRequestId
            && ReferenceEquals(metadata, VideoMetadata)
            && ReferenceEquals(artifact, _managedVideoArtifactSession.Current)
            && _renderWorkflowState.VideoArtifactIdentity == artifact.Identity
            && !_renderWorkflowState.IsVideoArtifactStale;
    }

    private string? SelectOutputPath(ImageExportFormat format) => ImageMetadata is null
        ? null
        : _imageSaveDialogService.SelectOutputFile(ImageMetadata.FilePath, format);

    private (long OperationId, CancellationToken Token) BeginImageOperation(
        RenderWorkflowOperation operation,
        string title,
        string text)
    {
        CancelImageOperationCore();
        CancellationTokenSource cancellation = new();
        _imageOperationCancellation = cancellation;
        long operationId = Interlocked.Increment(ref _imageOperationId);
        _renderWorkflowState = _renderWorkflowState.BeginOperation(operationId, operation);
        BeginOperationPresentation(title, text);
        return (operationId, cancellation.Token);
    }

    private (long OperationId, CancellationToken Token) BeginManagedVideoRenderOperation(
        FullVideoRenderIdentity identity)
    {
        CancelImageOperationCore();
        CancellationTokenSource cancellation = new();
        _imageOperationCancellation = cancellation;
        long operationId = Interlocked.Increment(ref _imageOperationId);
        _renderWorkflowState = _renderWorkflowState.BeginVideoRender(operationId, identity);
        CancelReviewRecovery();
        _videoRenderErrorPresentation = null;
        BeginOperationPresentation(
            "Rendering video...",
            identity.AudioMode == AudioExportMode.KeepAudio
                ? "Streaming symbolized frames and preserving source audio locally"
                : "Streaming symbolized frames to a managed video-only MP4");
        return (operationId, cancellation.Token);
    }

    private void BeginOperationPresentation(string title, string text)
    {
        _videoPlaybackCoordinator.PauseAll();
        _activeOperationTitle = title;
        _activeOperationText = text;
        _exportFrameText = string.Empty;
        _elapsedText = "Elapsed 00:00:00";
        _lastExportElapsed = TimeSpan.Zero;
        _remainingText = string.Empty;
        IsExportProgressIndeterminate = true;
        IsExportCancellationRequested = false;
        ExportProgress = 0;
        ExportStage = VideoExportStage.Preparing;
        _exportPresentationGeneration++;
        OnPropertyChanged(nameof(ExportPresentationGeneration));
        _exportStopwatch.Restart();
        _exportElapsedTimer.Start();
        IsImageProcessing = true;
        ApplyPresentationState(_presentationState.BeginExport());
        StatusMessage = _renderWorkflowState.ActiveOperation switch
        {
            RenderWorkflowOperation.RenderImage => "Rendering the symbolized image...",
            RenderWorkflowOperation.PreviewVideoFrame => "Generating a symbolized frame preview...",
            RenderWorkflowOperation.RenderVideo => "Rendering the complete symbolized video...",
            RenderWorkflowOperation.ExportImage => "Exporting the rendered image...",
            RenderWorkflowOperation.ExportVideo => "Publishing the reviewed video...",
            _ => "Processing media locally..."
        };
        OnPropertyChanged(nameof(ExportOperationTitle));
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(ExportPercentageText));
        OnPropertyChanged(nameof(ExportFrameText));
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(RemainingText));
        NotifyCommandStates();
        NotifyImageProperties();
        NotifyVideoArtifactProperties();
    }

    private void EndImageOperation(long operationId)
    {
        if (operationId != _imageOperationId)
        {
            return;
        }

        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _imageOperationCancellation, null);
        cancellation?.Dispose();
        _exportElapsedTimer.Stop();
        _exportStopwatch.Stop();
        _renderWorkflowState = _renderWorkflowState.EndOperation(operationId);
        _managedVideoCommitStarted = false;
        IsImageProcessing = false;
        IsExportCancellationRequested = false;
        ApplyPresentationState(_presentationState.EndExport());
        NotifyImageProperties();
        NotifyVideoArtifactProperties();
        NotifyCommandStates();
    }

    private async Task EndImageOperationAsync(long operationId)
    {
        if (_disposed || operationId != _imageOperationId)
        {
            return;
        }

        EndImageOperation(operationId);
        // Recovery starts only after the operation has released its cancellation/progress
        // lifetime. The shared gate coalesces any Loaded notification racing this boundary.
        if (!_disposed && operationId == _imageOperationId && !_reviewViewUnloaded
            && _reviewRecoveryPending && !IsExportActive)
        {
            await EnsureAcceptedReviewAsync();
        }
    }

    private void CancelActiveImageOperation()
    {
        CancellationTokenSource? cancellation = _imageOperationCancellation;
        if (cancellation is null || !CanCancelExport)
        {
            return;
        }

        IsExportCancellationRequested = true;
        _renderWorkflowState = _renderWorkflowState.BeginCancellation();
        IsExportProgressIndeterminate = true;
        ExportStage = VideoExportStage.Cancelling;
        bool renderingVideo = _renderWorkflowState.ActiveOperation == RenderWorkflowOperation.RenderVideo;
        _activeOperationText = renderingVideo
            ? "Cancelling video rendering and cleaning managed candidates..."
            : IsVideoSelected
                ? "Cancelling export and cleaning partial files..."
            : "Cancelling the current image operation...";
        _remainingText = string.Empty;
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(RemainingText));
        NotifyImageProperties();
        NotifyVideoArtifactProperties();
        NotifyCommandStates();
        cancellation.Cancel();
        StatusMessage = renderingVideo
            ? "Cancelling video rendering... GlyphFrame is cleaning the managed candidate."
            : IsVideoSelected
                ? "Cancelling export... GlyphFrame is removing partial files."
            : "Cancelling the current image operation...";
    }

    private void CancelImageOperationCore()
    {
        CancelExportCompletionPresentationCore();
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _imageOperationCancellation, null);
        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
            Interlocked.Increment(ref _imageOperationId);
            _renderWorkflowState = _renderWorkflowState.CancelActiveOperation();
        }

        if (IsImageProcessing)
        {
            _exportElapsedTimer.Stop();
            _exportStopwatch.Stop();
            IsImageProcessing = false;
            IsExportCancellationRequested = false;
            ApplyPresentationState(_presentationState.EndExport());
        }
    }

    private void DispatchVideoExportProgress(
        long operationId,
        VideoMetadata metadata,
        VideoExportProgress progress)
    {
        if (_disposed)
        {
            return;
        }

        void Apply() => ApplyVideoExportProgress(operationId, metadata, progress);
        if (_synchronizationContext is null
            || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            Apply();
            return;
        }

        try
        {
            _synchronizationContext.Send(static state => ((Action)state!).Invoke(), (Action)Apply);
        }
        catch (InvalidAsynchronousStateException) when (_disposed)
        {
            // The WPF synchronization context can close while an already-cancelled export unwinds.
        }
    }

    private void ApplyVideoExportProgress(
        long operationId,
        VideoMetadata metadata,
        VideoExportProgress progress)
    {
        if (_disposed
            || operationId != _imageOperationId
            || !ReferenceEquals(metadata, VideoMetadata)
            || !IsExportActive
            || (IsExportCancellationRequested && progress.Stage != VideoExportStage.Cancelling))
        {
            return;
        }

        if (_renderWorkflowState.ActiveOperation == RenderWorkflowOperation.RenderVideo
            && (_reviewTransitionActive || progress.Stage == VideoExportStage.Completed))
        {
            return;
        }

        _activeOperationText = progress.StageDescription;
        ExportStage = progress.Stage;
        _exportFrameText = BuildFrameProgressText(progress);
        _lastExportElapsed = progress.Elapsed > _lastExportElapsed
            ? progress.Elapsed
            : _lastExportElapsed;
        _elapsedText = $"Elapsed {FormatExportDuration(_lastExportElapsed)}";
        _remainingText = progress.EstimatedRemaining is TimeSpan remaining
            ? $"Remaining about {FormatExportDuration(remaining)}"
            : string.Empty;
        IsExportProgressIndeterminate = progress.IsIndeterminate;
        if (progress.Percentage is double percentage)
        {
            ExportProgress = Math.Max(ExportProgress, percentage);
        }

        if (progress.Stage == VideoExportStage.Cancelling)
        {
            IsExportCancellationRequested = true;
        }

        NotifyExportProgressProperties();
        NotifyCommandStates();
    }

    private void NotifyExportProgressProperties()
    {
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(ExportFrameText));
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(ExportPercentageText));
    }

    private void StartExportCompletionPresentation(long operationId)
    {
        if (_disposed || operationId != _imageOperationId)
        {
            return;
        }

        CancelExportCompletionPresentationCore();
        CancellationTokenSource cancellation = new();
        _exportCompletionCancellation = cancellation;
        long presentationId = Interlocked.Increment(ref _exportCompletionPresentationId);
        IsExportCompletionVisible = true;
        _ = HoldExportCompletionPresentationAsync(presentationId, cancellation);
    }

    private async Task HoldExportCompletionPresentationAsync(
        long presentationId,
        CancellationTokenSource cancellation)
    {
        try
        {
            await _exportStatusPresentationService.HoldCompletedStateAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _exportCompletionCancellation, null, cancellation),
                    cancellation))
            {
                cancellation.Dispose();
                if (!_disposed && presentationId == _exportCompletionPresentationId)
                {
                    IsExportCompletionVisible = false;
                }
            }
        }
    }

    private void CancelExportCompletionPresentationCore()
    {
        Interlocked.Increment(ref _exportCompletionPresentationId);
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _exportCompletionCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        IsExportCompletionVisible = false;
    }

    private void OnExportElapsedTimerTick(object? sender, EventArgs e)
    {
        if (!IsExportActive)
        {
            return;
        }

        if (_exportStopwatch.Elapsed > _lastExportElapsed)
        {
            _lastExportElapsed = _exportStopwatch.Elapsed;
        }

        _elapsedText = $"Elapsed {FormatExportDuration(_lastExportElapsed)}";
        OnPropertyChanged(nameof(ElapsedText));
    }

    private static string BuildFrameProgressText(VideoExportProgress progress)
    {
        if (progress.TotalFrames is long totalFrames)
        {
            return $"Frame {progress.ProcessedFrames:N0} / {totalFrames:N0}";
        }

        return progress.ProcessedFrames > 0
            ? $"Frame {progress.ProcessedFrames:N0}"
            : string.Empty;
    }

    private static string FormatExportDuration(TimeSpan duration)
    {
        TimeSpan safe = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        long totalHours = (long)Math.Floor(safe.TotalHours);
        return $"{totalHours:00}:{safe.Minutes:00}:{safe.Seconds:00}";
    }

    private bool IsCurrentImageOperation(
        long operationId,
        PreviewIdentity identity,
        DecodedImage source,
        RenderWorkflowOperation operation) =>
        operationId == _imageOperationId
        && _renderWorkflowState.CanAcceptResult(operationId, identity, operation)
        && ReferenceEquals(source, _decodedImage);

    private void ApplyRenderedResult(
        ImagePipelineResult result,
        PreviewIdentity identity,
        RenderWorkflowOperation operation)
    {
        ImageSource imageSource = _imageSourceFactory.Create(result.RenderedImage);
        RenderedImage? previous = _renderedImage;
        _renderedImage = result.RenderedImage;
        _symbolizedImageSource = imageSource;
        _renderWorkflowState = operation == RenderWorkflowOperation.RenderImage
            ? _renderWorkflowState.CompleteImageRender(_renderWorkflowState.ActiveOperationId, identity)
            : _renderWorkflowState.CompleteVideoFramePreview(_renderWorkflowState.ActiveOperationId, identity);
        previous?.Dispose();
        _lastGridDimensions = $"{result.Grid.Columns} × {result.Grid.Rows}";
        _lastOutputDimensions = $"{result.RenderedImage.Dimensions.Width} × {result.RenderedImage.Dimensions.Height}";
        _lastGridSummary = $"{_lastGridDimensions} symbols  •  {_lastOutputDimensions} output";
        if (operation == RenderWorkflowOperation.RenderImage)
        {
            _lastImageExportPath = null;
            _imageRenderWarning = result.GlyphMetrics is { UsedFallback: true, DiagnosticMessage.Length: > 0 }
                ? result.GlyphMetrics.DiagnosticMessage
                : null;
        }

        NotifyImageProperties();
    }

    private void ClearSymbolizedPreview()
    {
        RenderedImage? previous = _renderedImage;
        _renderedImage = null;
        _symbolizedImageSource = null;
        _renderWorkflowState = _renderWorkflowState.ClearResult();
        previous?.Dispose();
        _lastGridSummary = string.Empty;
        _lastGridDimensions = string.Empty;
        _lastOutputDimensions = string.Empty;
        _imageRenderWarning = null;
        _lastImageExportPath = null;
        PreviewMode = PreviewMode.Original;
        NotifyImageProperties();
    }

    private void InvalidateSymbolizedPreview(ImageRenderSetting setting)
    {
        _renderWorkflowState = _renderWorkflowState.RenderingSettingChanged(setting);
        CancelInvalidatedPublication();
        if (HasSymbolizedReview)
        {
            OnPropertyChanged(nameof(IsSymbolizedPreviewStale));
            OnPropertyChanged(nameof(PreviewStaleMessage));
            StatusMessage = HasVideoArtifact
                ? "Render settings changed. The managed video remains playable but is now stale."
                : "Render settings changed. The symbolized preview is now stale.";
        }

        NotifyImageProperties();
        NotifyVideoArtifactProperties();
        NotifyCommandStates();
    }

    private void CancelInvalidatedPublication()
    {
        if (_renderWorkflowState.ActiveOperation == RenderWorkflowOperation.ExportVideo)
        {
            CancelActiveImageOperation();
        }
    }

    private ImageRenderSettings CaptureRenderSettings() => new(
        SelectedFont,
        SelectedFontSize,
        ResolveOutputResolution(),
        SelectedSymbolPolarity);

    private OutputResolution ResolveOutputResolution() =>
        AppSettingsCatalog.OutputResolutions
            .FirstOrDefault(option => option.DisplayName.Equals(SelectedOutputResolution, StringComparison.OrdinalIgnoreCase))
            ?.Value
        ?? OutputResolution.SameAsSource;

    private ImageExportFormat ResolveImageExportFormat() =>
        AppSettingsCatalog.ImageExportFormats
            .FirstOrDefault(option => option.DisplayName.Equals(SelectedImageOutputFormat, StringComparison.OrdinalIgnoreCase))
            ?.Value
        ?? ImageExportFormat.Png;

    private bool CanRenderImage() =>
        IsImageSelected
        && _renderWorkflowState.CanRenderImage
        && _decodedImage is not null
        && !IsLoadingMedia
        && !IsVideoFrameLoading
        && !IsExportActive;

    private bool CanPreviewFrame() =>
        IsVideoSelected
        && !HasVideoArtifact
        && _renderWorkflowState.CanPreviewVideoFrame
        && (_decodedImage is not null || IsOriginalVideoPlaybackReady)
        && !IsLoadingMedia
        && !IsVideoFrameLoading
        && !IsExportActive;

    private bool CanRenderVideo() => IsVideoSelected
        && VideoMetadata is not null
        && _renderWorkflowState.CanRenderVideo
        && _videoRenderToolsAvailable
        && !IsLoadingMedia
        && !IsVideoFrameLoading
        && !IsExportActive
        && !_reviewViewUnloaded
        && !_disposed;

    private bool CanExportImage() => IsImageSelected
        && _renderWorkflowState.CanExportImage
        && _renderedImage is not null
        && !IsLoadingMedia
        && !IsExportActive;

    private bool CanExportVideo() => IsVideoSelected
        && VideoMetadata is not null && HasVideoArtifact && _renderWorkflowState.CanExportVideo
        && !_disposed && !IsLoadingMedia && !IsVideoFrameLoading && !IsExportActive
        && !_reviewTransitionActive;

    private bool CanControlOriginalVideo() => IsOriginalPreviewSelected
        && IsOriginalVideoPlaybackReady
        && !IsExportActive
        && !_disposed;


    private bool IsCurrentManagedVideoRender(
        long operationId,
        VideoMetadata metadata,
        FullVideoRenderIdentity identity) =>
        !_disposed
        && operationId == _imageOperationId
        && ReferenceEquals(metadata, VideoMetadata)
        && _renderWorkflowState.CanAcceptVideoRender(operationId, identity);

    private async Task RejectManagedVideoCandidateAsync(
        ManagedVideoArtifact candidate, long operationId, bool reportFailure = true)
    {
        try
        {
            if (_managedVideoArtifactSession.Current?.ArtifactId != candidate.ArtifactId)
            {
                await _managedVideoArtifactSession.RejectAsync(candidate, CancellationToken.None);
            }
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.ManagedArtifact,
            exception))
        {
            if (reportFailure && !_disposed && operationId == _imageOperationId)
            {
                _videoRenderErrorPresentation = ErrorPresentation.Create(
                    ApplicationErrorCode.ArtifactCleanupFailed,
                    ErrorPresentationContext.ManagedArtifact,
                    exception.ToString());
                LogAcceptedFailure(_videoRenderErrorPresentation);
            }
        }
    }

    private async Task RestorePreviousVideoReviewAsync(
        ManagedVideoArtifact? previous,
        TimeSpan position,
        bool wasPlaying)
    {
        if (_disposed || _reviewViewUnloaded || previous is null
            || _managedVideoArtifactSession.Current?.ArtifactId != previous.ArtifactId
            || !File.Exists(previous.FilePath))
        {
            return;
        }

        _reviewRecoveryPending = true;
        _suspendedReviewPosition = position;
        long operationId = _imageOperationId;
        long viewGeneration = _reviewViewGeneration;
        if (await EnsureAcceptedReviewAsync(allowActiveOperation: true) && wasPlaying
            && !_disposed && !_reviewViewUnloaded && operationId == _imageOperationId
            && viewGeneration == _reviewViewGeneration
            && _managedVideoArtifactSession.Current?.ArtifactId == previous.ArtifactId
            && _renderedVideoPlayer.SourcePath == previous.FilePath)
        {
            _renderedVideoPlayer.Play();
        }
    }

    private async Task SelectOriginalPreviewAsync()
    {
        if (PreviewMode == PreviewMode.Original)
        {
            return;
        }

        long comparisonGeneration = BeginComparisonSwitch();
        if (!IsVideoSelected || VideoMetadata is not VideoMetadata metadata)
        {
            _videoPlaybackCoordinator.PauseAll();
            PreviewMode = PreviewMode.Original;
            return;
        }

        long mediaVersion = _renderWorkflowState.MediaVersion;
        long operationId = _imageOperationId;
        long viewGeneration = _reviewViewGeneration;
        PlaybackTimestampTransfer transfer = _videoPlaybackCoordinator.PrepareOriginalSwitch(
            ResolveOriginalComparisonDuration(),
            ResolveTrustworthyFramesPerSecond(metadata.Fps));
        PreviewMode = PreviewMode.Original;
        if (!IsCurrentComparison(
                comparisonGeneration,
                mediaVersion,
                operationId,
                viewGeneration,
                metadata.FilePath,
                PreviewMode.Original))
        {
            return;
        }

        if (IsOriginalVideoPlaybackReady)
        {
            _videoPlaybackCoordinator.ApplyOriginalTransfer(transfer);
            NotifyOriginalVideoPlayerProperties();
            return;
        }

        PendingOriginalComparison pending = new(
            comparisonGeneration,
            mediaVersion,
            operationId,
            viewGeneration,
            metadata.FilePath,
            _originalVideoOpenGeneration,
            transfer);
        if (IsOriginalVideoPlaybackOpening)
        {
            _pendingOriginalComparison = pending;
            return;
        }

        await ApplyOriginalFallbackComparisonAsync(pending, metadata);
    }

    private void SelectSymbolizedPreview()
    {
        if (PreviewMode == PreviewMode.Symbolized)
        {
            return;
        }

        long comparisonGeneration = BeginComparisonSwitch();
        ManagedVideoArtifact? artifact = _managedVideoArtifactSession.Current;
        if (IsVideoSelected && artifact is not null && IsAcceptedReviewOpen)
        {
            TimeSpan? outgoingTimestamp = IsOriginalVideoPlaybackReady
                ? null
                : ResolveTimelineTimestamp(TimelinePosition, ResolveOriginalComparisonDuration());
            PlaybackTimestampTransfer transfer = _videoPlaybackCoordinator.PrepareRenderedSwitch(
                ResolveRenderedComparisonDuration(artifact),
                ResolveTrustworthyFramesPerSecond(artifact.FramesPerSecond),
                outgoingTimestamp);
            Guid artifactId = artifact.ArtifactId;
            long mediaVersion = _renderWorkflowState.MediaVersion;
            PreviewMode = PreviewMode.Symbolized;
            if (!_disposed
                && comparisonGeneration == _comparisonGeneration
                && mediaVersion == _renderWorkflowState.MediaVersion
                && _managedVideoArtifactSession.Current?.ArtifactId == artifactId
                && IsAcceptedReviewOpen)
            {
                _videoPlaybackCoordinator.ApplyRenderedTransfer(transfer);
                NotifyVideoPlayerProperties();
            }
            return;
        }

        _videoPlaybackCoordinator.PauseAll();
        PreviewMode = PreviewMode.Symbolized;
    }

    private long BeginComparisonSwitch()
    {
        CancelComparisonSwitch();
        return _comparisonGeneration;
    }

    private void CancelComparisonSwitch()
    {
        Interlocked.Increment(ref _comparisonGeneration);
        _pendingOriginalComparison = null;
        if (_comparisonFrameRequestActive)
        {
            _comparisonFrameRequestActive = false;
            CancelVideoFrameRequestCore();
        }
    }

    private bool IsCurrentComparison(
        long comparisonGeneration,
        long mediaVersion,
        long operationId,
        long viewGeneration,
        string inputPath,
        PreviewMode targetMode) =>
        !_disposed
        && !_reviewViewUnloaded
        && comparisonGeneration == _comparisonGeneration
        && mediaVersion == _renderWorkflowState.MediaVersion
        && operationId == _imageOperationId
        && viewGeneration == _reviewViewGeneration
        && PreviewMode == targetMode
        && VideoMetadata?.FilePath.Equals(inputPath, StringComparison.OrdinalIgnoreCase) == true;

    private bool TryApplyPendingOriginalComparison(
        VideoMetadata metadata,
        long mediaVersion,
        long openGeneration)
    {
        PendingOriginalComparison? pending = _pendingOriginalComparison;
        if (pending is null
            || pending.OriginalOpenGeneration != openGeneration
            || !IsCurrentComparison(
                pending.ComparisonGeneration,
                mediaVersion,
                pending.OperationId,
                pending.ViewGeneration,
                metadata.FilePath,
                PreviewMode.Original))
        {
            return false;
        }

        _pendingOriginalComparison = null;
        _videoPlaybackCoordinator.ApplyOriginalTransfer(pending.Transfer);
        StatusMessage = "Original video playback is ready at the comparison timestamp.";
        NotifyOriginalVideoPlayerProperties();
        return true;
    }

    private async Task ApplyPendingOriginalFallbackAsync(
        VideoMetadata metadata,
        long mediaVersion,
        long openGeneration)
    {
        PendingOriginalComparison? pending = _pendingOriginalComparison;
        if (pending is null
            || pending.OriginalOpenGeneration != openGeneration
            || !IsCurrentComparison(
                pending.ComparisonGeneration,
                mediaVersion,
                pending.OperationId,
                pending.ViewGeneration,
                metadata.FilePath,
                PreviewMode.Original))
        {
            return;
        }

        _pendingOriginalComparison = null;
        await ApplyOriginalFallbackComparisonAsync(pending, metadata);
    }

    private async Task ApplyOriginalFallbackComparisonAsync(
        PendingOriginalComparison pending,
        VideoMetadata metadata)
    {
        if (!IsCurrentComparison(
                pending.ComparisonGeneration,
                pending.MediaVersion,
                pending.OperationId,
                pending.ViewGeneration,
                pending.InputPath,
                PreviewMode.Original))
        {
            return;
        }

        TimeSpan? duration = pending.Transfer.TargetDuration;
        double normalizedPosition = duration is TimeSpan usableDuration && usableDuration > TimeSpan.Zero
            ? Math.Clamp(pending.Transfer.TargetTimestamp.TotalSeconds / usableDuration.TotalSeconds, 0d, 1d)
            : 0d;
        SetTimelinePositionSilently(normalizedPosition);
        _comparisonFrameRequestActive = true;
        try
        {
            await LoadVideoFrameAsync(
                metadata.FilePath,
                normalizedPosition,
                pending.MediaVersion,
                debounce: false);
        }
        finally
        {
            if (pending.ComparisonGeneration == _comparisonGeneration)
            {
                _comparisonFrameRequestActive = false;
            }
        }
    }

    private void ToggleRenderedVideoPlayback()
    {
        if (_renderedVideoPlayer.HasEnded)
        {
            _videoPlaybackCoordinator.RestartRendered();
        }
        else if (_renderedVideoPlayer.IsPlaying)
        {
            _renderedVideoPlayer.Pause();
        }
        else
        {
            _videoPlaybackCoordinator.PlayRendered();
        }
    }

    private void ToggleOriginalVideoPlayback()
    {
        if (_originalVideoPlayer.HasEnded)
        {
            _videoPlaybackCoordinator.RestartOriginal();
        }
        else if (_originalVideoPlayer.IsPlaying)
        {
            _originalVideoPlayer.Pause();
        }
        else
        {
            _videoPlaybackCoordinator.PlayOriginal();
        }
    }

    private void OnRenderedVideoPlayerStateChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        void Apply()
        {
            if (_disposed)
            {
                return;
            }

            if (!_reviewTransitionActive && !_reviewRecoveryActive && HasVideoArtifact
                && !string.IsNullOrWhiteSpace(_renderedVideoPlayer.FailureMessage))
            {
                bool newFailure = !_videoReviewUnavailable;
                SetReviewUnavailable(_renderedVideoPlayer.TechnicalDetail);
                if (newFailure)
                {
                    LogAcceptedFailure(_videoReviewErrorPresentation);
                }
                StatusMessage = _videoReviewErrorPresentation?.UserMessage
                    ?? "GlyphFrame could not open the rendered video for review. The review file is retained.";
            }

            _suppressRenderedVideoSeek = true;
            try
            {
                NotifyVideoArtifactProperties();
            }
            finally
            {
                _suppressRenderedVideoSeek = false;
            }
        }

        if (_synchronizationContext is null
            || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            Apply();
        }
        else
        {
            try
            {
                _synchronizationContext.Send(static state => ((Action)state!).Invoke(), (Action)Apply);
            }
            catch (InvalidAsynchronousStateException) when (_disposed)
            {
                // The WPF synchronization context can close while the media backend releases handles.
            }
        }
    }

    private void OnOriginalVideoPlayerStateChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        void Apply()
        {
            if (_disposed || !IsVideoSelected
                || VideoMetadata?.FilePath.Equals(
                    _originalVideoPlayer.SourcePath,
                    StringComparison.OrdinalIgnoreCase) != true)
            {
                return;
            }

            if (_originalVideoPlayer.FallbackRequired
                && !string.IsNullOrWhiteSpace(_originalVideoPlayer.FailureMessage)
                && !IsExportActive)
            {
                bool newFailure = _originalPlaybackErrorPresentation is null;
                _originalPlaybackErrorPresentation = _errorPresentationService.PlaybackFailure(
                    ErrorPresentationContext.OriginalPlayback,
                    _originalVideoPlayer.FailureMessage,
                    _originalVideoPlayer.TechnicalDetail);
                if (newFailure && _originalVideoOpenCancellation is null)
                {
                    LogAcceptedFailure(_originalPlaybackErrorPresentation);
                }
                StatusMessage = _originalPlaybackErrorPresentation.UserMessage;
            }
            else if (_originalVideoPlayer.IsPlaybackReady)
            {
                _originalPlaybackErrorPresentation = null;
            }

            _suppressOriginalVideoSeek = true;
            try
            {
                NotifyOriginalVideoPlayerProperties();
            }
            finally
            {
                _suppressOriginalVideoSeek = false;
            }
        }

        if (_synchronizationContext is null
            || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            Apply();
        }
        else
        {
            try
            {
                _synchronizationContext.Send(static state => ((Action)state!).Invoke(), (Action)Apply);
            }
            catch (InvalidAsynchronousStateException) when (_disposed)
            {
            }
        }
    }

    private static string FormatAudioOutcomeValue(AudioMuxOutcome outcome) => outcome switch
    {
        AudioMuxOutcome.StreamCopied => "Original",
        AudioMuxOutcome.TranscodedToAac => "AAC",
        AudioMuxOutcome.Removed => "Removed",
        AudioMuxOutcome.NoSourceAudio => "No source",
        _ => "Unavailable"
    };

    private static string FormatAudioOutcomeDescription(AudioMuxOutcome outcome) => outcome switch
    {
        AudioMuxOutcome.StreamCopied => "Original audio preserved",
        AudioMuxOutcome.TranscodedToAac => "Audio transcoded to AAC",
        AudioMuxOutcome.Removed => "Audio removed from rendered video",
        AudioMuxOutcome.NoSourceAudio => "Source video contains no audio",
        _ => "Audio outcome unavailable"
    };

    private TimeSpan ResolveRenderedVideoDuration() => _renderedVideoPlayer.Duration > TimeSpan.Zero
        ? _renderedVideoPlayer.Duration
        : _managedVideoArtifactSession.Current?.Duration is TimeSpan duration && duration > TimeSpan.Zero
            ? duration
            : TimeSpan.Zero;

    private TimeSpan ResolveOriginalVideoDuration() => _originalVideoPlayer.Duration > TimeSpan.Zero
        ? _originalVideoPlayer.Duration
        : VideoMetadata?.Duration ?? TimeSpan.Zero;

    private TimeSpan? ResolveOriginalComparisonDuration() =>
        IsUsableDuration(VideoMetadata?.Duration) ? VideoMetadata!.Duration
        : IsUsableDuration(_originalVideoPlayer.Duration) ? _originalVideoPlayer.Duration
        : null;

    private TimeSpan? ResolveRenderedComparisonDuration(ManagedVideoArtifact artifact) =>
        IsUsableDuration(artifact.Duration) ? artifact.Duration
        : IsUsableDuration(_renderedVideoPlayer.Duration) ? _renderedVideoPlayer.Duration
        : null;

    private static bool IsUsableDuration(TimeSpan? duration) =>
        duration is TimeSpan value && value > TimeSpan.Zero;

    private static double? ResolveTrustworthyFramesPerSecond(double? framesPerSecond) =>
        framesPerSecond is double fps && double.IsFinite(fps) && fps > 0d ? fps : null;

    private static int NormalizeVideoRotation(int rotation)
    {
        int normalized = ((rotation % 360) + 360) % 360;
        return normalized is 90 or 180 or 270 ? normalized : 0;
    }


    private bool CanOpenExportFolderAction() => CanOpenExportFolder;

    private async Task OpenExportFolderAsync()
    {
        string? exportedPath = IsImageSelected ? _lastImageExportPath : _lastVideoExportPath;
        if (string.IsNullOrWhiteSpace(exportedPath))
        {
            return;
        }

        string? folder = Path.GetDirectoryName(exportedPath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "GlyphFrame could not resolve the exported file folder.";
            return;
        }

        try
        {
            await _folderLauncher.OpenFolderAsync(folder);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Win32Exception)
        {
            StatusMessage = "GlyphFrame could not open the exported image folder.";
        }
    }

    private void ApplyImmediateFailure(MediaInputResult failure)
    {
        CancellationTokenSource? previous = Interlocked.Exchange(ref _mediaLoadCancellation, null);
        previous?.Cancel();
        previous?.Dispose();
        CancelVideoFrameRequestCore();
        CancelImageOperationCore();

        long requestId = Interlocked.Increment(ref _mediaRequestId);
        MediaInputState loading = _mediaInputState.BeginLoading(requestId, null);
        _pendingFilePath = null;
        _mediaInputErrorPresentation = _errorPresentationService.From(failure);
        ApplyMediaInputState(loading.Complete(requestId, failure));
        LogAcceptedFailure(_mediaInputErrorPresentation);
        StatusMessage = _mediaInputErrorPresentation?.UserMessage
            ?? "GlyphFrame could not read this media. Choose another file and try again.";
    }

    private void ApplyMediaInputState(MediaInputState state)
    {
        if (!SetProperty(ref _mediaInputState, state, nameof(MediaInputState)))
        {
            return;
        }

        NotifyMediaProperties();
        NotifyCommandStates();
    }

    private void ApplyPresentationState(UiPresentationState state)
    {
        if (!SetProperty(ref _presentationState, state, nameof(PresentationState)))
        {
            return;
        }

        OnPropertyChanged(nameof(IsExportActive));
        OnPropertyChanged(nameof(IsExportStatusVisible));
        OnPropertyChanged(nameof(IsProcessingResultPanelVisible));
        OnPropertyChanged(nameof(CanCancelExport));
        OnPropertyChanged(nameof(AreSettingsEnabled));
        OnPropertyChanged(nameof(IsBrowseMediaAvailable));
        OnPropertyChanged(nameof(ExportPercentageText));
        NotifyCommandStates();
    }

    private void NotifyMediaProperties()
    {
        string[] propertyNames =
        [
            nameof(CurrentMedia), nameof(ImageMetadata), nameof(VideoMetadata),
            nameof(IsNoMediaSelected), nameof(IsMediaSelected), nameof(IsImageSelected), nameof(IsVideoSelected),
            nameof(IsLoadingMedia), nameof(HasMediaInputError), nameof(MediaInputErrorPresentation),
            nameof(ShowMediaInputErrorDetails), nameof(ShowRetainedMediaInputErrorDetails),
            nameof(ShowEmptyPreview), nameof(ShowLoadingPreview),
            nameof(HasVideoFrame), nameof(IsVideoFrameLoading), nameof(HasVideoFrameError),
            nameof(VideoFrameStatusMessage), nameof(VideoFrameTechnicalDetail), nameof(VideoFrameErrorPresentation),
            nameof(ShowVideoFrameErrorDetails),
            nameof(ShowVideoFrameLoading),
            nameof(ShowVideoFrameError), nameof(ShowMediaPlaceholder), nameof(ShowImagePreview),
            nameof(ShowErrorPreview), nameof(IsBrowseMediaAvailable), nameof(IsVideoSeekEnabled),
            nameof(AreSettingsEnabled), nameof(InputLabel), nameof(PreviewFrameActionLabel),
            nameof(SelectedFileName), nameof(SelectedFilePath), nameof(MediaTypeText), nameof(MetadataPrimaryText),
            nameof(MetadataSecondaryText), nameof(FormattedFileSize), nameof(FormattedDimensions),
            nameof(FormattedDuration), nameof(FormattedFps), nameof(FormattedFrameCount),
            nameof(FormattedCodecInformation), nameof(OrientationText), nameof(AudioInformation),
            nameof(InputStatusMessage), nameof(InputMetadataSummary), nameof(TimelineText),
            nameof(VideoFramePositionText),
            nameof(IsOriginalPreviewSelected), nameof(IsSymbolizedPreviewSelected),
            nameof(ShowResolutionWarning), nameof(CurrentPreviewImageSource), nameof(CurrentPreviewPixelWidth), nameof(CurrentPreviewPixelHeight),
            nameof(WorkflowStatus), nameof(ShowImageResultPanel), nameof(ShowImageExportControls),
            nameof(IsProcessingResultPanelVisible), nameof(HasVideoArtifact), nameof(HasSymbolizedReview),
            nameof(ShowVideoResultPanel), nameof(VideoRenderActionLabel), nameof(ShowRenderedVideoPlayer),
            nameof(ShowRenderedVideoReviewError), nameof(ShowVideoFrameControls),
            nameof(IsOriginalVideoPlaybackOpening), nameof(IsOriginalVideoPlaybackReady),
            nameof(IsOriginalVideoPlaybackFallback), nameof(ShowOriginalVideoPlayer),
            nameof(ShowOriginalPlaybackOpening), nameof(ShowOriginalPlaybackFallback),
            nameof(OriginalVideoPlaybackLimitationMessage), nameof(OriginalVideoPlaybackTechnicalDetail),
            nameof(OriginalPlaybackErrorPresentation), nameof(ShowOriginalPlaybackErrorDetails)
        ];

        foreach (string propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void NotifyImageProperties()
    {
        OnPropertyChanged(nameof(OriginalImageSource));
        OnPropertyChanged(nameof(SymbolizedImageSource));
        OnPropertyChanged(nameof(CurrentPreviewImageSource));
        OnPropertyChanged(nameof(CurrentPreviewPixelWidth));
        OnPropertyChanged(nameof(CurrentPreviewPixelHeight));
        OnPropertyChanged(nameof(ShowImagePreview));
        OnPropertyChanged(nameof(HasVideoFrame));
        OnPropertyChanged(nameof(HasSymbolizedPreview));
        OnPropertyChanged(nameof(HasSymbolizedReview));
        OnPropertyChanged(nameof(IsSymbolizedPreviewStale));
        OnPropertyChanged(nameof(HasImageRender));
        OnPropertyChanged(nameof(IsImageRenderStale));
        OnPropertyChanged(nameof(WorkflowStatus));
        OnPropertyChanged(nameof(ShowImageResultPanel));
        OnPropertyChanged(nameof(ShowImageExportControls));
        OnPropertyChanged(nameof(ShowImageRenderMetrics));
        OnPropertyChanged(nameof(ImageGridDimensionsText));
        OnPropertyChanged(nameof(ImageOutputDimensionsText));
        OnPropertyChanged(nameof(ImageRenderWarningText));
        OnPropertyChanged(nameof(ShowImageRenderWarning));
        OnPropertyChanged(nameof(IsProcessingResultPanelVisible));
        OnPropertyChanged(nameof(IsImageResultSuccess));
        OnPropertyChanged(nameof(IsImageResultWarning));
        OnPropertyChanged(nameof(IsImageResultFailure));
        OnPropertyChanged(nameof(IsImageExported));
        OnPropertyChanged(nameof(CanOpenExportFolder));
        OnPropertyChanged(nameof(HasVideoExportDestination));
        OnPropertyChanged(nameof(VideoExportDestinationText));
        OnPropertyChanged(nameof(VideoExportActionLabel));
        OnPropertyChanged(nameof(ImageRenderActionLabel));
        OnPropertyChanged(nameof(ImageExportActionLabel));
        OnPropertyChanged(nameof(ImageResultTitle));
        OnPropertyChanged(nameof(ImageResultSummary));
        OnPropertyChanged(nameof(ImageRenderErrorPresentation));
        OnPropertyChanged(nameof(ImageExportErrorPresentation));
        OnPropertyChanged(nameof(ShowImageResultErrorDetails));
        OnPropertyChanged(nameof(PreviewStaleMessage));
        NotifyCommandStates();
    }

    private void NotifyVideoArtifactProperties()
    {
        string[] propertyNames =
        [
            nameof(HasVideoArtifact), nameof(HasSymbolizedReview), nameof(IsSymbolizedPreviewStale),
            nameof(WorkflowStatus), nameof(ShowVideoResultPanel), nameof(IsVideoResultSuccess),
            nameof(IsVideoResultWarning), nameof(IsVideoResultFailure), nameof(VideoRenderActionLabel),
            nameof(VideoResultTitle), nameof(VideoResultSummary), nameof(VideoRenderTechnicalDetail),
            nameof(VideoRenderErrorPresentation), nameof(VideoPublicationErrorPresentation),
            nameof(VideoReviewErrorPresentation), nameof(ShowVideoReviewErrorDetails),
            nameof(ShowVideoResultErrorDetails),
            nameof(ShowVideoRenderMetrics), nameof(VideoRenderedFrameCountText), nameof(VideoOutputDimensionsText),
            nameof(VideoAudioOutcomeText), nameof(VideoAudioOutcomeDescription),
            nameof(HasVideoExportDestination), nameof(VideoExportDestinationText), nameof(VideoExportActionLabel), nameof(CanOpenExportFolder),
            nameof(IsProcessingResultPanelVisible), nameof(ShowRenderedVideoPlayer),
            nameof(ShowRenderedVideoReviewError), nameof(ShowImagePreview), nameof(PreviewStaleMessage),
            nameof(ShowVideoFrameControls), nameof(ShowMediaPlaceholder)
        ];

        foreach (string propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }

        NotifyVideoPlayerProperties();
        NotifyCommandStates();
    }

    private void NotifyVideoPlayerProperties()
    {
        string[] propertyNames =
        [
            nameof(RenderedVideoPlaybackSource), nameof(ShowRenderedVideoPlayer),
            nameof(ShowRenderedVideoReviewError), nameof(ShowVideoFrameControls),
            nameof(RenderedVideoPlaybackActionLabel),
            nameof(RenderedVideoMuteActionLabel), nameof(RenderedVideoPositionSeconds),
            nameof(RenderedVideoDurationSeconds), nameof(RenderedVideoVolume),
            nameof(IsRenderedVideoPlaying), nameof(IsRenderedVideoMuted), nameof(ShowRenderedVideoReplay),
            nameof(RenderedVideoPixelWidth), nameof(RenderedVideoPixelHeight),
            nameof(RenderedVideoCurrentTimeText), nameof(RenderedVideoTotalTimeText),
            nameof(ShowMediaPlaceholder), nameof(ShowVideoReviewErrorDetails),
            nameof(ShowVideoResultErrorDetails)
        ];

        foreach (string propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }

        _toggleRenderedVideoPlaybackCommand.NotifyCanExecuteChanged();
        _restartRenderedVideoCommand.NotifyCanExecuteChanged();
        _toggleRenderedVideoMuteCommand.NotifyCanExecuteChanged();
        _fullscreenCommand.NotifyCanExecuteChanged();
    }

    private void NotifyOriginalVideoPlayerProperties()
    {
        string[] propertyNames =
        [
            nameof(IsOriginalVideoPlaybackOpening), nameof(IsOriginalVideoPlaybackReady),
            nameof(IsOriginalVideoPlaybackFallback), nameof(ShowOriginalVideoPlayer),
            nameof(ShowOriginalPlaybackOpening), nameof(ShowOriginalPlaybackFallback),
            nameof(OriginalVideoPlaybackSource), nameof(OriginalVideoPlaybackLimitationMessage),
            nameof(OriginalVideoPlaybackTechnicalDetail), nameof(OriginalPlaybackErrorPresentation),
            nameof(ShowOriginalPlaybackErrorDetails),
            nameof(OriginalVideoPlaybackActionLabel),
            nameof(OriginalVideoMuteActionLabel), nameof(IsOriginalVideoPlaying),
            nameof(IsOriginalVideoMuted), nameof(ShowOriginalVideoReplay),
            nameof(OriginalVideoPixelWidth), nameof(OriginalVideoPixelHeight),
            nameof(OriginalVideoRotationDegrees), nameof(OriginalVideoCurrentTimeText),
            nameof(OriginalVideoTotalTimeText), nameof(OriginalVideoPositionSeconds),
            nameof(OriginalVideoDurationSeconds), nameof(OriginalVideoVolume),
            nameof(ShowImagePreview), nameof(ShowVideoFrameControls), nameof(ShowVideoFrameLoading),
            nameof(ShowVideoFrameError), nameof(ShowMediaPlaceholder)
        ];

        foreach (string propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }

        _toggleOriginalVideoPlaybackCommand.NotifyCanExecuteChanged();
        _restartOriginalVideoCommand.NotifyCanExecuteChanged();
        _toggleOriginalVideoMuteCommand.NotifyCanExecuteChanged();
        _originalFullscreenCommand.NotifyCanExecuteChanged();
        _previewFrameCommand.NotifyCanExecuteChanged();
    }

    private void NotifyVideoFrameProperties()
    {
        string[] propertyNames =
        [
            nameof(HasVideoFrame), nameof(IsVideoFrameLoading), nameof(HasVideoFrameError),
            nameof(VideoFrameStatusMessage), nameof(VideoFrameTechnicalDetail), nameof(VideoFrameErrorPresentation),
            nameof(ShowVideoFrameErrorDetails), nameof(ShowVideoResultErrorDetails),
            nameof(ShowVideoFrameLoading), nameof(ShowVideoFrameError), nameof(ShowMediaPlaceholder),
            nameof(ShowImagePreview), nameof(IsVideoSeekEnabled), nameof(AreSettingsEnabled),
            nameof(InputStatusMessage), nameof(TimelineText), nameof(VideoFramePositionText),
            nameof(CurrentPreviewImageSource), nameof(CurrentPreviewPixelWidth), nameof(CurrentPreviewPixelHeight)
        ];

        foreach (string propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void NotifyCommandStates()
    {
        _browseMediaCommand.NotifyCanExecuteChanged();
        _dropMediaCommand.NotifyCanExecuteChanged();
        _renderImageCommand.NotifyCanExecuteChanged();
        _previewFrameCommand.NotifyCanExecuteChanged();
        _renderVideoCommand.NotifyCanExecuteChanged();
        _exportImageCommand.NotifyCanExecuteChanged();
        _exportVideoCommand.NotifyCanExecuteChanged();
        _openExportFolderCommand.NotifyCanExecuteChanged();
        _cancelExportCommand.NotifyCanExecuteChanged();
        _selectOriginalCommand.NotifyCanExecuteChanged();
        _selectSymbolizedCommand.NotifyCanExecuteChanged();
        _toggleRenderedVideoPlaybackCommand.NotifyCanExecuteChanged();
        _restartRenderedVideoCommand.NotifyCanExecuteChanged();
        _toggleRenderedVideoMuteCommand.NotifyCanExecuteChanged();
        _fullscreenCommand.NotifyCanExecuteChanged();
        _toggleOriginalVideoPlaybackCommand.NotifyCanExecuteChanged();
        _restartOriginalVideoCommand.NotifyCanExecuteChanged();
        _toggleOriginalVideoMuteCommand.NotifyCanExecuteChanged();
        _originalFullscreenCommand.NotifyCanExecuteChanged();
        _showMediaInputErrorDetailsCommand.NotifyCanExecuteChanged();
        _showVideoFrameErrorDetailsCommand.NotifyCanExecuteChanged();
        _showOriginalPlaybackErrorDetailsCommand.NotifyCanExecuteChanged();
        _showVideoReviewErrorDetailsCommand.NotifyCanExecuteChanged();
        _showImageResultErrorDetailsCommand.NotifyCanExecuteChanged();
        _showVideoResultErrorDetailsCommand.NotifyCanExecuteChanged();
        SettingsCommand.NotifyCanExecuteChanged();
    }

    private void LogAcceptedFailure(ErrorPresentation? presentation)
    {
        if (!_disposed && presentation is not null)
        {
            _diagnosticLogger?.Record(presentation.Code, presentation.Context);
        }
    }

    private RelayCommand CreateErrorDetailsCommand(Func<ErrorPresentation?> currentPresentation) => new(
        () =>
        {
            ErrorPresentation? presentation = currentPresentation();
            if (HasTechnicalDetails(presentation))
            {
                _errorDetailsDialogService.Show(presentation!);
            }
        },
        () => HasTechnicalDetails(currentPresentation()));

    private static bool HasTechnicalDetails(ErrorPresentation? presentation) =>
        !string.IsNullOrWhiteSpace(presentation?.TechnicalDetail);

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        ApplyApplicationSettings(settings, invalidatePreview: true);
        StatusMessage = "Settings saved and applied to the current editor.";
    }

    private void ApplyApplicationSettings(AppSettings settings, bool invalidatePreview)
    {
        AudioExportMode previousAudioMode = _selectedAudioMode;
        CharacterPresetOption preset = _presetProvider.Get(settings.DefaultCharacterPreset);
        bool changed = !Equals(_selectedPreset, preset)
            || !_selectedFont.Equals(settings.DefaultFontFamily, StringComparison.Ordinal)
            || _selectedFontSize != settings.DefaultFontSize
            || !_selectedOutputResolution.Equals(
                AppSettingsCatalog.GetDisplayName(settings.DefaultOutputResolution),
                StringComparison.Ordinal)
            || _selectedSymbolPolarity != settings.DefaultSymbolPolarity;

        _selectedPreset = preset;
        _selectedFont = settings.DefaultFontFamily;
        _selectedFontSize = settings.DefaultFontSize;
        _selectedOutputResolution = AppSettingsCatalog.GetDisplayName(settings.DefaultOutputResolution);
        _selectedSymbolPolarity = settings.DefaultSymbolPolarity;
        _selectedImageOutputFormat = AppSettingsCatalog.GetDisplayName(settings.DefaultImageExportFormat);
        _selectedAudioMode = settings.DefaultVideoAudioMode;
        if (_selectedAudioMode != previousAudioMode)
        {
            _renderWorkflowState = _renderWorkflowState.VideoAudioModeChanged(_selectedAudioMode);
        }

        OnPropertyChanged(nameof(SelectedPreset));
        OnPropertyChanged(nameof(CharacterRamp));
        OnPropertyChanged(nameof(SelectedFont));
        OnPropertyChanged(nameof(SelectedFontSize));
        OnPropertyChanged(nameof(SelectedOutputResolution));
        OnPropertyChanged(nameof(SelectedSymbolPolarity));
        OnPropertyChanged(nameof(SelectedImageOutputFormat));
        OnPropertyChanged(nameof(SelectedAudioMode));
        OnPropertyChanged(nameof(IsKeepAudioSelected));
        OnPropertyChanged(nameof(IsRemoveAudioSelected));

        if (invalidatePreview && changed)
        {
            InvalidateSymbolizedPreview(ImageRenderSetting.ApplicationDefaults);
        }
    }

    private void CancelVideoFrameRequestCore()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _videoFrameCancellation, null);
        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
            Interlocked.Increment(ref _videoFrameRequestId);
        }

        if (IsVideoFrameLoading)
        {
            IsVideoFrameLoading = false;
            NotifyVideoFrameProperties();
        }
    }

    private void ResetVideoFramePresentation()
    {
        CancelVideoFrameRequestCore();
        _resolvedVideoPosition = 0d;
        _resolvedVideoTimestamp = null;
        _resolvedVideoDuration = null;
        _resolvedVideoFrameIndex = null;
        _resolvedVideoFrameIdentity = null;
        _videoFrameError = null;
        _videoFrameTechnicalDetail = null;
        _videoFrameErrorPresentation = null;
        SetTimelinePositionSilently(0d);
        NotifyVideoFrameProperties();
    }

    private void SetTimelinePositionSilently(double position)
    {
        _suppressTimelineSeek = true;
        try
        {
            TimelinePosition = NormalizeVideoPosition(position);
        }
        finally
        {
            _suppressTimelineSeek = false;
        }
    }

    private static double NormalizeVideoPosition(double position) => double.IsFinite(position)
        ? Math.Clamp(position, 0d, 1d)
        : 0d;

    private static TimeSpan? ResolveTimelineTimestamp(double position, TimeSpan? duration) =>
        duration is TimeSpan knownDuration && knownDuration > TimeSpan.Zero
            ? TimeSpan.FromTicks((long)Math.Round(knownDuration.Ticks * NormalizeVideoPosition(position)))
            : null;

    private static string FormatNormalizedPosition(double position) => $"{NormalizeVideoPosition(position):P0}";

    private static string FormatVideoPosition(TimeSpan? timestamp, double position) => timestamp is TimeSpan time
        ? FormatDuration(time)
        : FormatNormalizedPosition(position);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Increment(ref _comparisonGeneration);
        _pendingOriginalComparison = null;
        _comparisonFrameRequestActive = false;
        CancelReviewRecovery();
        _reviewRecoveryCancellation.Dispose();
        CancelOriginalVideoOpen();
        _originalVideoPlayer.StateChanged -= OnOriginalVideoPlayerStateChanged;
        _originalVideoPlayer.Close();
        _renderedVideoPlayer.StateChanged -= OnRenderedVideoPlayerStateChanged;
        _renderedVideoPlayer.Close();
        CancelExportCompletionPresentationCore();
        _exportElapsedTimer.Stop();
        _exportElapsedTimer.Tick -= OnExportElapsedTimerTick;
        _exportStopwatch.Stop();
        _settingsManager.SettingsChanged -= OnSettingsChanged;
        CancellationTokenSource? mediaCancellation = Interlocked.Exchange(ref _mediaLoadCancellation, null);
        mediaCancellation?.Cancel();
        mediaCancellation?.Dispose();
        CancellationTokenSource? videoCancellation = Interlocked.Exchange(ref _videoFrameCancellation, null);
        videoCancellation?.Cancel();
        videoCancellation?.Dispose();
        CancellationTokenSource? imageCancellation = Interlocked.Exchange(ref _imageOperationCancellation, null);
        imageCancellation?.Cancel();
        imageCancellation?.Dispose();
        RenderedImage? rendered = Interlocked.Exchange(ref _renderedImage, null);
        rendered?.Dispose();
        _symbolizedImageSource = null;
        Interlocked.Increment(ref _mediaRequestId);
        Interlocked.Increment(ref _videoFrameRequestId);
        Interlocked.Increment(ref _imageOperationId);
    }

    private static MediaInputErrorCategory MapImageError(ImageProcessingErrorCategory category) => category switch
    {
        ImageProcessingErrorCategory.FileNotFound => MediaInputErrorCategory.FileNotFound,
        ImageProcessingErrorCategory.InvalidPath => MediaInputErrorCategory.InvalidPath,
        ImageProcessingErrorCategory.AccessDenied => MediaInputErrorCategory.AccessDenied,
        ImageProcessingErrorCategory.Cancelled => MediaInputErrorCategory.Cancelled,
        ImageProcessingErrorCategory.UnsupportedDimensions => MediaInputErrorCategory.MetadataUnavailable,
        _ => MediaInputErrorCategory.InvalidImage
    };

    private static string NormalizeOutputExtension(string path, ImageExportFormat format)
    {
        string extension = Path.GetExtension(path);
        bool valid = format == ImageExportFormat.Png
            ? extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            : extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

        if (valid)
        {
            return path;
        }

        return Path.ChangeExtension(path, format == ImageExportFormat.Png ? ".png" : ".jpg");
    }

    private static string NormalizeVideoOutputExtension(string path) =>
        Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.ChangeExtension(path, ".mp4");

    private static bool IsAbove1080p(int width, int height) => height > width
        ? width > 1080 || height > 1920
        : width > 1920 || height > 1080;

    private static string GetSafeFileName(string path)
    {
        try
        {
            return Path.GetFileName(path) is { Length: > 0 } fileName ? fileName : "selected media";
        }
        catch (ArgumentException)
        {
            return "selected media";
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string FormatDimensions(int width, int height) => $"{width} x {height}";

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    private static string BuildImageSecondaryMetadata(ImageMetadata metadata)
    {
        string orientation = FormatOrientation(metadata);
        return metadata.DisplayWidth != metadata.Width || metadata.DisplayHeight != metadata.Height
            ? $"{orientation}  -  Display {FormatDimensions(metadata.DisplayWidth, metadata.DisplayHeight)}"
            : orientation;
    }

    private static string FormatOrientation(ImageMetadata metadata)
    {
        if (!metadata.HasOrientationMetadata)
        {
            return "Orientation not specified";
        }

        return metadata.Orientation switch
        {
            ImageOrientation.Normal => "Orientation: Normal",
            ImageOrientation.MirroredHorizontally => "Orientation: Mirrored horizontally",
            ImageOrientation.Rotated180 => "Orientation: Rotated 180 degrees",
            ImageOrientation.MirroredVertically => "Orientation: Mirrored vertically",
            ImageOrientation.MirroredHorizontallyThenRotated270Clockwise => "Orientation: Mirrored, rotated 270 degrees",
            ImageOrientation.Rotated90Clockwise => "Orientation: Rotated 90 degrees clockwise",
            ImageOrientation.MirroredHorizontallyThenRotated90Clockwise => "Orientation: Mirrored, rotated 90 degrees",
            ImageOrientation.Rotated270Clockwise => "Orientation: Rotated 270 degrees clockwise",
            _ => $"Orientation metadata: {metadata.RawOrientationValue}"
        };
    }

    private static string BuildAudioInformation(VideoMetadata metadata)
    {
        string codec = string.IsNullOrWhiteSpace(metadata.AudioCodec) ? "Audio track" : metadata.AudioCodec;
        return metadata.AudioSampleRate is int sampleRate
            ? $"{codec}, {sampleRate / 1000d:0.###} kHz"
            : codec;
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        private readonly Action<T> _callback = callback ?? throw new ArgumentNullException(nameof(callback));

        public void Report(T value) => _callback(value);
    }

    private readonly record struct VideoFrameLoadResult(
        bool Success,
        long RequestId,
        long MediaVersion,
        string InputPath,
        double RequestedNormalizedPosition,
        DecodedImage? Frame,
        PreviewIdentity? Identity)
    {
        public static VideoFrameLoadResult NotAccepted(
            long requestId,
            long mediaVersion,
            string inputPath,
            double requestedNormalizedPosition) =>
            new(false, requestId, mediaVersion, inputPath, requestedNormalizedPosition, null, null);

        public VideoFrameLoadResult Accept(DecodedImage frame, PreviewIdentity? identity) =>
            identity is null
                ? this
                : this with { Success = true, Frame = frame, Identity = identity };
    }

    private sealed record PendingOriginalComparison(
        long ComparisonGeneration,
        long MediaVersion,
        long OperationId,
        long ViewGeneration,
        string InputPath,
        long OriginalOpenGeneration,
        PlaybackTimestampTransfer Transfer);
}
