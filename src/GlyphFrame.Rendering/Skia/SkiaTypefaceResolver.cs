using SkiaSharp;

namespace GlyphFrame.Rendering.Skia;

internal static class SkiaTypefaceResolver
{
    private static readonly string[] UnicodeFallbacks = ["Segoe UI Symbol", "Segoe UI", "Arial Unicode MS"];

    public static ResolvedTypeface Resolve(string requestedFamily, string characters)
    {
        SKFontManager manager = SKFontManager.Default;
        HashSet<string> available = new(manager.FontFamilies, StringComparer.OrdinalIgnoreCase);
        List<string> candidates = [];

        if (!string.IsNullOrWhiteSpace(requestedFamily))
        {
            candidates.Add(requestedFamily);
        }

        if (!candidates.Contains("Consolas", StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add("Consolas");
        }

        foreach (string unicodeFallback in UnicodeFallbacks)
        {
            if (!candidates.Contains(unicodeFallback, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(unicodeFallback);
            }
        }

        foreach (string candidate in candidates)
        {
            if (!available.Contains(candidate))
            {
                continue;
            }

            SKTypeface? typeface = manager.MatchFamily(candidate, SKFontStyle.Normal);
            if (typeface is null)
            {
                continue;
            }

            using SKFont probe = new(typeface, 16);
            if (probe.ContainsGlyphs(characters))
            {
                bool fallback = !candidate.Equals(requestedFamily, StringComparison.OrdinalIgnoreCase);
                string? diagnostic = fallback
                    ? $"Font '{requestedFamily}' was unavailable or lacked required glyphs; '{typeface.FamilyName}' was used."
                    : null;
                return new ResolvedTypeface(typeface, typeface.FamilyName, fallback, diagnostic, ownsTypeface: true);
            }

            typeface.Dispose();
        }

        SKTypeface safeTypeface = SKTypeface.Default;
        return new ResolvedTypeface(
            safeTypeface,
            safeTypeface.FamilyName,
            true,
            $"Font '{requestedFamily}' and the preferred fallbacks were unavailable; the system default font was used.",
            ownsTypeface: false);
    }

    internal sealed class ResolvedTypeface : IDisposable
    {
        public ResolvedTypeface(
            SKTypeface typeface,
            string familyName,
            bool usedFallback,
            string? diagnostic,
            bool ownsTypeface)
        {
            Typeface = typeface;
            FamilyName = familyName;
            UsedFallback = usedFallback;
            Diagnostic = diagnostic;
            _ownsTypeface = ownsTypeface;
        }

        private readonly bool _ownsTypeface;

        public SKTypeface Typeface { get; }

        public string FamilyName { get; }

        public bool UsedFallback { get; }

        public string? Diagnostic { get; }

        public void Dispose()
        {
            if (_ownsTypeface)
            {
                Typeface.Dispose();
            }
        }
    }
}
