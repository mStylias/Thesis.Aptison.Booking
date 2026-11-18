namespace Aptison.Booking.Infrastructure.Analytics;

/// <summary>
/// The analytics queries. Dapper skips the ownership and soft-delete filters, so every query starts
/// from <see cref="ScopedAppointments"/>, and every table joined to it checks its own
/// <c>owner_id</c> and <c>is_deleted</c>. Columns are aliased to the row record's property names.
/// </summary>
internal static class AnalyticsSql
{
    private const string ScopedAppointments =
        """
        WITH scoped AS (
            SELECT a.*
            FROM appointments a
            WHERE a.owner_id = @CompanyId
              AND NOT a.is_deleted
              AND a.local_date BETWEEN @From AND @To
              AND (@StaffMemberId::uuid IS NULL OR a.staff_member_id = @StaffMemberId::uuid))
        """;

    public const string AppointmentDailyFacts =
        ScopedAppointments +
        """

        SELECT a.local_date AS "LocalDate",
               a.staff_member_id AS "StaffMemberId",
               a.status AS "Status",
               a.source AS "Source",
               (a.cancelled_by_id IS NOT NULL AND a.cancelled_by_id = a.customer_id) AS "CancelledByCustomer",
               count(*)::int AS "Appointments",
               sum(a.total_price) AS "Revenue",
               sum(round(extract(epoch FROM a.ends_at - a.starts_at) / 60))::int AS "Minutes"
        FROM scoped a
        GROUP BY 1, 2, 3, 4, 5
        """;

    // No is_deleted check on services: a deleted service's past lines still count.
    public const string ServiceLineFacts =
        ScopedAppointments +
        """

        SELECT l.service_id AS "ServiceId",
               s.category_id AS "CategoryId",
               a.status AS "Status",
               count(*)::int AS "Lines",
               sum(l.price_at_booking) AS "Revenue"
        FROM scoped a
        JOIN appointment_services l ON l.appointment_id = a.id AND l.owner_id = @CompanyId AND NOT l.is_deleted
        JOIN services s ON s.id = l.service_id AND s.owner_id = @CompanyId
        GROUP BY 1, 2, 3
        """;

    public const string HourlyFacts =
        ScopedAppointments +
        """

        SELECT extract(isodow FROM a.local_date)::int AS "IsoWeekday",
               extract(hour FROM a.local_start_time)::int AS "Hour",
               a.status AS "Status",
               count(*)::int AS "Appointments"
        FROM scoped a
        GROUP BY 1, 2, 3
        """;

    public const string LeadTimeFacts =
        ScopedAppointments +
        """

        SELECT a.local_date - (a.created_at AT TIME ZONE c.timezone)::date AS "LeadDays",
               a.source AS "Source",
               a.status AS "Status",
               count(*)::int AS "Appointments"
        FROM scoped a
        JOIN companies c ON c.id = a.owner_id
        GROUP BY 1, 2, 3
        """;

    // The window runs over the customer's whole history at the shop, so the range and staff filter
    // apply after it, not in it.
    public const string CustomerVisitFacts =
        """
        WITH history AS (
            SELECT a.customer_id,
                   a.staff_member_id,
                   a.local_date,
                   a.total_price,
                   lag(a.local_date) OVER visits AS previous_visit_on,
                   min(a.local_date) OVER (PARTITION BY a.customer_id) AS first_visit_on
            FROM appointments a
            WHERE a.owner_id = @CompanyId
              AND NOT a.is_deleted
              AND a.status = @Completed
              AND a.local_date <= @To
            WINDOW visits AS (PARTITION BY a.customer_id ORDER BY a.local_date, a.starts_at))
        SELECT h.customer_id AS "CustomerId",
               h.local_date AS "LocalDate",
               h.total_price AS "TotalPrice",
               h.previous_visit_on AS "PreviousVisitOn",
               h.first_visit_on AS "FirstVisitOn"
        FROM history h
        WHERE h.local_date BETWEEN @From AND @To
          AND (@StaffMemberId::uuid IS NULL OR h.staff_member_id = @StaffMemberId::uuid)
        """;

    public const string ReviewDailyFacts =
        ScopedAppointments +
        """

        SELECT a.local_date AS "LocalDate",
               a.staff_member_id AS "StaffMemberId",
               r.rating::int AS "Rating",
               count(*)::int AS "Reviews"
        FROM scoped a
        JOIN reviews r ON r.appointment_id = a.id AND r.owner_id = @CompanyId AND NOT r.is_deleted
        GROUP BY 1, 2, 3
        """;
}
