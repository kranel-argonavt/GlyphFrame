using System.IO;

namespace GlyphFrame.App.Services;

/// <summary>
/// Explicit opt-in for the package verifier; ordinary launches retain their user-local paths.
/// A marker prevents an accidentally supplied directory from becoming smoke-test storage.
/// </summary>
internal static class PackageVerificationDataRoot
{
    internal const string Argument = "--verification-data-root";
    internal const string Marker = ".glyphframe-package-verification";

    public static string? Resolve(string[] arguments)
    {
        int index = Array.IndexOf(arguments, Argument);
        if (index < 0)
        {
            return null;
        }
        if (arguments.Length != 2 || index != 0 || !Path.IsPathFullyQualified(arguments[1]))
        {
            throw new ArgumentException("Package verification requires one absolute, isolated data root.");
        }
        string root = Path.GetFullPath(arguments[1]);
        string userRoot = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        if (root.TrimEnd(Path.DirectorySeparatorChar).Equals(userRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || root.Equals(Path.Combine(userRoot, "GlyphFrame"), StringComparison.OrdinalIgnoreCase)
            || root.StartsWith(Path.Combine(userRoot, "GlyphFrame") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Path.Combine(root, Marker)))
        {
            throw new ArgumentException("Package verification requires a marked directory outside user application data.");
        }
        return root;
    }
}
