using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Companies;

/// <summary>
/// Who can perform which service, for how much and for how long — <c>staff_services</c> joined to
/// <c>services</c> and flattened, with each barber's overrides already applied. It exists so the
/// anonymous browse and booking queries never read either, both being required-owner and so
/// invisible without a company scope.
/// </summary>
public class CompanyServiceOffering : IEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid StaffMemberId { get; set; }
    public Guid ServiceId { get; set; }

    /// <summary>Null when the service is uncategorised, which no category filter matches.</summary>
    public Guid? CategoryId { get; set; }

    public decimal Price { get; set; }

    /// <summary>The minutes the customer is told. Buffers are deliberately not in it.</summary>
    public short DurationMinutes { get; set; }

    public short BufferBeforeMinutes { get; set; }
    public short BufferAfterMinutes { get; set; }

    /// <summary>
    /// <see cref="DurationMinutes"/> plus both buffers — what one of these on its own costs the
    /// calendar. A chain of several is not their sum; see <c>BookingDuration</c>.
    /// </summary>
    public short RequiredMinutes { get; set; }
}
