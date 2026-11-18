namespace Aptison.Booking.Api.ClientAddress;

/// <summary>
/// Who is allowed to tell this API what a caller's address is. Empty means nobody, and the address
/// is read from the connection.
/// </summary>
/// <remarks>
/// Leaving this empty behind a proxy is a silent failure, not an error: every caller then arrives
/// as the proxy's address, they all share one rate-limit bucket, and place search starts refusing
/// everyone at three searches a second between them. Trusting the header unconditionally is the
/// opposite failure — anyone could forge it and never be limited at all.
/// </remarks>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>Addresses of the proxies themselves, as this API sees them connect.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>CIDR ranges, for a proxy whose address is not fixed — <c>172.16.0.0/12</c>.</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>How many proxies deep to read. One nginx in front is 1; nginx behind a CDN is 2.</summary>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    /// <c>X-Forwarded-For</c> for nginx and most load balancers; <c>CF-Connecting-IP</c> for
    /// Cloudflare.
    /// </summary>
    public string ForwardedForHeaderName { get; set; } = "X-Forwarded-For";

    public bool HasTrustedProxy => KnownProxies.Length > 0 || KnownNetworks.Length > 0;
}
