using Carpathians.Platform.ApiGateway.Logging;
using Carpathians.Platform.ServiceDefaults.Security;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (спільні налаштування платформи)
builder.AddServiceDefaults();

builder.Services.AddSingleton<GatewayMetrics>();

// Лаб. 09: Gateway перевіряє JWT ПЕРЕД проксуванням. Політика маршруту (AuthorizationPolicy в appsettings.json):
// "default" — потрібен дійсний токен (інакше 401 без звернення до сервісу), "anonymous" — /auth/** (вхід, реєстрація).
// Заголовок Authorization передається сервісу без змін: сервіси перевіряють токен самостійно.
builder.AddPlatformAuthentication();

// YARP: маршрути й кластери — у секції ReverseProxy (appsettings.json);
// адреси http://booking-api, http://catalog-api ... підставляє service discovery Aspire.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddTransforms(transforms => transforms.AddRequestTransform(context =>
    {
        // X-User-Id — ідентифікатор користувача з перевіреного токена (для логів і простих сервісів).
        // Значення від клієнта завжди відкидається: підробити його неможливо.
        context.ProxyRequest.Headers.Remove(UserIdHeader);
        if (context.HttpContext.User.FindFirst(PlatformClaimTypes.Subject)?.Value is { Length: > 0 } userId)
            context.ProxyRequest.Headers.TryAddWithoutValidation(UserIdHeader, userId);
        return ValueTask.CompletedTask;
    }));

var app = builder.Build();

// Порядок важливий: CorrelationId генерується ДО маршрутизації, тож YARP передасть його сервісу.
app.UseCorrelationId();
app.UseMiddleware<GatewayLoggingMiddleware>();
app.UsePlatformAuthentication();

app.MapReverseProxy(proxy =>
{
    // рішення про маршрутизацію логується до проксування (кластер обрано, сервіс ще не викликано)
    proxy.Use(async (context, next) =>
    {
        var feature = context.GetReverseProxyFeature();
        app.Logger.LogDebug("Routing {Method} {Path} via route {RouteId} to cluster {ClusterId} ({Destinations} destinations)",
            context.Request.Method, context.Request.Path, feature.Route.Config.RouteId,
            feature.Cluster?.Config.ClusterId, feature.AvailableDestinations.Count);
        await next();
    });
    proxy.UseSessionAffinity();
    proxy.UseLoadBalancing();
    proxy.UsePassiveHealthChecks();
});

app.MapDefaultEndpoints();

app.Run();

public partial class Program
{
    /// <summary>Заголовок з ідентифікатором автентифікованого користувача для сервісів за Gateway.</summary>
    public const string UserIdHeader = "X-User-Id";
}
