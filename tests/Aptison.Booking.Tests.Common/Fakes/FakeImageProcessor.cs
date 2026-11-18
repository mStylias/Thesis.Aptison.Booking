using Aptison.Booking.Application.Abstractions.Images;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// Answers every image with a fixed WebP, or with nothing when <see cref="Decodes"/> is false. A
/// scaled image is read as <see cref="SourceWidth"/> × <see cref="SourceHeight"/>.
/// </summary>
public sealed class FakeImageProcessor : IImageProcessor
{
    private static readonly byte[] WebpBytes = [0x52, 0x49, 0x46, 0x46];

    public bool Decodes { get; set; } = true;
    public int SourceWidth { get; set; } = 1600;
    public int SourceHeight { get; set; } = 1200;

    /// <summary>Runs as an image is scaled, so a test can change the world between a handler's two reads.</summary>
    public Action? OnScale { get; set; }

    public Task<ProcessedImage?> CreateSquareWebpAsync(Stream input, int size, CancellationToken cancellationToken) =>
        Task.FromResult(Decodes ? new ProcessedImage(WebpBytes, "image/webp", size, size) : null);

    public Task<ScaledImage?> CreateScaledWebpAsync(Stream input, int maxSide, CancellationToken cancellationToken)
    {
        if (!Decodes)
        {
            return Task.FromResult<ScaledImage?>(null);
        }

        OnScale?.Invoke();

        var scale = Math.Min(1d, (double)maxSide / Math.Max(SourceWidth, SourceHeight));
        var image = new ProcessedImage(
            WebpBytes, "image/webp", (int)Math.Round(SourceWidth * scale), (int)Math.Round(SourceHeight * scale));

        return Task.FromResult<ScaledImage?>(new ScaledImage(image, SourceWidth, SourceHeight));
    }
}
