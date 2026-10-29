using System.IO;
using System.Text.Json;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class NativeTermsTests
{
    [Fact]
    public void CompleteSyntheticPreview_HasStableIdentityAndInertReferenceText()
    {
        using Fixture f = new();
        NativeTermsBundle bundle = Assert.IsType<NativeTermsBundle>(NativeTermsBundle.Load(f.Root));
        Assert.Equal(NativeTermsBundle.TextHash(File.ReadAllText(Path.Combine(f.Root, NativeTermsBundle.ProfilePath))), bundle.Identity);
        Assert.Contains("NONOPERATIVE", bundle.Text);
        Assert.Contains("synthetic reference", bundle.References);
        Assert.Contains("===== LICENSE =====", bundle.Notices);
        Assert.Equal("ImplementationPreview", NativeTermsBundle.Purpose);
    }

    [Fact]
    public void FrameworkDependentWithoutBundledComponents_DoesNotPromptOrCreateRecord()
    {
        using Fixture f = new();
        f.FrameworkDependent();
        foreach (string name in NativeTermsBundle.ComponentNames) { File.Delete(Path.Combine(f.Root, name)); }
        Assert.True(NativeTermsStartupGate.EnsureAcknowledged(f.Root, f.Data, (_, _, _) => throw new Exception("Unexpected prompt")));
        Assert.False(Directory.Exists(f.Data));
    }

    [Theory]
    [InlineData("missing-component")]
    [InlineData("changed-component")]
    [InlineData("changed-profile")]
    [InlineData("changed-terms")]
    [InlineData("changed-reference")]
    [InlineData("changed-runtime")]
    [InlineData("partial-framework-dependent")]
    [InlineData("missing-runtimeconfig")]
    [InlineData("corrupt-runtimeconfig")]
    public void UnverifiedScope_FailsClosedWithoutProducingAcknowledgement(string damage)
    {
        using Fixture f = new();
        switch (damage)
        {
            case "missing-component": File.Delete(Path.Combine(f.Root, NativeTermsBundle.ComponentNames[0])); break;
            case "changed-component": f.Write(NativeTermsBundle.ComponentNames[0], "tampered"); break;
            case "changed-profile": f.Write(NativeTermsBundle.ProfilePath, "{}"); break;
            case "changed-terms": f.Write(NativeTermsBundle.TermsPath, "changed"); break;
            case "changed-reference": f.Write(f.References[0], "changed"); break;
            case "changed-runtime": f.Runtime("10.0.99"); break;
            case "partial-framework-dependent": f.FrameworkDependent(); break;
            case "missing-runtimeconfig": File.Delete(Path.Combine(f.Root, "GlyphFrame.App.runtimeconfig.json")); break;
            case "corrupt-runtimeconfig": f.Write("GlyphFrame.App.runtimeconfig.json", "broken"); break;
        }
        bool shown = false;
        Assert.False(NativeTermsStartupGate.EnsureAcknowledged(f.Root, f.Data, (bundle, error, save) =>
        {
            shown = true;
            Assert.Null(bundle); Assert.NotNull(error);
            Assert.Throws<InvalidOperationException>(save);
            return true; // Even a faulty presenter cannot bypass an unverified scope.
        }));
        Assert.True(shown);
        Assert.False(Directory.Exists(f.Data));
    }

    [Fact]
    public void FreshDecline_DoesNotWriteSettingsRecordOrMediaState()
    {
        using Fixture f = new();
        Assert.False(NativeTermsStartupGate.EnsureAcknowledged(f.Root, f.Data, (bundle, error, _) =>
        {
            Assert.NotNull(bundle); Assert.Null(error); return false;
        }));
        Assert.False(Directory.Exists(f.Data));
    }

    [Fact]
    public void PromptCannotClaimSuccessWithoutPersistingExplicitPreviewChoice()
    {
        using Fixture f = new();
        Assert.False(NativeTermsStartupGate.EnsureAcknowledged(f.Root, f.Data, (_, _, _) => true));
        Assert.False(Directory.Exists(f.Data));
    }

    [Fact]
    public void ExplicitSyntheticPreviewChoice_SkipsOnlyTheIdenticalBundleAndNeverChangesSettings()
    {
        using Fixture f = new();
        Directory.CreateDirectory(f.Data);
        string settings = Path.Combine(f.Data, "settings.json");
        File.WriteAllText(settings, "synthetic settings unchanged");
        Assert.True(NativeTermsStartupGate.EnsureAcknowledged(f.Root, f.Data, (_, _, save) => { save(); return true; }));
        Assert.True(NativeTermsStartupGate.EnsureAcknowledged(f.Root, f.Data, (_, _, _) => throw new Exception("Duplicate prompt")));
        Assert.Equal("synthetic settings unchanged", File.ReadAllText(settings));
        Assert.False(Directory.Exists(Path.Combine(f.Data, "Cache")));
        f.ReviewVersion = "changed-synthetic-review"; f.Bind();
        Assert.False(NativeTermsStartupGate.EnsureAcknowledged(f.Root, f.Data, (_, _, _) => false));
    }

    [Theory]
    [InlineData("wrong-purpose")]
    [InlineData("wrong-bundle")]
    [InlineData("bad-time")]
    [InlineData("extra-line")]
    [InlineData("empty")]
    [InlineData("oversize")]
    public void CorruptForeignOrOperativeRecord_DoesNotAcknowledgePreview(string damage)
    {
        using Fixture f = new();
        string identity = NativeTermsBundle.Load(f.Root)!.Identity;
        string record = NativeTermsAcknowledgementStore.Format(identity, DateTimeOffset.UtcNow);
        record = damage switch
        {
            "wrong-purpose" => record.Replace("ImplementationPreview", "OperativeAgreement"),
            "wrong-bundle" => record.Replace(identity, new string('a', 64)),
            "bad-time" => record[..record.IndexOf("AcknowledgedUtc=", StringComparison.Ordinal)] + "AcknowledgedUtc=not-a-time\n",
            "extra-line" => record + "extra\n",
            "empty" => "",
            _ => new string('x', 2048)
        };
        Directory.CreateDirectory(f.Data);
        File.WriteAllText(Path.Combine(f.Data, NativeTermsAcknowledgementStore.FileName), record);
        Assert.False(new NativeTermsAcknowledgementStore(f.Data).IsCurrent(identity));
    }

    [Fact]
    public void SharedRecordProtocol_IsCanonicalUtcAndIndependentFromSettings()
    {
        string id = new('a', 64);
        string record = NativeTermsAcknowledgementStore.Format(id,
            new DateTimeOffset(2026, 10, 29, 3, 4, 5, TimeSpan.FromHours(2)));
        Assert.Contains("AcknowledgedUtc=2026-10-29T01:04:05.000Z", record);
        Assert.True(NativeTermsAcknowledgementStore.Matches(record.TrimEnd('\n').Split('\n'), id));
        Assert.DoesNotContain("settings", record, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteFailure_DoesNotOverwriteAnExistingRecordOrLeakTemporaryFiles()
    {
        using Fixture f = new();
        Directory.CreateDirectory(f.Data);
        string path = Path.Combine(f.Data, NativeTermsAcknowledgementStore.FileName);
        File.WriteAllText(path, "prior synthetic record");
        using (FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? failure = Record.Exception(() => new NativeTermsAcknowledgementStore(f.Data).Save(new string('a', 64)));
            Assert.True(failure is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("prior synthetic record", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(f.Data, "*.tmp"));
    }

    [Fact]
    public void RepeatedWrites_AreCompleteAndDoNotTouchOtherUserFiles()
    {
        using Fixture f = new();
        NativeTermsAcknowledgementStore store = new(f.Data);
        for (int i = 0; i < 10; i++) { store.Save(new string('a', 64)); }
        Assert.True(store.IsCurrent(new string('a', 64)));
        Assert.Single(Directory.GetFiles(f.Data));
    }

    internal sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "GlyphFrame.NativeTerms.Tests", Guid.NewGuid().ToString("N"));
        internal string Data => Path.Combine(Root, "isolated-profile");
        internal string ReviewVersion { get; set; } = "synthetic-review";
        internal string[] References { get; } = ["licenses/dotnet/NET-LIBRARY-LICENSE.txt", "licenses/WPF/WINDOWS-SDK-LICENSE.txt", "licenses/dotnet/WINDOWS-LICENSE-MAPPING.txt"];
        internal Fixture()
        {
            Directory.CreateDirectory(Root);
            foreach (string name in NativeTermsBundle.ComponentNames) { Write(name, "synthetic inert component " + name); }
            foreach (string path in References) { Write(path, "synthetic reference " + path); }
            Write("LICENSE", "synthetic original-code license");
            Write("THIRD_PARTY_NOTICES.md", "synthetic test notices");
            Write(NativeTermsBundle.TermsPath, "NONOPERATIVE REVIEW DRAFT — synthetic text only, no real vendor agreement");
            Runtime("10.0.12"); Bind();
        }
        internal void Write(string path, string text)
        {
            string file = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text);
        }
        internal void Runtime(string version) => Write("GlyphFrame.App.runtimeconfig.json", JsonSerializer.Serialize(new
        {
            runtimeOptions = new { includedFrameworks = new[] { new { name = "Microsoft.NETCore.App", version }, new { name = "Microsoft.WindowsDesktop.App", version } } }
        }));
        internal void FrameworkDependent() => Write("GlyphFrame.App.runtimeconfig.json", "{\"runtimeOptions\":{\"framework\":{\"name\":\"Microsoft.WindowsDesktop.App\",\"version\":\"10.0.0\"}}}");
        internal void Bind()
        {
            object[] files = NativeTermsBundle.ComponentNames.Select(name => (object)new
            {
                Path = name,
                Length = new FileInfo(Path.Combine(Root, name)).Length,
                Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, name)))).ToLowerInvariant()
            }).ToArray();
            Write(NativeTermsBundle.ProfilePath, JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                Purpose = NativeTermsBundle.Purpose,
                Version = ReviewVersion,
                TermsPath = NativeTermsBundle.TermsPath,
                TermsTextSha256 = Hash(NativeTermsBundle.TermsPath),
                RuntimeFrameworks = new Dictionary<string, string> { ["Microsoft.NETCore.App"] = "10.0.12", ["Microsoft.WindowsDesktop.App"] = "10.0.12" },
                Components = files,
                References = References.Select(path => new { Path = path, TextSha256 = Hash(path) })
            }));
            Write("licenses/manifest.json", JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                WindowsNativeTerms = new { ProfilePath = NativeTermsBundle.ProfilePath, ProfileTextSha256 = Hash(NativeTermsBundle.ProfilePath), Purpose = NativeTermsBundle.Purpose, OperativeAgreementAdopted = false },
                Files = References.Concat(["LICENSE", "THIRD_PARTY_NOTICES.md", NativeTermsBundle.TermsPath]).Select(path => new { Path = path, TextSha256 = Hash(path) })
            }));
        }
        private string Hash(string path) => NativeTermsBundle.TextHash(File.ReadAllText(Path.Combine(Root, path)));
        public void Dispose() => Directory.Delete(Root, true);
    }
}
