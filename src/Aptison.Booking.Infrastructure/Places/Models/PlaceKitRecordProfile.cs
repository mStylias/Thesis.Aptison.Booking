using Aptison.Booking.Application.Abstractions.Places;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Domain.Reference;
using Synax.Mapper.Core;

namespace Aptison.Booking.Infrastructure.Places.Models;

/// <summary>
/// PlaceKit's vocabulary onto this API's. The only place that knows PlaceKit's field names, which
/// is what makes swapping provider an Infrastructure change and nothing else.
/// </summary>
/// <remarks>
/// Maps a record that <see cref="PlaceKitRecord.Position"/> answers for; the caller drops the rest.
/// </remarks>
internal sealed class PlaceKitRecordProfile : MapperProfile
{
    public PlaceKitRecordProfile()
    {
        CreateMap<PlaceKitRecord, ProviderPlace>()
            .For(source => source.Name, source => (source.Name ?? string.Empty).Trim())
            .For(source => source.Type, source => MapType(source.Type))
            .For(source => source.Latitude, source => source.Position!.Value.Latitude)
            .For(source => source.Longitude, source => source.Position!.Value.Longitude)
            .For(source => source.CountryCode, source => (source.CountryCode ?? string.Empty).Trim())
            .For(source => source.City, source => StringUtils.TrimToNull(source.City))
            .For(source => source.County, source => StringUtils.TrimToNull(source.County))
            .For(source => source.Region, source => StringUtils.TrimToNull(source.Administrative))
            .For(source => source.Country, source => StringUtils.TrimToNull(source.Country))
            .For(
                source => source.PostalCode,
                source => StringUtils.TrimToNull(
                    source.ZipCode == null ? null : source.ZipCode.FirstOrDefault()));
    }

    /// <summary>
    /// PlaceKit's kinds onto the closed set. <c>PlaceTypes.Address</c> has no source here —
    /// PlaceKit returns a house number as part of a <c>street</c> record — and is in the set for
    /// the providers and use cases that do distinguish one.
    /// </summary>
    private static string MapType(string? placeKitType) => placeKitType?.ToLowerInvariant() switch
    {
        "street" => PlaceTypes.Street,
        "city" => PlaceTypes.City,
        "administrative" => PlaceTypes.Administrative,
        "county" => PlaceTypes.County,
        "country" => PlaceTypes.Country,
        "airport" => PlaceTypes.Airport,
        "train" => PlaceTypes.Train,
        "bus" => PlaceTypes.Bus,
        "townhall" => PlaceTypes.TownHall,
        _ => PlaceTypes.Other
    };
}
