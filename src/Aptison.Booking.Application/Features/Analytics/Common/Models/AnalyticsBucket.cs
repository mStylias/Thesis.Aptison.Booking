namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>One point on a time axis: a day, week or month, cut to the requested range. Both ends inclusive.</summary>
public readonly record struct AnalyticsBucket(DateOnly From, DateOnly To);
