using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.EntityAbstractions;

public abstract class Entity : IEntity
{
    public Guid Id { get; set; }
}
