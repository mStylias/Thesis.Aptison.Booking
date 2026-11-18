using Aptison.Booking.Infrastructure.Persistence;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Aptison.Booking.Infrastructure.Persistence.MultiTenancy;
using Aptison.Booking.Api;
using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.ClientAddress;
using Aptison.Booking.Api.Cors;
using Aptison.Booking.Api.Observability;
using Scalar.AspNetCore;
using Synax.AspNetCore;
using Synax.Mapper.Core;
using Synax.Modules;

var builder = WebApplication.CreateBuilder(args);
{
    builder.AddObservability();

    builder.Services
        .AddPresentation(builder.Configuration, builder.Environment)
        .AddSynaxModules(builder.Configuration, typeof(BookingApiModule).Assembly);
}

var app = builder.Build();
{
    var migrationLogger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigrations");

    DatabaseMigrationRunner.Run(app.Configuration, migrationLogger);

    // Every environment: the failure mode is another company's rows in a response. Building the
    // model here also runs Synax's own ownership-filter check.
    app.Services.AssertCompanyIsolationDeclared();

    if (app.Environment.IsDevelopment())
    {
        // Otherwise the fallback policy demands a token before the docs can load.
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();

        // Fail fast on any destination member nothing maps to.
        app.Services.GetRequiredService<MapperConfiguration>().AssertConfigurationIsValid();

        // If the identity bridge wins the TryAdd race, audit columns silently fill with Keycloak
        // subjects instead of users.id. Nothing else catches that.
        app.Services.AssertTranslatingUserProvidersRegistered();
    }

    app.UseExceptionHandler();

    app.UseTelemetryExclusions();

    // Before the rate limiter, which keys on the address this settles.
    app.UseRealClientAddress();

    app.UseRouting();
    
    app.UseCors(ClientCorsOptions.PolicyName);

    app.UseAuthentication();

    // Pinned on both sides: after UseAuthentication, which supplies the principal, and before
    // UseAuthorization, which consumes the permissions it resolves.
    app.UseCurrentAppUser();

    app.UseAuthorization();

    // After routing, or it cannot see which policy an endpoint asked for.
    app.UseRateLimiter();

    app.MapSynaxEndpoints();
    app.MapSynaxHealthChecks();

    // After mapping, because it reads the mapped routes.
    await app.AssertPermissionsDeclaredAsync();
}

app.Run();
