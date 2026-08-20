using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media;
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
    private readonly ICharacterPresetProvider _presetProvider;
    private readonly AsyncRelayCommand _browseMediaCommand;
    private readonly AsyncRelayCommand<IReadOnlyList<string>> _dropMediaCommand;
    private readonly RelayCommand _browseOutputCommand;
    private readonly AsyncRelayCommand _previewCommand;
    private readonly AsyncRelayCommand _exportCommand;
    private readonly RelayCommand _cancelExportCommand;
    private readonly RelayCommand _selectOriginalCommand;
    private readonly RelayCommand _selectSymbolizedCommand;
    private readonly RelayCommand _fullscreenCommand;
    private UiPresentationState _presentationState = UiPresentationState.Empty;
    private MediaInputState _mediaInputState = MediaInputState.Empty;
    private ImageWorkflowState _imageWorkflowState = ImageWorkflowState.Empty;
    private CancellationTokenSource? _mediaLoadCancellation;
    private CancellationTokenSource? _videoFrameCancellation;
    private CancellationTokenSource? _imageOperationCancellation;
    private readonly SemaphoreSlim _videoReadGate = new(1, 1);
    private long _mediaRequestId;
    private long _videoFrameRequestId;
    private long _imageOperationId;
    private string? _pendingFilePath;
    private DecodedImage? _decodedImage;
    private RenderedImage? _renderedImage;
    private ImageSource? _originalImageSource;
    private ImageSource? _symbolizedImageSource;
    private string? _selectedOutputPath;
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
    private double _timelinePosition;
    private double _resolvedVideoPosition;
    private TimeSpan? _resolvedVideoTimestamp;
    private TimeSpan? _resolvedVideoDuration;
    private long? _resolvedVideoFrameIndex;
    private double _exportProgress;
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
        ICharacterPresetProvider presetProvider)
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
        _presetProvider = presetProvider ?? throw new ArgumentNullException(nameof(presetProvider));

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
        _previewCommand = new AsyncRelayCommand(GeneratePreviewAsync, CanGeneratePreview);
        _exportCommand = new AsyncRelayCommand(ExportAsync, CanExportMedia);
        _cancelExportCommand = new RelayCommand(CancelActiveImageOperation, () => IsExportActive);
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

    public IAsyncRelayCommand PreviewCommand => _previewCommand;

    public IAsyncRelayCommand ExportCommand => _exportCommand;

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

    public bool IsImageProcessing
    {
        get => _isImageProcessing;
        private set => SetProperty(ref _isImageProcessing, value);
    }

    public bool CanUseMediaActions => CanGeneratePreview();

    public bool HasSymbolizedPreview => _imageWorkflowState.HasSymbolizedPreview
        && _renderedImage is not null
        && _symbolizedImageSource is not null;

    public bool IsSymbolizedPreviewStale => HasSymbolizedPreview && _imageWorkflowState.IsPreviewStale;

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

    public string PreviewActionLabel => _mediaInputState.SelectedMediaType switch
    {
        MediaSelectionKind.Image => "Preview Symbolized Image",
        MediaSelectionKind.Video => "Preview Symbolized Frame",
        _ => "Preview Symbolized"
    };

    public string ExportActionLabel => _mediaInputState.SelectedMediaType switch
    {
        MediaSelectionKind.Image => "Export Image",
        MediaSelectionKind.Video => "Export Video",
        _ => "Export"
    };

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

    public string ExportPercentageText => ExportProgress <= 0 && IsExportActive ? "..." : $"{ExportProgress:0}%";

    public string ElapsedText => "Local processing";

    public string RemainingText => string.Empty;

    public string PreviewStaleMessage => IsSymbolizedPreviewStale
        ? IsVideoSelected
            ? "Frame position or settings changed - generate a new symbolized frame."
            : "Settings changed - generate a new preview before export."
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
                _imageWorkflowState = _imageWorkflowState.OutputFormatChanged();
                _selectedOutputPath = null;
                OnPropertyChanged(nameof(OutputPathText));
                OnPropertyChanged(nameof(OutputFormatText));
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
            _imageWorkflowState = _imageWorkflowState.SelectMedia(
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
            _imageWorkflowState.MediaVersion,
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
        _imageWorkflowState = _imageWorkflowState.SourceChanged();
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
                _imageWorkflowState = _imageWorkflowState.OriginalAvailable(mediaVersion);
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
        && mediaVersion == _imageWorkflowState.MediaVersion
        && IsVideoSelected
        && VideoMetadata?.FilePath.Equals(inputPath, StringComparison.OrdinalIgnoreCase) == true;

    private async Task GeneratePreviewAsync()
    {
        if (_decodedImage is null
            || _imageWorkflowState.CurrentIdentity is not PreviewIdentity identity)
        {
            return;
        }

        string? inputPath = ImageMetadata?.FilePath ?? VideoMetadata?.FilePath;
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            StatusMessage = IsVideoSelected
                ? "The selected video no longer exists. Choose the video again to continue."
                : "The selected image no longer exists. Choose the image again to continue.";
            return;
        }

        DecodedImage source = _decodedImage;
        (long operationId, CancellationToken token) = BeginImageOperation(
            "Generating preview...",
            "Measuring glyphs and rendering symbol art");

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            ImagePipelineResult result = await _imagePipelineService.RenderAsync(
                source,
                CaptureRenderSettings(),
                SelectedPreset,
                token);

            if (!IsCurrentImageOperation(operationId, identity, source))
            {
                return;
            }

            ApplyRenderedResult(result, identity);
            PreviewMode = PreviewMode.Symbolized;
            ExportProgress = 100;
            StatusMessage = result.GlyphMetrics.UsedFallback
                ? $"Symbolized preview generated in {stopwatch.Elapsed.TotalSeconds:0.##} seconds. {result.GlyphMetrics.DiagnosticMessage}"
                : $"Symbolized preview generated in {stopwatch.Elapsed.TotalSeconds:0.##} seconds.";
        }
        catch (OperationCanceledException)
        {
            if (operationId == _imageOperationId)
            {
                StatusMessage = "Symbolized preview generation was cancelled. The original preview remains available.";
            }
        }
        catch (ImageProcessingException exception)
        {
            if (operationId == _imageOperationId)
            {
                StatusMessage = exception.UserMessage;
                PreviewMode = PreviewMode.Original;
            }
        }
        catch (Exception)
        {
            if (operationId == _imageOperationId)
            {
                StatusMessage = "GlyphFrame could not generate the symbolized preview.";
                PreviewMode = PreviewMode.Original;
            }
        }
        finally
        {
            EndImageOperation(operationId);
        }
    }

    private Task ExportAsync() => IsVideoSelected ? ExportVideoAsync() : ExportImageAsync();

    private async Task ExportImageAsync()
    {
        if (_decodedImage is null
            || ImageMetadata is null
            || _imageWorkflowState.CurrentIdentity is not PreviewIdentity identity)
        {
            return;
        }

        if (!File.Exists(ImageMetadata.FilePath))
        {
            StatusMessage = "The selected image no longer exists. Choose the image again to continue.";
            return;
        }

        ImageExportFormat format = ResolveImageExportFormat();
        string? outputPath = _selectedOutputPath ?? SelectOutputPath(format);
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

        DecodedImage source = _decodedImage;
        (long operationId, CancellationToken token) = BeginImageOperation(
            "Exporting image...",
            "Rendering symbol art with the current settings");

        try
        {
            RenderedImage rendered;
            if (_renderedImage is null || IsSymbolizedPreviewStale)
            {
                ImagePipelineResult result = await _imagePipelineService.RenderAsync(
                    source,
                    CaptureRenderSettings(),
                    SelectedPreset,
                    token);

                if (!IsCurrentImageOperation(operationId, identity, source))
                {
                    return;
                }

                ApplyRenderedResult(result, identity);
                rendered = result.RenderedImage;
            }
            else
            {
                rendered = _renderedImage;
            }

            _activeOperationText = $"Encoding {AppSettingsCatalog.GetDisplayName(format)} output";
            OnPropertyChanged(nameof(ExportProgressText));
            await _imageExporter.ExportAsync(rendered, outputPath, format, overwrite, token);

            if (!IsCurrentImageOperation(operationId, identity, source))
            {
                return;
            }

            _selectedOutputPath = outputPath;
            ExportProgress = 100;
            OnPropertyChanged(nameof(OutputPathText));
            StatusMessage = $"Symbolized image exported to {Path.GetFileName(outputPath)}.";
        }
        catch (OperationCanceledException)
        {
            if (operationId == _imageOperationId)
            {
                StatusMessage = "Image export was cancelled. No partial output was kept.";
            }
        }
        catch (ImageProcessingException exception)
        {
            if (operationId == _imageOperationId)
            {
                StatusMessage = exception.UserMessage;
            }
        }
        catch (Exception)
        {
            if (operationId == _imageOperationId)
            {
                StatusMessage = "GlyphFrame could not export the image.";
            }
        }
        finally
        {
            EndImageOperation(operationId);
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
            "Exporting video...",
            "Streaming symbolized frames to a local video-only MP4");
        try
        {
            VideoExportJob job = new(
                metadata.FilePath,
                resolvedOutput,
                metadata,
                CaptureRenderSettings(),
                SelectedPreset,
                overwrite);
            VideoExportResult result = await _videoExportService.ExportAsync(job, token);
            if (operationId != _imageOperationId || !ReferenceEquals(metadata, VideoMetadata))
            {
                return;
            }

            if (result.Success)
            {
                _selectedOutputPath = resolvedOutput;
                ExportProgress = 100;
                OnPropertyChanged(nameof(OutputPathText));
                StatusMessage = $"Symbolized video exported to {Path.GetFileName(resolvedOutput)} without audio.";
            }
            else
            {
                StatusMessage = result.UserMessage;
            }
        }
        catch (Exception)
        {
            if (operationId == _imageOperationId)
            {
                StatusMessage = "GlyphFrame could not export the symbolized video.";
            }
        }
        finally
        {
            EndImageOperation(operationId);
        }
    }

    private void BrowseOutput()
    {
        string? selected = IsVideoSelected
            ? VideoMetadata is null ? null : _imageSaveDialogService.SelectVideoOutputFile(VideoMetadata.FilePath)
            : SelectOutputPath(ResolveImageExportFormat());
        if (!string.IsNullOrWhiteSpace(selected))
        {
            _selectedOutputPath = IsVideoSelected
                ? NormalizeVideoOutputExtension(selected)
                : NormalizeOutputExtension(selected, ResolveImageExportFormat());
            OnPropertyChanged(nameof(OutputPathText));
            StatusMessage = IsVideoSelected
                ? "Video output destination selected. Phase 9 exports video only, without audio."
                : "Image output destination selected.";
        }
    }

    private string? SelectOutputPath(ImageExportFormat format) => ImageMetadata is null
        ? null
        : _imageSaveDialogService.SelectOutputFile(ImageMetadata.FilePath, format);

    private (long OperationId, CancellationToken Token) BeginImageOperation(string title, string text)
    {
        CancelImageOperationCore();
        CancellationTokenSource cancellation = new();
        _imageOperationCancellation = cancellation;
        long operationId = Interlocked.Increment(ref _imageOperationId);
        _imageWorkflowState = _imageWorkflowState.BeginOperation(operationId);
        _activeOperationTitle = title;
        _activeOperationText = text;
        ExportProgress = 0;
        IsImageProcessing = true;
        ApplyPresentationState(_presentationState.BeginExport());
        OnPropertyChanged(nameof(ExportOperationTitle));
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(ExportPercentageText));
        NotifyCommandStates();
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
        _imageWorkflowState = _imageWorkflowState.EndOperation(operationId);
        IsImageProcessing = false;
        ApplyPresentationState(_presentationState.EndExport());
        NotifyCommandStates();
    }

    private void CancelActiveImageOperation()
    {
        _imageOperationCancellation?.Cancel();
        StatusMessage = IsVideoSelected
            ? "Cancelling the current video export..."
            : "Cancelling the current image operation...";
    }

    private void CancelImageOperationCore()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _imageOperationCancellation, null);
        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
            Interlocked.Increment(ref _imageOperationId);
            _imageWorkflowState = _imageWorkflowState.CancelActiveOperation();
        }

        if (IsImageProcessing)
        {
            IsImageProcessing = false;
            ApplyPresentationState(_presentationState.EndExport());
        }
    }

    private bool IsCurrentImageOperation(
        long operationId,
        PreviewIdentity identity,
        DecodedImage source) =>
        operationId == _imageOperationId
        && _imageWorkflowState.CanAcceptResult(operationId, identity)
        && ReferenceEquals(source, _decodedImage);

    private void ApplyRenderedResult(ImagePipelineResult result, PreviewIdentity identity)
    {
        _renderedImage = result.RenderedImage;
        _symbolizedImageSource = _imageSourceFactory.Create(result.RenderedImage);
        _imageWorkflowState = _imageWorkflowState.CompletePreview(
            _imageWorkflowState.ActiveOperationId,
            identity);
        _lastGridSummary = $"{result.Grid.Columns} x {result.Grid.Rows} symbols  -  {result.RenderedImage.Dimensions.Width} x {result.RenderedImage.Dimensions.Height} output";
        NotifyImageProperties();
    }

    private void ClearSymbolizedPreview()
    {
        _renderedImage = null;
        _symbolizedImageSource = null;
        _imageWorkflowState = _imageWorkflowState.ClearPreview();
        _lastGridSummary = string.Empty;
        PreviewMode = PreviewMode.Original;
        NotifyImageProperties();
    }

    private void InvalidateSymbolizedPreview(ImageRenderSetting setting)
    {
        _imageWorkflowState = _imageWorkflowState.RenderingSettingChanged(setting);
        if (HasSymbolizedPreview)
        {
            OnPropertyChanged(nameof(IsSymbolizedPreviewStale));
            OnPropertyChanged(nameof(PreviewStaleMessage));
            StatusMessage = "Render settings changed. The symbolized preview is now stale.";
        }

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

    private bool CanGeneratePreview() =>
        _imageWorkflowState.CanPreview
        && _decodedImage is not null
        && !IsLoadingMedia
        && !IsVideoFrameLoading
        && !IsExportActive;

    private bool CanExportMedia() => IsVideoSelected
        ? VideoMetadata is not null && !IsLoadingMedia && !IsVideoFrameLoading && !IsExportActive
        : IsImageSelected && _imageWorkflowState.CanExport && CanGeneratePreview();

    private bool CanBrowseOutput() => (IsImageSelected || IsVideoSelected)
        && !IsLoadingMedia
        && !IsExportActive;

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
            nameof(AreSettingsEnabled), nameof(InputLabel), nameof(PreviewActionLabel), nameof(ExportActionLabel),
            nameof(SelectedFileName), nameof(SelectedFilePath), nameof(MediaTypeText), nameof(MetadataPrimaryText),
            nameof(MetadataSecondaryText), nameof(FormattedFileSize), nameof(FormattedDimensions),
            nameof(FormattedDuration), nameof(FormattedFps), nameof(FormattedFrameCount),
            nameof(FormattedCodecInformation), nameof(OrientationText), nameof(AudioInformation),
            nameof(InputStatusMessage), nameof(InputMetadataSummary), nameof(TimelineText),
            nameof(VideoFramePositionText), nameof(OutputPathText),
            nameof(OutputFormatText), nameof(IsOriginalPreviewSelected), nameof(IsSymbolizedPreviewSelected),
            nameof(ShowResolutionWarning), nameof(CurrentPreviewImageSource)
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
        _previewCommand.NotifyCanExecuteChanged();
        _exportCommand.NotifyCanExecuteChanged();
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
}
