using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Aptison.Booking.Api.ClientAddress;

/// <summary>
/// Makes <c>HttpContext.Connection.RemoteIpAddress</c> the one answer to "who is calling", whether
/// or not a proxy sits in front. Everything that keys on a caller reads it from there and asks no
/// further questions.
/// </summary>
public static class ClientAddressExtensions
{
    public static IServiceCollection AddRealClientAddress(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ReverseProxyOptions.SectionName);
        services.Configure<ReverseProxyOptions>(section);

        var proxy = section.Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedForHeaderName = proxy.ForwardedForHeaderName;
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = proxy.ForwardLimit;

            // The defaults trust loopback, which is one container talking to another in this
            // compose file. Only what is configured is trusted.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var address in proxy.KnownProxies.Where(value => IPAddress.TryParse(value, out _)))
            {
                options.KnownProxies.Add(IPAddress.Parse(address));
            }

            foreach (var network in proxy.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    /// <summary>
    /// Must run before anything that reads the caller's address — the rate limiter above all.
    /// </summary>
    public static WebApplication UseRealClientAddress(this WebApplication app)
    {
        var proxy = app.Configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>()
            ?? new ReverseProxyOptions();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ClientAddress");

        if (!proxy.HasTrustedProxy)
        {
            logger.LogInformation(
                "No trusted reverse proxy is configured, so a caller is identified by the address " +
                "this API sees connect. Put a proxy in front without filling in {Section} and every " +
                "caller arrives as that proxy, sharing one rate-limit bucket between them",
                ReverseProxyOptions.SectionName);

            return app;
        }

        logger.LogInformation(
            "Trusting {HeaderName} from {ProxyCount} proxy address(es) and {NetworkCount} network(s), " +
            "{ForwardLimit} deep",
            proxy.ForwardedForHeaderName, proxy.KnownProxies.Length, proxy.KnownNetworks.Length,
            proxy.ForwardLimit);

        app.UseForwardedHeaders();

        return app;
    }
}
