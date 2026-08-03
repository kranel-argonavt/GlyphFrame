using GlyphFrame.Core.Enums;

namespace GlyphFrame.App.Services;

public interface IAccentThemeService
{
    void Apply(AccentPreset preset);

    string GetPrimaryColorHex(AccentPreset preset);
}
