using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Tests.Common.Data;

/// <summary>A customer's review of one appointment.</summary>
public static class ReviewFactory
{
    public static Review Create(
        Guid companyId,
        Guid appointmentId,
        Guid customerId,
        Guid staffMemberId,
        short rating = 5,
        bool isPublished = true,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.CreateVersion7(),
            OwnerId = companyId,
            AppointmentId = appointmentId,
            CustomerId = customerId,
            StaffMemberId = staffMemberId,
            Rating = rating,
            IsPublished = isPublished
        };
}
