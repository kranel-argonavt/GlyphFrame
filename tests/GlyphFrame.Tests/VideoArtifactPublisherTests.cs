using System.ComponentModel;
using System.Runtime.InteropServices;
using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Infrastructure.Media;
using GlyphFrame.Tests.TestInfrastructure;

namespace GlyphFrame.Tests;

public sealed class VideoArtifactPublisherTests
{
    [Theory]
    [InlineData(AudioMuxOutcome.Removed)]
    [InlineData(AudioMuxOutcome.NoSourceAudio)]
    [InlineData(AudioMuxOutcome.StreamCopied)]
    [InlineData(AudioMuxOutcome.TranscodedToAac)]
    public async Task Publication_IsByteIdenticalRepeatableAndRetainsArtifact(AudioMuxOutcome outcome)
    {
        using Fixture fixture = new(outcome);
        List<VideoExportProgress> updates = [];
        for (int i = 0; i < 2; i++)
        {
            string destination = fixture.Output($"відео 日本語 {i}.mp4");
            VideoExportResult result = await fixture.Publish(destination, progress: new Observer(update => updates.Add(update)));
            Assert.True(result.Success, result.TechnicalDetail);
            Assert.Equal(outcome, result.AudioOutcome);
            Assert.Equal(fixture.Bytes, await File.ReadAllBytesAsync(destination));
            using FileStream exclusive = File.Open(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        Assert.Equal(fixture.Bytes, await File.ReadAllBytesAsync(fixture.Artifact.FilePath));
        Assert.Contains(updates, x => x.Stage == VideoExportStage.CopyingArtifact && x.Percentage < 100);
        Assert.Equal(100, updates[^1].Percentage);
        Assert.All(updates, x => Assert.Equal(0, x.ProcessedFrames));
        fixture.AssertNoPartials();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedOverwriteAndDecline_ProtectExistingBytes(bool overwrite)
    {
        using Fixture fixture = new();
        string output = fixture.Output("existing.mp4");
        await File.WriteAllBytesAsync(output, [8, 9]);
        VideoExportResult result = await fixture.Publish(output, overwrite);
        Assert.Equal(overwrite, result.Success);
        Assert.Equal(overwrite ? fixture.Bytes : [8, 9], await File.ReadAllBytesAsync(output));
        fixture.AssertNoPartials();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestinationAppearsOrChangesAfterConsent_IsNotOverwritten(bool existed)
    {
        using Fixture fixture = new();
        string output = fixture.Output("conflict.mp4");
        if (existed) await File.WriteAllBytesAsync(output, [1]);
        fixture.Publisher.BeforeCommit = _ => File.WriteAllBytesAsync(output, [9, 9]);
        VideoExportResult result = await fixture.Publish(output, overwrite: true);
        Assert.False(result.Success);
        Assert.Equal(VideoExportErrorCategory.DestinationConflict, result.ErrorCategory);
        Assert.Equal(new byte[] { 9, 9 }, await File.ReadAllBytesAsync(output));
        fixture.AssertNoPartials();
    }

    [Theory]
    [InlineData("access", VideoExportErrorCategory.DestinationAccessDenied)]
    [InlineData("win32-access", VideoExportErrorCategory.DestinationAccessDenied)]
    [InlineData("disk", VideoExportErrorCategory.InsufficientDiskSpace)]
    [InlineData("atomic", VideoExportErrorCategory.OutputFinalizationFailed)]
    public async Task PublicationFailures_PreserveFilesystemIdentityAndHideRawText(
        string failure,
        VideoExportErrorCategory expectedCategory)
    {
        using Fixture fixture = new();
        string output = fixture.Output($"{failure}.mp4");
        const string raw = "controlled publication diagnostic";
        fixture.Publisher.BeforeCommit = _ => Task.FromException(failure switch
        {
            "access" => new UnauthorizedAccessException(raw),
            "win32-access" => new Win32Exception(5, raw),
            "disk" => new ControlledIOException(raw, unchecked((int)0x80070070)),
            _ => new InvalidOperationException(raw)
        });

        VideoExportResult result = await fixture.Publish(output);

        Assert.Equal(expectedCategory, result.ErrorCategory);
        Assert.Contains(raw, result.TechnicalDetail, StringComparison.Ordinal);
        Assert.DoesNotContain(raw, result.UserMessage, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
        Assert.True(File.Exists(fixture.Artifact.FilePath));
        fixture.AssertNoPartials();
    }

    [Fact]
    public async Task ClassifiedFailure_RemainsAccurateWhenOwnedPartialCleanupAlsoFails()
    {
        using Fixture fixture = new();
        string output = fixture.Output("access-with-cleanup-debt.mp4");
        fixture.Publisher.BeforeCommit = _ => Task.FromException(
            new UnauthorizedAccessException("controlled access failure"));
        fixture.Publisher.FailCleanup = true;

        VideoExportResult result = await fixture.Publish(output);

        Assert.Equal(VideoExportErrorCategory.DestinationAccessDenied, result.ErrorCategory);
        string incomplete = Assert.IsType<string>(result.IncompleteOutputPath);
        Assert.Contains("controlled access failure", result.TechnicalDetail, StringComparison.Ordinal);
        Assert.Contains("Owned incomplete file:", result.TechnicalDetail, StringComparison.Ordinal);
        Assert.True(File.Exists(incomplete));
        Assert.True(File.Exists(fixture.Artifact.FilePath));
        Assert.False(File.Exists(output));
        File.Delete(incomplete); // explicitly test-owned, recorded cleanup debt
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("finalizing")]
    [InlineData("committed")]
    public async Task Cancellation_HonorsAtomicCommitBoundary(string boundary)
    {
        using Fixture fixture = new();
        using CancellationTokenSource cancellation = new();
        string output = fixture.Output("cancel.mp4");
        await File.WriteAllBytesAsync(output, [7]);
        VideoExportResult result = await fixture.Publish(output, true, cancellation.Token, new Observer(update =>
        {
            if ((boundary == "copy" && update.Stage == VideoExportStage.CopyingArtifact)
                || (boundary == "finalizing" && update.Stage == VideoExportStage.Finalizing)
                || (boundary == "committed" && update.Stage == VideoExportStage.Completed)) cancellation.Cancel();
        }));
        Assert.Equal(boundary == "committed", result.Success);
        Assert.Equal(boundary == "committed" ? fixture.Bytes : [7], await File.ReadAllBytesAsync(output));
        Assert.True(File.Exists(fixture.Artifact.FilePath));
        fixture.AssertNoPartials();
    }

    [Fact]
    public async Task CommitFailure_PreservesDestinationAndReportsOwnedCleanupFailure()
    {
        using Fixture fixture = new();
        string output = fixture.Output("failed.mp4");
        await File.WriteAllBytesAsync(output, [7]);
        fixture.Publisher.FailCommit = true;
        fixture.Publisher.FailCleanup = true;
        VideoExportResult result = await fixture.Publish(output, true);
        Assert.Equal(VideoExportErrorCategory.OutputFinalizationFailed, result.ErrorCategory);
        Assert.Contains("Owned incomplete file:", result.TechnicalDetail);
        Assert.DoesNotContain("controlled", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new byte[] { 7 }, await File.ReadAllBytesAsync(output));
        string partial = Assert.Single(Directory.GetFiles(fixture.Data.RootPath, "*.partial", SearchOption.AllDirectories));
        File.Delete(partial); // explicitly test-owned, recorded cleanup debt
    }

    [Fact]
    public async Task WriteOrPreparationFailure_KeepsSourceAndExistingOutput()
    {
        using Fixture fixture = new();
        string output = fixture.Output("locked.mp4");
        await File.WriteAllBytesAsync(output, [4]);
        VideoPublicationDestination consent = await fixture.Publisher.PrepareAsync(output, fixture.Original);
        using FileStream locked = File.Open(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        VideoExportResult result = await fixture.Publisher.PublishAsync(new(fixture.Artifact, fixture.Original, consent, true));
        Assert.False(result.Success);
        Assert.True(File.Exists(fixture.Artifact.FilePath));
        fixture.AssertNoPartials();
    }

    [Theory]
    [InlineData("source")]
    [InlineData("artifact")]
    [InlineData("cache")]
    [InlineData("missing-directory")]
    [InlineData("extension")]
    public async Task ProtectedAndInvalidDestinations_AreRejected(string kind)
    {
        using Fixture fixture = new();
        string output = kind switch
        {
            "source" => fixture.Original,
            "artifact" => fixture.Artifact.FilePath,
            "cache" => Path.Combine(fixture.Store.CacheRoot, "user.mp4"),
            "missing-directory" => fixture.Output("missing/output.mp4"),
            _ => fixture.Output("output.png")
        };
        await Assert.ThrowsAsync<IOException>(() => fixture.Publisher.PrepareAsync(output, fixture.Original));
        Assert.Equal(fixture.Bytes, await File.ReadAllBytesAsync(fixture.Artifact.FilePath));
        fixture.AssertNoPartials();
    }

    [Fact]
    public async Task MissingArtifact_FailsWithoutPublishing()
    {
        using Fixture fixture = new();
        File.Delete(fixture.Artifact.FilePath);
        VideoExportResult result = await fixture.Publish(fixture.Output("missing.mp4"));
        Assert.False(result.Success);
        Assert.False(File.Exists(fixture.Output("missing.mp4")));
        fixture.AssertNoPartials();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HardLinkAliases_CannotOverwriteSourceOrArtifact(bool artifact)
    {
        using Fixture fixture = new();
        string source = artifact ? fixture.Artifact.FilePath : fixture.Original;
        byte[] originalBytes = await File.ReadAllBytesAsync(source);
        string alias = fixture.Output("інше ім'я.mp4");
        Assert.True(CreateHardLink(alias, source, IntPtr.Zero), $"Win32 error {Marshal.GetLastWin32Error()}");
        await Assert.ThrowsAsync<IOException>(() => fixture.Publisher.PrepareAsync(alias, fixture.Original));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(source));
        fixture.AssertNoPartials();
    }

    [Fact]
    public async Task ThrowingProgressObserver_CannotLoseSuccessfulPublication()
    {
        using Fixture fixture = new();
        string output = fixture.Output("observer.mp4");
        VideoExportResult result = await fixture.Publish(output, progress: new Observer(_ =>
            throw new InvalidOperationException("Controlled observer failure.")));
        Assert.True(result.Success, result.TechnicalDetail);
        Assert.Equal(fixture.Bytes, await File.ReadAllBytesAsync(output));
        fixture.AssertNoPartials();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Borrow_SerializesClearAndShutdownUntilPublicationReleasesSource(bool shutdown)
    {
        using Fixture fixture = new();
        await using ManagedVideoArtifactSession session = new(fixture.Store);
        await session.AcceptAsync(fixture.Artifact);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Publisher.BeforeCommit = async _ => { entered.SetResult(); await release.Task.ConfigureAwait(false); };
        Task<VideoExportResult> copy = session.UseCurrentAsync(fixture.Artifact, _ => fixture.Publish(fixture.Output("borrowed.mp4")));
        await entered.Task;
        Task cleanup = shutdown ? session.DisposeAsync().AsTask() : session.ClearAsync();
        Assert.False(cleanup.IsCompleted);
        Assert.True(File.Exists(fixture.Artifact.FilePath));
        release.SetResult();
        Assert.True((await copy).Success);
        await cleanup;
        Assert.False(File.Exists(fixture.Artifact.FilePath));
        fixture.AssertNoPartials();
    }

    private sealed class Observer(Action<VideoExportProgress> callback) : IProgress<VideoExportProgress>
    {
        public void Report(VideoExportProgress value) => callback(value);
    }

    private sealed class ControlledIOException : IOException
    {
        public ControlledIOException(string message, int hResult)
            : base(message)
        {
            HResult = hResult;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string path, string existingPath, IntPtr security);

    private sealed class ControlledPublisher(ManagedVideoArtifactStore store) : VideoArtifactPublisher(store)
    {
        public Func<CancellationToken, Task> BeforeCommit { get; set; } = _ => Task.CompletedTask;
        public bool FailCommit { get; set; }
        public bool FailCleanup { get; set; }
        protected override Task BeforeCommitAsync(CancellationToken token) => BeforeCommit(token);
        protected override void Commit(string partial, string output, bool replace)
        {
            if (FailCommit) throw new IOException("Controlled atomic publication failure.");
            base.Commit(partial, output, replace);
        }
        protected override void DeletePartial(string path)
        {
            if (FailCleanup) throw new IOException("Controlled cleanup failure.");
            base.DeletePartial(path);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public TemporaryApplicationData Data { get; } = new();
        public ManagedVideoArtifactStore Store { get; }
        public ControlledPublisher Publisher { get; }
        public ManagedVideoArtifact Artifact { get; }
        public byte[] Bytes { get; } = Enumerable.Range(0, 300_000).Select(i => (byte)(i % 251)).ToArray();
        public string Original => Output("оригінал source.mp4");

        public Fixture(AudioMuxOutcome outcome = AudioMuxOutcome.Removed)
        {
            Store = new(Data.Paths, "публікація session");
            ManagedVideoArtifactTarget target = Store.CreateTarget();
            File.WriteAllBytes(target.CompletedPath, Bytes);
            File.WriteAllBytes(Original, [1, 2, 3]);
            Artifact = new(target.ArtifactId, target.CompletedPath,
                new(1, 1, 0, CharacterPresetKind.DetailedAscii, " .#", "Consolas", 10,
                    OutputResolution.SameAsSource, SymbolPolarity.LightOnDark, AudioExportMode.KeepAudio),
                new(64, 48), 30, 30, TimeSpan.FromSeconds(1), AudioExportMode.KeepAudio, outcome, Bytes.Length);
            Publisher = new(Store);
        }

        public string Output(string name) => Path.Combine(Data.RootPath, name);
        public async Task<VideoExportResult> Publish(string output, bool overwrite = false,
            CancellationToken token = default, IProgress<VideoExportProgress>? progress = null)
        {
            VideoPublicationDestination destination = await Publisher.PrepareAsync(output, Original, token);
            return await Publisher.PublishAsync(new(Artifact, Original, destination, overwrite), token, progress);
        }
        public void AssertNoPartials() => Assert.Empty(Directory.GetFiles(Data.RootPath, "*.partial", SearchOption.AllDirectories));
        public void Dispose() { Store.Dispose(); Data.Dispose(); }
    }
}
