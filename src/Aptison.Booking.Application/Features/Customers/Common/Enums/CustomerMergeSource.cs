using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Customers.Common.Enums;

/// <summary>Which of the two merged walk-ins a value is taken from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CustomerMergeSource>))]
public enum CustomerMergeSource
{
    Kept,
    Removed
}
