namespace ImageToolkit.Core;

public sealed class ImageFrame
{
    public const int BytesPerPixel = 4;

    private readonly byte[] _pixels;

    public ImageFrame(int width, int height, byte[] pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be positive.");
        }

        ArgumentNullException.ThrowIfNull(pixels);

        var expectedLength = checked(width * height * BytesPerPixel);
        if (pixels.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Expected {expectedLength} bytes for a {width}x{height} BGRA32 image, but received {pixels.Length}.",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        _pixels = pixels.ToArray();
    }

    public int Width { get; }

    public int Height { get; }

    public ImageFrame Clone()
    {
        return new ImageFrame(Width, Height, _pixels);
    }

    public byte[] CopyPixels()
    {
        return _pixels.ToArray();
    }

    public PixelColor GetPixel(int x, int y)
    {
        var offset = GetOffset(x, y);
        return new PixelColor(
            R: _pixels[offset + 2],
            G: _pixels[offset + 1],
            B: _pixels[offset],
            A: _pixels[offset + 3]);
    }

    internal ReadOnlySpan<byte> AsReadOnlySpan()
    {
        return _pixels;
    }

    private int GetOffset(int x, int y)
    {
        if (x < 0 || x >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return ((y * Width) + x) * BytesPerPixel;
    }
}
