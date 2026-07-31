using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class ApplicationInfoTests
{
    [Fact]
    public void Phase2_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase2;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 2 — Repository and Solution", applicationInfo.Status);
    }
}
