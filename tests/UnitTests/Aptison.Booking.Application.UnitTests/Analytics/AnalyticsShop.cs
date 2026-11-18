using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Testing;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Analytics;

/// <summary>One shop in Athens, the fake port behind every analytics handler, and a clock on 1 October 2026.</summary>
internal sealed class AnalyticsShop
{
    public static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public AnalyticsShop()
    {
        UnitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        UnitOfWork.Seed(CompanyFactory.Create(CompanyId));
    }

    public Guid CompanyId { get; } = Guid.CreateVersion7();
    public FakeUnitOfWork<BookingContext> UnitOfWork { get; }
    public FakeAnalyticsReader Reader { get; } = new();
    public FixedTimeProvider Clock { get; } = new(Now);

    public Guid SeedStaffMember(string firstName, string lastName, Guid? companyId = null, DateTime? joinedAt = null)
    {
        var user = UserFactory.Create(firstName: firstName, lastName: lastName);
        var staffMember = StaffFactory.Create(companyId ?? CompanyId, user.Id, joinedAt: joinedAt);

        // The fake does no navigation fixup.
        staffMember.User = user;
        UnitOfWork.Seed(user);
        UnitOfWork.Seed(staffMember);
        return staffMember.Id;
    }

    public void AddDay(
        DateOnly day,
        Guid staffMemberId,
        string status,
        int appointments = 1,
        decimal revenue = 20m,
        int minutes = 30,
        string source = AppointmentSource.Online) =>
        Reader.AppointmentDailyFacts.Add(new AppointmentDailyFacts
        {
            LocalDate = day,
            StaffMemberId = staffMemberId,
            Status = status,
            Source = source,
            Appointments = appointments,
            Revenue = revenue,
            Minutes = minutes
        });

    /// <summary>Every analytics handler takes the same four dependencies.</summary>
    public async Task<Result<TResponse, HttpError>> SendAsync<THandler, TRequest, TResponse>(TRequest request)
        where THandler : AnalyticsQueryHandler<TRequest, TResponse>
        where TRequest : AnalyticsRequest, IQuery<Result<TResponse, HttpError>>
    {
        request.CompanyId = CompanyId;
        var handler = (THandler)Activator.CreateInstance(typeof(THandler), UnitOfWork, Reader, FakeWorld.Mapper, Clock)!;

        return await handler.ValidateAndHandleAsync(request);
    }
}
