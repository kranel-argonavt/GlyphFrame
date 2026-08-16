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
        Assert.Equal("Phase 2 - Repository and Solution", applicationInfo.Status);
    }

    [Fact]
    public void Phase3_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase3;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 3 - UI Skeleton", applicationInfo.Status);
    }

    [Fact]
    public void Phase4_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase4;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 4 - Settings Infrastructure", applicationInfo.Status);
    }

    [Fact]
    public void Phase5_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase5;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 5 - Media Input", applicationInfo.Status);
    }

    [Fact]
    public void Phase6_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase6;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 6 - Image Pipeline", applicationInfo.Status);
    }

    [Fact]
    public void Phase7_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase7;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 7 - Bundled FFmpeg", applicationInfo.Status);
    }

    [Fact]
    public void Phase8_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase8;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 8 - Video Preview", applicationInfo.Status);
    }
}
