using System.IO;
using System.Security.Cryptography;
using System.Text;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class InstallationLifetimeMarkerTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("GlyphFrame.App.exe")]
    public void RelativeOrEmptyPaths_AreRejected(string path) =>
        Assert.ThrowsAny<ArgumentException>(() => InstallationLifetimeMarker.GetName(path));

    [Fact]
    public void CanonicalCaseInsensitivePath_UsesUtf16Sha256Protocol()
    {
        const string path = @"C:\Users\Example\Programs\GlyphFrame\GlyphFrame.App.exe";
        string expected = @"Global\GlyphFrame.Installation." + Convert.ToHexString(
            SHA256.HashData(Encoding.Unicode.GetBytes(path.ToUpperInvariant()))).ToLowerInvariant();
        Assert.Equal(expected, InstallationLifetimeMarker.GetName(path));
        Assert.Equal(expected, InstallationLifetimeMarker.GetName(path.ToLowerInvariant()));
        Assert.Equal(expected, InstallationLifetimeMarker.GetName(
            @"C:\Users\Example\Programs\GlyphFrame\unused\..\GlyphFrame.App.exe"));
    }

    [Fact]
    public void DifferentInstallations_DoNotBlockEachOther() => Assert.NotEqual(
        InstallationLifetimeMarker.GetName(@"C:\one\GlyphFrame.App.exe"),
        InstallationLifetimeMarker.GetName(@"C:\two\GlyphFrame.App.exe"));

    [Fact]
    public void MultipleInstances_KeepMarkerUntilLastHandleCloses()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "GlyphFrame.App.exe");
        string name = InstallationLifetimeMarker.GetName(path);
        using (Mutex first = InstallationLifetimeMarker.CreateForExecutable(path))
        {
            using (Mutex second = InstallationLifetimeMarker.CreateForExecutable(path)) { Assert.True(Exists(name)); }
            Assert.True(Exists(name));
        }

        Assert.False(Exists(name));
    }

    private static bool Exists(string name)
    {
        bool found = Mutex.TryOpenExisting(name, out Mutex? handle);
        handle?.Dispose();
        return found;
    }
}
