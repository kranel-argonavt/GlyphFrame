using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class CharacterPresetOptionTests
{
    [Fact]
    public void BuiltIn_ContainsApprovedMvpPresetsWithDetailedAsciiAsDefault()
    {
        Assert.Collection(
            CharacterPresetOption.BuiltIn,
            preset => Assert.Equal(CharacterPresetKind.SimpleAscii, preset.Kind),
            preset => Assert.Equal(CharacterPresetKind.DetailedAscii, preset.Kind),
            preset => Assert.Equal(CharacterPresetKind.Blocks, preset.Kind));

        Assert.Equal(CharacterPresetKind.DetailedAscii, CharacterPresetOption.Default.Kind);
        Assert.Equal("░▒▓█", CharacterPresetOption.BuiltIn[2].Characters);
    }
}
