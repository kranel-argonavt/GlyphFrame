using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using GlyphFrame.App.Views;

namespace GlyphFrame.App.Services;

/// <summary>Opens the shipped document as inert, selectable local text, only on activation.</summary>
public sealed class ThirdPartyNoticesService
{
    private readonly string _directory;
    private readonly Action<Window, string> _showDocument;

    public ThirdPartyNoticesService() : this(AppContext.BaseDirectory, (owner, text) =>
        new NoticesWindow(text) { Owner = owner }.ShowDialog())
    { }

    internal ThirdPartyNoticesService(string installationDirectory, Action<Window, string> showDocument)
    {
        _directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installationDirectory));
        _showDocument = showDocument;
    }

    public string? Show(Window owner)
    {
        string text;
        try { text = ReadDocuments(); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException
            or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException)
        {
            return "The third-party notices could not be read. Check that THIRD_PARTY_NOTICES.md is present and readable beside GlyphFrame, together with LICENSE, licenses/ and Tools/ffmpeg/ legal documents, or extract the complete portable package again.";
        }
        _showDocument(owner, text);
        return null;
    }

    private string ReadDocuments()
    {
        // Only local inert text, with no URL fetching or arbitrary manifest-directed file access.
        string manifestPath = ResolveDocument("licenses/manifest.json", manifest: true);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (manifest.RootElement.GetProperty("SchemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException("Unsupported legal manifest.");
        }
        Dictionary<string, string> documents = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement file in manifest.RootElement.GetProperty("Files").EnumerateArray())
        {
            string relative = file.GetProperty("Path").GetString() ?? throw new InvalidDataException();
            string document = File.ReadAllText(ResolveDocument(relative));
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document.Replace("\r\n", "\n")))).ToLowerInvariant();
            if (document.Length == 0 || hash != file.GetProperty("TextSha256").GetString()
                || !documents.TryAdd(relative, document))
            {
                throw new InvalidDataException("Missing, changed or duplicate legal document.");
            }
        }
        if (!documents.ContainsKey("LICENSE") || !documents.ContainsKey("THIRD_PARTY_NOTICES.md"))
        {
            throw new InvalidDataException("Legal manifest omits the application license or notices.");
        }
        StringBuilder text = new(documents["THIRD_PARTY_NOTICES.md"]);
        foreach ((string relative, string document) in documents)
        {
            if (relative.Equals("THIRD_PARTY_NOTICES.md", StringComparison.OrdinalIgnoreCase)) { continue; }
            text.AppendLine().AppendLine().Append("===== ").Append(relative).AppendLine(" =====").Append(document);
        }
        return text.ToString();
    }

    private string ResolveDocument(string relative, bool manifest = false)
    {
        bool allowed = manifest ? relative == "licenses/manifest.json"
            : relative is "LICENSE" or "THIRD_PARTY_NOTICES.md" or "Tools/ffmpeg/LICENSE.txt" or "Tools/ffmpeg/DISTRIBUTION_README.txt"
                || (relative.StartsWith("licenses/", StringComparison.Ordinal)
                    && (relative.EndsWith(".txt", StringComparison.Ordinal) || relative.EndsWith(".md", StringComparison.Ordinal)));
        if (!allowed || relative.Contains('\\') || relative.Contains(':') || relative.Split('/').Any(segment =>
            segment is "" or "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || segment.EndsWith('.') || segment.EndsWith(' ')))
        {
            throw new InvalidDataException("Unsafe legal document path.");
        }
        string path = Path.GetFullPath(Path.Combine(_directory, relative));
        string? current = path;
        while (current is not null)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Linked legal documents are not followed.");
            }
            if (current.Equals(_directory, StringComparison.OrdinalIgnoreCase)) { return path; }
            current = Path.GetDirectoryName(current);
        }
        throw new InvalidDataException("Legal document is outside the application directory.");
    }
}
