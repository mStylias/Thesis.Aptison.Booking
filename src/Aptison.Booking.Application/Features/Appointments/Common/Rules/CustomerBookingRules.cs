using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

/// <summary>
/// The rules a customer's own booking is held to. Booking and moving both call these, so a move
/// can never reach a time a new booking could not.
/// </summary>
internal static class CustomerBookingRules
{
    public const int MaxNotesLength = 400;

    /// <summary>How many upcoming appointments one customer may hold at one barbershop.</summary>
    public const int MaxActiveFutureAppointments = 3;

    public static bool IsBanned(bool isBanned, DateTime? bannedUntil, DateTime utcNow) =>
        isBanned && (bannedUntil is null || bannedUntil > utcNow);

    public static HttpError CreateBannedError() =>
        HttpError.Forbidden("This account cannot book appointments.", errorCode: ErrorCodes.Appointments.CustomerBanned);

    /// <summary>The lead time and the horizon at submit time. The client's copy of both is for rendering.</summary>
    public static Result<HttpError> EnsureInsideBookingWindow(
        BookingCompanyDto company, TimeZoneInfo timeZone, DateTime startsAt, DateTime utcNow)
    {
        if (startsAt < utcNow.AddMinutes(company.MinLeadTimeMinutes))
        {
            return HttpError.UnprocessableEntity(
                "That time is too soon to book. Please pick a later one.",
                errorCode: ErrorCodes.Appointments.LeadTime);
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));
        var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startsAt, timeZone));

        if (localDay > today.AddDays(company.MaxAdvanceDays))
        {
            return HttpError.UnprocessableEntity(
                "That date is further ahead than this barbershop takes bookings.",
                errorCode: ErrorCodes.Appointments.TooFarAhead);
        }

        return Result.Ok();
    }

    /// <summary>
    /// The offerings index is rebuilt asynchronously, so it can still name a service that has since
    /// been deactivated. <c>services</c> is the arbiter.
    /// </summary>
    public static async Task<Result<HttpError>> EnsureServicesBookableAsync(
        IRepository<Service, BookingContext> serviceRepo,
        Guid companyId,
        IReadOnlyList<Guid> serviceIds,
        CancellationToken cancellationToken)
    {
        var wantedServiceIds = serviceIds.Distinct().ToArray();

        var known = await serviceRepo
            .QuerySelect(
                service => new { service.Id, service.IsActive },
                query => query.Where(service =>
                    service.OwnerId == companyId && wantedServiceIds.Contains(service.Id)))
            .ToListAsync(cancellationToken);

        if (known.Count != wantedServiceIds.Length)
        {
            // Unknown, deleted, or belonging to another barbershop. No client should reach this.
            return HttpError.BadRequest(
                "One of the services you chose does not belong to this barbershop.",
                errorCode: ErrorCodes.Appointments.InvalidServices);
        }

        if (known.Any(service => !service.IsActive))
        {
            return HttpError.Conflict(
                "One of the services you chose is no longer offered here.",
                errorCode: ErrorCodes.Appointments.ServiceUnavailable);
        }

        return Result.Ok();
    }

    public static HttpError CreateNoCandidateError(Guid? staffMemberId) =>
        staffMemberId is null
            ? HttpError.Conflict(
                "No barber here performs every service you chose.",
                errorCode: ErrorCodes.Appointments.ServiceUnavailable)
            : HttpError.Conflict(
                "That barber no longer performs every service you chose.",
                errorCode: ErrorCodes.Appointments.StaffUnavailable);

    public static HttpError CreateNotFreeError(Guid? staffMemberId) =>
        staffMemberId is null
            ? HttpError.Conflict(
                "That time is no longer available. Please pick another.",
                errorCode: ErrorCodes.Appointments.SlotUnavailable)
            : HttpError.Conflict(
                "That barber is not free then. Please pick another time or barber.",
                errorCode: ErrorCodes.Appointments.StaffUnavailable);

    /// <param name="movedAppointmentId">The appointment being moved, which counts against neither limit.</param>
    public static async Task<Result<HttpError>> EnsureWithinCustomerLimitsAsync(
        IRepository<Appointment, BookingContext> appointmentRepo,
        Guid customerId,
        Guid companyId,
        DateTime startsAt,
        int customerMinutes,
        DateTime utcNow,
        Guid? movedAppointmentId,
        CancellationToken cancellationToken)
    {
        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        var held = await appointmentRepo
            .QuerySelect(
                appointment => appointment.Id,
                query => query.Where(appointment =>
                    appointment.CustomerId == customerId
                    && appointment.OwnerId == companyId
                    && appointment.StartsAt >= utcNow
                    && activeStatuses.Contains(appointment.Status)
                    && (movedAppointmentId == null || appointment.Id != movedAppointmentId)))
            .CountAsync(cancellationToken);

        if (held >= MaxActiveFutureAppointments)
        {
            return HttpError.Conflict(
                $"You already have {MaxActiveFutureAppointments} upcoming appointments at this barbershop. " +
                "Cancel one before booking another.",
                errorCode: ErrorCodes.Appointments.LimitReached);
        }

        var noEarlierThan = startsAt - AppointmentLimits.MinimumCustomerGap;
        var noLaterThan = startsAt.AddMinutes(customerMinutes) + AppointmentLimits.MinimumCustomerGap;

        // Every barbershop, not only this one: a customer can only be in one chair at a time.
        var tooClose = await appointmentRepo
            .QuerySelect(
                appointment => appointment.Id,
                query => query.Where(appointment =>
                    appointment.CustomerId == customerId
                    && activeStatuses.Contains(appointment.Status)
                    && appointment.StartsAt < noLaterThan
                    && appointment.EndsAt > noEarlierThan
                    && (movedAppointmentId == null || appointment.Id != movedAppointmentId)))
            .AnyAsync(cancellationToken);

        if (tooClose)
        {
            return HttpError.Conflict(
                "You already have an appointment around that time. Leave at least " +
                $"{(int)AppointmentLimits.MinimumCustomerGap.TotalMinutes} minutes between two of them.",
                errorCode: ErrorCodes.Appointments.OverlapsExisting);
        }

        return Result.Ok();
    }

    /// <summary>
    /// Anyone: the free barber with the fewest appointments on that shop-local day, ties broken at
    /// random so the same person is not handed every quiet morning.
    /// </summary>
    public static async Task<BookingCandidate> ChooseBarberAsync(
        IRepository<Appointment, BookingContext> appointmentRepo,
        Guid companyId,
        IReadOnlyList<BookingCandidate> free,
        DateOnly localDay,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        if (free.Count == 1)
        {
            return free[0];
        }

        var dayStart = ShopTime.ToUtcSkippingGap(localDay.ToDateTime(TimeOnly.MinValue), timeZone);
        var dayEnd = ShopTime.ToUtcSkippingGap(localDay.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);
        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();
        var staffIds = free.Select(candidate => candidate.StaffMemberId).ToArray();

        var bookedStaffIds = await appointmentRepo
            .QuerySelect(
                appointment => appointment.StaffMemberId,
                query => query.Where(appointment =>
                    appointment.OwnerId == companyId
                    && staffIds.Contains(appointment.StaffMemberId)
                    && appointment.StartsAt >= dayStart
                    && appointment.StartsAt < dayEnd
                    && activeStatuses.Contains(appointment.Status)))
            .ToListAsync(cancellationToken);

        var countsByStaffIds = bookedStaffIds
            .GroupBy(staffMemberId => staffMemberId)
            .ToDictionary(staff => staff.Key, staff => staff.Count());

        return free
            .OrderBy(candidate => countsByStaffIds.GetValueOrDefault(candidate.StaffMemberId))
            .ThenBy(_ => Random.Shared.Next())
            .First();
    }
}
