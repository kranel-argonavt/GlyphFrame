using System.IO;
using GlyphFrame.App.Services;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.App.Tests;

public sealed class SettingsDraftTests
{
    [Fact]
    public async Task Reset_LoadsEveryDefaultIntoDraft_WithoutSavingOrPublishing()
    {
        Harness h = await Harness.Create();
        await h.ViewModel.ResetCommand.ExecuteAsync(null);
        AssertDraft(AppSettingsCatalog.Defaults, h.ViewModel);
        AssertUnchanged(h);
        Assert.Equal(AccentPreset.Violet, h.Theme.Applied);
        Assert.Equal("Defaults loaded. Select Save & Close to apply.", h.ViewModel.StatusMessage);
        Assert.Contains("draft", h.Dialogs.Message);
        Assert.Contains("Save & Close", h.Dialogs.Message);
        Assert.Equal("Reset to defaults", h.Dialogs.ConfirmLabel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reset_CancelOrEndEdit_DiscardsDraftAndRestoresPersistedAccent(bool endEdit)
    {
        Harness h = await Harness.Create();
        int closes = 0; h.ViewModel.RequestClose += (_, _) => closes++;
        h.ViewModel.SelectedAccentPreset = AccentPreset.Cyan;
        await h.ViewModel.ResetCommand.ExecuteAsync(null);
        if (endEdit) { h.ViewModel.EndEdit(); } // Window close/service finally path.
        else { h.ViewModel.CancelCommand.Execute(null); }
        AssertDraft(Harness.Persisted, h.ViewModel);
        Assert.Equal(Harness.Persisted.AccentPreset, h.Theme.Applied);
        AssertUnchanged(h);
        Assert.Equal(endEdit ? 0 : 1, closes);
        h.ViewModel.BeginEdit();
        AssertDraft(Harness.Persisted, h.ViewModel);
    }

    [Fact]
    public async Task DeclinedReset_PreservesEditedDraftAndPreview()
    {
        Harness h = await Harness.Create();
        h.Dialogs.Accept = false;
        h.ViewModel.SelectedAccentPreset = AccentPreset.Cyan;
        h.ViewModel.SelectedFontSize = 9;
        await h.ViewModel.ResetCommand.ExecuteAsync(null);
        Assert.Equal(AccentPreset.Cyan, h.ViewModel.SelectedAccentPreset);
        Assert.Equal(9, h.ViewModel.SelectedFontSize);
        Assert.Equal(AccentPreset.Cyan, h.Theme.Applied);
        AssertUnchanged(h);
    }

    [Fact]
    public async Task Reset_Save_UsesNormalSaveAndPublishesOnlyAfterSuccess()
    {
        Harness h = await Harness.Create();
        int closes = 0; h.ViewModel.RequestClose += (_, _) => closes++;
        await h.ViewModel.ResetCommand.ExecuteAsync(null);
        AssertUnchanged(h);
        await h.ViewModel.SaveCommand.ExecuteAsync(null);
        Assert.Equal(AppSettingsCatalog.Defaults, h.Store.Saved);
        Assert.Equal(AppSettingsCatalog.Defaults, h.Manager.Current);
        Assert.Equal(1, h.Store.Saves); Assert.Equal(0, h.Store.Resets);
        Assert.Equal(1, h.Published); Assert.Equal(1, closes);
        h.ViewModel.EndEdit();
        AssertDraft(AppSettingsCatalog.Defaults, h.ViewModel);
        Assert.Equal("Settings saved and applied.", h.ViewModel.StatusMessage);
    }

    [Fact]
    public async Task Reset_SaveFailure_IsTruthfulAndRetryCanSaveDraft()
    {
        Harness h = await Harness.Create();
        int closes = 0; h.ViewModel.RequestClose += (_, _) => closes++;
        await h.ViewModel.ResetCommand.ExecuteAsync(null);
        h.Store.FailSave = true;
        await h.ViewModel.SaveCommand.ExecuteAsync(null);
        Assert.Contains("could not be saved", h.ViewModel.StatusMessage);
        Assert.False(h.ViewModel.IsBusy); Assert.True(h.ViewModel.SaveCommand.CanExecute(null));
        Assert.Equal(0, closes); Assert.Equal(0, h.Published);
        Assert.Equal(Harness.Persisted, h.Manager.Current); Assert.Equal(Harness.Persisted, h.Store.Saved);
        AssertDraft(AppSettingsCatalog.Defaults, h.ViewModel);
        h.Store.FailSave = false;
        await h.ViewModel.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2, h.Store.Saves); Assert.Equal(0, h.Store.Resets);
        Assert.Equal(1, h.Published); Assert.Equal(1, closes);
        Assert.Equal(AppSettingsCatalog.Defaults, h.Store.Saved);
    }

    [Fact]
    public async Task FailedSave_ThenClose_RestoresPersistedDraftAndAccent()
    {
        Harness h = await Harness.Create();
        await h.ViewModel.ResetCommand.ExecuteAsync(null);
        h.Store.FailSave = true;
        await h.ViewModel.SaveCommand.ExecuteAsync(null);
        h.ViewModel.EndEdit();
        AssertDraft(Harness.Persisted, h.ViewModel);
        Assert.Equal(Harness.Persisted.AccentPreset, h.Theme.Applied);
        Assert.Equal(0, h.Published); Assert.Equal(0, h.Store.Resets);
    }

    [Fact]
    public async Task LogsFolderFailure_RetainsUsefulCopyGuidanceAndDoesNotSave()
    {
        Harness h = await Harness.Create(); h.Folders.Fail = true;
        await h.ViewModel.OpenLogsFolderCommand.ExecuteAsync(null);
        Assert.Contains("copy its path", h.ViewModel.StatusMessage);
        Assert.Equal(h.ViewModel.LogsDirectory, h.Folders.Path);
        AssertUnchanged(h);
    }

    private static void AssertUnchanged(Harness h)
    {
        Assert.Equal(Harness.Persisted, h.Manager.Current);
        Assert.Equal(Harness.Persisted, h.Store.Saved);
        Assert.Equal(0, h.Store.Saves); Assert.Equal(0, h.Store.Resets); Assert.Equal(0, h.Published);
    }

    internal static void AssertDraft(AppSettings settings, SettingsViewModel vm)
    {
        Assert.Equal(settings.AccentPreset, vm.SelectedAccentPreset);
        Assert.Equal(settings.DefaultCharacterPreset, vm.SelectedCharacterPreset.Kind);
        Assert.Equal(settings.DefaultFontFamily, vm.SelectedFontFamily);
        Assert.Equal(settings.DefaultFontSize, vm.SelectedFontSize);
        Assert.Equal(settings.DefaultOutputResolution, vm.SelectedOutputResolution);
        Assert.Equal(settings.DefaultSymbolPolarity, vm.SelectedSymbolPolarity);
        Assert.Equal(settings.DefaultImageExportFormat, vm.SelectedImageExportFormat);
        Assert.Equal(settings.DefaultVideoAudioMode, vm.SelectedVideoAudioMode);
    }

    internal sealed class Harness
    {
        internal static readonly AppSettings Persisted = new()
        {
            AccentPreset = AccentPreset.Rose,
            DefaultCharacterPreset = CharacterPresetKind.Blocks,
            DefaultFontFamily = "Consolas",
            DefaultFontSize = 12,
            DefaultOutputResolution = OutputResolution.Hd720,
            DefaultSymbolPolarity = SymbolPolarity.DarkOnLight,
            DefaultImageExportFormat = ImageExportFormat.Jpeg,
            DefaultVideoAudioMode = AudioExportMode.RemoveAudio
        };
        internal Store Store { get; } = new();
        internal Theme Theme { get; } = new();
        internal Dialogs Dialogs { get; } = new();
        internal Folders Folders { get; } = new();
        internal ApplicationSettingsManager Manager { get; }
        internal SettingsViewModel ViewModel { get; }
        internal int Published { get; private set; }
        private Harness()
        {
            Manager = new(Store, Theme);
            Manager.SettingsChanged += (_, _) => Published++;
            ViewModel = new(Manager, new Paths(), Theme, Folders, Dialogs, new Validation(), new ErrorPresentationService(), new Details());
        }
        internal static async Task<Harness> Create()
        {
            Harness h = new(); await h.Manager.InitializeAsync(); h.ViewModel.BeginEdit(); return h;
        }
    }

    internal sealed class Store : ISettingsService
    {
        internal AppSettings Saved { get; private set; } = Harness.Persisted;
        internal int Saves { get; private set; }
        internal int Resets { get; private set; }
        internal bool FailSave { get; set; }
        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SettingsLoadResult(Saved, SettingsLoadState.Loaded));
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Saves++;
            if (FailSave) { throw new UnauthorizedAccessException("Controlled storage failure"); }
            Saved = settings; return Task.CompletedTask;
        }
        public Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default)
        { Resets++; throw new InvalidOperationException("The draft must not call the persistence Reset API."); }
    }
    internal sealed class Theme : IAccentThemeService
    {
        internal AccentPreset Applied { get; private set; }
        public void Apply(AccentPreset preset) => Applied = preset;
        public string GetPrimaryColorHex(AccentPreset preset) => "#8B5CF6";
    }
    internal sealed class Dialogs : IUserDialogService
    {
        internal bool Accept { get; set; } = true;
        internal string Message { get; private set; } = "";
        internal string ConfirmLabel { get; private set; } = "";
        public bool Confirm(string title, string message, string confirmButtonText = "Confirm", string cancelButtonText = "Cancel")
        { Message = message; ConfirmLabel = confirmButtonText; return Accept; }
    }
    internal sealed class Folders : IFolderLauncher
    {
        internal bool Fail { get; set; }
        internal string? Path { get; private set; }
        public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default)
        { Path = folderPath; return Fail ? Task.FromException(new IOException("Controlled launch failure")) : Task.CompletedTask; }
    }
    internal sealed class Paths : IApplicationPaths
    {
        public string ApplicationDataDirectory => @"C:\Users\Example\Long local application data directory\Медіа_日本語\GlyphFrame";
        public string SettingsFilePath => Path.Combine(ApplicationDataDirectory, "settings.json");
        public string LogsDirectory => Path.Combine(ApplicationDataDirectory, "logs");
        public void EnsureDirectories() { }
    }
    private sealed class Validation : IFfmpegValidationService
    {
        public Task<FfmpegValidationResult> ValidateAsync(bool forceRefresh = false, CancellationToken cancellationToken = default) => Task.FromResult(
            new FfmpegValidationResult(@"C:\GlyphFrame\Tools\ffmpeg",
                new(FfmpegToolKind.Ffmpeg, FfmpegToolValidationStatus.Available, "ffmpeg.exe", "test", "Available", "diagnostic"),
                new(FfmpegToolKind.Ffprobe, FfmpegToolValidationStatus.Available, "ffprobe.exe", "test", "Available", "diagnostic"), DateTimeOffset.UtcNow));
    }
    private sealed class Details : IErrorDetailsDialogService { public void Show(ErrorPresentation presentation) { } }
}
