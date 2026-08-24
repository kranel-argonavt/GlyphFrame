using GlyphFrame.Core.Enums;
using GlyphFrame.Core.Models;
using GlyphFrame.Core.Policies;
using System.Globalization;

namespace GlyphFrame.Tests;

public sealed class AudioCompatibilityPolicyTests
{
    private readonly Mp4AudioCompatibilityPolicy _policy = new();

    [Theory]
    [InlineData("aac")]
    [InlineData("AAC")]
    [InlineData(" aAc ")]
    public void ResolveStrategy_AacUsesStreamCopy(string codec) =>
        Assert.Equal(AudioMuxStrategy.StreamCopy, _policy.ResolveStrategy(codec));

    [Theory]
    [InlineData("pcm_s16le")]
    [InlineData("opus")]
    [InlineData("vorbis")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveStrategy_IncompatibleOrUnknownCodecUsesAac(string? codec) =>
        Assert.Equal(AudioMuxStrategy.TranscodeToAac, _policy.ResolveStrategy(codec));

    [Fact]
    public void FormatDuration_UsesInvariantFractionalSeconds()
    {
        using CultureScope culture = new("de-DE");

        Assert.Equal("1.234567", AudioMuxPolicy.FormatDuration(TimeSpan.FromTicks(12_345_670)));
    }

    [Fact]
    public void FormatDuration_RejectsNonPositiveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioMuxPolicy.FormatDuration(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioMuxPolicy.FormatDuration(TimeSpan.FromSeconds(-1)));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string cultureName)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
