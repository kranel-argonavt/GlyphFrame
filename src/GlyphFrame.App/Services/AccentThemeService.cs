using System.Windows.Media;
using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Services;

public sealed class AccentThemeService : IAccentThemeService
{
    private static readonly IReadOnlyDictionary<AccentPreset, AccentPalette> Palettes =
        new Dictionary<AccentPreset, AccentPalette>
        {
            [AccentPreset.Violet] = new("#8B5CF6", "#A78BFA", "#7C3AED", "#2E2149"),
            [AccentPreset.Purple] = new("#A855F7", "#C084FC", "#9333EA", "#342047"),
            [AccentPreset.Blue] = new("#3B82F6", "#60A5FA", "#2563EB", "#172A49"),
            [AccentPreset.Cyan] = new("#06B6D4", "#22D3EE", "#0891B2", "#12333B"),
            [AccentPreset.Emerald] = new("#10B981", "#34D399", "#059669", "#15352D"),
            [AccentPreset.Rose] = new("#F43F5E", "#FB7185", "#E11D48", "#421F2A")
        };

    public void Apply(AccentPreset preset)
    {
        AccentPalette palette = Palettes.TryGetValue(preset, out AccentPalette? selected)
            ? selected
            : Palettes[AccentPreset.Violet];

        UpdateBrush("AccentBrush", palette.Primary);
        UpdateBrush("AccentHoverBrush", palette.Hover);
        UpdateBrush("AccentPressedBrush", palette.Pressed);
        UpdateBrush("AccentSubtleBrush", palette.Subtle);
    }

    public string GetPrimaryColorHex(AccentPreset preset) =>
        Palettes.TryGetValue(preset, out AccentPalette? palette)
            ? palette.Primary
            : Palettes[AccentPreset.Violet].Primary;

    private static void UpdateBrush(string resourceKey, string colorValue)
    {
        Color color = (Color)ColorConverter.ConvertFromString(colorValue);
        System.Windows.Application.Current.Resources[resourceKey] = new SolidColorBrush(color);
    }

    private sealed record AccentPalette(string Primary, string Hover, string Pressed, string Subtle);
}
