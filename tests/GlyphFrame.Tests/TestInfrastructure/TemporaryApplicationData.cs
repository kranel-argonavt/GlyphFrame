using GlyphFrame.Infrastructure.Paths;

namespace GlyphFrame.Tests.TestInfrastructure;

internal sealed class TemporaryApplicationData : IDisposable
{
    public TemporaryApplicationData()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "GlyphFrame.Tests",
            Guid.NewGuid().ToString("N"));
        Paths = new ApplicationPaths(RootPath);
    }

    public string RootPath { get; }

    public ApplicationPaths Paths { get; }

    public void Dispose()
    {
        string resolvedRoot = Path.GetFullPath(RootPath);
        string expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GlyphFrame.Tests"));

        if (resolvedRoot.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(resolvedRoot))
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
    }
}
