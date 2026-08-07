using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Rendering.Mapping;

namespace GlyphFrame.Tests;

public sealed class BrightnessToSymbolMapperTests
{
    private readonly BrightnessToSymbolMapper _mapper = new();

    public static TheoryData<CharacterPresetOption, SymbolPolarity, char, char> PresetEndpoints
    {
        get
        {
            TheoryData<CharacterPresetOption, SymbolPolarity, char, char> data = new();
            foreach (CharacterPresetOption preset in CharacterPresetOption.BuiltIn)
            {
                data.Add(preset, SymbolPolarity.LightOnDark, preset.Characters[0], preset.Characters[^1]);
                data.Add(preset, SymbolPolarity.DarkOnLight, preset.Characters[^1], preset.Characters[0]);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PresetEndpoints))]
    public void Map_MapsPresetEndpointsByPolarity(
        CharacterPresetOption preset,
        SymbolPolarity polarity,
        char expectedBlack,
        char expectedWhite)
    {
        Assert.Equal(expectedBlack, _mapper.Map(0, preset.Characters, polarity));
        Assert.Equal(expectedWhite, _mapper.Map(255, preset.Characters, polarity));
    }

    [Theory]
    [InlineData(SymbolPolarity.LightOnDark, 0, ' ')]
    [InlineData(SymbolPolarity.LightOnDark, 63, ' ')]
    [InlineData(SymbolPolarity.LightOnDark, 64, '.')]
    [InlineData(SymbolPolarity.LightOnDark, 128, '.')]
    [InlineData(SymbolPolarity.LightOnDark, 191, '.')]
    [InlineData(SymbolPolarity.LightOnDark, 192, '#')]
    [InlineData(SymbolPolarity.LightOnDark, 255, '#')]
    [InlineData(SymbolPolarity.DarkOnLight, 0, '#')]
    [InlineData(SymbolPolarity.DarkOnLight, 63, '#')]
    [InlineData(SymbolPolarity.DarkOnLight, 64, '.')]
    [InlineData(SymbolPolarity.DarkOnLight, 128, '.')]
    [InlineData(SymbolPolarity.DarkOnLight, 191, '.')]
    [InlineData(SymbolPolarity.DarkOnLight, 192, ' ')]
    [InlineData(SymbolPolarity.DarkOnLight, 255, ' ')]
    public void Map_UsesDeterministicRoundedBoundaries(
        SymbolPolarity polarity,
        byte luminance,
        char expected)
    {
        Assert.Equal(expected, _mapper.Map(luminance, " .#", polarity));
        Assert.Equal(expected, _mapper.Map(luminance, " .#", polarity));
    }

    [Theory]
    [InlineData(SymbolPolarity.LightOnDark)]
    [InlineData(SymbolPolarity.DarkOnLight)]
    public void Map_OneCharacterRampAlwaysReturnsThatCharacter(SymbolPolarity polarity)
    {
        Assert.Equal('X', _mapper.Map(0, "X", polarity));
        Assert.Equal('X', _mapper.Map(128, "X", polarity));
        Assert.Equal('X', _mapper.Map(255, "X", polarity));
    }

    [Fact]
    public void Map_RejectsEmptyCharacterSet()
    {
        Assert.Throws<ArgumentException>(() =>
            _mapper.Map(128, string.Empty, SymbolPolarity.LightOnDark));
    }

    [Fact]
    public void Map_RejectsUndefinedPolarity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _mapper.Map(128, " .#", (SymbolPolarity)999));
    }
}
