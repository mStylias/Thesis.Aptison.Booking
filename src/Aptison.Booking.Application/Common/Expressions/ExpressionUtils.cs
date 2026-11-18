using System.Linq.Expressions;

namespace Aptison.Booking.Application.Common.Expressions;

/// <summary>Composes expression trees so a rule written once can run in SQL wherever it is needed.</summary>
public static class ExpressionUtils
{
    /// <summary>
    /// <c>source => then(select(source))</c>, with <paramref name="then"/> inlined rather than invoked,
    /// so EF Core translates it.
    /// </summary>
    public static Expression<Func<TSource, TResult>> Chain<TSource, TMiddle, TResult>(
        Expression<Func<TSource, TMiddle>> select, Expression<Func<TMiddle, TResult>> then) =>
        Expression.Lambda<Func<TSource, TResult>>(Inline(then, select.Body), select.Parameters);

    /// <summary>The body of <paramref name="lambda"/> with its single parameter replaced by <paramref name="argument"/>.</summary>
    public static Expression Inline(LambdaExpression lambda, Expression argument) =>
        new ParameterReplacer(lambda.Parameters[0], argument).Visit(lambda.Body);

    private sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }
}
