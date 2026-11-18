using System.Linq.Expressions;
using System.Reflection;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Infrastructure.Persistence;

/// <summary>
/// Makes OData's <c>contains</c>, <c>startswith</c> and <c>endswith</c> case- and accent-insensitive
/// by comparing <c>fold(field)</c> with <c>fold(value)</c>, so "γιωργος" finds "Γιώργος". <c>eq</c> and
/// <c>ne</c> stay exact. Synax hands it only the client's query options, never a handler's LINQ.
/// </summary>
internal sealed class ODataTextFoldingRewriter : ExpressionVisitor, IODataExpressionRewriter
{
    private static readonly MethodInfo[] TextMatches =
    [
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!,
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!
    ];

    private static readonly MethodInfo Fold = typeof(BookingDbFunctions).GetMethod(nameof(BookingDbFunctions.Fold))!;

    public Expression Rewrite(Expression expression) => Visit(expression);

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Object is null || !TextMatches.Contains(node.Method))
        {
            return base.VisitMethodCall(node);
        }

        return Expression.Call(
            Expression.Call(Fold, Visit(node.Object)),
            node.Method,
            Expression.Call(Fold, Visit(node.Arguments[0])));
    }
}
