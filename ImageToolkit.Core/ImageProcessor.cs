namespace ImageToolkit.Core;

public sealed class ImageProcessor
{
    public ImageFrame Apply(ImageFrame image, ImageOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(image);

        return request.Type switch
        {
            ImageOperationType.None => image.Clone(),
            ImageOperationType.Grayscale => ApplyGrayscale(image),
            ImageOperationType.Invert => ApplyInvert(image),
            ImageOperationType.Brightness => ApplyBrightness(image, request.Parameter),
            ImageOperationType.Threshold => ApplyThreshold(image, request.Parameter),
            ImageOperationType.MirrorHorizontal => ApplyMirrorHorizontal(image),
            ImageOperationType.RotateRight => ApplyRotateRight(image),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Type, "Unsupported image operation.")
        };
    }

    private static ImageFrame ApplyGrayscale(ImageFrame image)
    {
        var source = image.AsReadOnlySpan();
        var result = new byte[source.Length];

        for (var index = 0; index < source.Length; index += ImageFrame.BytesPerPixel)
        {
            var blue = source[index];
            var green = source[index + 1];
            var red = source[index + 2];
            var alpha = source[index + 3];
            var luminance = (byte)Math.Round((0.114 * blue) + (0.587 * green) + (0.299 * red));

            result[index] = luminance;
            result[index + 1] = luminance;
            result[index + 2] = luminance;
            result[index + 3] = alpha;
        }

        return new ImageFrame(image.Width, image.Height, result);
    }

    private static ImageFrame ApplyInvert(ImageFrame image)
    {
        var source = image.AsReadOnlySpan();
        var result = new byte[source.Length];

        for (var index = 0; index < source.Length; index += ImageFrame.BytesPerPixel)
        {
            result[index] = (byte)(255 - source[index]);
            result[index + 1] = (byte)(255 - source[index + 1]);
            result[index + 2] = (byte)(255 - source[index + 2]);
            result[index + 3] = source[index + 3];
        }

        return new ImageFrame(image.Width, image.Height, result);
    }

    private static ImageFrame ApplyBrightness(ImageFrame image, int delta)
    {
        var source = image.AsReadOnlySpan();
        var result = new byte[source.Length];

        for (var index = 0; index < source.Length; index += ImageFrame.BytesPerPixel)
        {
            result[index] = ClampToByte(source[index] + delta);
            result[index + 1] = ClampToByte(source[index + 1] + delta);
            result[index + 2] = ClampToByte(source[index + 2] + delta);
            result[index + 3] = source[index + 3];
        }

        return new ImageFrame(image.Width, image.Height, result);
    }

    private static ImageFrame ApplyThreshold(ImageFrame image, int threshold)
    {
        var source = image.AsReadOnlySpan();
        var result = new byte[source.Length];
        var limitedThreshold = Math.Clamp(threshold, 0, 255);

        for (var index = 0; index < source.Length; index += ImageFrame.BytesPerPixel)
        {
            var blue = source[index];
            var green = source[index + 1];
            var red = source[index + 2];
            var alpha = source[index + 3];
            var luminance = (byte)Math.Round((0.114 * blue) + (0.587 * green) + (0.299 * red));
            var output = luminance >= limitedThreshold ? (byte)255 : (byte)0;

            result[index] = output;
            result[index + 1] = output;
            result[index + 2] = output;
            result[index + 3] = alpha;
        }

        return new ImageFrame(image.Width, image.Height, result);
    }

    private static ImageFrame ApplyMirrorHorizontal(ImageFrame image)
    {
        var source = image.AsReadOnlySpan();
        var result = new byte[source.Length];

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var sourceOffset = ((y * image.Width) + x) * ImageFrame.BytesPerPixel;
                var targetOffset = ((y * image.Width) + (image.Width - 1 - x)) * ImageFrame.BytesPerPixel;

                source.Slice(sourceOffset, ImageFrame.BytesPerPixel).CopyTo(result.AsSpan(targetOffset, ImageFrame.BytesPerPixel));
            }
        }

        return new ImageFrame(image.Width, image.Height, result);
    }

    private static ImageFrame ApplyRotateRight(ImageFrame image)
    {
        var source = image.AsReadOnlySpan();
        var targetWidth = image.Height;
        var targetHeight = image.Width;
        var result = new byte[targetWidth * targetHeight * ImageFrame.BytesPerPixel];

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var sourceOffset = ((y * image.Width) + x) * ImageFrame.BytesPerPixel;
                var targetX = image.Height - 1 - y;
                var targetY = x;
                var targetOffset = ((targetY * targetWidth) + targetX) * ImageFrame.BytesPerPixel;

                source.Slice(sourceOffset, ImageFrame.BytesPerPixel).CopyTo(result.AsSpan(targetOffset, ImageFrame.BytesPerPixel));
            }
        }

        return new ImageFrame(targetWidth, targetHeight, result);
    }

    private static byte ClampToByte(int value)
    {
        return (byte)Math.Clamp(value, 0, 255);
    }
}
