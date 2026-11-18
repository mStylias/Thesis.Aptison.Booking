namespace Aptison.Booking.Api.Endpoints.V1.Company;

internal static class CompanyEndpoints
{
    public const string MemberReadJustification =
        "Every member of a shop may read its settings. The handler refuses a caller with no company, " +
        "which is a customer, a deactivated member, or a platform administrator who named no shop.";
}
