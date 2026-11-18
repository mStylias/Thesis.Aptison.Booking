using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace Aptison.Booking.Infrastructure.Persistence;

/// <summary>
/// OData compares an <c>Edm.Date</c> as <c>year * 10000 + month * 100 + day</c>, which no index can
/// serve, so a calendar month would scan the company's whole history. This folds the arithmetic back
/// into a plain <see cref="DateOnly"/> comparison. <see cref="DateTime"/> is left alone: comparing
/// it directly would count the time of day, which the arithmetic does not.
/// </summary>
internal sealed class ODataDateComparisonInterceptor : IQueryExpressionInterceptor
{
    public static readonly ODataDateComparisonInterceptor Instance = new();

    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData) =>
        new DateComparisonRewriter().Visit(queryExpression);

    private sealed class DateComparisonRewriter : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            var isComparison = node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
                or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
                or ExpressionType.LessThan or ExpressionType.LessThanOrEqual;

            var leftIsDate = TryGetDate(node.Left, out var left);
            var rightIsDate = TryGetDate(node.Right, out var right);

            if (isComparison && (leftIsDate || rightIsDate))
            {
                // By the time EF compiles, the literal's arithmetic has been evaluated into a parameter.
                left ??= TryBuildDate(node.Left);
                right ??= TryBuildDate(node.Right);

                if (left is not null && right is not null)
                {
                    return Expression.MakeBinary(node.NodeType, Visit(left), Visit(right));
                }
            }

            return base.VisitBinary(node);
        }

        /// <summary>The <c>DateOnly</c> behind <c>(d.Year * 10000 + d.Month * 100) + d.Day</c>.</summary>
        private static bool TryGetDate(Expression expression, out Expression? date)
        {
            date = null;

            if (Unwrap(expression) is not BinaryExpression { NodeType: ExpressionType.Add } outer
                || Unwrap(outer.Left) is not BinaryExpression { NodeType: ExpressionType.Add } inner
                || GetScaledPart(inner.Left, nameof(DateOnly.Year), 10000) is not { } year
                || GetScaledPart(inner.Right, nameof(DateOnly.Month), 100) is not { } month
                || GetPart(outer.Right, nameof(DateOnly.Day)) is not { } day
                || year.Type != typeof(DateOnly)
                || !ExpressionEqualityComparer.Instance.Equals(year, month)
                || !ExpressionEqualityComparer.Instance.Equals(year, day))
            {
                return false;
            }

            date = year;
            return true;
        }

        private static Expression? TryBuildDate(Expression expression)
        {
            var number = Unwrap(expression);

            if (number.Type != typeof(int))
            {
                return null;
            }

            return Expression.New(
                typeof(DateOnly).GetConstructor([typeof(int), typeof(int), typeof(int)])!,
                Expression.Divide(number, Expression.Constant(10000)),
                Expression.Modulo(Expression.Divide(number, Expression.Constant(100)), Expression.Constant(100)),
                Expression.Modulo(number, Expression.Constant(100)));
        }

        private static Expression? GetScaledPart(Expression expression, string part, int scale) =>
            Unwrap(expression) is BinaryExpression { NodeType: ExpressionType.Multiply } multiply
            && Unwrap(multiply.Right) is ConstantExpression { Value: int factor }
            && factor == scale
                ? GetPart(multiply.Left, part)
                : null;

        private static Expression? GetPart(Expression expression, string part) =>
            Unwrap(expression) is MemberExpression { Expression: { } source } member && member.Member.Name == part
                ? Unwrap(source)
                : null;

        private static Expression Unwrap(Expression expression) =>
            expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert
                ? Unwrap(convert.Operand)
                : expression;
    }
}
