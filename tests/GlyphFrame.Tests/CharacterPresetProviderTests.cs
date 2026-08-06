using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Presets;

namespace GlyphFrame.Tests;

public sealed class CharacterPresetProviderTests
{
    private readonly CharacterPresetProvider _provider = new();

    [Fact]
    public void GetPresets_ReturnsExactApprovedPresetsInStableOrder()
    {
        var presets = _provider.GetPresets();

        Assert.Collection(
            presets,
            preset =>
            {
                Assert.Equal(CharacterPresetKind.SimpleAscii, preset.Kind);
                Assert.Equal("Simple ASCII", preset.Name);
                Assert.Equal(" .:-=+*#%@", preset.Characters);
                Assert.False(preset.IsUnicode);
            },
            preset =>
            {
                Assert.Equal(CharacterPresetKind.DetailedAscii, preset.Kind);
                Assert.Equal("Detailed ASCII", preset.Name);
                Assert.Equal(" .'`^\",:;Il!i~+_-?][}{1)(|\\/tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$", preset.Characters);
                Assert.False(preset.IsUnicode);
            },
            preset =>
            {
                Assert.Equal(CharacterPresetKind.Blocks, preset.Kind);
                Assert.Equal("Blocks", preset.Name);
                Assert.Equal(" ░▒▓█", preset.Characters);
                Assert.True(preset.IsUnicode);
            });

        Assert.All(presets, preset => Assert.StartsWith(" ", preset.Characters, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(CharacterPresetKind.SimpleAscii, 10)]
    [InlineData(CharacterPresetKind.DetailedAscii, 68)]
    [InlineData(CharacterPresetKind.Blocks, 5)]
    public void Get_ReturnsPresetWithExpectedRampLength(CharacterPresetKind kind, int expectedLength)
    {
        Assert.Equal(expectedLength, _provider.Get(kind).Characters.Length);
    }
}
