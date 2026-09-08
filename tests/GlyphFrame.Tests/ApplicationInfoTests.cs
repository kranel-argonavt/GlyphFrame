using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class ApplicationInfoTests
{
    [Fact]
    public void Phase11B4_IdentifiesReviewedArtifactPublication()
    {
        Assert.Equal("Phase 11B.4 - Publish the Reviewed Video Artifact", ApplicationInfo.Phase11B4.Status);
    }
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

    [Fact]
    public void Phase9_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase9;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 9 - Streaming Video Export", applicationInfo.Status);
    }

    [Fact]
    public void Phase10_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase10;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 10 - Audio", applicationInfo.Status);
    }

    [Fact]
    public void Phase11_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase11;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 11 - Progress and Cancellation", applicationInfo.Status);
    }

    [Fact]
    public void Phase11B2_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase11B2;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 11B.2 - Managed Video Artifact Foundation", applicationInfo.Status);
    }

    [Fact]
    public void Phase11B3_ProvidesExpectedApplicationIdentityAndStatus()
    {
        ApplicationInfo applicationInfo = ApplicationInfo.Phase11B3;

        Assert.Equal("GlyphFrame", applicationInfo.Name);
        Assert.Equal("Video & Image to Symbol Converter", applicationInfo.Description);
        Assert.Equal("Phase 11B.3 - Full Video Render and Review UI", applicationInfo.Status);
    }
}
