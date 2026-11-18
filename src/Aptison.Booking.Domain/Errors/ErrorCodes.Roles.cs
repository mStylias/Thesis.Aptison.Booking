namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Roles
    {
        public const string NotFound = "ROLE_NOT_FOUND";
        public const string FetchFailed = "ROLE_FETCH_FAILED";
        public const string CreateFailed = "ROLE_CREATE_FAILED";
        public const string UpdateFailed = "ROLE_UPDATE_FAILED";
        public const string DeleteFailed = "ROLE_DELETE_FAILED";

        public const string InvalidName = "ROLE_INVALID_NAME";

        /// <summary>Another role visible in this scope already carries the name.</summary>
        public const string NameTaken = "ROLE_NAME_TAKEN";

        /// <summary>One of the roles the platform seeds.</summary>
        public const string SystemRoleImmutable = "ROLE_SYSTEM_IMMUTABLE";

        /// <summary>Readable here but owned elsewhere — a platform role seen from a company.</summary>
        public const string NotOwnedByCompany = "ROLE_NOT_OWNED_BY_COMPANY";

        /// <summary>A requested permission code has no row in the catalog. A typo, not a refusal.</summary>
        public const string UnknownPermission = "ROLE_UNKNOWN_PERMISSION";

        /// <summary>
        /// The code is real but <c>permissions.is_tenant_grantable = false</c>, so a company-owned
        /// role may never contain it.
        /// </summary>
        public const string PermissionNotTenantGrantable = "ROLE_PERMISSION_NOT_TENANT_GRANTABLE";

        /// <summary>The caller tried to put authority they do not hold into a role.</summary>
        public const string PermissionEscalation = "ROLE_PERMISSION_ESCALATION";
    }

    public static class RoleAssignments
    {
        public const string NotFound = "ROLE_ASSIGNMENT_NOT_FOUND";
        public const string FetchFailed = "ROLE_ASSIGNMENT_FETCH_FAILED";
        public const string AssignFailed = "ROLE_ASSIGNMENT_FAILED";
        public const string RevokeFailed = "ROLE_ASSIGNMENT_REVOKE_FAILED";

        /// <summary>The user already holds the role in this scope.</summary>
        public const string AlreadyAssigned = "ROLE_ASSIGNMENT_ALREADY_EXISTS";

        /// <summary>The target is not an active member of the company's staff.</summary>
        public const string NotStaffMember = "ROLE_ASSIGNMENT_NOT_STAFF_MEMBER";
    }
}
