using Aptison.Booking.Application;
using Aptison.Booking.Domain;
using Microsoft.Extensions.DependencyInjection;
using Synax.EntityFrameworkCore.Mapper;
using Synax.EntityFrameworkCore.Testing;
using Synax.Identity;
using Synax.Mapper;
using Synax.Mapper.Core;

namespace Aptison.Booking.Tests.Common;

/// <summary>
/// The plumbing a handler unit test needs: the application's real mapper, the real ambient-identity
/// scope, and a <see cref="FakeUnitOfWork{TContext}"/> wired the way the real DbContext registration
/// is. No database, no container, no DI container.
/// </summary>
public static class FakeWorld
{
    /// <summary>
    /// Every <see cref="MapperProfile"/> in the Application assembly, discovered rather than listed
    /// — a profile added later is covered without anyone remembering to come back here.
    /// </summary>
    public static MapperConfiguration BuildMapperConfiguration() =>
        new(configuration =>
        {
            var profiles = typeof(BookingApplicationModule).Assembly
                .GetTypes()
                .Where(type => typeof(MapperProfile).IsAssignableFrom(type)
                               && type is { IsAbstract: false, IsGenericTypeDefinition: false })
                .Select(type => (MapperProfile)Activator.CreateInstance(type, nonPublic: true)!);

            foreach (var profile in profiles)
            {
                configuration.AddProfile(profile);
            }
        });

    /// <summary>The shared mapper. Immutable once built, so one instance serves every test.</summary>
    public static IMapper Mapper { get; } = BuildMapperConfiguration().CreateMapper();

    /// <summary>
    /// The real ambient-identity scope, not a fake — it is an <c>AsyncLocal</c> with no
    /// dependencies, so a handler's <c>BecomeAllOwners()</c> should exercise the real one. Give the
    /// same instance to <see cref="CreateUnitOfWork"/> and the scope will widen its reads too.
    /// </summary>
    public static IUserContextScope CreateUserContext() =>
        new ServiceCollection()
            .AddSynaxUserContext()
            .BuildServiceProvider()
            .GetRequiredService<IUserContextScope>();

    /// <summary>
    /// A unit of work over in-memory stores that emulate the ownership and soft-delete query
    /// filters. Pass <paramref name="userContext"/> to make <c>BecomeAllOwners()</c> widen reads
    /// here as it does in SQL; without it the fake stays scoped to <paramref name="company"/>.
    /// </summary>
    public static FakeUnitOfWork<BookingContext> CreateUnitOfWork(
        Guid? company = null,
        Guid? user = null,
        TimeProvider? clock = null,
        IUserContextScope? userContext = null) =>
        new(new FakeUnitOfWorkOptions
        {
            // Mirrors CompanyOwnerProvider: null inside a cross-company scope, so a write to a
            // required-owner entity there fails rather than stamping the route's company.
            CurrentOwnerId = () => userContext?.Current switch
            {
                { AllOwners: true } => null,
                { UserId: null, OwnerId: Guid owner } => owner,
                _ => company
            },
            CrossOwner = () => userContext?.Current is { AllOwners: true },
            CurrentUserId = () => user,
            TimeProvider = clock ?? TimeProvider.System,
            Mapper = new SynaxMapperRepositoryMapper(Mapper)
        });
}
