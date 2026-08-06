namespace GlyphFrame.Core.Models;

public readonly record struct PixelDimensions
{
    public PixelDimensions(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public bool IsPortrait => Height > Width;

    public double AspectRatio => Width / (double)Height;
}
