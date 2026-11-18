namespace Aptison.Booking.Application.Features.Services.Common.Rules;

internal static class ServiceLimits
{
    public const decimal MaxPrice = 9999.99m;
    public const int MinDurationMinutes = 5;
    public const int MaxDurationMinutes = 480;
    public const int MaxBufferMinutes = 120;
    public const int MinuteStep = 5;

    // service_translations.name is varchar(200).
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 1000;
    public const int MaxCategoryNameLength = 100;

    public const int CategorySortOrderStep = 10;

    public static readonly string PriceRule = $"The price must be 0 to {MaxPrice} with at most two decimals.";

    public static readonly string DurationRule =
        $"The duration must be {MinDurationMinutes} to {MaxDurationMinutes} minutes, in steps of {MinuteStep}.";

    public static bool IsValidPrice(decimal price) =>
        price is >= 0 and <= MaxPrice && decimal.Round(price, 2) == price;

    public static bool IsValidDuration(int minutes) =>
        minutes is >= MinDurationMinutes and <= MaxDurationMinutes && minutes % MinuteStep == 0;
}
