using Carpathians.Platform.IdentityServer;
using Carpathians.Platform.IdentityServer.Data;
using Carpathians.Platform.IdentityServer.Seeding;
using Carpathians.Platform.ServiceDefaults.Health;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks (спільні налаштування платформи)
builder.AddServiceDefaults();

// ---------- База IdentityServerDb на ОКРЕМОМУ SQL Server (ізоляція від бізнес-сервісів) ----------
// Рядок підключення передає AppHost. Запасне значення — лише для dotnet ef (генерація міграцій без AppHost).
var connectionString = builder.Configuration.GetConnectionString("IdentityServerDb")
                       ?? "Server=localhost;Database=IdentityServerDb;Integrated Security=false;TrustServerCertificate=true";
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddRazorPages();

// ASP.NET Identity: користувачі й ролі IdentityServer
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---------- Duende IdentityServer ----------
builder.Services.AddIdentityServer(options =>
    {
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseInformationEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;

        // Ключ Community Edition (необов'язково): без нього — режим розробки з обмеженнями.
        if (builder.Configuration["IdentityServer:LicenseKey"] is { Length: > 0 } licenseKey)
            options.LicenseKey = licenseKey;
    })
    .AddAspNetIdentity<ApplicationUser>()                       // користувачі — з ASP.NET Identity
    .AddConfigurationStore(options =>                           // Clients, ApiResources, ApiScopes, IdentityResources
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)))
    .AddOperationalStore(options =>                             // PersistedGrants, DeviceCodes, Keys
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
        options.EnableTokenCleanup = true;
        options.TokenCleanupInterval = 3600;
    });

builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, healthQuery: "SELECT 1;", name: "identityserver-db",
        failureStatus: HealthStatus.Unhealthy, tags: [HealthTags.Database, "sql", HealthTags.Ready],
        timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();

// Міграції трьох контекстів, конфігурація IdentityServer і тестові користувачі — під час старту
await IdentityServerSeeder.SeedAsync(app.Services, app.Configuration, app.Logger);

app.UseCorrelationId();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();           // /connect/authorize, /connect/token, /.well-known/openid-configuration ...
app.UseAuthorization();

// Quickstart UI (Razor Pages): вхід, згода, вихід. Сторінки без [AllowAnonymous] — лише для автентифікованих.
app.MapRazorPages().RequireAuthorization();
app.MapDefaultEndpoints();

app.Run();
