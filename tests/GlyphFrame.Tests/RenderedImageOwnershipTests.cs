using GlyphFrame.Core.Models;

namespace GlyphFrame.Tests;

public sealed class RenderedImageOwnershipTests
{
    [Fact]
    public void Dispose_ReleasesOwnedPixelsAndIsIdempotent()
    {
        RenderedImage image = new(1, 1, [1, 2, 3, 255]);

        image.Dispose();
        image.Dispose();

        Assert.True(image.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = image.RgbaPixels);
    }
}
