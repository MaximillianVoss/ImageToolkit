using ImageToolkit.Core;

namespace ImageToolkit.Core.Tests;

public sealed class ImageProcessorTests
{
    private readonly ImageProcessor _processor = new();

    [Fact]
    public void Apply_Grayscale_UsesWeightedLuminance()
    {
        var frame = CreateFrame(1, 1, new PixelColor(200, 100, 50));

        var result = _processor.Apply(frame, new ImageOperationRequest(ImageOperationType.Grayscale));

        Assert.Equal(new PixelColor(124, 124, 124), result.GetPixel(0, 0));
    }

    [Fact]
    public void Apply_Invert_InvertsEveryRgbChannel()
    {
        var frame = CreateFrame(1, 1, new PixelColor(20, 70, 100));

        var result = _processor.Apply(frame, new ImageOperationRequest(ImageOperationType.Invert));

        Assert.Equal(new PixelColor(235, 185, 155), result.GetPixel(0, 0));
    }

    [Fact]
    public void Apply_Brightness_ClampsChannelValues()
    {
        var frame = CreateFrame(1, 1, new PixelColor(250, 10, 100));

        var result = _processor.Apply(frame, new ImageOperationRequest(ImageOperationType.Brightness, 20));

        Assert.Equal(new PixelColor(255, 30, 120), result.GetPixel(0, 0));
    }

    [Fact]
    public void Apply_Threshold_ProducesBinaryImage()
    {
        var frame = CreateFrame(
            2,
            1,
            new PixelColor(40, 40, 40),
            new PixelColor(200, 200, 200));

        var result = _processor.Apply(frame, new ImageOperationRequest(ImageOperationType.Threshold, 100));

        Assert.Equal(new PixelColor(0, 0, 0), result.GetPixel(0, 0));
        Assert.Equal(new PixelColor(255, 255, 255), result.GetPixel(1, 0));
    }

    [Fact]
    public void Apply_MirrorHorizontal_SwapsPixelsWithinRow()
    {
        var frame = CreateFrame(
            3,
            1,
            new PixelColor(255, 0, 0),
            new PixelColor(0, 255, 0),
            new PixelColor(0, 0, 255));

        var result = _processor.Apply(frame, new ImageOperationRequest(ImageOperationType.MirrorHorizontal));

        Assert.Equal(new PixelColor(0, 0, 255), result.GetPixel(0, 0));
        Assert.Equal(new PixelColor(0, 255, 0), result.GetPixel(1, 0));
        Assert.Equal(new PixelColor(255, 0, 0), result.GetPixel(2, 0));
    }

    [Fact]
    public void Apply_RotateRight_SwapsDimensionsAndMovesPixels()
    {
        var frame = CreateFrame(
            2,
            2,
            new PixelColor(255, 0, 0),
            new PixelColor(0, 255, 0),
            new PixelColor(0, 0, 255),
            new PixelColor(255, 255, 0));

        var result = _processor.Apply(frame, new ImageOperationRequest(ImageOperationType.RotateRight));

        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(new PixelColor(0, 0, 255), result.GetPixel(0, 0));
        Assert.Equal(new PixelColor(255, 0, 0), result.GetPixel(1, 0));
        Assert.Equal(new PixelColor(255, 255, 0), result.GetPixel(0, 1));
        Assert.Equal(new PixelColor(0, 255, 0), result.GetPixel(1, 1));
    }

    private static ImageFrame CreateFrame(int width, int height, params PixelColor[] pixels)
    {
        Assert.Equal(width * height, pixels.Length);

        var buffer = new byte[pixels.Length * ImageFrame.BytesPerPixel];
        for (var index = 0; index < pixels.Length; index++)
        {
            var pixel = pixels[index];
            var offset = index * ImageFrame.BytesPerPixel;

            buffer[offset] = pixel.B;
            buffer[offset + 1] = pixel.G;
            buffer[offset + 2] = pixel.R;
            buffer[offset + 3] = pixel.A;
        }

        return new ImageFrame(width, height, buffer);
    }
}
