namespace Aptison.Booking.Domain.Authorization;

/// <summary>
/// The compile-time handle on the permission catalog; the rows are the source of truth. Drift is
/// caught by <c>EndpointPermissionGuard</c>, which fails the host on a declared code with no row.
/// <para>
/// Adding a code is two edits — a row in the schema migration and a constant here. Prefer an
/// ownership check in a handler for "your own" rules; a code is for authority a role hands out.
/// </para>
/// </summary>
public static class PermissionCodes
{
    public static class Members
    {
        public const string Read = "members:read";
        public const string Invite = "members:invite";
        public const string Update = "members:update";
        public const string Remove = "members:remove";
    }

    public static class Services
    {
        public const string Read = "services:read";
        public const string Create = "services:create";
        public const string Update = "services:update";
        public const string Delete = "services:delete";
    }

    /// <summary>The <c>ManageOwn...</c> codes reach only the caller's own staff row.</summary>
    public static class Staff
    {
        public const string ManageSchedule = "staff:manage_schedule";
        public const string ManageServices = "staff:manage_services";
        public const string ManageTimeOff = "staff:manage_timeoff";
        public const string ManageOwnSchedule = "staff:manage_own_schedule";
        public const string ManageOwnTimeOff = "staff:manage_own_timeoff";
    }

    public static class Appointments
    {
        public const string ReadAll = "appointments:read_all";
        public const string Create = "appointments:create";
        public const string Update = "appointments:update";
        public const string Cancel = "appointments:cancel";
    }

    /// <summary>The shop's own settings. Reading them needs membership only.</summary>
    public static class Company
    {
        public const string Update = "company:update";
    }

    public static class Analytics
    {
        public const string Read = "analytics:read";
    }

    /// <summary>The shop's customers page. Every write also needs <see cref="Read"/>.</summary>
    public static class Customers
    {
        public const string Read = "customers:read";
        public const string Update = "customers:update";
        public const string Block = "customers:block";
        public const string Merge = "customers:merge";

        /// <summary>Gates the page's export button only: the list itself is what <see cref="Read"/> allows.</summary>
        public const string Export = "customers:export";
    }

    public static class Users
    {
        /// <summary>Reads any account across the platform. Never tenant-grantable.</summary>
        public const string Read = "users:read";
    }

    public static class Roles
    {
        public const string Read = "roles:read";
        public const string Create = "roles:create";
        public const string Update = "roles:update";
        public const string Delete = "roles:delete";
        public const string Assign = "roles:assign";
    }

    public static class Platform
    {
        /// <summary>
        /// The code the superadmin bypass keys on — checked once in
        /// <c>PermissionAuthorizationHandler</c> for every requirement, never by name at a call
        /// site. Keying on a permission rather than the <c>super_admin</c> role name means a
        /// permission added tomorrow does not exclude superadmins until <c>role_permissions</c> is
        /// backfilled. Marked <c>is_tenant_grantable = false</c> in the catalog.
        /// </summary>
        public const string Manage = "platform:manage";
    }
}
