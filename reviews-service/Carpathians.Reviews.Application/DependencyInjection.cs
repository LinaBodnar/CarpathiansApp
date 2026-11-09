using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carpathians.Reviews.Application;

public static class DependencyInjection
{
    /// <summary>Реєстрація шару Application: MediatR (команди, запити, обробники) та AutoMapper.</summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration config)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        var licenseKey = config["LuckyPenny:LicenseKey"];

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = licenseKey;
            cfg.RegisterServicesFromAssembly(assembly);
        });

        services.AddAutoMapper(cfg =>
        {
            cfg.LicenseKey = licenseKey;
            cfg.AddMaps(assembly);
        });

        return services;
    }
}
