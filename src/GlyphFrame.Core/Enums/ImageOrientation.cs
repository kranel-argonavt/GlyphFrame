namespace GlyphFrame.Core.Enums;

public enum ImageOrientation
{
    Unspecified = 0,
    Normal = 1,
    MirroredHorizontally = 2,
    Rotated180 = 3,
    MirroredVertically = 4,
    MirroredHorizontallyThenRotated270Clockwise = 5,
    Rotated90Clockwise = 6,
    MirroredHorizontallyThenRotated90Clockwise = 7,
    Rotated270Clockwise = 8
}

public static class ImageOrientationExtensions
{
    public static bool SwapsDimensions(this ImageOrientation orientation) =>
        orientation is ImageOrientation.MirroredHorizontallyThenRotated270Clockwise
            or ImageOrientation.Rotated90Clockwise
            or ImageOrientation.MirroredHorizontallyThenRotated90Clockwise
            or ImageOrientation.Rotated270Clockwise;
}
