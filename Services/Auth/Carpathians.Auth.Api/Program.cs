using Carpathians.Auth.Api.Controllers;
using Carpathians.Auth.Api.Data;
using Carpathians.Auth.Api.HealthChecks;
using Carpathians.Auth.Api.Services;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (спільні налаштування платформи)
builder.AddServiceDefaults();

// ---------- ASP.NET Identity на окремій базі IdentityDb (рядок підключення передає AppHost) ----------
var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
                       ?? throw new InvalidOperationException("Connection string 'IdentityDb' is missing");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;          // вхід лише після підтвердження email
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Токени підтвердження email і скидання пароля: обмежений термін дії; одноразовість — через security stamp.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(authOptions.EmailTokenLifetimeHours));

// ---------- JWT: перевірка access token (ті самі issuer, audience і ключ, що в Gateway та мікросервісах) ----------
// Викликається ПІСЛЯ AddIdentity: схемою за замовчуванням стає Bearer, а не cookie Identity.
builder.AddPlatformAuthentication();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddSingleton<IEmailSender, DevelopmentEmailSender>();     // mock email (листи — у лог і /auth/dev/emails)

// Health checks: identity-db (SQL Server) — readiness
builder.AddAuthHealthChecks(connectionString);

builder.Services.AddControllers();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Carpathians Auth Service", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token з POST /auth/login"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

var app = builder.Build();

// Міграції IdentityDb, ролі User/Admin, адміністратор — під час старту
await IdentitySeeder.SeedAsync(app.Services, app.Logger);

app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UsePlatformAuthentication();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
