namespace Aptison.Booking.Tests.Common.Data;

/// <summary>
/// The reference rows the migrations insert. They are committed once when the container starts, so
/// every test may point at them — and must, because they are foreign keys.
/// </summary>
public static class TestIds
{
    public static class Languages
    {
        public static readonly Guid English = new("11111122-2222-4111-8111-111111111111");
        public static readonly Guid Greek = new("11111122-2222-4111-8111-111111111112");
    }

    public static class BusinessTypes
    {
        public static readonly Guid Barbershop = new("20000000-0000-4000-8000-000000000001");
        public static readonly Guid HairSalon = new("20000000-0000-4000-8000-000000000002");
    }

    /// <summary>The platform categories of the barbershop vertical, in sort order.</summary>
    public static class ServiceCategories
    {
        public static readonly Guid Haircuts = new("30000000-0000-4000-8000-000000000001");
        public static readonly Guid BeardAndShave = new("30000000-0000-4000-8000-000000000002");
        public static readonly Guid Grooming = new("30000000-0000-4000-8000-000000000003");
        public static readonly Guid Colour = new("30000000-0000-4000-8000-000000000004");
        public static readonly Guid Kids = new("30000000-0000-4000-8000-000000000005");
    }

    public static class Roles
    {
        public static readonly Guid SuperAdmin = new("10000000-0000-4000-8000-000000000001");
        public static readonly Guid CompanyAdmin = new("10000000-0000-4000-8000-000000000002");
        public static readonly Guid Staff = new("10000000-0000-4000-8000-000000000003");
        public static readonly Guid Customer = new("10000000-0000-4000-8000-000000000004");
        public static readonly Guid StaffSenior = new("10000000-0000-4000-8000-000000000005");
        public static readonly Guid StaffAdmin = new("10000000-0000-4000-8000-000000000006");
    }
}
