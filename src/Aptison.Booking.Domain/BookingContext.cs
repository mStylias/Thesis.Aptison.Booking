using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain;

/// <summary>
/// The booking module's persistence context tag. Handlers resolve
/// <c>IUnitOfWork&lt;BookingContext&gt;</c> against this type, which lives in Domain, so Application
/// never references Infrastructure to name its own database. Infrastructure binds it to the real
/// context via <c>builder.AddPersistence&lt;BookingDbContext, BookingContext&gt;()</c>.
/// </summary>
public sealed class BookingContext : ISynaxContext;
