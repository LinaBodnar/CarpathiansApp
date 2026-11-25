using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Carpathians.Platform.ServiceDefaults.Security;

/// <summary>
/// Налаштування JWT Auth Service (секція "Jwt"). Значення передає AppHost: issuer і audience однакові
/// для всіх сервісів, секрет — параметр-секрет AppHost (jwt-secret), у репозиторії його немає.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "carpathians-auth";
    public string Audience { get; set; } = "carpathians-api";

    /// <summary>Ключ HMAC-SHA256, щонайменше 32 символи.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Час життя access token — 15 хвилин.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Час життя refresh token — 7 днів.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SecretKey));
}

/// <summary>Короткі імена claims у токенах платформи (без мапінгу на довгі URI ClaimTypes).</summary>
public static class PlatformClaimTypes
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string UserName = JwtRegisteredClaimNames.UniqueName;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Role = "role";
}

/// <summary>Ролі Auth Service.</summary>
public static class PlatformRoles
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static readonly string[] All = [User, Admin];
}

/// <summary>
/// Автентифікація запитів у мікросервісах і Gateway: кожен сервіс сам перевіряє підпис, issuer, audience
/// і термін дії JWT — без звернення до Auth Service на кожен запит.
/// <para>Схема за замовчуванням "Bearer" — policy scheme: за issuer токена вона передає перевірку
/// відповідній JwtBearer-схемі: Auth Service, IdentityServer або Keycloak.</para>
/// </summary>
public static class PlatformAuthentication
{
    /// <summary>Схема за замовчуванням: вибір конкретної схеми за issuer токена.</summary>
    public const string DefaultScheme = "Bearer";

    /// <summary>Токени власного Auth Service (ASP.NET Identity + JWT, симетричний ключ).</summary>
    public const string AuthServiceScheme = "AuthService";

    /// <summary>Токени Duende IdentityServer (OAuth 2.0 / OIDC): ключі — з discovery document Authority.</summary>
    public const string IdentityServerScheme = "IdentityServer";

    /// <summary>Токени Keycloak (realm DevRealmDrivers): access token і RPT з permissions Authorization Services.</summary>
    public const string KeycloakScheme = "Keycloak";

    /// <summary>Реєструє автентифікацію JWT та авторизацію. Викликати в Program.cs сервісу після AddServiceDefaults().</summary>
    public static TBuilder AddPlatformAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var section = builder.Configuration.GetSection(JwtSettings.SectionName);
        var settings = section.Get<JwtSettings>() ?? new JwtSettings();
        builder.Services.Configure<JwtSettings>(section);

        if (settings.SecretKey.Length < 32)
        {
            // Без секрету (сервіс запущено поза AppHost) жоден токен Auth Service не пройде перевірку підпису.
            settings.SecretKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
            builder.Services.AddSingleton<IStartupFilter>(new MissingSecretWarning());
        }

        builder.Services
            .AddAuthentication(options =>
            {
                // Явно для всіх дій: ASP.NET Identity (Auth Service) реєструє cookie-схеми за замовчуванням,
                // а API платформи працюють лише з Bearer-токенами.
                options.DefaultScheme = DefaultScheme;
                options.DefaultAuthenticateScheme = DefaultScheme;
                options.DefaultChallengeScheme = DefaultScheme;
                options.DefaultForbidScheme = DefaultScheme;
            })
            .AddPolicyScheme(DefaultScheme, "JWT (вибір схеми за issuer)", options =>
                options.ForwardDefaultSelector = SelectScheme)
            .AddJwtBearer(AuthServiceScheme, options =>
            {
                options.MapInboundClaims = false;            // claims як у токені: sub, unique_name, role
                options.TokenValidationParameters = CreateValidationParameters(settings);
            });

        builder.AddIdentityServerJwtBearer();

        builder.Services.AddAuthorization();
        return builder;
    }

    /// <summary>
    /// Токени IdentityServer (частина 2): Authority = адреса IdentityServer (IdentityServer:Authority від AppHost),
    /// Audience = API resource сервісу (IdentityServer:Audience; кілька — через кому, напр. у Gateway).
    /// Ключі підпису завантажуються з /.well-known/openid-configuration; перевірка — локальна.
    /// </summary>
    private static void AddIdentityServerJwtBearer(this IHostApplicationBuilder builder)
    {
        var authority = builder.Configuration["IdentityServer:Authority"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(authority))
            return;                                        // сервіс запущено без IdentityServer

        var audiences = (builder.Configuration["IdentityServer:Audience"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var isDevelopment = builder.Environment.IsDevelopment();

        builder.Services.AddAuthentication()
            .AddJwtBearer(IdentityServerScheme, options =>
            {
                options.Authority = authority;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidateAudience = audiences.Length > 0;
                options.TokenValidationParameters.ValidAudiences = audiences;
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = PlatformClaimTypes.Role;
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
                if (isDevelopment)
                    options.BackchannelHttpHandler = DevelopmentCertificates.CreateHandler();
            });

        builder.Services.AddIssuerScheme(IdentityServerScheme,
            issuer => string.Equals(issuer.TrimEnd('/'), authority, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// JWT Bearer на realm Keycloak (частина 3) — викликається в Program.cs сервісу після AddPlatformAuthentication():
    /// Authority = адреса realm (Keycloak:Authority від AppHost, напр. http://localhost:8080/realms/DevRealmDrivers),
    /// Audience = client id resource server (Keycloak:Audience; кілька — через кому).
    /// Ключі підпису завантажуються з OIDC discovery document realm. Permissions читаються з claims
    /// <c>authorization</c> (RPT) і <c>permissions</c> (client-ролі resource servers), ролі — з claim <c>role</c>
    /// (<see cref="PermissionClaims"/>).
    /// </summary>
    public static TBuilder AddKeycloakJwtBearer<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var authority = builder.Configuration["Keycloak:Authority"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(authority))
            return builder;                                // сервіс запущено без Keycloak

        var audiences = (builder.Configuration["Keycloak:Audience"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        builder.Services.AddAuthentication()
            .AddJwtBearer(KeycloakScheme, options =>
            {
                options.Authority = authority;
                // Keycloak у Docker під Aspire працює по HTTP лише в Development; в інших середовищах — тільки HTTPS
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidateAudience = audiences.Length > 0;
                options.TokenValidationParameters.ValidAudiences = audiences;
                options.TokenValidationParameters.NameClaimType = "preferred_username";
                options.TokenValidationParameters.RoleClaimType = PlatformClaimTypes.Role;
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            });

        builder.Services.AddIssuerScheme(KeycloakScheme,
            issuer => string.Equals(issuer.TrimEnd('/'), authority, StringComparison.OrdinalIgnoreCase));
        return builder;
    }

    /// <summary>Middleware автентифікації й авторизації (після UseRouting / UseExceptionHandler, до Map*).</summary>
    public static WebApplication UsePlatformAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>Параметри перевірки токенів Auth Service — ті самі в Auth Service, Gateway і мікросервісах.</summary>
    public static TokenValidationParameters CreateValidationParameters(JwtSettings settings) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = settings.Issuer,
        ValidateAudience = true,
        ValidAudience = settings.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = settings.CreateSigningKey(),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = PlatformClaimTypes.UserName,
        RoleClaimType = PlatformClaimTypes.Role
    };

    /// <summary>
    /// Вибір схеми за claim "iss" (без перевірки підпису — її виконає обрана схема).
    /// Невідомий issuer -> схема Auth Service, яка й поверне 401.
    /// </summary>
    private static string SelectScheme(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthServiceScheme;

        var token = header["Bearer ".Length..].Trim();
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return AuthServiceScheme;

        var issuer = handler.ReadJwtToken(token).Issuer;
        if (string.IsNullOrEmpty(issuer))
            return AuthServiceScheme;
        var issuers = context.RequestServices.GetRequiredService<IOptions<IssuerSchemeOptions>>().Value;
        foreach (var (scheme, matches) in issuers.Schemes)
            if (matches(issuer))
                return scheme;

        return AuthServiceScheme;
    }

    /// <summary>Реєструє додаткову JwtBearer-схему (IdentityServer, Keycloak) і правило розпізнавання її issuer.</summary>
    public static void AddIssuerScheme(this IServiceCollection services, string scheme, Func<string, bool> matchesIssuer)
        => services.Configure<IssuerSchemeOptions>(o => o.Schemes.Add((scheme, matchesIssuer)));

    /// <summary>Додаткові схеми з правилом розпізнавання issuer (порядок = пріоритет).</summary>
    public sealed class IssuerSchemeOptions
    {
        public List<(string Scheme, Func<string, bool> MatchesIssuer)> Schemes { get; } = [];
    }

    private sealed class MissingSecretWarning : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.ApplicationServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Carpathians.Security")
                .LogWarning("Jwt:SecretKey is not configured: tokens of the Auth Service cannot be validated (run the service through AppHost)");
            next(app);
        };
    }
}
