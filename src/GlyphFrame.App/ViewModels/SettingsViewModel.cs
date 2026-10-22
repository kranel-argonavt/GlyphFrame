using System.IO;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlyphFrame.App.Services;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IApplicationSettingsManager _settingsManager;
    private readonly IApplicationPaths _paths;
    private readonly IAccentThemeService _themeService;
    private readonly IFolderLauncher _folderLauncher;
    private readonly IUserDialogService _dialogService;
    private readonly IFfmpegValidationService _ffmpegValidationService;
    private readonly IErrorPresentationService _errorPresentationService;
    private readonly IErrorDetailsDialogService _errorDetailsDialogService;
    private AccentPreset _selectedAccentPreset;
    private CharacterPresetOption _selectedCharacterPreset = CharacterPresetOption.Default;
    private string _selectedFontFamily = AppSettingsCatalog.Defaults.DefaultFontFamily;
    private int _selectedFontSize = AppSettingsCatalog.Defaults.DefaultFontSize;
    private OutputResolution _selectedOutputResolution = AppSettingsCatalog.Defaults.DefaultOutputResolution;
    private SymbolPolarity _selectedSymbolPolarity = AppSettingsCatalog.Defaults.DefaultSymbolPolarity;
    private ImageExportFormat _selectedImageExportFormat = AppSettingsCatalog.Defaults.DefaultImageExportFormat;
    private AudioExportMode _selectedVideoAudioMode = AppSettingsCatalog.Defaults.DefaultVideoAudioMode;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isInitializing;
    private bool _isFfmpegChecking;
    private string _ffmpegAvailability = "Checking";
    private string _ffmpegStatusMessage = "Checking the bundled FFmpeg tools…";
    private string _ffmpegVersion = "Unknown";
    private string _ffprobeVersion = "Unknown";
    private string _bundledToolsDirectory = "Resolving bundled tools path…";
    private string _ffmpegTechnicalDetails = "Validation has not completed yet.";
    private ErrorPresentation? _ffmpegErrorPresentation;

    public SettingsViewModel(
        IApplicationSettingsManager settingsManager,
        IApplicationPaths paths,
        IAccentThemeService themeService,
        IFolderLauncher folderLauncher,
        IUserDialogService dialogService,
        IFfmpegValidationService ffmpegValidationService,
        IErrorPresentationService errorPresentationService,
        IErrorDetailsDialogService errorDetailsDialogService)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _folderLauncher = folderLauncher ?? throw new ArgumentNullException(nameof(folderLauncher));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _ffmpegValidationService = ffmpegValidationService ?? throw new ArgumentNullException(nameof(ffmpegValidationService));
        _errorPresentationService = errorPresentationService
            ?? throw new ArgumentNullException(nameof(errorPresentationService));
        _errorDetailsDialogService = errorDetailsDialogService
            ?? throw new ArgumentNullException(nameof(errorDetailsDialogService));

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => !IsBusy);
        ResetCommand = new AsyncRelayCommand(ResetAsync, () => !IsBusy);
        OpenLogsFolderCommand = new AsyncRelayCommand(OpenLogsFolderAsync, () => !IsBusy);
        RefreshFfmpegCommand = new AsyncRelayCommand(
            () => RefreshFfmpegStatusAsync(forceRefresh: true),
            () => !IsFfmpegChecking);
        ShowFfmpegErrorDetailsCommand = new RelayCommand(
            OpenFfmpegErrorDetails,
            () => ShowFfmpegErrorDetails);
    }

    public event EventHandler? RequestClose;

    public IReadOnlyList<SettingOption<AccentPreset>> AccentPresets => AppSettingsCatalog.AccentPresets;

    public IReadOnlyList<CharacterPresetOption> CharacterPresets => AppSettingsCatalog.CharacterPresets;

    public IReadOnlyList<string> FontFamilies => AppSettingsCatalog.FontFamilies;

    public IReadOnlyList<int> FontSizes => AppSettingsCatalog.FontSizes;

    public IReadOnlyList<SettingOption<OutputResolution>> OutputResolutions => AppSettingsCatalog.OutputResolutions;

    public IReadOnlyList<SettingOption<SymbolPolarity>> SymbolPolarities => AppSettingsCatalog.SymbolPolarities;

    public IReadOnlyList<SettingOption<ImageExportFormat>> ImageExportFormats => AppSettingsCatalog.ImageExportFormats;

    public IReadOnlyList<SettingOption<AudioExportMode>> AudioModes => AppSettingsCatalog.AudioModes;

    public string ApplicationDataDirectory => _paths.ApplicationDataDirectory;

    public string LogsDirectory => _paths.LogsDirectory;

    public string FfmpegAvailability
    {
        get => _ffmpegAvailability;
        private set => SetProperty(ref _ffmpegAvailability, value);
    }

    public string FfmpegStatusMessage
    {
        get => _ffmpegStatusMessage;
        private set => SetProperty(ref _ffmpegStatusMessage, value);
    }

    public string FfmpegVersion
    {
        get => _ffmpegVersion;
        private set => SetProperty(ref _ffmpegVersion, value);
    }

    public string FfprobeVersion
    {
        get => _ffprobeVersion;
        private set => SetProperty(ref _ffprobeVersion, value);
    }

    public string BundledToolsDirectory
    {
        get => _bundledToolsDirectory;
        private set => SetProperty(ref _bundledToolsDirectory, value);
    }

    public string FfmpegTechnicalDetails
    {
        get => _ffmpegTechnicalDetails;
        private set => SetProperty(ref _ffmpegTechnicalDetails, value);
    }

    public ErrorPresentation? FfmpegErrorPresentation
    {
        get => _ffmpegErrorPresentation;
        private set
        {
            if (SetProperty(ref _ffmpegErrorPresentation, value))
            {
                OnPropertyChanged(nameof(ShowFfmpegErrorDetails));
                ShowFfmpegErrorDetailsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool ShowFfmpegErrorDetails => !string.IsNullOrWhiteSpace(FfmpegErrorPresentation?.TechnicalDetail);

    public bool IsFfmpegChecking
    {
        get => _isFfmpegChecking;
        private set
        {
            if (SetProperty(ref _isFfmpegChecking, value))
            {
                RefreshFfmpegCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand ResetCommand { get; }

    public IAsyncRelayCommand OpenLogsFolderCommand { get; }

    public IAsyncRelayCommand RefreshFfmpegCommand { get; }

    public IRelayCommand ShowFfmpegErrorDetailsCommand { get; }

    public AccentPreset SelectedAccentPreset
    {
        get => _selectedAccentPreset;
        set
        {
            if (SetProperty(ref _selectedAccentPreset, value))
            {
                OnPropertyChanged(nameof(AccentPreviewColor));
                if (!_isInitializing)
                {
                    _themeService.Apply(value);
                }
            }
        }
    }

    public string AccentPreviewColor => _themeService.GetPrimaryColorHex(SelectedAccentPreset);

    public CharacterPresetOption SelectedCharacterPreset
    {
        get => _selectedCharacterPreset;
        set
        {
            if (value is not null)
            {
                SetProperty(ref _selectedCharacterPreset, value);
            }
        }
    }

    public string SelectedFontFamily
    {
        get => _selectedFontFamily;
        set => SetProperty(ref _selectedFontFamily, value);
    }

    public int SelectedFontSize
    {
        get => _selectedFontSize;
        set => SetProperty(ref _selectedFontSize, value);
    }

    public OutputResolution SelectedOutputResolution
    {
        get => _selectedOutputResolution;
        set => SetProperty(ref _selectedOutputResolution, value);
    }

    public SymbolPolarity SelectedSymbolPolarity
    {
        get => _selectedSymbolPolarity;
        set => SetProperty(ref _selectedSymbolPolarity, value);
    }

    public ImageExportFormat SelectedImageExportFormat
    {
        get => _selectedImageExportFormat;
        set => SetProperty(ref _selectedImageExportFormat, value);
    }

    public AudioExportMode SelectedVideoAudioMode
    {
        get => _selectedVideoAudioMode;
        set => SetProperty(ref _selectedVideoAudioMode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value)) { OnPropertyChanged(nameof(HasStatusMessage)); }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyCommandStates();
            }
        }
    }

    public void BeginEdit()
    {
        LoadDraft(_settingsManager.Current);
        StatusMessage = string.Empty;
        if (!IsFfmpegChecking)
        {
            _ = RefreshFfmpegStatusAsync(forceRefresh: false);
        }
    }

    public void EndEdit() => LoadDraft(_settingsManager.Current);

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await _settingsManager.SaveAsync(CreateDraft());
            StatusMessage = "Settings saved and applied.";
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            StatusMessage = "Settings could not be saved. Check access to the application-data folder and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Cancel()
    {
        EndEdit();
        StatusMessage = "Unsaved changes were discarded.";
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private Task ResetAsync()
    {
        bool confirmed = _dialogService.Confirm(
            "Reset draft to defaults",
            "Load default settings into this draft? Select Save & Close to apply. Logs and media files will not be changed.",
            "Reset to defaults");
        if (!confirmed)
        {
            return Task.CompletedTask;
        }

        LoadDraft(AppSettingsCatalog.Defaults);
        StatusMessage = "Defaults loaded. Select Save & Close to apply.";
        return Task.CompletedTask;
    }

    private async Task OpenLogsFolderAsync()
    {
        IsBusy = true;
        try
        {
            _paths.EnsureDirectories();
            await _folderLauncher.OpenFolderAsync(_paths.LogsDirectory);
            StatusMessage = "Opened the GlyphFrame logs folder.";
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception) || exception is System.ComponentModel.Win32Exception)
        {
            StatusMessage = "The logs folder could not be opened. You can copy its path from this window.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private AppSettings CreateDraft() => new()
    {
        AccentPreset = SelectedAccentPreset,
        DefaultCharacterPreset = SelectedCharacterPreset.Kind,
        DefaultFontFamily = SelectedFontFamily,
        DefaultFontSize = SelectedFontSize,
        DefaultOutputResolution = SelectedOutputResolution,
        DefaultSymbolPolarity = SelectedSymbolPolarity,
        DefaultImageExportFormat = SelectedImageExportFormat,
        DefaultVideoAudioMode = SelectedVideoAudioMode
    };

    private void LoadDraft(AppSettings settings)
    {
        _isInitializing = true;
        try
        {
            SelectedAccentPreset = settings.AccentPreset;
            SelectedCharacterPreset = CharacterPresetOption.BuiltIn.First(
                option => option.Kind == settings.DefaultCharacterPreset);
            SelectedFontFamily = settings.DefaultFontFamily;
            SelectedFontSize = settings.DefaultFontSize;
            SelectedOutputResolution = settings.DefaultOutputResolution;
            SelectedSymbolPolarity = settings.DefaultSymbolPolarity;
            SelectedImageExportFormat = settings.DefaultImageExportFormat;
            SelectedVideoAudioMode = settings.DefaultVideoAudioMode;
        }
        finally
        {
            _isInitializing = false;
        }

        _themeService.Apply(settings.AccentPreset);
    }

    private void NotifyCommandStates()
    {
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
        OpenLogsFolderCommand.NotifyCanExecuteChanged();
        ShowFfmpegErrorDetailsCommand.NotifyCanExecuteChanged();
    }

    private void OpenFfmpegErrorDetails()
    {
        ErrorPresentation? presentation = FfmpegErrorPresentation;
        if (!string.IsNullOrWhiteSpace(presentation?.TechnicalDetail))
        {
            _errorDetailsDialogService.Show(presentation);
        }
    }

    private async Task RefreshFfmpegStatusAsync(bool forceRefresh)
    {
        if (IsFfmpegChecking)
        {
            return;
        }

        SetFfmpegCheckingState();
        try
        {
            FfmpegValidationResult result = await _ffmpegValidationService.ValidateAsync(forceRefresh);
            FfmpegStatusPresentation presentation = FfmpegStatusPresentation.FromValidation(result);
            FfmpegAvailability = presentation.Availability;
            FfmpegToolValidationResult? failedTool = new[] { result.Ffmpeg, result.Ffprobe }
                .FirstOrDefault(tool => !tool.IsAvailable);
            ErrorPresentation? error = failedTool is null
                ? null
                : _errorPresentationService.From(failedTool);
            FfmpegErrorPresentation = error is null
                ? null
                : ErrorPresentation.Create(
                    error.Code,
                    error.Context,
                    presentation.TechnicalDetails);
            FfmpegStatusMessage = FfmpegErrorPresentation?.UserMessage ?? presentation.Message;
            FfmpegVersion = presentation.FfmpegVersion;
            FfprobeVersion = presentation.FfprobeVersion;
            BundledToolsDirectory = presentation.ToolsDirectory;
            FfmpegTechnicalDetails = presentation.TechnicalDetails;
        }
        catch (OperationCanceledException)
        {
            FfmpegErrorPresentation = null;
            SetFfmpegUnavailableState(
                "FFmpeg validation was cancelled. Select Refresh to try again.",
                "Validation was cancelled before it completed.");
        }
        catch (Exception exception) when (_errorPresentationService.IsRecoverable(
            ErrorPresentationContext.ToolValidation,
            exception))
        {
            FfmpegErrorPresentation = _errorPresentationService.FromException(
                ErrorPresentationContext.ToolValidation,
                exception);
            SetFfmpegUnavailableState(
                FfmpegErrorPresentation?.UserMessage
                    ?? "GlyphFrame could not validate the bundled video tools.",
                FfmpegErrorPresentation?.TechnicalDetail ?? exception.ToString());
        }
        finally
        {
            IsFfmpegChecking = false;
        }
    }

    private void SetFfmpegCheckingState()
    {
        IsFfmpegChecking = true;
        FfmpegAvailability = "Checking";
        FfmpegStatusMessage = "Checking the bundled FFmpeg tools…";
        FfmpegVersion = "Checking";
        FfprobeVersion = "Checking";
        BundledToolsDirectory = "Resolving bundled tools path…";
        FfmpegTechnicalDetails = "Validation is in progress.";
        FfmpegErrorPresentation = null;
    }

    private void SetFfmpegUnavailableState(string message, string technicalDetails)
    {
        FfmpegAvailability = "Unavailable";
        FfmpegStatusMessage = message;
        FfmpegVersion = "Unavailable";
        FfprobeVersion = "Unavailable";
        BundledToolsDirectory = "Unavailable";
        FfmpegTechnicalDetails = technicalDetails;
    }

    private static bool IsExpectedStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;
}
