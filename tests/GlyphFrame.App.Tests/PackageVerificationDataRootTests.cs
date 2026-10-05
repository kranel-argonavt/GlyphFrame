using System.IO;
using GlyphFrame.App.Services;

namespace GlyphFrame.App.Tests;

public sealed class PackageVerificationDataRootTests
{
    [Fact]
    public void OrdinaryLaunch_DoesNotOverrideApplicationPaths()
    {
        Assert.Null(PackageVerificationDataRoot.Resolve([]));
    }

    [Theory]
    [InlineData()]
    [InlineData("relative-directory")]
    [InlineData("C:\\unmarked-package-directory")]
    public void Verification_RejectsMissingRelativeOrUnmarkedRoot(params string[] values)
    {
        Assert.Throws<ArgumentException>(() => PackageVerificationDataRoot.Resolve(
            [PackageVerificationDataRoot.Argument, .. values]));
    }

    [Fact]
    public void Verification_RejectsRealUserApplicationData()
    {
        Assert.Throws<ArgumentException>(() => PackageVerificationDataRoot.Resolve(
            [PackageVerificationDataRoot.Argument, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)]));
    }

    [Fact]
    public void Verification_AcceptsOnlyExplicitMarkedIsolatedRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "GlyphFrame.PackageVerification.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, PackageVerificationDataRoot.Marker), "test-owned");
            Assert.Equal(root, PackageVerificationDataRoot.Resolve([PackageVerificationDataRoot.Argument, root]));
        }
        finally
        {
            File.Delete(Path.Combine(root, PackageVerificationDataRoot.Marker));
            Directory.Delete(root);
        }
    }
}
