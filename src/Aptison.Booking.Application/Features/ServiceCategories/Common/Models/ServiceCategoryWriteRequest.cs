using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.ServiceCategories.Common.Models;

/// <summary>The body create and rename share.</summary>
public abstract class ServiceCategoryWriteRequest
{
    /// <summary>The request's resolved company, never bound from the body.</summary>
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    /// <summary>The language of the returned name.</summary>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    /// <summary>One per platform language.</summary>
    public IReadOnlyList<ServiceCategoryTranslationAddEditDto> Translations { get; set; } = [];
}
