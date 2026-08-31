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
    private readonly IVideoExportService _videoExportService;
    private readonly IImagePipelineService _imagePipelineService;
    private readonly IImageExporter _imageExporter;
    private readonly IImageSaveDialogService _imageSaveDialogService;
    private readonly IImageSourceFactory _imageSourceFactory;
    private readonly IUserDialogService _userDialogService;
    private readonly IFolderLauncher _folderLauncher;
    private readonly ICharacterPresetProvider _presetProvider;
    private readonly IExportStatusPresentationService _exportStatusPresentationService;
    private readonly SynchronizationContext? _synchronizationContext;
    private readonly DispatcherTimer _exportElapsedTimer;
    private readonly Stopwatch _exportStopwatch = new();
    private readonly AsyncRelayCommand _browseMediaCommand;
    private readonly AsyncRelayCommand<IReadOnlyList<string>> _dropMediaCommand;
    private readonly RelayCommand _browseOutputCommand;
    private readonly AsyncRelayCommand _renderImageCommand;
    private readonly AsyncRelayCommand _previewFrameCommand;
    private readonly AsyncRelayCommand _exportImageCommand;
    private readonly AsyncRelayCommand _exportVideoCommand;
    private readonly AsyncRelayCommand _openExportFolderCommand;
    private readonly RelayCommand _cancelExportCommand;
    private readonly RelayCommand _selectOriginalCommand;
    private readonly RelayCommand _selectSymbolizedCommand;
    private readonly RelayCommand _fullscreenCommand;
    private UiPresentationState _presentationState = UiPresentationState.Empty;
    private MediaInputState _mediaInputState = MediaInputState.Empty;
    private RenderWorkflowState _renderWorkflowState = RenderWorkflowState.Empty;
    private CancellationTokenSource? _mediaLoadCancellation;
    private CancellationTokenSource? _videoFrameCancellation;
    private CancellationTokenSource? _imageOperationCancellation;
    private CancellationTokenSource? _exportCompletionCancellation;
    private readonly SemaphoreSlim _videoReadGate = new(1, 1);
    private long _mediaRequestId;
    private long _videoFrameRequestId;
    private long _imageOperationId;
    private long _exportCompletionPresentationId;
    private long _exportPresentationGeneration;
    private string? _pendingFilePath;
    private DecodedImage? _decodedImage;
    private RenderedImage? _renderedImage;
    private ImageSource? _originalImageSource;
    private ImageSource? _symbolizedImageSource;
    private string? _selectedOutputPath;
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
    private double _timelinePosition;
    private double _resolvedVideoPosition;
    private TimeSpan? _resolvedVideoTimestamp;
    private TimeSpan? _resolvedVideoDuration;
    private long? _resolvedVideoFrameIndex;
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
    private bool _disposed;
    private string? _videoFrameError;
    private string? _videoFrameTechnicalDetail;

    public MainWindowViewModel(
        ApplicationInfo applicationInfo,
        IApplicationSettingsManager settingsManager,
        ISettingsDialogService settingsDialogService,
        IMediaFileDialogService mediaFileDialogService,
        IMediaInputService mediaInputService,
        IVideoFrameReader videoFrameReader,
        IVideoExportService videoExportService,
        IImagePipelineService imagePipelineService,
        IImageExporter imageExporter,
        IImageSaveDialogService imageSaveDialogService,
        IImageSourceFactory imageSourceFactory,
        IUserDialogService userDialogService,
        IFolderLauncher folderLauncher,
        ICharacterPresetProvider presetProvider,
        IExportStatusPresentationService exportStatusPresentationService)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _settingsDialogService = settingsDialogService ?? throw new ArgumentNullException(nameof(settingsDialogService));
        _mediaFileDialogService = mediaFileDialogService ?? throw new ArgumentNullException(nameof(mediaFileDialogService));
        _mediaInputService = mediaInputService ?? throw new ArgumentNullException(nameof(mediaInputService));
        _videoFrameReader = videoFrameReader ?? throw new ArgumentNullException(nameof(videoFrameReader));
        _videoExportService = videoExportService ?? throw new ArgumentNullException(nameof(videoExportService));
        _imagePipelineService = imagePipelineService ?? throw new ArgumentNullException(nameof(imagePipelineService));
        _imageExporter = imageExporter ?? throw new ArgumentNullException(nameof(imageExporter));
        _imageSaveDialogService = imageSaveDialogService ?? throw new ArgumentNullException(nameof(imageSaveDialogService));
        _imageSourceFactory = imageSourceFactory ?? throw new ArgumentNullException(nameof(imageSourceFactory));
        _userDialogService = userDialogService ?? throw new ArgumentNullException(nameof(userDialogService));
        _folderLauncher = folderLauncher ?? throw new ArgumentNullException(nameof(folderLauncher));
        _presetProvider = presetProvider ?? throw new ArgumentNullException(nameof(presetProvider));
        _exportStatusPresentationService = exportStatusPresentationService
            ?? throw new ArgumentNullException(nameof(exportStatusPresentationService));
        _synchronizationContext = SynchronizationContext.Current;
        _exportElapsedTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _exportElapsedTimer.Tick += OnExportElapsedTimerTick;

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
        _browseOutputCommand = new RelayCommand(BrowseOutput, CanBrowseOutput);
        _renderImageCommand = new AsyncRelayCommand(RenderImageAsync, CanRenderImage);
        _previewFrameCommand = new AsyncRelayCommand(PreviewFrameAsync, CanPreviewFrame);
        _exportImageCommand = new AsyncRelayCommand(ExportImageAsync, CanExportImage);
        _exportVideoCommand = new AsyncRelayCommand(ExportVideoAsync, CanExportVideo);
        _openExportFolderCommand = new AsyncRelayCommand(OpenExportFolderAsync, CanOpenExportFolderAction);
        _cancelExportCommand = new RelayCommand(CancelActiveImageOperation, () => CanCancelExport);
        _selectOriginalCommand = new RelayCommand(
            () => PreviewMode = PreviewMode.Original,
            () => IsMediaSelected && !IsLoadingMedia && !IsExportActive);
        _selectSymbolizedCommand = new RelayCommand(
            () => PreviewMode = PreviewMode.Symbolized,
            () => HasSymbolizedPreview && !IsLoadingMedia && !IsExportActive);
        _fullscreenCommand = new RelayCommand(
            () => StatusMessage = "Fullscreen preview is planned for a later phase.",
            () => false);

        AboutCommand = new RelayCommand(
            () => StatusMessage = "GlyphFrame v1.0.0 - local media processing with no telemetry.");
        SettingsCommand = new RelayCommand(
            () => _settingsDialogService.Show(),
            () => AreSettingsEnabled);
    }

    public ApplicationInfo ApplicationInfo { get; }

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

    public IRelayCommand BrowseOutputCommand => _browseOutputCommand;

    public IAsyncRelayCommand RenderImageCommand => _renderImageCommand;

    public IAsyncRelayCommand PreviewFrameCommand => _previewFrameCommand;

    public IAsyncRelayCommand ExportImageCommand => _exportImageCommand;

    public IAsyncRelayCommand ExportVideoCommand => _exportVideoCommand;

    public IAsyncRelayCommand OpenExportFolderCommand => _openExportFolderCommand;

    public IRelayCommand CancelExportCommand => _cancelExportCommand;

    public IRelayCommand SelectOriginalCommand => _selectOriginalCommand;

    public IRelayCommand SelectSymbolizedCommand => _selectSymbolizedCommand;

    public IRelayCommand FullscreenCommand => _fullscreenCommand;

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

    public bool ShowVideoFrameLoading => IsVideoSelected && IsVideoFrameLoading;

    public bool ShowVideoFrameError => IsVideoSelected && HasVideoFrameError && !IsVideoFrameLoading;

    public bool ShowMediaPlaceholder => IsVideoSelected
        && !IsLoadingMedia
        && !HasVideoFrame
        && !IsVideoFrameLoading
        && !HasVideoFrameError;

    public bool ShowImagePreview => IsMediaSelected && CurrentPreviewImageSource is not null && !IsLoadingMedia;

    public bool ShowErrorPreview => IsNoMediaSelected && HasMediaInputError;

    public bool IsBrowseMediaAvailable => !IsExportActive && !IsLoadingMedia;

    public bool IsExportActive => _presentationState.IsExportActive;

    public bool IsExportStatusVisible => IsExportActive || IsExportCompletionVisible;

    public bool IsProcessingResultPanelVisible => IsExportStatusVisible || ShowImageResultPanel;

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

    public bool CanCancelExport => IsExportActive && !IsExportCancellationRequested;

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

    public bool CanUseMediaActions => CanRenderImage() || CanPreviewFrame();

    public RenderWorkflowStatus WorkflowStatus => _renderWorkflowState.Status;

    public bool HasSymbolizedPreview => _renderWorkflowState.HasSymbolizedPreview
        && _renderedImage is not null
        && _symbolizedImageSource is not null;

    public bool IsSymbolizedPreviewStale => HasSymbolizedPreview && _renderWorkflowState.IsPreviewStale;

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

    public bool ShowImageExportControls => HasImageRender;

    public bool ShowImageRenderMetrics => HasImageRender
        && !string.IsNullOrWhiteSpace(_lastGridDimensions)
        && !string.IsNullOrWhiteSpace(_lastOutputDimensions);

    public string ImageGridDimensionsText => _lastGridDimensions;

    public string ImageOutputDimensionsText => _lastOutputDimensions;

    public bool IsImageResultSuccess => _renderWorkflowState.Status is
        RenderWorkflowStatus.RenderReady or RenderWorkflowStatus.Exported;

    public bool IsImageResultWarning => _renderWorkflowState.Status is
        RenderWorkflowStatus.RenderStale or RenderWorkflowStatus.Cancelled;

    public bool IsImageResultFailure => _renderWorkflowState.Status == RenderWorkflowStatus.Failed;

    public bool IsImageExported => _renderWorkflowState.Status == RenderWorkflowStatus.Exported;

    public bool CanOpenExportFolder => IsImageExported
        && !string.IsNullOrWhiteSpace(_lastImageExportPath)
        && !IsExportActive;

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
            => "GlyphFrame could not export the image. The rendered image remains ready to try again.",
        RenderWorkflowStatus.Failed when HasImageRender
            => "GlyphFrame could not render the image. Your previous rendered image is still available.",
        RenderWorkflowStatus.Failed
            => "GlyphFrame could not render this image. You can try again.",
        _ => string.IsNullOrWhiteSpace(_lastGridSummary)
            ? "The symbolized image is ready for review and export."
            : "Ready for review and export."
    };

    public bool ShowOutputCard => IsVideoSelected;

    public bool AreSettingsEnabled => !IsExportActive && !IsLoadingMedia && !IsVideoFrameLoading;

    public bool IsVideoSeekEnabled => IsVideoSelected
        && VideoMetadata is not null
        && !IsLoadingMedia
        && !IsExportActive;

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

    public string ExportActionLabel => IsVideoSelected ? "Export Video" : "Export";

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
        MediaInputStatus.Failed => _mediaInputState.LastFailure?.UserMessage ?? "GlyphFrame could not read this media.",
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

    public string OutputPathText => _selectedOutputPath
        ?? (IsImageSelected
            ? "Choose a PNG or JPEG destination"
            : IsVideoSelected ? "Choose an MP4 destination" : "Available after media selection");

    public string OutputFormatText => _mediaInputState.SelectedMediaType switch
    {
        MediaSelectionKind.Image => SelectedImageOutputFormat,
        MediaSelectionKind.Video => "MP4",
        _ => "Select media"
    };

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
            ? "Frame position or settings changed - generate a new symbolized frame."
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
                OnPropertyChanged(nameof(OutputPathText));
                OnPropertyChanged(nameof(OutputFormatText));
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
                OnPropertyChanged(nameof(IsKeepAudioSelected));
                OnPropertyChanged(nameof(IsRemoveAudioSelected));
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
            if (SetProperty(ref _previewMode, value))
            {
                OnPropertyChanged(nameof(IsOriginalPreviewSelected));
                OnPropertyChanged(nameof(IsSymbolizedPreviewSelected));
                OnPropertyChanged(nameof(CurrentPreviewImageSource));
                OnPropertyChanged(nameof(ShowImagePreview));
            }
        }
    }

    public bool IsOriginalPreviewSelected => IsMediaSelected && PreviewMode == PreviewMode.Original;

    public bool IsSymbolizedPreviewSelected => PreviewMode == PreviewMode.Symbolized;

    public string SelectedFitMode
    {
        get => _selectedFitMode;
        set => SetProperty(ref _selectedFitMode, value);
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
            if (!_suppressTimelineSeek && IsVideoSelected && VideoMetadata is not null)
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
        ApplyMediaInputState(_mediaInputState.BeginLoading(requestId, GetSafeFileName(path)));
        StatusMessage = $"Validating {GetSafeFileName(path)} locally...";

        MediaInputResult result = await _mediaInputService.LoadAsync(path, cancellation.Token);
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
        ApplyMediaInputState(completed);

        if (result.Success)
        {
            ApplyPresentationState(UiPresentationState.ForMedia(result.MediaType));
            PreviewMode = PreviewMode.Original;
            _selectedOutputPath = null;
            _renderWorkflowState = _renderWorkflowState.SelectMedia(
                result.MediaType,
                requestId,
                result.MediaType == MediaSelectionKind.Image && decodedImage is not null);

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
            StatusMessage = result.UserMessage;
        }
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

    private async Task LoadVideoFrameAsync(
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
        IsVideoFrameLoading = true;
        PreviewMode = PreviewMode.Original;
        StatusMessage = $"Loading video frame at {normalizedPosition:P0}...";
        NotifyVideoFrameProperties();
        NotifyImageProperties();

        bool gateEntered = false;
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
                return;
            }

            VideoFrameReadResult result = await _videoFrameReader.ReadAsync(
                inputPath,
                normalizedPosition,
                VideoMetadata,
                cancellation.Token);

            if (!IsCurrentVideoFrameRequest(requestId, mediaVersion, inputPath))
            {
                return;
            }

            if (result.Success && result.Frame is not null)
            {
                _decodedImage = result.Frame;
                _originalImageSource = _imageSourceFactory.Create(result.Frame);
                _renderWorkflowState = _renderWorkflowState.OriginalAvailable(mediaVersion);
                _resolvedVideoPosition = result.ActualNormalizedPosition ?? normalizedPosition;
                _resolvedVideoTimestamp = result.ActualTimestamp
                    ?? ResolveTimelineTimestamp(_resolvedVideoPosition, result.Duration ?? VideoMetadata?.Duration);
                _resolvedVideoDuration = result.Duration ?? VideoMetadata?.Duration;
                _resolvedVideoFrameIndex = result.ActualFrameIndex;
                _videoFrameError = null;
                _videoFrameTechnicalDetail = null;
                SetTimelinePositionSilently(_resolvedVideoPosition);
                StatusMessage = $"Video frame ready at {FormatVideoPosition(_resolvedVideoTimestamp, _resolvedVideoPosition)}.";
                NotifyImageProperties();
            }
            else if (result.Status != VideoFrameReadStatus.Cancelled)
            {
                _videoFrameError = result.UserMessage;
                _videoFrameTechnicalDetail = result.TechnicalDetail;
                SetTimelinePositionSilently(_resolvedVideoPosition);
                StatusMessage = result.UserMessage;
            }
        }
        catch (OperationCanceledException)
        {
            // A superseded seek is an expected state transition and is not presented as an error.
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            if (IsCurrentVideoFrameRequest(requestId, mediaVersion, inputPath))
            {
                _videoFrameError = "GlyphFrame could not load this video frame.";
                _videoFrameTechnicalDetail = exception.ToString();
                SetTimelinePositionSilently(_resolvedVideoPosition);
                StatusMessage = _videoFrameError;
            }
        }
        catch (Exception exception)
        {
            if (IsCurrentVideoFrameRequest(requestId, mediaVersion, inputPath))
            {
                _videoFrameError = "GlyphFrame could not load this video frame.";
                _videoFrameTechnicalDetail = exception.ToString();
                SetTimelinePositionSilently(_resolvedVideoPosition);
                StatusMessage = _videoFrameError;
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
    }

    private bool IsCurrentVideoFrameRequest(long requestId, long mediaVersion, string inputPath) =>
        !_disposed
        && requestId == _videoFrameRequestId
        && mediaVersion == _renderWorkflowState.MediaVersion
        && IsVideoSelected
        && VideoMetadata?.FilePath.Equals(inputPath, StringComparison.OrdinalIgnoreCase) == true;

    private Task RenderImageAsync() => RenderCurrentSourceAsync(RenderWorkflowOperation.RenderImage);

    private Task PreviewFrameAsync() => RenderCurrentSourceAsync(RenderWorkflowOperation.PreviewVideoFrame);

    private async Task RenderCurrentSourceAsync(RenderWorkflowOperation operation)
    {
        bool imageRender = operation == RenderWorkflowOperation.RenderImage;
        if (_decodedImage is null
            || _renderWorkflowState.CurrentIdentity is not PreviewIdentity identity
            || imageRender != IsImageSelected)
        {
            return;
        }

        string? inputPath = imageRender ? ImageMetadata?.FilePath : VideoMetadata?.FilePath;
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            StatusMessage = imageRender
                ? "The selected image no longer exists. Choose the image again to continue."
                : "The selected video no longer exists. Choose the video again to continue.";
            return;
        }

        DecodedImage source = _decodedImage;
        (long operationId, CancellationToken token) = BeginImageOperation(
            operation,
            imageRender ? "Rendering image..." : "Previewing frame...",
            "Measuring glyphs and rendering symbol art");

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

            GlyphMetrics glyphMetrics = result.GlyphMetrics;
            ApplyRenderedResult(result, identity, operation);
            result = null;
            PreviewMode = PreviewMode.Symbolized;
            ExportProgress = 100;
            StatusMessage = imageRender
                ? BuildImageRenderSuccessMessage(stopwatch.Elapsed, _lastGridSummary, glyphMetrics)
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
        {
            result?.RenderedImage.Dispose();
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                StatusMessage = exception.UserMessage;
                if (!HasSymbolizedPreview)
                {
                    PreviewMode = PreviewMode.Original;
                }
            }
        }
        catch (Exception)
        {
            result?.RenderedImage.Dispose();
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                StatusMessage = imageRender
                    ? "GlyphFrame could not render this image."
                    : "GlyphFrame could not generate the symbolized frame.";
                if (!HasSymbolizedPreview)
                {
                    PreviewMode = PreviewMode.Original;
                }
            }
        }
        finally
        {
            EndImageOperation(operationId);
            NotifyImageProperties();
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
            StatusMessage = "The selected image no longer exists. Choose the image again to continue.";
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
            StatusMessage = "Choose a different output file so the source image is not overwritten.";
            return;
        }

        if (destinationRequirement == ImageExportDestinationRequirement.InvalidPath)
        {
            StatusMessage = "The selected output path is not valid.";
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
        {
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                StatusMessage = exception.UserMessage;
            }
        }
        catch (Exception)
        {
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                StatusMessage = "GlyphFrame could not export the image.";
            }
        }
        finally
        {
            EndImageOperation(operationId);
            NotifyImageProperties();
        }
    }

    private async Task ExportVideoAsync()
    {
        if (VideoMetadata is not VideoMetadata metadata)
        {
            return;
        }

        if (!File.Exists(metadata.FilePath))
        {
            StatusMessage = "The selected video no longer exists. Choose the video again to continue.";
            return;
        }

        string? outputPath = _selectedOutputPath ?? _imageSaveDialogService.SelectVideoOutputFile(metadata.FilePath);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        outputPath = NormalizeVideoOutputExtension(outputPath);
        string resolvedInput;
        string resolvedOutput;
        bool showCompletionPresentation = false;
        try
        {
            resolvedInput = Path.GetFullPath(metadata.FilePath);
            resolvedOutput = Path.GetFullPath(outputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            StatusMessage = "The selected video output path is not valid.";
            return;
        }

        if (resolvedInput.Equals(resolvedOutput, StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Choose a different output file so the source video is not overwritten.";
            return;
        }

        bool overwrite = File.Exists(resolvedOutput);
        if (overwrite && !_userDialogService.Confirm(
                "Replace existing video?",
                $"A file named '{Path.GetFileName(resolvedOutput)}' already exists. Replace it with the new symbolized video?",
                "Replace Video",
                "Cancel"))
        {
            StatusMessage = "Export cancelled. The existing file was not changed.";
            return;
        }

        (long operationId, CancellationToken token) = BeginImageOperation(
            RenderWorkflowOperation.ExportVideo,
            "Exporting video...",
            SelectedAudioMode == AudioExportMode.KeepAudio
                ? "Streaming symbolized frames and preserving source audio locally"
                : "Streaming symbolized frames to a local video-only MP4");
        try
        {
            AudioExportMode capturedAudioMode = SelectedAudioMode;
            VideoExportJob job = new(
                metadata.FilePath,
                resolvedOutput,
                metadata,
                CaptureRenderSettings(),
                SelectedPreset,
                overwrite,
                capturedAudioMode);
            CallbackProgress<VideoExportProgress> progress = new(update =>
                DispatchVideoExportProgress(operationId, metadata, update));
            VideoExportResult result = await _videoExportService.ExportAsync(job, token, progress);
            if (operationId != _imageOperationId || !ReferenceEquals(metadata, VideoMetadata))
            {
                return;
            }

            if (result.Success)
            {
                _renderWorkflowState = _renderWorkflowState.CompleteExport(operationId);
                _selectedOutputPath = resolvedOutput;
                ExportStage = VideoExportStage.Completed;
                ExportProgress = 100;
                IsExportProgressIndeterminate = false;
                _activeOperationTitle = "Video export complete";
                _activeOperationText = "Export completed.";
                _exportFrameText = result.FramesWritten > 0
                    ? $"Frame {result.FramesWritten:N0} / {result.FramesWritten:N0}"
                    : string.Empty;
                _remainingText = string.Empty;
                NotifyExportProgressProperties();
                OnPropertyChanged(nameof(ExportOperationTitle));
                OnPropertyChanged(nameof(OutputPathText));
                StatusMessage = $"{result.UserMessage} Output: {Path.GetFileName(resolvedOutput)}";
                showCompletionPresentation = true;
            }
            else
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                StatusMessage = result.UserMessage;
            }
        }
        catch (Exception)
        {
            if (operationId == _imageOperationId)
            {
                _renderWorkflowState = _renderWorkflowState.FailActiveOperation(operationId);
                StatusMessage = "GlyphFrame could not export the symbolized video.";
            }
        }
        finally
        {
            EndImageOperation(operationId);
        }

        if (showCompletionPresentation)
        {
            StartExportCompletionPresentation(operationId);
        }
    }

    private void BrowseOutput()
    {
        string? selected = IsVideoSelected && VideoMetadata is not null
            ? _imageSaveDialogService.SelectVideoOutputFile(VideoMetadata.FilePath)
            : null;
        if (!string.IsNullOrWhiteSpace(selected))
        {
            _selectedOutputPath = NormalizeVideoOutputExtension(selected);
            OnPropertyChanged(nameof(OutputPathText));
            StatusMessage = SelectedAudioMode == AudioExportMode.KeepAudio
                ? "Video output destination selected. Source audio will be preserved when available."
                : "Video output destination selected. The exported video will not contain audio.";
        }
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
        OnPropertyChanged(nameof(ExportOperationTitle));
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(ExportPercentageText));
        OnPropertyChanged(nameof(ExportFrameText));
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(RemainingText));
        NotifyCommandStates();
        NotifyImageProperties();
        return (operationId, cancellation.Token);
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
        IsImageProcessing = false;
        IsExportCancellationRequested = false;
        ApplyPresentationState(_presentationState.EndExport());
        NotifyImageProperties();
        NotifyCommandStates();
    }

    private void CancelActiveImageOperation()
    {
        CancellationTokenSource? cancellation = _imageOperationCancellation;
        if (cancellation is null || IsExportCancellationRequested)
        {
            return;
        }

        IsExportCancellationRequested = true;
        _renderWorkflowState = _renderWorkflowState.BeginCancellation();
        IsExportProgressIndeterminate = true;
        ExportStage = VideoExportStage.Cancelling;
        _activeOperationText = IsVideoSelected
            ? "Cancelling export and cleaning partial files..."
            : "Cancelling the current image operation...";
        _remainingText = string.Empty;
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(RemainingText));
        NotifyImageProperties();
        NotifyCommandStates();
        cancellation.Cancel();
        StatusMessage = IsVideoSelected
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
        _lastImageExportPath = null;
        PreviewMode = PreviewMode.Original;
        NotifyImageProperties();
    }

    private void InvalidateSymbolizedPreview(ImageRenderSetting setting)
    {
        _renderWorkflowState = _renderWorkflowState.RenderingSettingChanged(setting);
        if (HasSymbolizedPreview)
        {
            OnPropertyChanged(nameof(IsSymbolizedPreviewStale));
            OnPropertyChanged(nameof(PreviewStaleMessage));
            StatusMessage = "Render settings changed. The symbolized preview is now stale.";
        }

        NotifyImageProperties();
        NotifyCommandStates();
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
        && _renderWorkflowState.CanPreviewVideoFrame
        && _decodedImage is not null
        && !IsLoadingMedia
        && !IsVideoFrameLoading
        && !IsExportActive;

    private bool CanExportImage() => IsImageSelected
        && _renderWorkflowState.CanExportImage
        && _renderedImage is not null
        && !IsLoadingMedia
        && !IsExportActive;

    private bool CanExportVideo() => IsVideoSelected
        && VideoMetadata is not null
        && !IsLoadingMedia
        && !IsVideoFrameLoading
        && !IsExportActive;

    private bool CanBrowseOutput() => IsVideoSelected
        && !IsLoadingMedia
        && !IsExportActive;

    private bool CanOpenExportFolderAction() => CanOpenExportFolder;

    private async Task OpenExportFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastImageExportPath))
        {
            return;
        }

        string? folder = Path.GetDirectoryName(_lastImageExportPath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusMessage = "GlyphFrame could not resolve the exported image folder.";
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

    private static string BuildImageRenderSuccessMessage(
        TimeSpan elapsed,
        string summary,
        GlyphMetrics glyphMetrics)
    {
        string message = string.IsNullOrWhiteSpace(summary)
            ? $"Image rendered in {elapsed.TotalSeconds:0.##} seconds."
            : $"Image rendered in {elapsed.TotalSeconds:0.##} seconds. {summary}.";
        return glyphMetrics is { UsedFallback: true, DiagnosticMessage.Length: > 0 }
            ? $"{message} {glyphMetrics.DiagnosticMessage}"
            : message;
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
        ApplyMediaInputState(loading.Complete(requestId, failure));
        StatusMessage = failure.UserMessage;
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
            nameof(IsLoadingMedia), nameof(HasMediaInputError), nameof(ShowEmptyPreview), nameof(ShowLoadingPreview),
            nameof(HasVideoFrame), nameof(IsVideoFrameLoading), nameof(HasVideoFrameError),
            nameof(VideoFrameStatusMessage), nameof(VideoFrameTechnicalDetail), nameof(ShowVideoFrameLoading),
            nameof(ShowVideoFrameError), nameof(ShowMediaPlaceholder), nameof(ShowImagePreview),
            nameof(ShowErrorPreview), nameof(IsBrowseMediaAvailable), nameof(IsVideoSeekEnabled),
            nameof(AreSettingsEnabled), nameof(InputLabel), nameof(PreviewFrameActionLabel), nameof(ExportActionLabel),
            nameof(SelectedFileName), nameof(SelectedFilePath), nameof(MediaTypeText), nameof(MetadataPrimaryText),
            nameof(MetadataSecondaryText), nameof(FormattedFileSize), nameof(FormattedDimensions),
            nameof(FormattedDuration), nameof(FormattedFps), nameof(FormattedFrameCount),
            nameof(FormattedCodecInformation), nameof(OrientationText), nameof(AudioInformation),
            nameof(InputStatusMessage), nameof(InputMetadataSummary), nameof(TimelineText),
            nameof(VideoFramePositionText), nameof(OutputPathText),
            nameof(OutputFormatText), nameof(IsOriginalPreviewSelected), nameof(IsSymbolizedPreviewSelected),
            nameof(ShowResolutionWarning), nameof(CurrentPreviewImageSource), nameof(ShowOutputCard),
            nameof(WorkflowStatus), nameof(ShowImageResultPanel), nameof(ShowImageExportControls),
            nameof(IsProcessingResultPanelVisible)
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
        OnPropertyChanged(nameof(ShowImagePreview));
        OnPropertyChanged(nameof(HasVideoFrame));
        OnPropertyChanged(nameof(HasSymbolizedPreview));
        OnPropertyChanged(nameof(IsSymbolizedPreviewStale));
        OnPropertyChanged(nameof(HasImageRender));
        OnPropertyChanged(nameof(IsImageRenderStale));
        OnPropertyChanged(nameof(WorkflowStatus));
        OnPropertyChanged(nameof(ShowImageResultPanel));
        OnPropertyChanged(nameof(ShowImageExportControls));
        OnPropertyChanged(nameof(ShowImageRenderMetrics));
        OnPropertyChanged(nameof(ImageGridDimensionsText));
        OnPropertyChanged(nameof(ImageOutputDimensionsText));
        OnPropertyChanged(nameof(IsProcessingResultPanelVisible));
        OnPropertyChanged(nameof(IsImageResultSuccess));
        OnPropertyChanged(nameof(IsImageResultWarning));
        OnPropertyChanged(nameof(IsImageResultFailure));
        OnPropertyChanged(nameof(IsImageExported));
        OnPropertyChanged(nameof(CanOpenExportFolder));
        OnPropertyChanged(nameof(ImageRenderActionLabel));
        OnPropertyChanged(nameof(ImageExportActionLabel));
        OnPropertyChanged(nameof(ImageResultTitle));
        OnPropertyChanged(nameof(ImageResultSummary));
        OnPropertyChanged(nameof(PreviewStaleMessage));
        OnPropertyChanged(nameof(OutputPathText));
        NotifyCommandStates();
    }

    private void NotifyVideoFrameProperties()
    {
        string[] propertyNames =
        [
            nameof(HasVideoFrame), nameof(IsVideoFrameLoading), nameof(HasVideoFrameError),
            nameof(VideoFrameStatusMessage), nameof(VideoFrameTechnicalDetail),
            nameof(ShowVideoFrameLoading), nameof(ShowVideoFrameError), nameof(ShowMediaPlaceholder),
            nameof(ShowImagePreview), nameof(IsVideoSeekEnabled), nameof(AreSettingsEnabled),
            nameof(InputStatusMessage), nameof(TimelineText), nameof(VideoFramePositionText),
            nameof(CurrentPreviewImageSource)
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
        _browseOutputCommand.NotifyCanExecuteChanged();
        _renderImageCommand.NotifyCanExecuteChanged();
        _previewFrameCommand.NotifyCanExecuteChanged();
        _exportImageCommand.NotifyCanExecuteChanged();
        _exportVideoCommand.NotifyCanExecuteChanged();
        _openExportFolderCommand.NotifyCanExecuteChanged();
        _cancelExportCommand.NotifyCanExecuteChanged();
        _selectOriginalCommand.NotifyCanExecuteChanged();
        _selectSymbolizedCommand.NotifyCanExecuteChanged();
        _fullscreenCommand.NotifyCanExecuteChanged();
        SettingsCommand.NotifyCanExecuteChanged();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        ApplyApplicationSettings(settings, invalidatePreview: true);
        StatusMessage = "Settings saved and applied to the current editor.";
    }

    private void ApplyApplicationSettings(AppSettings settings, bool invalidatePreview)
    {
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
        _videoFrameError = null;
        _videoFrameTechnicalDetail = null;
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
}
