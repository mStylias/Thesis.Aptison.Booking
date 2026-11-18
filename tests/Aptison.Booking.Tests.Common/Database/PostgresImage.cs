using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;

namespace Aptison.Booking.Tests.Common.Database;

/// <summary>
/// Builds the repository's own <c>Dockerfile.postgres</c> so tests run against the same image as
/// development. A stock Postgres image cannot run the migrations: they need PostGIS and
/// <c>pg_partman</c>, and <c>pg_partman</c> is an OS package rather than a bundled extension.
/// </summary>
public static class PostgresImage
{
    private const string Tag = "aptison-booking-postgres-tests:18-3.6-partman";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static IFutureDockerImage? _image;

    /// <summary>
    /// Builds the image once per test session and returns it. The Docker layer cache makes every
    /// run after the first one near-instant, and <c>WithCleanUp(false)</c> is what keeps Ryuk from
    /// deleting it at session end and forcing the <c>apt-get</c> layer to be rebuilt next time.
    /// </summary>
    public static async Task<IFutureDockerImage> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (_image is not null)
        {
            return _image;
        }

        await Gate.WaitAsync(cancellationToken);

        try
        {
            if (_image is not null)
            {
                return _image;
            }

            var image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(CommonDirectoryPath.GetGitDirectory(), string.Empty)
                .WithDockerfile("Dockerfile.postgres")
                .WithName(Tag)
                .WithCleanUp(false)
                .Build();

            await image.CreateAsync(cancellationToken);

            _image = image;
            return image;
        }
        finally
        {
            Gate.Release();
        }
    }
}
