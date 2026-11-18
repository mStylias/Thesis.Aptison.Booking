using Aptison.Booking.Domain;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;

namespace Aptison.Booking.Application.Behaviors;

/// <summary>
/// Commits a handler's whole unit of work as one transaction, attached per handler with
/// <c>[MediatorBehavior(typeof(BookingTransactionBehavior&lt;,&gt;))]</c>.
/// <para>
/// Only for a handler that needs several round trips to land together. One
/// <c>SaveChangesAsync</c> is already one transaction.
/// </para>
/// <para>
/// <b>Never attach it to a handler that invalidates the permission cache.</b> The transaction is
/// still open when the handler returns, so eviction would run before the commit and the cache
/// would refill from the old rows.
/// </para>
/// </summary>
public sealed class BookingTransactionBehavior<TRequest, TResponse>(IUnitOfWork<BookingContext> unitOfWork)
    : TransactionBehavior<BookingContext, TRequest, TResponse>(unitOfWork)
    where TRequest : IRequest<TResponse>;
