namespace Aptison.Booking.Application.Abstractions.Images;

public interface IImageProcessor
{
    /// <summary>
    /// Upright, centre-cropped to a square of <paramref name="size"/> pixels, WebP, with no
    /// metadata — which is what strips a photo's location. Null when the input cannot be decoded.
    /// </summary>
    Task<ProcessedImage?> CreateSquareWebpAsync(Stream input, int size, CancellationToken cancellationToken);

    /// <summary>
    /// Upright, scaled down to at most <paramref name="maxSide"/> pixels on its longer side and never
    /// up, WebP, with no metadata. Null when the input cannot be decoded.
    /// </summary>
    Task<ScaledImage?> CreateScaledWebpAsync(Stream input, int maxSide, CancellationToken cancellationToken);
}

public sealed record ProcessedImage(byte[] Content, string ContentType, int Width, int Height);

/// <summary>The upright size of the input, before scaling, is what a minimum-size rule checks.</summary>
public sealed record ScaledImage(ProcessedImage Image, int SourceWidth, int SourceHeight);
