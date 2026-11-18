using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Infrastructure.Persistence;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aptison.Booking.Infrastructure.Analytics;

/// <summary>
/// Runs <see cref="AnalyticsSql"/> on the request's own connection and transaction. The company
/// comes only from <see cref="AnalyticsReadScope.CompanyId"/>: nothing here sees the ownership filter.
/// </summary>
internal sealed class DapperAnalyticsReader(BookingDbContext dbContext) : IAnalyticsReader
{
    public Task<IReadOnlyList<AppointmentDailyFacts>> ReadAppointmentDailyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        QueryAsync<AppointmentDailyFacts>(AnalyticsSql.AppointmentDailyFacts, scope, cancellationToken);

    public Task<IReadOnlyList<ServiceLineFacts>> ReadServiceLineFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        QueryAsync<ServiceLineFacts>(AnalyticsSql.ServiceLineFacts, scope, cancellationToken);

    public Task<IReadOnlyList<HourlyFacts>> ReadHourlyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        QueryAsync<HourlyFacts>(AnalyticsSql.HourlyFacts, scope, cancellationToken);

    public Task<IReadOnlyList<LeadTimeFacts>> ReadLeadTimeFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        QueryAsync<LeadTimeFacts>(AnalyticsSql.LeadTimeFacts, scope, cancellationToken);

    public Task<IReadOnlyList<CustomerVisitFacts>> ReadCustomerVisitFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        QueryAsync<CustomerVisitFacts>(AnalyticsSql.CustomerVisitFacts, scope, cancellationToken);

    public Task<IReadOnlyList<ReviewDailyFacts>> ReadReviewDailyFactsAsync(
        AnalyticsReadScope scope, CancellationToken cancellationToken) =>
        QueryAsync<ReviewDailyFacts>(AnalyticsSql.ReviewDailyFacts, scope, cancellationToken);

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql, AnalyticsReadScope scope, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            scope.CompanyId,
            scope.From,
            scope.To,
            scope.StaffMemberId,
            Completed = AppointmentStatus.Completed
        };

        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            var rows = await dbContext.Database.GetDbConnection().QueryAsync<T>(new CommandDefinition(
                sql,
                parameters,
                dbContext.Database.CurrentTransaction?.GetDbTransaction(),
                cancellationToken: cancellationToken));

            return rows.AsList();
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
