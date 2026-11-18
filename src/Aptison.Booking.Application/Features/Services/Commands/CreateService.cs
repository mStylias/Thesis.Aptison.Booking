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

/// <summary>The client sends the barbers to assign; nobody is assigned by default.</summary>
public class CreateServiceRequest : ServiceWriteRequest, ICommand<Result<ServiceResponse, HttpError>>;

public class CreateServiceHandler(IUnitOfWork<BookingContext> unitOfWork, IOutbox<BookingContext> outbox)
    : ICommandHandler<CreateServiceRequest, Result<ServiceResponse, HttpError>>,
        IWithValidation<CreateServiceRequest, Result<ServiceWriteInput, HttpError>>
{
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<ServiceTranslation, BookingContext> _serviceTranslationRepo =
        unitOfWork.GetRepository<ServiceTranslation>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();

    public Task<Result<ServiceWriteInput, HttpError>> ValidateAsync(
        CreateServiceRequest request, CancellationToken cancellationToken) =>
        ServiceWriteValidation.ValidateAsync(unitOfWork, request, excludingServiceId: null, cancellationToken);

    public async Task<Result<ServiceResponse, HttpError>> HandleAsync(
        CreateServiceRequest request, CancellationToken cancellationToken)
    {
        var input = this.ValidationData();

        var service = new Service
        {
            Id = Guid.CreateVersion7(),
            OwnerId = request.CompanyId,
            CategoryId = request.CategoryId,
            Price = request.Price,
            DurationMinutes = request.DurationMinutes,
            BufferBeforeMinutes = request.BufferBeforeMinutes,
            BufferAfterMinutes = request.BufferAfterMinutes,
            IsActive = request.IsActive
        };

        var saved = await Result.TryAsync(
            async () =>
            {
                await _serviceRepo.AddAsync(service, cancellationToken);

                foreach (var translation in input.Translations)
                {
                    await _serviceTranslationRepo.AddAsync(
                        new ServiceTranslation
                        {
                            ServiceId = service.Id,
                            LanguageId = translation.LanguageId,
                            Name = translation.Name,
                            Description = translation.Description
                        },
                        cancellationToken);
                }

                await StaffAssignmentWriter.WriteAsync(
                    _staffServiceRepo, request.CompanyId, service.Id, [], input.Staff, cancellationToken);

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The service could not be saved. Please try again.",
                errorCode: ErrorCodes.Services.CreateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return await ServiceReader.ReadAsync(_serviceRepo, service.Id, cancellationToken);
    }
}
