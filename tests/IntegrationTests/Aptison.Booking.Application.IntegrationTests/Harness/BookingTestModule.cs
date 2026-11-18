using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Abstractions.Auditing;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Aptison.Booking.Application.Abstractions.Availability;
using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Abstractions.Images;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Abstractions.Places;
using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Infrastructure.Analytics;
using Aptison.Booking.Infrastructure.Auditing;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Aptison.Booking.Infrastructure.Persistence.MultiTenancy;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Synax.EntityFrameworkCore.Abstractions.Auditing;
using Synax.EntityFrameworkCore.Abstractions.Ownership;
using Synax.EntityFrameworkCore.Abstractions.SoftDelete;
using Synax.EntityFrameworkCore.Mapper;
using Synax.EntityFrameworkCore.Modules;
using Synax.EntityFrameworkCore.OData;
using Synax.Identity;
using Synax.Modules;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// Everything the real host registers that a handler can see, and nothing it cannot. Depends on the
/// real <see cref="BookingApplicationModule"/>, so handlers, profiles, the outbox and the
/// notification names are the application's own declaration rather than a copy; persistence is
/// wired as <c>BookingInfrastructureModule</c> wires it, and the outbound integrations are faked.
/// </summary>
public sealed class BookingTestModule : ISynaxModule
{
    public void Configure(ISynaxModuleBuilder builder)
    {
        builder.DependsOn<BookingApplicationModule>();

        builder.IncludeAssembly(typeof(User).Assembly);
        builder.AddPersistence<BookingDbContext, BookingContext>();

        builder.AddServices(services =>
        {
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddSingleton(TimeProvider.System);

            // Bridges QueryDto/AddDtoAsync to Synax.Mapper; without it QueryDto fails at runtime.
            services.AddSynaxRepositoryMapper();

            // Infrastructure's real one: the OData lists are only answerable with it applied.
            services.AddSingleton<IODataExpressionRewriter, ODataTextFoldingRewriter>();

            // The AsyncLocal behind BecomeAllOwners(). The real one, because a handler widening its
            // reads is exactly what these tests are for.
            services.AddSynaxUserContext();

            AddIdentity(services);
            AddPorts(services);
            AddOptions(services);

            // L1 only: no IDistributedCache is registered, so HybridCache runs in-process. Keys
            // carry the ids a test generated, so one test's entries do not answer another's.
            services.AddHybridCache();
        });
    }

    /// <summary>
    /// The scoped identity a test sets, under Infrastructure's own ownership and audit providers —
    /// the two places where a copy would let tenant isolation drift without anything failing.
    /// </summary>
    private static void AddIdentity(IServiceCollection services)
    {
        services.AddScoped<TestIdentity>();
        services.AddScoped<ICompanyScope, TestCompanyScope>();
        services.AddScoped<ICurrentAppUser, TestCurrentAppUser>();

        services.AddScoped<IOwnerProvider<Guid>, CompanyOwnerProvider>();
        services.AddScoped<IAuditUserProvider, AppUserAuditProvider>();
        services.AddScoped<ISoftDeleteUserProvider, AppUserAuditProvider>();
    }

    private static void AddPorts(IServiceCollection services)
    {
        // Scoped and registered twice so a test can resolve the concrete fake to configure it and
        // read what it recorded, while the handler sees only the port.
        AddFake<IEmailSender, FakeEmailSender>(services);
        AddFake<IIdentityAccountsManager, FakeIdentityAccountsManager>(services);
        AddFake<IVerificationCodeStore, FakeVerificationCodeStore>(services);
        AddFake<IPlaceSearchProvider, FakePlaceSearchProvider>(services);
        AddFake<ICurrentPermissions, FakeCurrentPermissions>(services);
        AddFake<IPermissionResolver, FakePermissionResolver>(services);
        AddFake<IPermissionCacheInvalidator, FakePermissionCacheInvalidator>(services);
        AddFake<ICompanyRebuildLock, FakeCompanyRebuildLock>(services);
        AddFake<ICompanySettingsLock, FakeCompanySettingsLock>(services);
        AddFake<IFileStorage, FakeFileStorage>(services);
        AddFake<IImageProcessor, FakeImageProcessor>(services);
        AddFake<IPushSender, FakePushSender>(services);
        AddFake<IReminderScanLock, FakeReminderScanLock>(services);

        // Infrastructure's real reader, wrapped so a test can see what a handler caught inside an
        // exception filter. Not faked: reading a lost race off Npgsql is the behaviour under test.
        AddFake<IDatabaseConstraintReader, RecordingConstraintReader>(services);

        // Infrastructure's real one: its SQL, and the company filter it must apply by hand, are what the tests are for.
        services.AddScoped<IAnalyticsReader, DapperAnalyticsReader>();

        // Infrastructure's real one: the row it adds to the handler's save is what the tests check. No
        // request here, so the address and user agent stay empty.
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditTrail, AuditTrail>();
    }

    private static void AddOptions(IServiceCollection services)
    {
        services.AddOptions<AvailabilityIndexOptions>();
        services.AddOptions<CompanyBrowseOptions>();
        services.AddOptions<FileStorageOptions>();
        services.AddOptions<EmailBrandingOptions>();
        services.AddOptions<EmailSenderOptions>().Configure(sender => sender.Address = "no-reply@mail.koureia.gr");
        services.AddOptions<PlaceSearchOptions>();
        services.AddOptions<AppointmentReminderOptions>()
            .Configure(reminders => reminders.UnsubscribeSecret = TestSecrets.ReminderUnsubscribeSecret);
        services.AddOptions<PushNotificationOptions>().Configure(push =>
        {
            push.Enabled = true;
            push.PublicKey = "BPVP0_a1YV-TsyvQxwfQ-4LNvDHovGZh94QEx81zFvOdcdohljLyhHVKscjOsVfhx1nF_nYJH9XvkCQpIVQ_Rlk";
        });
    }

    private static void AddFake<TPort, TFake>(IServiceCollection services)
        where TPort : class
        where TFake : class, TPort
    {
        services.AddScoped<TFake>();
        services.AddScoped<TPort>(serviceProvider => serviceProvider.GetRequiredService<TFake>());
    }
}
