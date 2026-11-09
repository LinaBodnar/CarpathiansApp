using Carpathians.Platform.ApiGateway.Logging;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (спільні налаштування платформи)
builder.AddServiceDefaults();

builder.Services.AddSingleton<GatewayMetrics>();

// YARP: маршрути й кластери — у секції ReverseProxy (appsettings.json);
// адреси http://booking-api, http://catalog-api ... підставляє service discovery Aspire.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

// Порядок важливий: CorrelationId генерується ДО маршрутизації, тож YARP передасть його сервісу.
app.UseCorrelationId();
app.UseMiddleware<GatewayLoggingMiddleware>();

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
