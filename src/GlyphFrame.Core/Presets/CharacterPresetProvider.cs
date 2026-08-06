using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Interfaces;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Presets;

public sealed class CharacterPresetProvider : ICharacterPresetProvider
{
    public IReadOnlyList<CharacterPresetOption> GetPresets() => CharacterPresetOption.BuiltIn;

    public CharacterPresetOption Get(CharacterPresetKind kind) =>
        CharacterPresetOption.BuiltIn.FirstOrDefault(preset => preset.Kind == kind)
        ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported character preset.");
}
