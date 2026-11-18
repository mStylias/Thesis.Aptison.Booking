using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Customers.Common.Models;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Domain.Entities.Users;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Customers.Common.Profiles;

/// <summary>The customer details. <see cref="CustomerSelectors"/> has the rules every member follows.</summary>
public class CustomerResponseProfile : MapperProfile
{
    public CustomerResponseProfile()
    {
        var selectors = new CustomerSelectors(
            Parameter<DateTime>("utcNow"), Parameter<DateOnly>("shopToday"), Parameter<bool>("includeSpend"));
        var canUpdate = Parameter<bool>("canUpdate");
        var canMerge = Parameter<bool>("canMerge");

        CreateMap<User, CustomerResponse>()
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
            .For(dto => dto.CustomerSince, selectors.CustomerSince)
            .For(dto => dto.TotalSpend, selectors.TotalSpend)
            .For(dto => dto.AverageSpend, selectors.AverageSpend)
            .For(dto => dto.Note, user => user.CompanyCustomers.FirstOrDefault(customer => customer.Note != null))
            .For(dto => dto.Block, user => user.Bans
                .Where(ban => ban.OwnerId != null && ban.LiftedAt == null)
                .OrderByDescending(ban => ban.BannedAt)
                .FirstOrDefault())
            .For(dto => dto.CanEditContactDetails, user => canUpdate.Value && user.OwnerId != null)
            .For(dto => dto.CanMerge, user => canMerge.Value && user.OwnerId != null);

        CreateMap<CompanyCustomer, CustomerNoteViewDto>()
            .For(dto => dto.Text, customer => customer.Note!)
            .For(dto => dto.UpdatedAt, customer => (DateTimeOffset?)customer.NoteUpdatedAt)
            .For(dto => dto.UpdatedByDisplayName,
                StaffMemberNames.BuildOptionalDisplayNameSelector<CompanyCustomer>(customer => customer.NoteUpdatedBy));

        CreateMap<UserBan, CustomerBlockViewDto>()
            .For(dto => dto.BlockedAt, ban => (DateTimeOffset)ban.BannedAt)
            .For(dto => dto.BlockedByDisplayName,
                StaffMemberNames.BuildDisplayNameSelector<UserBan>(ban => ban.BannedBy));
    }
}
