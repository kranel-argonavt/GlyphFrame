using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlyphFrame.App.Services;

/// <summary>Content identity and deployment scope, not a legal-compliance or authenticity certificate.</summary>
internal sealed record NativeTermsBundle(string Identity, string Version, string Text, string References, string Notices)
{
    internal const string Purpose = "ComponentTermsAcceptance";
    internal const string Edition = "1.0";
    internal const string ProfilePath = "licenses/WindowsNative/profile.json";
    internal const string TermsPath = "licenses/WindowsNative/COMPONENT-TERMS.txt";
    internal static readonly string[] ComponentNames =
    ["coreclr.dll", "PresentationNative_cor3.dll", "vcruntime140_cor3.dll", "wpfgfx_cor3.dll", "D3DCompiler_47_cor3.dll"];
    private static readonly string[] ReferencePaths =
    ["licenses/dotnet/NET-LIBRARY-LICENSE.txt", "licenses/WPF/WINDOWS-SDK-LICENSE.txt", "licenses/dotnet/WINDOWS-LICENSE-MAPPING.txt"];

    internal static NativeTermsBundle? Load(string directory)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        using JsonDocument runtime = JsonDocument.Parse(Read(root, "GlyphFrame.App.runtimeconfig.json", 65536));
        JsonElement options = runtime.RootElement.GetProperty("runtimeOptions");
        bool selfContained = options.TryGetProperty("includedFrameworks", out JsonElement included);
        bool frameworkDependent = options.TryGetProperty("framework", out _) || options.TryGetProperty("frameworks", out _);
        int present = ComponentNames.Count(name => File.Exists(Path.Combine(root, name)));
        if (!selfContained && frameworkDependent && present == 0) { return null; }
        if (!selfContained || present != ComponentNames.Length)
        {
            throw new InvalidDataException("The bundled Microsoft runtime scope is missing, partial or inconsistent.");
        }

        string profileText = Read(root, ProfilePath, 65536);
        using JsonDocument profile = JsonDocument.Parse(profileText);
        JsonElement p = profile.RootElement;
        string identity = TextHash(profileText);
        using JsonDocument manifest = JsonDocument.Parse(Read(root, "licenses/manifest.json", 262144));
        JsonElement binding = manifest.RootElement.GetProperty("WindowsNativeTerms");
        if (p.GetProperty("SchemaVersion").GetInt32() != 1 ||
            p.GetProperty("Purpose").GetString() != Purpose ||
            p.GetProperty("TermsPath").GetString() != TermsPath ||
            p.GetProperty("Version").GetString() != Edition ||
            binding.GetProperty("ProfilePath").GetString() != ProfilePath ||
            binding.GetProperty("Purpose").GetString() != Purpose ||
            !binding.GetProperty("OperativeAgreementAdopted").GetBoolean() ||
            binding.GetProperty("ProfileTextSha256").GetString() != identity)
        {
            throw new InvalidDataException("Unreviewed Microsoft component profile.");
        }
        JsonElement frameworks = p.GetProperty("RuntimeFrameworks");
        JsonElement[] actualFrameworks = included.EnumerateArray().ToArray();
        if (frameworks.EnumerateObject().Count() != 2 || actualFrameworks.Length != 2 ||
            new[] { "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App" }.Any(name =>
                actualFrameworks.Count(f => f.GetProperty("name").GetString() == name &&
                    f.GetProperty("version").GetString() == frameworks.GetProperty(name).GetString()) != 1))
        {
            throw new InvalidDataException("The included runtime has not received this component review.");
        }
        JsonElement[] components = p.GetProperty("Components").EnumerateArray().ToArray();
        if (components.Length != ComponentNames.Length) { throw new InvalidDataException("Incomplete component scope."); }
        foreach (string name in ComponentNames)
        {
            JsonElement[] entry = components.Where(c => c.GetProperty("Path").GetString() == name).ToArray();
            if (entry.Length != 1) { throw new InvalidDataException("Missing or duplicate Microsoft component."); }
            string path = Resolve(root, name);
            using FileStream file = File.OpenRead(path);
            if (file.Length != entry[0].GetProperty("Length").GetInt64() ||
                Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant() != entry[0].GetProperty("Sha256").GetString())
            {
                throw new InvalidDataException("The bundled Microsoft component differs from its reviewed identity.");
            }
        }
        string text = Read(root, TermsPath, 131072);
        if (TextHash(text) != p.GetProperty("TermsTextSha256").GetString() ||
            !text.Contains("VERSION 1.0 — 29 OCTOBER 2026", StringComparison.Ordinal) ||
            text.Contains("NONOPERATIVE REVIEW DRAFT", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Missing or changed Microsoft component terms.");
        }
        JsonElement[] references = p.GetProperty("References").EnumerateArray().ToArray();
        if (references.Length != ReferencePaths.Length) { throw new InvalidDataException("Incomplete reference texts."); }
        StringBuilder referenceText = new();
        foreach (string relative in ReferencePaths)
        {
            JsonElement[] entry = references.Where(r => r.GetProperty("Path").GetString() == relative).ToArray();
            string reference = Read(root, relative, 262144);
            if (entry.Length != 1 || TextHash(reference) != entry[0].GetProperty("TextSha256").GetString())
            {
                throw new InvalidDataException("Missing, duplicate or changed Microsoft reference text.");
            }
            referenceText.AppendLine(relative).AppendLine().AppendLine(reference).AppendLine();
        }
        // Existing inert-text reader independently verifies every indexed text, including the component terms.
        string notices = new ThirdPartyNoticesService(root, (_, _) => { }).ReadDocuments();
        return new(identity, p.GetProperty("Version").GetString() ?? throw new InvalidDataException(),
            text, referenceText.ToString(), notices);
    }

    internal static string TextHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n")))).ToLowerInvariant();

    private static string Read(string root, string relative, int maximum)
    {
        string path = Resolve(root, relative);
        if (new FileInfo(path).Length is <= 0 || new FileInfo(path).Length > maximum)
        {
            throw new InvalidDataException("Missing or oversized component document.");
        }
        return File.ReadAllText(path, new UTF8Encoding(false, true));
    }

    private static string Resolve(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Component path is outside the package.");
        }
        string? current = path;
        while (current is not null)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Linked component documents are not followed.");
            }
            if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) { return path; }
            current = Path.GetDirectoryName(current);
        }
        throw new InvalidDataException("Unsafe component path.");
    }
}
