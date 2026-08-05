namespace GlyphFrame.Tests.TestInfrastructure;

internal sealed class TemporaryMediaDirectory : IDisposable
{
    public TemporaryMediaDirectory()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "GlyphFrame.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string CopyFixture(string fixtureName, string? destinationFileName = null)
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException($"The committed media fixture '{fixtureName}' is missing.", fixturePath);
        }

        string destinationPath = Path.Combine(RootPath, destinationFileName ?? fixtureName);
        File.Copy(fixturePath, destinationPath);
        return destinationPath;
    }

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
