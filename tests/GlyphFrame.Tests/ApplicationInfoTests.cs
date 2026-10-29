using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class ApplicationInfoTests
{
    [Fact]
    public void Phase11B4_IdentifiesReviewedArtifactPublication()
    {
        Assert.Equal("Phase 11B.4 - Publish the Reviewed Video Artifact", ApplicationInfo.Phase11B4.Status);
    }
}
