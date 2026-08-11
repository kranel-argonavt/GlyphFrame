using System.ComponentModel;
using System.IO;
using GlyphFrame.App.Services;
using GlyphFrame.App.ViewModels;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Settings;

namespace GlyphFrame.App.Tests;

public sealed class SettingsViewModelFfmpegTests
{
    [Fact]
    public void Constructor_NullFfmpegValidationServiceThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(
            new FakeSettingsManager(),
            new FakeApplicationPaths(),
            new FakeAccentThemeService(),
            new FakeFolderLauncher(),
            new FakeUserDialogService(),
            null!));
    }

    [Fact]
    public void InitialPresentation_IsWaitingForValidationWithoutClaimingAvailability()
    {
        SettingsViewModel viewModel = CreateViewModel(new ControlledValidationService());

        Assert.Equal("Checking", viewModel.FfmpegAvailability);
        Assert.Contains("Checking", viewModel.FfmpegStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Unknown", viewModel.FfmpegVersion);
        Assert.Equal("Unknown", viewModel.FfprobeVersion);
        Assert.False(viewModel.IsFfmpegChecking);
    }

    [Fact]
    public async Task BeginEdit_InitiatesOneValidationAndEntersCheckingState()
    {
        TaskCompletionSource<FfmpegValidationResult> pending = NewPendingValidation();
        ControlledValidationService validation = new((_, _) => pending.Task);
        SettingsViewModel viewModel = CreateViewModel(validation);

        viewModel.BeginEdit();

        Assert.Equal(1, validation.CallCount);
        Assert.False(validation.ForceRefreshValues.Single());
        Assert.True(viewModel.IsFfmpegChecking);
        Assert.Equal("Checking", viewModel.FfmpegAvailability);
        Assert.False(viewModel.RefreshFfmpegCommand.CanExecute(null));

        Task completed = WaitForPropertyAsync(viewModel, nameof(viewModel.IsFfmpegChecking), () => !viewModel.IsFfmpegChecking);
        pending.SetResult(AvailableResult());
        await completed;
    }

    [Fact]
    public async Task SuccessfulValidation_PropagatesAvailabilityVersionsPathAndDetails()
    {
        TaskCompletionSource<FfmpegValidationResult> pending = NewPendingValidation();
        SettingsViewModel viewModel = CreateViewModel(new ControlledValidationService((_, _) => pending.Task));
        viewModel.BeginEdit();
        Task completed = WaitForPropertyAsync(viewModel, nameof(viewModel.IsFfmpegChecking), () => !viewModel.IsFfmpegChecking);

        pending.SetResult(AvailableResult());
        await completed;

        Assert.Equal("Available", viewModel.FfmpegAvailability);
        Assert.Equal("9.0-ffmpeg-test", viewModel.FfmpegVersion);
        Assert.Equal("9.0-ffprobe-test", viewModel.FfprobeVersion);
        Assert.Equal(ToolsDirectory, viewModel.BundledToolsDirectory);
        Assert.Contains("ffmpeg technical", viewModel.FfmpegTechnicalDetails, StringComparison.Ordinal);
        Assert.Contains("ffprobe technical", viewModel.FfmpegTechnicalDetails, StringComparison.Ordinal);
        Assert.True(viewModel.RefreshFfmpegCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(FfmpegToolValidationStatus.Missing)]
    [InlineData(FfmpegToolValidationStatus.InvalidVersionOutput)]
    public async Task InvalidTools_ProduceUnavailableStablePresentation(FfmpegToolValidationStatus status)
    {
        ControlledValidationService validation = new((_, _) => Task.FromResult(UnavailableResult(status)));
        SettingsViewModel viewModel = CreateViewModel(validation);
        Task completed = WaitForPropertyAsync(viewModel, nameof(viewModel.IsFfmpegChecking), () => !viewModel.IsFfmpegChecking);

        viewModel.BeginEdit();
        await completed;

        Assert.Equal("Unavailable", viewModel.FfmpegAvailability);
        Assert.Equal("Unavailable", viewModel.FfmpegVersion);
        Assert.Equal("9.0-ffprobe-test", viewModel.FfprobeVersion);
        Assert.Contains("unavailable", viewModel.FfmpegStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", viewModel.FfmpegStatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshCommand_ForcesValidationAndIsDisabledUntilCompletion()
    {
        TaskCompletionSource<FfmpegValidationResult> refresh = NewPendingValidation();
        ControlledValidationService validation = new((forceRefresh, _) =>
            forceRefresh ? refresh.Task : Task.FromResult(AvailableResult()));
        SettingsViewModel viewModel = CreateViewModel(validation);
        viewModel.BeginEdit();
        Assert.Equal("Available", viewModel.FfmpegAvailability);
        Assert.False(viewModel.IsFfmpegChecking);

        Task commandTask = viewModel.RefreshFfmpegCommand.ExecuteAsync(null);

        Assert.Equal(2, validation.CallCount);
        Assert.True(validation.ForceRefreshValues.Last());
        Assert.True(viewModel.IsFfmpegChecking);
        Assert.False(viewModel.RefreshFfmpegCommand.CanExecute(null));
        refresh.SetResult(AvailableResult());
        await commandTask;
        Assert.False(viewModel.IsFfmpegChecking);
        Assert.True(viewModel.RefreshFfmpegCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancellation_ProducesUnavailableStateAndClearsCheckingValues()
    {
        ControlledValidationService validation = new((_, cancellationToken) =>
            Task.FromCanceled<FfmpegValidationResult>(new CancellationToken(canceled: true)));
        SettingsViewModel viewModel = CreateViewModel(validation);
        Task completed = WaitForPropertyAsync(viewModel, nameof(viewModel.IsFfmpegChecking), () => !viewModel.IsFfmpegChecking);

        viewModel.BeginEdit();
        await completed;

        Assert.Equal("Unavailable", viewModel.FfmpegAvailability);
        Assert.Equal("Unavailable", viewModel.FfmpegVersion);
        Assert.Equal("Unavailable", viewModel.FfprobeVersion);
        Assert.Equal("Unavailable", viewModel.BundledToolsDirectory);
        Assert.Contains("cancelled", viewModel.FfmpegStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cancelled", viewModel.FfmpegTechnicalDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExpectedValidationException_ProducesFriendlyPrimaryMessageAndTechnicalDetails()
    {
        const string rawFailure = "raw filesystem exception detail";
        ControlledValidationService validation = new((_, _) => Task.FromException<FfmpegValidationResult>(new IOException(rawFailure)));
        SettingsViewModel viewModel = CreateViewModel(validation);
        Task completed = WaitForPropertyAsync(viewModel, nameof(viewModel.IsFfmpegChecking), () => !viewModel.IsFfmpegChecking);

        viewModel.BeginEdit();
        await completed;

        Assert.Equal("Unavailable", viewModel.FfmpegAvailability);
        Assert.DoesNotContain(rawFailure, viewModel.FfmpegStatusMessage, StringComparison.Ordinal);
        Assert.Contains(rawFailure, viewModel.FfmpegTechnicalDetails, StringComparison.Ordinal);
        Assert.Equal("Unavailable", viewModel.FfmpegVersion);
        Assert.Equal("Unavailable", viewModel.FfprobeVersion);
        Assert.Equal("Unavailable", viewModel.BundledToolsDirectory);
    }

    [Fact]
    public async Task AvailableThenFailedForcedRefresh_DoesNotRetainSuccessfulVersionsPathOrDetails()
    {
        int call = 0;
        ControlledValidationService validation = new((_, _) => ++call == 1
            ? Task.FromResult(AvailableResult())
            : Task.FromException<FfmpegValidationResult>(new InvalidOperationException("new refresh failure")));
        SettingsViewModel viewModel = CreateViewModel(validation);
        viewModel.BeginEdit();
        Assert.Equal("Available", viewModel.FfmpegAvailability);

        await viewModel.RefreshFfmpegCommand.ExecuteAsync(null);

        Assert.Equal("Unavailable", viewModel.FfmpegAvailability);
        Assert.Equal("Unavailable", viewModel.FfmpegVersion);
        Assert.Equal("Unavailable", viewModel.FfprobeVersion);
        Assert.Equal("Unavailable", viewModel.BundledToolsDirectory);
        Assert.DoesNotContain("ffmpeg technical", viewModel.FfmpegTechnicalDetails, StringComparison.Ordinal);
        Assert.Contains("new refresh failure", viewModel.FfmpegTechnicalDetails, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepeatedBeginEdit_DoesNotStartDuplicateValidationWhileOneIsRunning()
    {
        TaskCompletionSource<FfmpegValidationResult> pending = NewPendingValidation();
        ControlledValidationService validation = new((_, _) => pending.Task);
        SettingsViewModel viewModel = CreateViewModel(validation);

        viewModel.BeginEdit();
        viewModel.BeginEdit();
        viewModel.BeginEdit();

        Assert.Equal(1, validation.CallCount);
        Task completed = WaitForPropertyAsync(viewModel, nameof(viewModel.IsFfmpegChecking), () => !viewModel.IsFfmpegChecking);
        pending.SetResult(AvailableResult());
        await completed;
    }

    private static SettingsViewModel CreateViewModel(IFfmpegValidationService validationService) => new(
        new FakeSettingsManager(),
        new FakeApplicationPaths(),
        new FakeAccentThemeService(),
        new FakeFolderLauncher(),
        new FakeUserDialogService(),
        validationService);

    private static TaskCompletionSource<FfmpegValidationResult> NewPendingValidation() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static FfmpegValidationResult AvailableResult() => new(
        ToolsDirectory,
        ToolResult(FfmpegToolKind.Ffmpeg, FfmpegToolValidationStatus.Available, "9.0-ffmpeg-test", "ffmpeg technical"),
        ToolResult(FfmpegToolKind.Ffprobe, FfmpegToolValidationStatus.Available, "9.0-ffprobe-test", "ffprobe technical"),
        DateTimeOffset.UtcNow);

    private static FfmpegValidationResult UnavailableResult(FfmpegToolValidationStatus status) => new(
        ToolsDirectory,
        ToolResult(FfmpegToolKind.Ffmpeg, status, null, "current failure technical"),
        ToolResult(FfmpegToolKind.Ffprobe, FfmpegToolValidationStatus.Available, "9.0-ffprobe-test", "ffprobe technical"),
        DateTimeOffset.UtcNow);

    private static FfmpegToolValidationResult ToolResult(
        FfmpegToolKind tool,
        FfmpegToolValidationStatus status,
        string? version,
        string technicalDetails)
    {
        string displayName = tool == FfmpegToolKind.Ffmpeg ? "FFmpeg" : "ffprobe";
        string fileName = tool == FfmpegToolKind.Ffmpeg ? "ffmpeg.exe" : "ffprobe.exe";
        return new FfmpegToolValidationResult(
            tool,
            status,
            Path.Combine(ToolsDirectory, fileName),
            version,
            status == FfmpegToolValidationStatus.Available
                ? $"{displayName} is available."
                : $"{displayName} is unavailable.",
            technicalDetails);
    }

    private static async Task WaitForPropertyAsync(
        INotifyPropertyChanged source,
        string propertyName,
        Func<bool> condition)
    {
        if (condition())
        {
            return;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == propertyName && condition())
            {
                source.PropertyChanged -= handler;
                completion.TrySetResult();
            }
        };
        source.PropertyChanged += handler;
        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            source.PropertyChanged -= handler;
        }
    }

    private const string ToolsDirectory = "C:\\GlyphFrame Test\\Tools\\ffmpeg";

    private sealed class ControlledValidationService(
        Func<bool, CancellationToken, Task<FfmpegValidationResult>>? handler = null) : IFfmpegValidationService
    {
        private readonly Func<bool, CancellationToken, Task<FfmpegValidationResult>> _handler =
            handler ?? ((_, _) => Task.FromResult(AvailableResult()));
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public List<bool> ForceRefreshValues { get; } = [];

        public Task<FfmpegValidationResult> ValidateAsync(
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            lock (ForceRefreshValues)
            {
                ForceRefreshValues.Add(forceRefresh);
            }

            return _handler(forceRefresh, cancellationToken);
        }
    }

    private sealed class FakeSettingsManager : IApplicationSettingsManager
    {
        public AppSettings Current { get; private set; } = AppSettingsCatalog.Defaults;

        public string? StartupStatusMessage => null;

        public event EventHandler<AppSettings>? SettingsChanged
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            return Task.CompletedTask;
        }

        public Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default)
        {
            Current = AppSettingsCatalog.Defaults;
            return Task.FromResult(Current);
        }
    }

    private sealed class FakeApplicationPaths : IApplicationPaths
    {
        public string ApplicationDataDirectory => "C:\\GlyphFrame Test";

        public string SettingsFilePath => Path.Combine(ApplicationDataDirectory, "settings.json");

        public string LogsDirectory => Path.Combine(ApplicationDataDirectory, "logs");

        public void EnsureDirectories()
        {
        }
    }

    private sealed class FakeAccentThemeService : IAccentThemeService
    {
        public void Apply(AccentPreset preset)
        {
        }

        public string GetPrimaryColorHex(AccentPreset preset) => "#8B5CF6";
    }

    private sealed class FakeFolderLauncher : IFolderLauncher
    {
        public Task OpenFolderAsync(string folderPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUserDialogService : IUserDialogService
    {
        public bool Confirm(string title, string message, string confirmButtonText = "Confirm", string cancelButtonText = "Cancel") => false;
    }
}
