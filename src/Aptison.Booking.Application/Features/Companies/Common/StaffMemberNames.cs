using System.Globalization;
using System.Linq.Expressions;
using Aptison.Booking.Application.Common.Expressions;
using Aptison.Booking.Domain.Entities.Users;

namespace Aptison.Booking.Application.Features.Companies.Common;

/// <summary>
/// How a staff member is named on a public page: first name and last-name initial, never the full
/// last name.
/// </summary>
internal static class StaffMemberNames
{
    /// <summary>"Γιώργος Π." — or the first name alone when there is no last name.</summary>
    public static string GetDisplayName(string firstName, string lastName)
    {
        var lastInitial = GetFirstLetter(lastName);

        return lastInitial.Length == 0 ? firstName.Trim() : $"{firstName.Trim()} {lastInitial}.";
    }

    /// <summary>
    /// <see cref="GetDisplayName"/> as SQL can run it, for a projection. It trims but does not split text
    /// elements; <c>ListAppointmentsODataHandlerTests</c> keeps the two in step.
    /// </summary>
    public static Expression<Func<TSource, string>> BuildDisplayNameSelector<TSource>(
        Expression<Func<TSource, User>> selectUser) =>
        ExpressionUtils.Chain(selectUser, DisplayNameRule);

    /// <summary><see cref="BuildDisplayNameSelector{TSource}"/> for an optional user: null without one.</summary>
    public static Expression<Func<TSource, string?>> BuildOptionalDisplayNameSelector<TSource>(
        Expression<Func<TSource, User?>> selectUser) =>
        Expression.Lambda<Func<TSource, string?>>(
            Expression.Condition(
                Expression.Equal(selectUser.Body, Expression.Constant(null, typeof(User))),
                Expression.Constant(null, typeof(string)),
                ExpressionUtils.Inline(DisplayNameRule, selectUser.Body)),
            selectUser.Parameters);

    private static readonly Expression<Func<User, string>> DisplayNameRule = user =>
        user.LastName.Trim() == ""
            ? user.FirstName.Trim()
            : user.FirstName.Trim() + " " + user.LastName.Trim().Substring(0, 1).ToUpper() + ".";

    /// <summary>"ΓΠ".</summary>
    public static string GetInitials(string firstName, string lastName) =>
        GetFirstLetter(firstName) + GetFirstLetter(lastName);

    // A text element, not a char, so a letter stored with a combining accent is not split.
    private static string GetFirstLetter(string name)
    {
        var trimmed = name.Trim();

        return trimmed.Length == 0
            ? string.Empty
            : StringInfo.GetNextTextElement(trimmed).ToUpperInvariant();
    }
}
