using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Core.Interfaces;

public interface ICharacterPresetProvider
{
    IReadOnlyList<CharacterPresetOption> GetPresets();

    CharacterPresetOption Get(CharacterPresetKind kind);
}
