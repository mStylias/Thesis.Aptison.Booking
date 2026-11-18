using Aptison.Booking.Application.Features.Customers.Common.ResponseOData;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Customers.Common.ProfilesOData;

/// <summary>The customer grid. <see cref="CustomerSelectors"/> has the rules every member follows.</summary>
public class CustomerODataResponseProfile : MapperProfile
{
    public CustomerODataResponseProfile()
    {
        var selectors = new CustomerSelectors(
            Parameter<DateTime>("utcNow"), Parameter<DateOnly>("shopToday"), Parameter<bool>("includeSpend"));

        CreateMap<User, CustomerODataResponse>()
            .For(dto => dto.DisplayName, selectors.DisplayName)
            .For(dto => dto.PhoneNumber, selectors.PhoneNumber)
            .For(dto => dto.IsWalkIn, selectors.IsWalkIn)
            .For(dto => dto.VisitCount, selectors.VisitCount)
            .For(dto => dto.FirstVisitDate, selectors.FirstVisitDate)
            .For(dto => dto.LastVisitDate, selectors.LastVisitDate)
            .For(dto => dto.DaysSinceLastVisit, selectors.DaysSinceLastVisit)
            .For(dto => dto.NextAppointmentAt, selectors.NextAppointmentAt)
            .For(dto => dto.UpcomingCount, selectors.UpcomingCount)
            .For(dto => dto.NoShowCount, selectors.NoShowCount)
            .For(dto => dto.LastNoShowDate, selectors.LastNoShowDate)
            .For(dto => dto.CancellationCount, selectors.CancellationCount)
            .For(dto => dto.PreferredStaffMemberId, selectors.PreferredStaffMemberId)
            .For(dto => dto.PreferredStaffDisplayName, selectors.PreferredStaffDisplayName)
            .For(dto => dto.PreferredStaffIsActive, selectors.PreferredStaffIsActive)
            .For(dto => dto.HasNote, user => user.CompanyCustomers.Any(customer => customer.Note != null))
            .For(dto => dto.IsBlocked, user => user.Bans.Any(ban => ban.OwnerId != null && ban.LiftedAt == null))
            .For(dto => dto.CustomerSince, selectors.CustomerSince)
            .For(dto => dto.TotalSpend, selectors.TotalSpend)
            .For(dto => dto.AverageSpend, selectors.AverageSpend);
    }
}
