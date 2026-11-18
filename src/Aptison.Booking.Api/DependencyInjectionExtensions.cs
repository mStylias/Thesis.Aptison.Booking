using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Aptison.Booking.Api.Auth;
using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.ClientAddress;
using Aptison.Booking.Api.Cors;
using Aptison.Booking.Api.Observability;
using Aptison.Booking.Api.RateLimiting;
using Aptison.Booking.Application.Common.Observability;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Synax.EntityFrameworkCore.OData;
using Synax.Mediator;

namespace Aptison.Booking.Api;

/// <summary>
/// Host concerns outside any module: OpenAPI, authentication, CORS, telemetry. The module surface
/// is declared by <see cref="BookingApiModule"/>.
/// </summary>
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            // Lets Scalar send a bearer token against the secured endpoints.
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            };
            document.Security ??= new List<OpenApiSecurityRequirement>();
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
            return Task.CompletedTask;
        }));

        var keycloak = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>()
            ?? throw new InvalidOperationException(
                $"Missing '{KeycloakOptions.SectionName}' configuration section.");
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloak.Authority;
                options.Audience = keycloak.Audience;
                options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;

                // Without this "sub" is remapped to the legacy nameidentifier claim,
                // ICurrentUser<Guid> finds nothing, and every audit stamp silently becomes null.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Both hostnames the same realm can be reached through — see KeycloakOptions.
                    ValidIssuers = keycloak.ValidIssuers.Length > 0
                        ? keycloak.ValidIssuers
                        : [keycloak.Authority],
                    ValidAudience = keycloak.Audience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        var cors = configuration.GetSection(ClientCorsOptions.SectionName).Get<ClientCorsOptions>()
            ?? new ClientCorsOptions();
        services.Configure<ClientCorsOptions>(configuration.GetSection(ClientCorsOptions.SectionName));

        services.AddCors(options => options.AddPolicy(ClientCorsOptions.PolicyName, policy =>
        {
            if (cors.AllowedOrigins.Length == 0)
            {
                // Deny every cross-origin browser request rather than falling back to permissive.
                return;
            }

            policy
                .WithOrigins(cors.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                // Credentials would forbid the wildcards above; the SPA sends a bearer token.
                .DisallowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromHours(1));
        }));

        // A floor, not the enforcement — EndpointPermissionGuard refuses to let an endpoint rely on
        // it silently. Health checks opt out inside MapSynaxHealthChecks.
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                // The email_verified gate for every endpoint declaring no permission.
                // .AllowUnverifiedEmail() is the only supported way to subtract it.
                .AddRequirements(new EmailVerifiedRequirement())
                .Build();
        });

        services.AddPermissionEnforcement();

        // The place-search limit keys on the caller's address, so how that address is established
        // has to be configured alongside it.
        services.AddRealClientAddress(configuration);
        services.AddPlaceSearchRateLimiting(configuration);

        // Keys on the caller's subject rather than their address, so a shared network is not one
        // customer's budget. Both policies share RateLimitRejection.WriteProblemAsync.
        services.AddAppointmentRateLimiting(configuration);
        services.AddStaffCandidateRateLimiting(configuration);
        services.AddCustomerAccountLookupRateLimiting(configuration);
        services.AddTestPushRateLimiting(configuration);

        services.AddSynaxOData();

        // Before AddSynaxModules registers SynaxExceptionHandler: handlers run in registration
        // order and the first to return true ends the chain.
        services.AddExceptionHandler<ExceptionTelemetryHandler>();

        // AddSynaxEfCoreIdentityBridge deliberately stays in the infrastructure module: called here
        // it runs before AddSynaxModules and its TryAdd-ed pass-through audit providers would beat
        // the translating ones, stamping raw Keycloak subjects into FKs to users.id.

        return services;
    }

    /// <summary>
    /// The enforcement half of authorization; resolution belongs to Infrastructure.
    /// </summary>
    private static void AddPermissionEnforcement(this IServiceCollection services)
    {
        // Plain Add displaces the TryAdd-ed default, which this one wraps rather than discards.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        // Scoped: it reads the per-request permission set, so a singleton would authorize everyone
        // as whoever called first.
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddSingleton<IAuthorizationHandler, EmailVerifiedAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PermissionDeniedResultHandler>();
    }

    private static readonly string[] DroppedInstruments =
    [
        "http.client.open_connections",
        "http.client.connection.duration",
        "http.client.request.time_in_queue",
        "http.client.active_requests",
        "aspnetcore.rate_limiting.request_lease.duration",
        "aspnetcore.rate_limiting.request.time_in_queue",
        "aspnetcore.rate_limiting.active_request_leases",
        "aspnetcore.rate_limiting.queued_requests",
        "dotnet.jit.*",
        "dotnet.assembly.count",
        "dotnet.timer.count",
        "dotnet.process.cpu.count",
        "dotnet.thread_pool.work_item.count",
        "dotnet.gc.last_collection.heap.fragmentation.size",
        "dotnet.gc.last_collection.memory.committed_size",
        "db.client.operation.npgsql.*",
        "db.client.connection.npgsql.create_time",
        "synax.mediator.request*"
    ];

    /// <summary>
    /// Exports traces, metrics and logs over OTLP. Endpoint and protocol come from the standard
    /// <c>OTEL_EXPORTER_OTLP_*</c> environment variables.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        // SetSampler makes the SDK ignore OTEL_TRACES_SAMPLER, so the ratio is read here.
        var samplingRatio = builder.Configuration.GetValue("OTEL_TRACES_SAMPLER_ARG", 1.0);

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: Telemetry.ServiceName,
                    serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(
                    new RootClientSpanSampler(new TraceIdRatioBasedSampler(samplingRatio))))
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !TelemetryExclusions.IsExcluded(context))
                .AddHttpClientInstrumentation()
                .AddRedisInstrumentation()
                .AddSource("Npgsql")
                .AddSource(MediatorTelemetry.SourceName)
                .AddProcessor(new RecordedExceptionMarker())
                .AddOtlpExporter())
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Diagnostics",
                        "Microsoft.AspNetCore.RateLimiting",
                        "Microsoft.AspNetCore.Authorization",
                        "System.Net.Http",
                        "System.Runtime",
                        "Npgsql",
                        MediatorTelemetry.SourceName,
                        Telemetry.PlaceSearchMeterName,
                        Telemetry.RemindersMeterName)
                    // Synax records seconds with no bucket advice; default ms buckets make its percentiles meaningless.
                    .AddView("synax.mediator.notification.duration", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                    });

                foreach (var instrumentName in DroppedInstruments)
                {
                    metrics.AddView(instrumentName, MetricStreamConfiguration.Drop);
                }

                metrics.AddOtlpExporter();
            });

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter();
        });

        return builder;
    }
}
