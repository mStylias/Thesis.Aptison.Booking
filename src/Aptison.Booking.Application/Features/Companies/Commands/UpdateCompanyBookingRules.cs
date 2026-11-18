using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>
/// Applies to every booking and cancellation after the save; the cancellation window is read at
/// cancel time, so existing bookings follow it too. Existing appointments keep their times.
/// </summary>
public class UpdateCompanyBookingRulesRequest : ICommand<Result<CompanyBookingRulesResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public int? SlotIntervalMinutes { get; set; }
    public int? MinLeadTimeMinutes { get; set; }
    public int? MaxAdvanceDays { get; set; }
    public int? CancellationCutoffMinutes { get; set; }
}

public class UpdateCompanyBookingRulesHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IMapper mapper)
    : ICommandHandler<UpdateCompanyBookingRulesRequest, Result<CompanyBookingRulesResponse, HttpError>>,
        IWithValidation<UpdateCompanyBookingRulesRequest, Result<HttpError>>
{
    private static readonly int[] SlotIntervals = [5, 10, 15, 20, 30, 60];

    private const int MaxNoticeMinutes = 10_080;

    // How far ahead the availability index is kept.
    private const int MaxAdvanceDays = 60;
    private const int MinuteStep = 5;

    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();

    public Task<Result<HttpError>> ValidateAsync(
        UpdateCompanyBookingRulesRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new Validation("The booking rules are invalid.")
            .Require(
                request.SlotIntervalMinutes is { } interval && SlotIntervals.Contains(interval),
                ErrorCodes.Companies.InvalidBookingRules,
                $"The slot interval must be one of {string.Join(", ", SlotIntervals)} minutes.",
                nameof(request.SlotIntervalMinutes))
            .Require(
                IsNotice(request.MinLeadTimeMinutes),
                ErrorCodes.Companies.InvalidBookingRules,
                $"The minimum notice must be 0 to {MaxNoticeMinutes} minutes, in steps of {MinuteStep}.",
                nameof(request.MinLeadTimeMinutes))
            .Require(
                request.MaxAdvanceDays is >= 1 and <= MaxAdvanceDays,
                ErrorCodes.Companies.InvalidBookingRules,
                $"Customers can book 1 to {MaxAdvanceDays} days ahead.",
                nameof(request.MaxAdvanceDays))
            .Require(
                IsNotice(request.CancellationCutoffMinutes),
                ErrorCodes.Companies.InvalidBookingRules,
                $"The free-cancellation window must be 0 to {MaxNoticeMinutes} minutes, in steps of {MinuteStep}.",
                nameof(request.CancellationCutoffMinutes))
            .ToResult());

    public async Task<Result<CompanyBookingRulesResponse, HttpError>> HandleAsync(
        UpdateCompanyBookingRulesRequest request, CancellationToken cancellationToken)
    {
        var company = await _companyRepo
            .Query(query => query.Where(row => row.Id == request.CompanyId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return CompanySettingsErrors.CreateNotFound();
        }

        var saved = await Result.TryAsync(
            async () =>
            {
                company.SlotIntervalMinutes = request.SlotIntervalMinutes!.Value;
                company.MinLeadTimeMinutes = request.MinLeadTimeMinutes!.Value;
                company.MaxAdvanceDays = request.MaxAdvanceDays!.Value;
                company.CancellationCutoffMinutes = request.CancellationCutoffMinutes!.Value;

                // The catalog flag evicts the cached booking and shop pages, which show these rules.
                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => CompanySettingsErrors.CreateUpdateFailed());

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return mapper.Map<CompanyBookingRulesResponse>(company);
    }

    private static bool IsNotice(int? minutes) =>
        minutes is >= 0 and <= MaxNoticeMinutes && minutes % MinuteStep == 0;
}
