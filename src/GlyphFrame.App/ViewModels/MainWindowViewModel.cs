using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IApplicationSettingsManager _settingsManager;
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly IMediaFileDialogService _mediaFileDialogService;
    private readonly IMediaInputService _mediaInputService;
    private readonly AsyncRelayCommand _browseMediaCommand;
    private readonly AsyncRelayCommand<IReadOnlyList<string>> _dropMediaCommand;
    private readonly RelayCommand _browseOutputCommand;
    private readonly RelayCommand _previewCommand;
    private readonly RelayCommand _exportCommand;
    private readonly RelayCommand _cancelExportCommand;
    private readonly RelayCommand _selectOriginalCommand;
    private readonly RelayCommand _selectSymbolizedCommand;
    private readonly RelayCommand _fullscreenCommand;
    private UiPresentationState _presentationState = UiPresentationState.Empty;
    private MediaInputState _mediaInputState = MediaInputState.Empty;
    private CancellationTokenSource? _mediaLoadCancellation;
    private long _mediaRequestId;
    private string? _pendingFilePath;
    private CharacterPresetOption _selectedPreset = CharacterPresetOption.Default;
    private string _selectedFont = "Cascadia Mono";
    private int _selectedFontSize = 10;
    private string _selectedOutputResolution = "Same as source";
    private string _selectedImageOutputFormat = "PNG";
    private AudioExportMode _selectedAudioMode = AudioExportMode.KeepAudio;
    private PreviewMode _previewMode = PreviewMode.Original;
    private string _selectedFitMode = "Fit";
    private string _statusMessage = "Ready - select an image or video to begin.";
    private double _timelinePosition;
    private double _exportProgress;

    public MainWindowViewModel(
        ApplicationInfo applicationInfo,
        IApplicationSettingsManager settingsManager,
        ISettingsDialogService settingsDialogService,
        IMediaFileDialogService mediaFileDialogService,
        IMediaInputService mediaInputService)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _settingsDialogService = settingsDialogService ?? throw new ArgumentNullException(nameof(settingsDialogService));
        _mediaFileDialogService = mediaFileDialogService ?? throw new ArgumentNullException(nameof(mediaFileDialogService));
        _mediaInputService = mediaInputService ?? throw new ArgumentNullException(nameof(mediaInputService));

        ApplicationInfo = applicationInfo;
        ApplyApplicationSettings(_settingsManager.Current);
        _settingsManager.SettingsChanged += OnSettingsChanged;

        if (!string.IsNullOrWhiteSpace(_settingsManager.StartupStatusMessage))
        {
            _statusMessage = _settingsManager.StartupStatusMessage;
        }

        _browseMediaCommand = new AsyncRelayCommand(
            BrowseMediaAsync,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        _dropMediaCommand = new AsyncRelayCommand<IReadOnlyList<string>>(
            HandleDroppedPathsAsync,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        _browseOutputCommand = new RelayCommand(
            () => StatusMessage = "Output selection will be implemented with export functionality.",
            () => false);
        _previewCommand = new RelayCommand(
            () => StatusMessage = "Symbolized preview generation begins in the image and video preview phases.",
            () => false);
        _exportCommand = new RelayCommand(
            () => StatusMessage = "Media export is not implemented in Phase 5.",
            () => false);
        _cancelExportCommand = new RelayCommand(CancelPresentationExport, () => IsExportActive);
        _selectOriginalCommand = new RelayCommand(
            () => PreviewMode = PreviewMode.Original,
            () => IsMediaSelected && !IsLoadingMedia && !IsExportActive);
        _selectSymbolizedCommand = new RelayCommand(
            () => PreviewMode = PreviewMode.Symbolized,
            () => false);
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

    public IReadOnlyList<CharacterPresetOption> Presets => CharacterPresetOption.BuiltIn;

    public IReadOnlyList<string> FontFamilies => AppSettingsCatalog.FontFamilies;

    public IReadOnlyList<int> FontSizes => AppSettingsCatalog.FontSizes;

    public IReadOnlyList<string> OutputResolutions { get; } =
        AppSettingsCatalog.OutputResolutions.Select(option => option.DisplayName).ToArray();

    public IReadOnlyList<string> ImageOutputFormats { get; } =
        AppSettingsCatalog.ImageExportFormats.Select(option => option.DisplayName).ToArray();

    public IReadOnlyList<string> ColorModes { get; } = ["Original Colors"];

    public IReadOnlyList<string> FitModes { get; } = ["Fit", "Fill", "100%"];

    public IAsyncRelayCommand BrowseMediaCommand => _browseMediaCommand;

    public IAsyncRelayCommand<IReadOnlyList<string>> DropMediaCommand => _dropMediaCommand;

    public IRelayCommand BrowseOutputCommand => _browseOutputCommand;

    public IRelayCommand PreviewCommand => _previewCommand;

    public IRelayCommand ExportCommand => _exportCommand;

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

    public bool ShowMediaPlaceholder => IsMediaSelected && !IsLoadingMedia;

    public bool ShowErrorPreview => IsNoMediaSelected && HasMediaInputError;

    public bool IsBrowseMediaAvailable => !IsExportActive;

    public bool IsExportActive => _presentationState.IsExportActive;

    public bool CanUseMediaActions => false;

    public bool HasSymbolizedPreview => false;

    public bool AreSettingsEnabled => !IsExportActive && !IsLoadingMedia;

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
        ? $"{FormatDimensions(ImageMetadata.Width, ImageMetadata.Height)}  -  {ImageMetadata.Format}  -  {FormatFileSize(ImageMetadata.FileSizeBytes)}"
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
        ? FormatDimensions(ImageMetadata.Width, ImageMetadata.Height)
        : VideoMetadata?.Width is int width && VideoMetadata.Height is int height
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
        MediaInputStatus.Loading => "Reading media metadata locally...",
        MediaInputStatus.ImageSelected => "Image metadata loaded. Pixel preview begins in Phase 6.",
        MediaInputStatus.VideoSelected => "Video metadata loaded. Frame preview begins in Phase 8.",
        MediaInputStatus.Failed => _mediaInputState.LastFailure?.UserMessage ?? "GlyphFrame could not read this media.",
        _ => "Browse or drop one supported image or video."
    };

    public string InputMetadataSummary => IsMediaSelected
        ? $"{MediaTypeText}  -  {FormattedDimensions}  -  {FormattedFileSize}"
        : InputStatusMessage;

    public string TimelineText => VideoMetadata?.Duration is TimeSpan duration
        ? $"00:00 / {FormatDuration(duration)}"
        : "--:-- / --:--";

    public string OutputPathText => IsMediaSelected
        ? "Output becomes available in a later phase"
        : "Available after media selection";

    public string OutputFormatText => _mediaInputState.SelectedMediaType switch
    {
        MediaSelectionKind.Image => SelectedImageOutputFormat,
        MediaSelectionKind.Video => "MP4",
        _ => "Select media"
    };

    public string ExportOperationTitle => IsVideoSelected ? "Exporting video..." : "Exporting image...";

    public string ExportProgressText => IsVideoSelected
        ? "Processing frame 0 of 0"
        : "Rendering symbolized image";

    public string ExportPercentageText => $"{ExportProgress:0}%";

    public string ElapsedText => "Elapsed  00:00:00";

    public string RemainingText => "Remaining  --:--:--";

    public CharacterPresetOption SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (value is not null && SetProperty(ref _selectedPreset, value))
            {
                OnPropertyChanged(nameof(CharacterRamp));
            }
        }
    }

    public string CharacterRamp => SelectedPreset.Characters;

    public string SelectedFont
    {
        get => _selectedFont;
        set => SetProperty(ref _selectedFont, value);
    }

    public int SelectedFontSize
    {
        get => _selectedFontSize;
        set => SetProperty(ref _selectedFontSize, value);
    }

    public string SelectedOutputResolution
    {
        get => _selectedOutputResolution;
        set => SetProperty(ref _selectedOutputResolution, value);
    }

    public string SelectedImageOutputFormat
    {
        get => _selectedImageOutputFormat;
        set
        {
            if (SetProperty(ref _selectedImageOutputFormat, value))
            {
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
        set => SetProperty(ref _timelinePosition, value);
    }

    public double ExportProgress
    {
        get => _exportProgress;
        set
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
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _mediaLoadCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();

        long requestId = Interlocked.Increment(ref _mediaRequestId);
        _pendingFilePath = path;
        ApplyMediaInputState(_mediaInputState.BeginLoading(requestId, GetSafeFileName(path)));
        StatusMessage = $"Reading metadata for {GetSafeFileName(path)}...";

        MediaInputResult result = await _mediaInputService.LoadAsync(path, cancellation.Token);
        MediaInputState completed = _mediaInputState.Complete(requestId, result);
        if (ReferenceEquals(completed, _mediaInputState))
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
            StatusMessage = result.MediaType == MediaSelectionKind.Image
                ? "Image metadata loaded locally. Pixel preview is not implemented yet."
                : "Video metadata loaded locally. Frame preview is not implemented yet.";
        }
        else if (result.ErrorCategory != MediaInputErrorCategory.Cancelled)
        {
            StatusMessage = result.UserMessage;
        }
    }

    private void ApplyImmediateFailure(MediaInputResult failure)
    {
        CancellationTokenSource? previous = Interlocked.Exchange(ref _mediaLoadCancellation, null);
        previous?.Cancel();
        previous?.Dispose();

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
        NotifyCommandStates();
    }

    private void NotifyMediaProperties()
    {
        string[] propertyNames =
        [
            nameof(CurrentMedia), nameof(ImageMetadata), nameof(VideoMetadata),
            nameof(IsNoMediaSelected), nameof(IsMediaSelected), nameof(IsImageSelected), nameof(IsVideoSelected),
            nameof(IsLoadingMedia), nameof(HasMediaInputError), nameof(ShowEmptyPreview), nameof(ShowLoadingPreview),
            nameof(ShowMediaPlaceholder), nameof(ShowErrorPreview), nameof(IsBrowseMediaAvailable),
            nameof(AreSettingsEnabled), nameof(InputLabel), nameof(PreviewActionLabel), nameof(ExportActionLabel),
            nameof(SelectedFileName), nameof(SelectedFilePath), nameof(MediaTypeText), nameof(MetadataPrimaryText),
            nameof(MetadataSecondaryText), nameof(FormattedFileSize), nameof(FormattedDimensions),
            nameof(FormattedDuration), nameof(FormattedFps), nameof(FormattedFrameCount),
            nameof(FormattedCodecInformation), nameof(OrientationText), nameof(AudioInformation),
            nameof(InputStatusMessage), nameof(InputMetadataSummary), nameof(TimelineText), nameof(OutputPathText),
            nameof(OutputFormatText), nameof(IsOriginalPreviewSelected), nameof(IsSymbolizedPreviewSelected)
        ];

        foreach (string propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void CancelPresentationExport()
    {
        ApplyPresentationState(_presentationState.EndExport());
        StatusMessage = "Export presentation state cancelled. No media work was performed.";
    }

    private void NotifyCommandStates()
    {
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
        ApplyApplicationSettings(settings);
        StatusMessage = "Settings saved and applied to the current editor.";
    }

    private void ApplyApplicationSettings(AppSettings settings)
    {
        SelectedPreset = CharacterPresetOption.BuiltIn.First(
            option => option.Kind == settings.DefaultCharacterPreset);
        SelectedFont = settings.DefaultFontFamily;
        SelectedFontSize = settings.DefaultFontSize;
        SelectedOutputResolution = AppSettingsCatalog.GetDisplayName(settings.DefaultOutputResolution);
        SelectedImageOutputFormat = AppSettingsCatalog.GetDisplayName(settings.DefaultImageExportFormat);
        SelectedAudioMode = settings.DefaultVideoAudioMode;
    }

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
