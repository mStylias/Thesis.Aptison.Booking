using Aptison.Booking.Domain;
using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Abstractions.Auditing;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Availability;
using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Abstractions.Places;
using Aptison.Booking.Application.Abstractions.Images;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Infrastructure.Analytics;
using Aptison.Booking.Infrastructure.Auditing;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization.BackgroundServices;
using Aptison.Booking.Infrastructure.Availability;
using Aptison.Booking.Infrastructure.Availability.BackgroundServices;
using Aptison.Booking.Infrastructure.Companies;
using Aptison.Booking.Infrastructure.Images;
using Aptison.Booking.Infrastructure.Storage;
using Aptison.Booking.Infrastructure.Auth.Identity;
using Aptison.Booking.Infrastructure.Auth.Identity.Keycloak;
using Aptison.Booking.Infrastructure.Auth.Identity.Revocation;
using Aptison.Booking.Infrastructure.Auth.Identity.Verification;
using Aptison.Booking.Infrastructure.Auth.Identity.Verification.BackgroundServices;
using Aptison.Booking.Infrastructure.Messaging;
using Aptison.Booking.Infrastructure.Persistence;
using Aptison.Booking.Infrastructure.Places;
using Aptison.Booking.Infrastructure.Places.BackgroundServices;
using Aptison.Booking.Infrastructure.Reminders;
using Aptison.Booking.Infrastructure.Reminders.BackgroundServices;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Aptison.Booking.Infrastructure.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Synax.EntityFrameworkCore.Abstractions.Auditing;
using Synax.EntityFrameworkCore.Abstractions.Ownership;
using Synax.EntityFrameworkCore.Abstractions.SoftDelete;
using Synax.EntityFrameworkCore.Auditing;
using Synax.EntityFrameworkCore.Mapper;
using Synax.EntityFrameworkCore.Mediator;
using Synax.EntityFrameworkCore.OData;
using Synax.EntityFrameworkCore.Ownership;
using Synax.EntityFrameworkCore.SoftDelete;

namespace Aptison.Booking.Infrastructure;

/// <summary>
/// The infrastructure wiring Synax has no opinion about; everything it owns is declared by
/// <see cref="BookingInfrastructureModule"/>, which also calls this.
/// </summary>
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<BookingDbContext>(options =>
            options
                .UseNpgsql(
                    DatabaseConnectionString.Read(config),
                    npgsqlOptions => npgsqlOptions.UseNetTopologySuite())
                .UseSnakeCaseNamingConvention()
                // Every case here is a deliberate SpansCompanies exception, so the warning has no
                // remedy. CompanyIsolationGuard catches the real case and names the entity.
                .ConfigureWarnings(warnings => warnings.Ignore(
                    CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
                .AddAuditingInterceptor()
                .AddSoftDeleteInterceptor()
                // Stamps OwnerId and fails the host if an owned entity lacks its query filter —
                // stamping without filtering writes rows nobody can then be prevented from reading.
                .AddOwnershipInterceptor<Guid>()
                .AddOutboxInterceptor<BookingContext, BookingDbContext>()
                .AddInterceptors(ODataDateComparisonInterceptor.Instance));

        // Synax resolves it from the request, so every OData endpoint gets it whatever options it passes.
        services.AddSingleton<IODataExpressionRewriter, ODataTextFoldingRewriter>();

        // Bridges QueryDto/AddDtoAsync to Synax.Mapper; without it QueryDto fails at runtime.
        services.AddSynaxRepositoryMapper();

        // Translates the Keycloak subject to users.id for audit stamping. Plain Add* and registered
        // before AddSynaxEfCoreIdentityBridge, whose pass-through providers TryAdd and so yield;
        // reversed, raw Keycloak subjects land in columns that are FKs to users.id.
        services.AddScoped<CurrentAppUser>();
        services.AddScoped<ICurrentAppUser>(sp => sp.GetRequiredService<CurrentAppUser>());
        services.AddScoped<IAuditUserProvider, AppUserAuditProvider>();
        services.AddScoped<ISoftDeleteUserProvider, AppUserAuditProvider>();

        // Separate from ICurrentAppUser: an unverified caller has a subject and no user row.
        services.AddScoped<CurrentAuthenticatedSubject>();
        services.AddScoped<ICurrentAuthenticatedSubject>(
            sp => sp.GetRequiredService<CurrentAuthenticatedSubject>());

        // Same TryAdd reason: the bridge's default scopes ownership to the caller's users.id, which
        // matches no company and empties every owned query.
        services.AddScoped<IOwnerProvider<Guid>, CompanyOwnerProvider>();

        services.Configure<UserProvisioningOptions>(
            config.GetSection(UserProvisioningOptions.SectionName));
        services.AddScoped<IUserProvisioner, UserProvisioner>();
        services.AddScoped<UnprovenPasswordGuard>();
        services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();

        // Scoped: it runs on the request's own DbContext connection and transaction.
        services.AddScoped<IAnalyticsReader, DapperAnalyticsReader>();

        services.AddHttpContextAccessor();
        services.AddScoped<IAuditTrail, AuditTrail>();

        services.AddPermissionResolution(config);
        services.AddCaching();
        services.AddEmailVerification(config);
        services.AddCompanyBrowse(config);
        services.AddPlaceSearch(config);
        services.AddFileStorage(config);
        services.AddAppointmentReminders(config);

        return services;
    }

    /// <summary>The reminder scan, Web Push, and the secret behind the unsubscribe links.</summary>
    private static void AddAppointmentReminders(this IServiceCollection services, IConfiguration config)
    {
        // ValidateOnStart: without the secret no reminder email can carry its unsubscribe link.
        services.AddOptions<AppointmentReminderOptions>()
            .Bind(config.GetSection(AppointmentReminderOptions.SectionName))
            .Validate(
                options => options.UnsubscribeSecret.Trim().Length >= 32,
                "Reminders:UnsubscribeSecret must be at least 32 characters. It signs the unsubscribe links in " +
                "reminder emails; changing it later breaks every link already sent.")
            .Validate(options => options.ScanInterval > TimeSpan.Zero, "Reminders:ScanInterval must be positive.")
            .Validate(options => options.MaxDevicesPerUser >= 1, "Reminders:MaxDevicesPerUser must be at least 1.")
            .ValidateOnStart();

        services.AddOptions<WebPushOptions>()
            .Bind(config.GetSection(WebPushOptions.SectionName))
            .Validate(
                options => string.IsNullOrWhiteSpace(options.PublicKey + options.PrivateKey) || VapidKeys.AreWellFormed(options),
                "WebPush needs a base64url P-256 key pair (65-byte public, 32-byte private) and a mailto: or https: " +
                "Subject, or neither key, which leaves push off.")
            .ValidateOnStart();

        services.AddOptions<PushNotificationOptions>()
            .Configure<IOptions<WebPushOptions>>((push, webPush) =>
            {
                push.Enabled = webPush.Value.IsConfigured;
                push.PublicKey = webPush.Value.IsConfigured ? webPush.Value.PublicKey.Trim() : string.Empty;
            });

        var webPush = config.GetSection(WebPushOptions.SectionName).Get<WebPushOptions>() ?? new WebPushOptions();

        services.AddSingleton<VapidKeys>();
        services.AddHttpClient<IPushSender, WebPushSender>(
            WebPushSender.HttpClientName, client => client.Timeout = webPush.RequestTimeout);

        // Scoped: it locks on the scan's own DbContext transaction.
        services.AddScoped<IReminderScanLock, ReminderScanLock>();

        services.AddHostedService<AppointmentReminderScanner>();
    }

    /// <summary>Uploaded files and the processing before they are stored.</summary>
    private static void AddFileStorage(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<FileStorageOptions>(config.GetSection(FileStorageOptions.SectionName));
        services.Configure<S3StorageOptions>(config.GetSection(FileStorageOptions.SectionName));

        services.AddSingleton<IFileStorage, S3FileStorage>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
    }

    /// <summary>
    /// The geocoder behind the public location control, its policy, and the sweep that bounds its
    /// Postgres cache. The API key stays here and never reaches a browser or a build artifact.
    /// </summary>
    private static void AddPlaceSearch(this IServiceCollection services, IConfiguration config)
    {
        // ValidateOnStart because every one of these is read on a hot path: a zero cell size
        // divides, and a zero result count would send billed requests that return nothing.
        services.AddOptions<PlaceSearchOptions>()
            .Bind(config.GetSection(PlaceSearchOptions.SectionName))
            .Validate(options => options.MinimumQueryLength > 0, "PlaceSearch:MinimumQueryLength must be at least 1.")
            .Validate(
                options => options.MaximumQueryLength >= options.MinimumQueryLength,
                "PlaceSearch:MaximumQueryLength must not be below MinimumQueryLength.")
            .Validate(options => options.MaxSuggestions is >= 1 and <= 20, "PlaceSearch:MaxSuggestions must be 1-20.")
            .Validate(options => options.CacheCellDegrees > 0, "PlaceSearch:CacheCellDegrees must be greater than zero.")
            .Validate(options => options.CacheLifetime > TimeSpan.Zero, "PlaceSearch:CacheLifetime must be positive.")
            .Validate(
                options => options.EmptyResultCacheLifetime > TimeSpan.Zero,
                "PlaceSearch:EmptyResultCacheLifetime must be positive.")
            .Validate(
                options => options.CacheSweepInterval > TimeSpan.Zero,
                "PlaceSearch:CacheSweepInterval must be positive.")
            .ValidateOnStart();

        services.Configure<PlaceKitOptions>(config.GetSection(PlaceKitOptions.SectionName));

        var placeKit = config.GetSection(PlaceKitOptions.SectionName).Get<PlaceKitOptions>() ?? new PlaceKitOptions();

        // The trailing slash matters: without it BaseAddress drops its last path segment.
        services.AddHttpClient<IPlaceSearchProvider, PlaceKitClient>(PlaceKitClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri($"{placeKit.BaseUrl.TrimEnd('/')}/");
            client.Timeout = placeKit.RequestTimeout;

            if (!string.IsNullOrWhiteSpace(placeKit.ApiKey))
            {
                client.DefaultRequestHeaders.Add("x-placekit-api-key", placeKit.ApiKey);
            }
        });

        services.AddHostedService<PlaceSearchCacheSweeper>();
    }

    /// <summary>
    /// <c>HybridCache</c> for every "keep this value for a while" need: an in-process L1 over Redis
    /// as L2, with serialization, single-flight on a miss and tag eviction built in. Inject it
    /// directly — a cache does not need a port, and one interface per cached thing was the wrong
    /// shape.
    /// <para>
    /// Redis-backed structures that are <em>not</em> caches keep talking to
    /// <see cref="IConnectionMultiplexer"/>: the verification code store needs hashes, TTL-as-cooldown
    /// and atomic set-if-absent, and the permission cache reads two structures and falls through to
    /// Postgres. Neither is a key holding one serializable value.
    /// </para>
    /// </summary>
    private static void AddCaching(this IServiceCollection services)
    {
        services.AddStackExchangeRedisCache(_ => { });

        // Reuses the multiplexer AddPermissionResolution registered rather than opening a second
        // connection pool — and with it AbortOnConnectFail = false, so a cold Redis does not stop
        // the host from starting.
        services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>(
            (redis, multiplexer) => redis.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer));

        services.AddHybridCache();
    }

    /// <summary>
    /// The public browse: its origin and caps, and the availability index behind its date filter.
    /// </summary>
    private static void AddCompanyBrowse(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<CompanyBrowseOptions>(config.GetSection(CompanyBrowseOptions.SectionName));
        services.Configure<SupportOptions>(config.GetSection(SupportOptions.SectionName));

        services.Configure<AvailabilityIndexOptions>(
            config.GetSection(AvailabilityIndexOptions.SectionName));

        // Scoped: it takes a lock on the rebuild's own DbContext transaction, so it must share it.
        services.AddScoped<ICompanyRebuildLock, CompanyRebuildLock>();
        services.AddScoped<ICompanySettingsLock, CompanySettingsLock>();

        services.AddSingleton<IDatabaseConstraintReader, PostgresConstraintReader>();

        services.AddHostedService<AvailabilityIndexMaintainer>();
    }

    /// <summary>
    /// Everything behind proving an email address with a code. Whether an unverified caller may act
    /// is <c>EmailVerifiedRequirement</c>'s decision, in the API host.
    /// </summary>
    private static void AddEmailVerification(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<EmailVerificationOptions>(
            config.GetSection(EmailVerificationOptions.SectionName));
        services.Configure<UnverifiedAccountSweeperOptions>(
            config.GetSection(UnverifiedAccountSweeperOptions.SectionName));
        services.Configure<KeycloakAdminOptions>(
            config.GetSection(KeycloakAdminOptions.SectionName));
        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
        services.AddOptions<EmailSenderOptions>()
            .Configure<IOptions<SmtpOptions>>((sender, smtp) => sender.Address = smtp.Value.FromAddress);
        services.Configure<EmailBrandingOptions>(
            config.GetSection(EmailBrandingOptions.SectionName));

        var admin = config.GetSection(KeycloakAdminOptions.SectionName).Get<KeycloakAdminOptions>()
                    ?? new KeycloakAdminOptions();

        // The trailing slash matters: without it BaseAddress drops its last path segment.
        services.AddHttpClient(KeycloakServiceAccountTokenProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri($"{admin.BaseUrl.TrimEnd('/')}/");
            client.Timeout = admin.RequestTimeout;
        });

        // Singleton: the point of caching a token is to hold one, not one per call.
        services.AddSingleton<KeycloakServiceAccountTokenProvider>();

        services.AddHttpClient<IIdentityAccountsManager, KeycloakAdminClient>(
            KeycloakServiceAccountTokenProvider.HttpClientName);

        services.AddSingleton<IVerificationCodeStore, RedisVerificationCodeStore>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.AddHostedService<UnverifiedAccountSweeper>();
    }

    /// <summary>
    /// The cache, the Postgres rebuild path behind it, and the request-scoped holders the
    /// middleware fills. Enforcement is the API host's.
    /// </summary>
    private static void AddPermissionResolution(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AuthorizationCacheOptions>(config.GetSection(AuthorizationCacheOptions.SectionName));

        var connectionString = config.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Refused rather than degraded: cache-less still works, so this would surface only as
            // an unexplained database load problem.
            throw new InvalidOperationException(
                "Missing the 'Redis' connection string. Permission resolution caches its inputs " +
                "there, and starting without it would silently put a four-table join on the hot " +
                "path of every request.");
        }

        var redisOptions = ConfigurationOptions.Parse(connectionString);

        // The API must start whether or not Redis is up; authorization degrades to Postgres.
        redisOptions.AbortOnConnectFail = false;

        // Measured: with the default queueing policy, every command issued while Redis is
        // unreachable waits the full 5s command timeout in the backlog before failing, so each
        // cache miss stalls the request for 5s and then returns the right answer anyway. Nothing on
        // this connection gains from that wait — the permission cache and the company list read
        // through to Postgres, and the verification store has to fail either way.
        redisOptions.BacklogPolicy = BacklogPolicy.FailFast;

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
        services.AddSingleton<IPermissionCache, RedisPermissionCache>();

        services.AddSingleton<IPermissionCacheInvalidator>(
            sp => sp.GetRequiredService<IPermissionCache>());

        services.AddScoped<IPermissionStore, PermissionStore>();
        services.AddScoped<PermissionResolver>();
        services.AddScoped<IPermissionResolver>(sp => sp.GetRequiredService<PermissionResolver>());
        services.AddScoped<CompanyScopeResolver>();

        // The concrete type is what CurrentAppUserMiddleware writes through; everything else sees
        // only the read-only interface.
        services.AddScoped<CompanyScope>();
        services.AddScoped<ICompanyScope>(sp => sp.GetRequiredService<CompanyScope>());
        services.AddScoped<CurrentPermissions>();
        services.AddScoped<ICurrentPermissions>(sp => sp.GetRequiredService<CurrentPermissions>());

        services.AddHostedService<PermissionCacheWarmer>();
    }
}
