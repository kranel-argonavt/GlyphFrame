using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IApplicationSettingsManager _settingsManager;
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly RelayCommand _browseMediaCommand;
    private readonly RelayCommand _browseOutputCommand;
    private readonly RelayCommand _previewCommand;
    private readonly RelayCommand _exportCommand;
    private readonly RelayCommand _cancelExportCommand;
    private readonly RelayCommand _selectOriginalCommand;
    private readonly RelayCommand _selectSymbolizedCommand;
    private UiPresentationState _presentationState = UiPresentationState.Empty;
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
        ISettingsDialogService settingsDialogService)
    {
        ArgumentNullException.ThrowIfNull(applicationInfo);
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _settingsDialogService = settingsDialogService ?? throw new ArgumentNullException(nameof(settingsDialogService));

        ApplicationInfo = applicationInfo;
        ApplyApplicationSettings(_settingsManager.Current);
        _settingsManager.SettingsChanged += OnSettingsChanged;

        if (!string.IsNullOrWhiteSpace(_settingsManager.StartupStatusMessage))
        {
            _statusMessage = _settingsManager.StartupStatusMessage;
        }

        _browseMediaCommand = new RelayCommand(
            () => StatusMessage = "Media browsing will be implemented in Phase 5.",
            () => !IsExportActive);
        _browseOutputCommand = new RelayCommand(
            () => StatusMessage = "Output selection will be implemented with export functionality.",
            () => IsMediaSelected && !IsExportActive);
        _previewCommand = new RelayCommand(
            () => StatusMessage = "Symbolized preview generation is not implemented in Phase 4.",
            () => CanUseMediaActions);
        _exportCommand = new RelayCommand(
            () => StatusMessage = "Media export is not implemented in Phase 4.",
            () => CanUseMediaActions);
        _cancelExportCommand = new RelayCommand(CancelPresentationExport, () => IsExportActive);
        _selectOriginalCommand = new RelayCommand(
            () => PreviewMode = PreviewMode.Original,
            () => CanUseMediaActions);
        _selectSymbolizedCommand = new RelayCommand(
            () => PreviewMode = PreviewMode.Symbolized,
            () => HasSymbolizedPreview && !IsExportActive);

        FullscreenCommand = new RelayCommand(
            () => StatusMessage = "Fullscreen preview is planned for a later phase.",
            () => IsMediaSelected && !IsExportActive);
        AboutCommand = new RelayCommand(
            () => StatusMessage = "GlyphFrame v1.0.0 - local media processing with no telemetry.");
        SettingsCommand = new RelayCommand(() => _settingsDialogService.Show());
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

    public IRelayCommand BrowseMediaCommand => _browseMediaCommand;

    public IRelayCommand BrowseOutputCommand => _browseOutputCommand;

    public IRelayCommand PreviewCommand => _previewCommand;

    public IRelayCommand ExportCommand => _exportCommand;

    public IRelayCommand CancelExportCommand => _cancelExportCommand;

    public IRelayCommand SelectOriginalCommand => _selectOriginalCommand;

    public IRelayCommand SelectSymbolizedCommand => _selectSymbolizedCommand;

    public IRelayCommand FullscreenCommand { get; }

    public IRelayCommand AboutCommand { get; }

    public IRelayCommand SettingsCommand { get; }

    public UiPresentationState PresentationState => _presentationState;

    public bool IsNoMediaSelected => _presentationState.IsNoMediaSelected;

    public bool IsMediaSelected => _presentationState.IsMediaSelected;

    public bool IsImageSelected => _presentationState.IsImageSelected;

    public bool IsVideoSelected => _presentationState.IsVideoSelected;

    public bool IsExportActive => _presentationState.IsExportActive;

    public bool CanUseMediaActions => _presentationState.CanUseMediaActions;

    public bool HasSymbolizedPreview => false;

    public bool AreSettingsEnabled => !IsExportActive;

    public string InputLabel => _presentationState.InputLabel;

    public string PreviewActionLabel => _presentationState.PreviewActionLabel;

    public string ExportActionLabel => _presentationState.ExportActionLabel;

    public string SelectedFileName => IsMediaSelected ? "Selected media" : "No media selected";

    public string SelectedFilePath => IsMediaSelected
        ? "Media path will appear here"
        : "Choose an image or video";

    public string MediaTypeText => _presentationState.MediaType switch
    {
        MediaSelectionKind.Image => "Image",
        MediaSelectionKind.Video => "Video",
        _ => "Not selected"
    };

    public string MetadataPrimaryText => _presentationState.MediaType switch
    {
        MediaSelectionKind.Image => "Dimensions  -  Format  -  File size",
        MediaSelectionKind.Video => "Resolution  -  Frame rate  -  Duration",
        _ => "Resolution  -  Duration  -  Frame rate"
    };

    public string MetadataSecondaryText => _presentationState.MediaType switch
    {
        MediaSelectionKind.Image => "Image metadata will appear after media selection",
        MediaSelectionKind.Video => "Video metadata will appear after media selection",
        _ => "No media metadata available"
    };

    public string TimelineText => IsVideoSelected ? "00:00 / 00:00" : "--:-- / --:--";

    public string OutputPathText => IsMediaSelected
        ? "Choose an output location"
        : "Available after media selection";

    public string OutputFormatText => _presentationState.MediaType switch
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

    public void ApplyPresentationState(UiPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!SetProperty(ref _presentationState, state, nameof(PresentationState)))
        {
            return;
        }

        if (state.IsNoMediaSelected)
        {
            PreviewMode = PreviewMode.Original;
        }

        OnPropertyChanged(nameof(IsNoMediaSelected));
        OnPropertyChanged(nameof(IsMediaSelected));
        OnPropertyChanged(nameof(IsImageSelected));
        OnPropertyChanged(nameof(IsVideoSelected));
        OnPropertyChanged(nameof(IsExportActive));
        OnPropertyChanged(nameof(CanUseMediaActions));
        OnPropertyChanged(nameof(IsOriginalPreviewSelected));
        OnPropertyChanged(nameof(IsSymbolizedPreviewSelected));
        OnPropertyChanged(nameof(AreSettingsEnabled));
        OnPropertyChanged(nameof(InputLabel));
        OnPropertyChanged(nameof(PreviewActionLabel));
        OnPropertyChanged(nameof(ExportActionLabel));
        OnPropertyChanged(nameof(SelectedFileName));
        OnPropertyChanged(nameof(SelectedFilePath));
        OnPropertyChanged(nameof(MediaTypeText));
        OnPropertyChanged(nameof(MetadataPrimaryText));
        OnPropertyChanged(nameof(MetadataSecondaryText));
        OnPropertyChanged(nameof(TimelineText));
        OnPropertyChanged(nameof(OutputPathText));
        OnPropertyChanged(nameof(OutputFormatText));
        OnPropertyChanged(nameof(ExportOperationTitle));
        OnPropertyChanged(nameof(ExportProgressText));

        NotifyCommandStates();
    }

    private void CancelPresentationExport()
    {
        ApplyPresentationState(_presentationState.EndExport());
        StatusMessage = "Export presentation state cancelled. No media work was performed.";
    }

    private void NotifyCommandStates()
    {
        _browseMediaCommand.NotifyCanExecuteChanged();
        _browseOutputCommand.NotifyCanExecuteChanged();
        _previewCommand.NotifyCanExecuteChanged();
        _exportCommand.NotifyCanExecuteChanged();
        _cancelExportCommand.NotifyCanExecuteChanged();
        _selectOriginalCommand.NotifyCanExecuteChanged();
        _selectSymbolizedCommand.NotifyCanExecuteChanged();
        FullscreenCommand.NotifyCanExecuteChanged();
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
}
