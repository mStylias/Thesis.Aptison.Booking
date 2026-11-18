using Aptison.Booking.Application.Abstractions.Images;
using SkiaSharp;

namespace Aptison.Booking.Infrastructure.Images;

internal sealed class SkiaImageProcessor : IImageProcessor
{
    private const int WebpQuality = 85;

    public async Task<ProcessedImage?> CreateSquareWebpAsync(Stream input, int size, CancellationToken cancellationToken)
    {
        using var upright = await DecodeUprightAsync(input, cancellationToken);

        if (upright is null)
        {
            return null;
        }

        using var uprightImage = SKImage.FromBitmap(upright);

        var side = Math.Min(upright.Width, upright.Height);
        var crop = SKRect.Create((upright.Width - side) / 2f, (upright.Height - side) / 2f, side, side);

        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(
            uprightImage, crop, SKRect.Create(size, size), new SKSamplingOptions(SKCubicResampler.Mitchell), paint: null);

        // A fresh encode carries no EXIF block, which is what drops the location.
        using var square = surface.Snapshot();
        using var encoded = square.Encode(SKEncodedImageFormat.Webp, WebpQuality);

        return encoded is null ? null : new ProcessedImage(encoded.ToArray(), "image/webp", size, size);
    }

    public async Task<ScaledImage?> CreateScaledWebpAsync(Stream input, int maxSide, CancellationToken cancellationToken)
    {
        using var upright = await DecodeUprightAsync(input, cancellationToken);

        if (upright is null)
        {
            return null;
        }

        var scale = Math.Min(1d, (double)maxSide / Math.Max(upright.Width, upright.Height));
        var width = Math.Max(1, (int)Math.Round(upright.Width * scale));
        var height = Math.Max(1, (int)Math.Round(upright.Height * scale));

        using var uprightImage = SKImage.FromBitmap(upright);
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(
            uprightImage, SKRect.Create(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell), paint: null);

        using var scaled = surface.Snapshot();
        using var encoded = scaled.Encode(SKEncodedImageFormat.Webp, WebpQuality);

        return encoded is null
            ? null
            : new ScaledImage(
                new ProcessedImage(encoded.ToArray(), "image/webp", width, height), upright.Width, upright.Height);
    }

    /// <summary>Null when the input cannot be decoded.</summary>
    private static async Task<SKBitmap?> DecodeUprightAsync(Stream input, CancellationToken cancellationToken)
    {
        // SKCodec needs a seekable stream; a request body is not one.
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var codec = SKCodec.Create(buffer);

        if (codec is null)
        {
            return null;
        }

        using var decoded = SKBitmap.Decode(codec);

        if (decoded is null)
        {
            return null;
        }

        // The codec reports the EXIF orientation but does not apply it, so rotate before anything else.
        var origin = codec.EncodedFormat == SKEncodedImageFormat.Png
            ? PngExifOrientation.Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
            : codec.EncodedOrigin;

        return CreateUpright(decoded, origin);
    }

    private static SKBitmap CreateUpright(SKBitmap source, SKEncodedOrigin origin)
    {
        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        var width = swapsSides ? source.Height : source.Width;
        var height = swapsSides ? source.Width : source.Height;

        var upright = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));

        using var canvas = new SKCanvas(upright);

        // The last transform applies first to the drawn pixels.
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Scale(-1, 1);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(width, height);
                canvas.Scale(1, -1);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                break;
        }

        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default, paint: null);
        return upright;
    }
}
