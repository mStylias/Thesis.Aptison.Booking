using System.Linq.Expressions;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Customers.Common;

/// <summary>
/// What the customer grid and the customer details share. <c>Appointments</c>, <c>CompanyCustomers</c>
/// and <c>Bans</c> mean this shop's only while the ownership filter is on, so never project under
/// <c>BecomeAllOwners</c>. Every selector must stay SQL-translatable: <c>$filter</c> and
/// <c>$orderby</c> run against the grid.
/// </summary>
internal sealed class CustomerSelectors
{
    public CustomerSelectors(MapParameter<DateTime> utcNow, MapParameter<DateOnly> shopToday, MapParameter<bool> includeSpend)
    {
        DaysSinceLastVisit = user =>
            shopToday.Value.DayNumber - user.Appointments
                .Where(appointment => appointment.Status == AppointmentStatus.Completed)
                .Max(appointment => (int?)appointment.LocalDate.DayNumber);

        // Upcoming: pending or confirmed, starting after now.
        NextAppointmentAt = user => (DateTimeOffset?)user.Appointments
            .Where(appointment =>
                (appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Confirmed)
                && appointment.StartsAt > utcNow.Value)
            .Min(appointment => (DateTime?)appointment.StartsAt);
        UpcomingCount = user => user.Appointments.Count(appointment =>
            (appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Confirmed)
            && appointment.StartsAt > utcNow.Value);

        TotalSpend = user => includeSpend.Value
            ? user.Appointments
                .Where(appointment => appointment.Status == AppointmentStatus.Completed)
                .Sum(appointment => (decimal?)appointment.TotalPrice) ?? 0m
            : null;
        AverageSpend = user =>
            includeSpend.Value && user.Appointments.Any(appointment => appointment.Status == AppointmentStatus.Completed)
                ? Math.Round(
                    user.Appointments
                        .Where(appointment => appointment.Status == AppointmentStatus.Completed)
                        .Average(appointment => appointment.TotalPrice),
                    2)
                : null;
    }

    public Expression<Func<User, string>> DisplayName { get; } = user => (user.FirstName + " " + user.LastName).Trim();

    public Expression<Func<User, string?>> PhoneNumber { get; } = user =>
        user.PhoneNumber ?? user.CompanyCustomers.Select(customer => customer.KeptPhoneNumber).FirstOrDefault();

    public Expression<Func<User, bool>> IsWalkIn { get; } = user => user.OwnerId != null;

    public Expression<Func<User, int>> VisitCount { get; } = user =>
        user.Appointments.Count(appointment => appointment.Status == AppointmentStatus.Completed);

    public Expression<Func<User, DateOnly?>> FirstVisitDate { get; } = user => user.Appointments
        .Where(appointment => appointment.Status == AppointmentStatus.Completed)
        .Min(appointment => (DateOnly?)appointment.LocalDate);

    public Expression<Func<User, DateOnly?>> LastVisitDate { get; } = user => user.Appointments
        .Where(appointment => appointment.Status == AppointmentStatus.Completed)
        .Max(appointment => (DateOnly?)appointment.LocalDate);

    public Expression<Func<User, int?>> DaysSinceLastVisit { get; }

    public Expression<Func<User, DateTimeOffset?>> NextAppointmentAt { get; }

    public Expression<Func<User, int>> UpcomingCount { get; }

    public Expression<Func<User, int>> NoShowCount { get; } = user =>
        user.Appointments.Count(appointment => appointment.Status == AppointmentStatus.NoShow);

    public Expression<Func<User, DateOnly?>> LastNoShowDate { get; } = user => user.Appointments
        .Where(appointment => appointment.Status == AppointmentStatus.NoShow)
        .Max(appointment => (DateOnly?)appointment.LocalDate);

    public Expression<Func<User, int>> CancellationCount { get; } = user =>
        user.Appointments.Count(appointment => appointment.Status == AppointmentStatus.Cancelled);

    public Expression<Func<User, DateOnly>> CustomerSince { get; } = user =>
        user.Appointments.Min(appointment => appointment.LocalDate);

    public Expression<Func<User, Guid?>> PreferredStaffMemberId { get; } = user =>
        user.CompanyCustomers.Select(customer => customer.PreferredStaffMemberId).FirstOrDefault();

    public Expression<Func<User, string?>> PreferredStaffDisplayName { get; } =
        StaffMemberNames.BuildOptionalDisplayNameSelector<User>(user =>
            user.CompanyCustomers.Select(customer => customer.PreferredStaffMember!.User).FirstOrDefault());

    public Expression<Func<User, bool?>> PreferredStaffIsActive { get; } = user =>
        user.CompanyCustomers.Select(customer => (bool?)customer.PreferredStaffMember!.IsActive).FirstOrDefault();

    public Expression<Func<User, decimal?>> TotalSpend { get; }

    public Expression<Func<User, decimal?>> AverageSpend { get; }
}
