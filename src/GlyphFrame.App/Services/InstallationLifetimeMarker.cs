using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GlyphFrame.App.Services;

/// <summary>Advertises a running executable to Setup without imposing single-instance behavior.</summary>
internal static class InstallationLifetimeMarker
{
    // Keep this handle rooted until process termination, including asynchronous shutdown cleanup.
    // The path hash scopes the global marker to this installation, not every portable instance.
    private static readonly Lazy<Mutex> Lifetime = new(() => CreateForExecutable(
        Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable.")));

    internal static void RegisterForCurrentProcess() => _ = Lifetime.Value;

    internal static Mutex CreateForExecutable(string executablePath) => new(false, GetName(executablePath));

    internal static string GetName(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("An absolute executable path is required.", nameof(executablePath));
        }

        string canonical = Path.GetFullPath(executablePath).ToUpperInvariant();
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(canonical))).ToLowerInvariant();
        // Matches GetSHA256OfUnicodeString(Uppercase(ExpandFileName(...))) in GlyphFrame.iss.
        return @"Global\GlyphFrame.Installation." + digest;
    }
}
