using System.Globalization;
using System.IO;
using System.Text;

namespace GlyphFrame.App.Services;

/// <summary>A separate local acceptance record, never authenticated legal proof or a Settings preference.</summary>
internal sealed class NativeTermsAcknowledgementStore(string applicationDataDirectory)
{
    internal const string FileName = "native-terms-acknowledgement.txt";
    internal const string Header = "GlyphFrame.NativeTermsAcknowledgement.1";
    private readonly string _directory = Path.GetFullPath(applicationDataDirectory);

    internal bool IsCurrent(string bundleIdentity)
    {
        try
        {
            VerifyDirectory();
            string path = Path.Combine(_directory, FileName);
            if (new FileInfo(path).Length > 1024 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) { return false; }
            return Matches(File.ReadAllLines(path, new UTF8Encoding(false, true)), bundleIdentity);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return false;
        }
    }

    internal void Save(string bundleIdentity)
    {
        if (bundleIdentity.Length != 64 || bundleIdentity.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c)))
        {
            throw new ArgumentException("A canonical bundle identity is required.", nameof(bundleIdentity));
        }
        Directory.CreateDirectory(_directory);
        VerifyDirectory();
        string path = Path.Combine(_directory, FileName);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("A linked terms record cannot be replaced.");
        }
        string temporary = Path.Combine(_directory, $".{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Format(bundleIdentity, DateTimeOffset.UtcNow));
            using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(true);
            }
            VerifyDirectory();
            File.Move(temporary, path, true);
        }
        finally
        {
            // Only the unique, owned sibling file, never a directory or other profile state.
            if (File.Exists(temporary)) { File.Delete(temporary); }
        }
    }

    internal static string Format(string identity, DateTimeOffset time) =>
        $"{Header}\nPurpose={NativeTermsBundle.Purpose}\nBundle={identity}\nAcknowledgedUtc={time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)}\n";

    internal static bool Matches(string[] lines, string identity) =>
        lines.Length == 4 && lines[0] == Header &&
        lines[1] == $"Purpose={NativeTermsBundle.Purpose}" && lines[2] == $"Bundle={identity}" &&
        lines[3].StartsWith("AcknowledgedUtc=", StringComparison.Ordinal) &&
        DateTimeOffset.TryParseExact(lines[3][16..], "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _);

    private void VerifyDirectory()
    {
        if (Directory.Exists(_directory) && (File.GetAttributes(_directory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("A linked application-data directory is not used for acknowledgement.");
        }
    }
}
