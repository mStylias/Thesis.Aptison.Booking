namespace Aptison.Booking.Application.Features.Companies.Common;

/// <summary>
/// HybridCache tags for the public company pages. Anything that changes what a shop's home page
/// shows evicts <see cref="GetCompanyTag"/> and <see cref="Sitemap"/>; see <c>docs/TODO.md</c>.
/// </summary>
internal static class CompanyCacheTags
{
    public const string Sitemap = "companies:sitemap";

    public static string GetCompanyTag(Guid companyId) => $"company:{companyId}";
}
