using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.Features.Services.Common.Readers;
using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Application.Features.Services.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Services.Commands;

/// <summary>
/// A full replace. A barber missing from <see cref="ServiceWriteRequest.Staff"/> loses the service,
/// so the client sends inactive assigned barbers back too.
/// </summary>
public class UpdateServiceRequest : ServiceWriteRequest, ICommand<Result<ServiceResponse, HttpError>>
{
    [JsonIgnore]
    public Guid ServiceId { get; set; }
}

public class UpdateServiceHandler(IUnitOfWork<BookingContext> unitOfWork, IOutbox<BookingContext> outbox)
    : ICommandHandler<UpdateServiceRequest, Result<ServiceResponse, HttpError>>,
        IWithValidation<UpdateServiceRequest, Result<ServiceWriteInput, HttpError>>
{
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<ServiceTranslation, BookingContext> _serviceTranslationRepo =
        unitOfWork.GetRepository<ServiceTranslation>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();

    public Task<Result<ServiceWriteInput, HttpError>> ValidateAsync(
        UpdateServiceRequest request, CancellationToken cancellationToken) =>
        ServiceWriteValidation.ValidateAsync(unitOfWork, request, request.ServiceId, cancellationToken);

    public async Task<Result<ServiceResponse, HttpError>> HandleAsync(
        UpdateServiceRequest request, CancellationToken cancellationToken)
    {
        var input = this.ValidationData();

        var service = await _serviceRepo
            .Query(query => query.Where(row => row.Id == request.ServiceId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return ServiceReader.CreateNotFoundError();
        }

        var currentTranslations = await _serviceTranslationRepo
            .Query(query => query.Where(translation => translation.ServiceId == service.Id), asNoTracking: false)
            .ToListAsync(cancellationToken);

        var currentAssignments = await _staffServiceRepo
            .Query(query => query.Where(assignment => assignment.ServiceId == service.Id), asNoTracking: false)
            .ToListAsync(cancellationToken);

        var saved = await Result.TryAsync(
            async () =>
            {
                service.CategoryId = request.CategoryId;
                service.Price = request.Price;
                service.DurationMinutes = request.DurationMinutes;
                service.BufferBeforeMinutes = request.BufferBeforeMinutes;
                service.BufferAfterMinutes = request.BufferAfterMinutes;
                service.IsActive = request.IsActive;

                await ReplaceTranslationsAsync(service.Id, currentTranslations, input.Translations, cancellationToken);

                await StaffAssignmentWriter.WriteAsync(
                    _staffServiceRepo, request.CompanyId, service.Id, currentAssignments, input.Staff,
                    cancellationToken);

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The service could not be saved. Please try again.",
                errorCode: ErrorCodes.Services.UpdateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return await ServiceReader.ReadAsync(_serviceRepo, service.Id, cancellationToken);
    }

    /// <summary>Row by row on the composite key. A language no longer offered keeps its row.</summary>
    private async Task ReplaceTranslationsAsync(
        Guid serviceId,
        IReadOnlyList<ServiceTranslation> currentTranslations,
        IReadOnlyList<ServiceTranslationInput> wantedTranslations,
        CancellationToken cancellationToken)
    {
        var currentByLanguageIds = currentTranslations.ToDictionary(translation => translation.LanguageId);

        foreach (var wanted in wantedTranslations)
        {
            if (currentByLanguageIds.TryGetValue(wanted.LanguageId, out var current))
            {
                current.Name = wanted.Name;
                current.Description = wanted.Description;
                continue;
            }

            await _serviceTranslationRepo.AddAsync(
                new ServiceTranslation
                {
                    ServiceId = serviceId,
                    LanguageId = wanted.LanguageId,
                    Name = wanted.Name,
                    Description = wanted.Description
                },
                cancellationToken);
        }
    }
}
