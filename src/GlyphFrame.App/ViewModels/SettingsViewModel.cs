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
    private AccentPreset _selectedAccentPreset;
    private CharacterPresetOption _selectedCharacterPreset = CharacterPresetOption.Default;
    private string _selectedFontFamily = AppSettingsCatalog.Defaults.DefaultFontFamily;
    private int _selectedFontSize = AppSettingsCatalog.Defaults.DefaultFontSize;
    private OutputResolution _selectedOutputResolution = AppSettingsCatalog.Defaults.DefaultOutputResolution;
    private ImageExportFormat _selectedImageExportFormat = AppSettingsCatalog.Defaults.DefaultImageExportFormat;
    private AudioExportMode _selectedVideoAudioMode = AppSettingsCatalog.Defaults.DefaultVideoAudioMode;
    private string _statusMessage = "Review the preferences below, then save or cancel.";
    private bool _isBusy;
    private bool _isInitializing;

    public SettingsViewModel(
        IApplicationSettingsManager settingsManager,
        IApplicationPaths paths,
        IAccentThemeService themeService,
        IFolderLauncher folderLauncher,
        IUserDialogService dialogService)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _folderLauncher = folderLauncher ?? throw new ArgumentNullException(nameof(folderLauncher));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => !IsBusy);
        ResetCommand = new AsyncRelayCommand(ResetAsync, () => !IsBusy);
        OpenLogsFolderCommand = new AsyncRelayCommand(OpenLogsFolderAsync, () => !IsBusy);
    }

    public event EventHandler? RequestClose;

    public IReadOnlyList<SettingOption<AccentPreset>> AccentPresets => AppSettingsCatalog.AccentPresets;

    public IReadOnlyList<CharacterPresetOption> CharacterPresets => AppSettingsCatalog.CharacterPresets;

    public IReadOnlyList<string> FontFamilies => AppSettingsCatalog.FontFamilies;

    public IReadOnlyList<int> FontSizes => AppSettingsCatalog.FontSizes;

    public IReadOnlyList<SettingOption<OutputResolution>> OutputResolutions => AppSettingsCatalog.OutputResolutions;

    public IReadOnlyList<SettingOption<ImageExportFormat>> ImageExportFormats => AppSettingsCatalog.ImageExportFormats;

    public IReadOnlyList<SettingOption<AudioExportMode>> AudioModes => AppSettingsCatalog.AudioModes;

    public string ApplicationDataDirectory => _paths.ApplicationDataDirectory;

    public string LogsDirectory => _paths.LogsDirectory;

    public string FfmpegStatus => "FFmpeg validation is not available in Phase 4.";

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand ResetCommand { get; }

    public IAsyncRelayCommand OpenLogsFolderCommand { get; }

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
        private set => SetProperty(ref _statusMessage, value);
    }

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
        StatusMessage = "Review the preferences below, then save or cancel.";
    }

    public void EndEdit() => _themeService.Apply(_settingsManager.Current.AccentPreset);

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
        _themeService.Apply(_settingsManager.Current.AccentPreset);
        StatusMessage = "Unsaved changes were discarded.";
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private async Task ResetAsync()
    {
        bool confirmed = _dialogService.Confirm(
            "Reset GlyphFrame settings",
            "Restore every GlyphFrame preference to its approved default? Logs and media files will not be changed.",
            "Reset Settings");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            AppSettings defaults = await _settingsManager.ResetAsync();
            LoadDraft(defaults);
            StatusMessage = "Settings were reset to approved defaults and saved.";
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            StatusMessage = "Settings could not be reset. Existing preferences were left unchanged.";
            _themeService.Apply(_settingsManager.Current.AccentPreset);
        }
        finally
        {
            IsBusy = false;
        }
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
    }

    private static bool IsExpectedStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;
}
