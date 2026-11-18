namespace Carpathians.Platform.ServiceDefaults.Security;

/// <summary>
/// Permissions (scopes) кожного Resource Server — дії, які можна дозволити чи заборонити окремо.
/// Назви за доменом платформи (замість orders/products з методички):
/// booking-api — бронювання (orders), catalog-api — маршрути (products), payments-api — платежі.
/// </summary>
public static class Permissions
{
    /// <summary>Resource Server booking-api (Проєкт №1, Бронювання).</summary>
    public static class Bookings
    {
        public const string ResourceServer = "booking-api";
        public const string Read = "bookings:read";
        public const string Create = "bookings:create";
        public const string Update = "bookings:update";
        public const string Delete = "bookings:delete";
        public const string Approve = "bookings:approve";

        public static readonly string[] All = [Read, Create, Update, Delete, Approve];
    }

    /// <summary>Resource Server catalog-api (Проєкт №2, Каталог маршрутів).</summary>
    public static class Routes
    {
        public const string ResourceServer = "catalog-api";
        public const string Read = "routes:read";
        public const string Create = "routes:create";
        public const string Update = "routes:update";
        public const string Delete = "routes:delete";
        public const string Publish = "routes:publish";

        public static readonly string[] All = [Read, Create, Update, Delete, Publish];
    }

    /// <summary>Resource Server payments-api (демо-сервіс платежів).</summary>
    public static class Payments
    {
        public const string ResourceServer = "payments-api";
        public const string Read = "payments:read";
        public const string Process = "payments:process";
        public const string Refund = "payments:refund";

        public static readonly string[] All = [Read, Process, Refund];
    }

    public static readonly string[] All = [.. Bookings.All, .. Routes.All, .. Payments.All];
}

/// <summary>Ролі платформи (realm-ролі Keycloak; ролі Auth Service User/Admin зіставляються з user/admin).</summary>
public static class PlatformRoleNames
{
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string User = "user";
    public const string Guest = "guest";

    public static readonly string[] All = [Admin, Manager, User, Guest];
}

/// <summary>
/// Матриця «роль -> permissions» (таблиця 7 методички). Централізовано її задає Keycloak (Pulumi, infra/keycloak);
/// тут — та сама матриця для токенів Auth Service, у яких є лише ролі.
/// payments-api свідомо доступний лише admin: платежі — найчутливіший ресурс.
/// </summary>
public static class RolePermissionMatrix
{
    public static readonly IReadOnlyDictionary<string, string[]> Permissions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [PlatformRoleNames.Admin] = Security.Permissions.All,
        [PlatformRoleNames.Manager] =
        [
            Security.Permissions.Bookings.Read, Security.Permissions.Bookings.Update, Security.Permissions.Bookings.Approve,
            Security.Permissions.Routes.Read, Security.Permissions.Routes.Update, Security.Permissions.Routes.Publish
        ],
        [PlatformRoleNames.User] =
        [
            Security.Permissions.Bookings.Read, Security.Permissions.Bookings.Create,
            Security.Permissions.Routes.Read
        ],
        [PlatformRoleNames.Guest] =
        [
            Security.Permissions.Routes.Read
        ]
    };

    public static IEnumerable<string> For(string role)
        => Permissions.TryGetValue(role, out var permissions) ? permissions : [];
}

/// <summary>
/// OAuth-scopes IdentityServer (Лаб. 09, частина 2) -> permissions. Scope грубіший за permission:
/// "*.read" — лише читання, "*.write" — усі зміни ресурсу.
/// </summary>
public static class ScopePermissionMap
{
    public const string BookingsRead = "bookings.read";
    public const string BookingsWrite = "bookings.write";
    public const string RoutesRead = "routes.read";
    public const string RoutesWrite = "routes.write";
    public const string PaymentsRead = "payments.read";
    public const string PaymentsWrite = "payments.write";

    public static readonly IReadOnlyDictionary<string, string[]> Permissions = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [BookingsRead] = [Security.Permissions.Bookings.Read],
        [BookingsWrite] =
        [
            Security.Permissions.Bookings.Create, Security.Permissions.Bookings.Update,
            Security.Permissions.Bookings.Delete, Security.Permissions.Bookings.Approve
        ],
        [RoutesRead] = [Security.Permissions.Routes.Read],
        [RoutesWrite] =
        [
            Security.Permissions.Routes.Create, Security.Permissions.Routes.Update,
            Security.Permissions.Routes.Delete, Security.Permissions.Routes.Publish
        ],
        [PaymentsRead] = [Security.Permissions.Payments.Read],
        [PaymentsWrite] = [Security.Permissions.Payments.Process, Security.Permissions.Payments.Refund]
    };
}
