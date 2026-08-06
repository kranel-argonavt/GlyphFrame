using GlyphFrame.Rendering.Mapping;

namespace GlyphFrame.Tests;

public sealed class BrightnessToSymbolMapperTests
{
    private readonly BrightnessToSymbolMapper _mapper = new();

    [Fact]
    public void Map_MapsBlackToDensestAndWhiteToLightestCharacter()
    {
        const string ramp = " .#";

        Assert.Equal('#', _mapper.Map(0, ramp));
        Assert.Equal(' ', _mapper.Map(255, ramp));
    }

    [Theory]
    [InlineData(191, '.')]
    [InlineData(128, '.')]
    [InlineData(64, '.')]
    [InlineData(63, '#')]
    public void Map_UsesDeterministicClampedBoundaries(byte luminance, char expected)
    {
        Assert.Equal(expected, _mapper.Map(luminance, " .#"));
    }

    [Fact]
    public void Map_RejectsEmptyCharacterSet()
    {
        Assert.Throws<ArgumentException>(() => _mapper.Map(128, string.Empty));
    }
}
