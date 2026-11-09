using Carpathians.Reviews.Application.Common.Behaviors;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carpathians.Reviews.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Реєстрація шару Application: MediatR (команди, запити, обробники, Pipeline Behaviors),
    /// валідатори FluentValidation та AutoMapper.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration config)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        var licenseKey = config["LuckyPenny:LicenseKey"];

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = licenseKey;
            cfg.RegisterServicesFromAssembly(assembly);

            // Порядок реєстрації = порядок виконання (зовнішній → внутрішній):
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));             // 1 — логування запиту
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));  // 2 — логування винятків
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));          // 3 — FluentValidation
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));         // 4 — час обробника
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddAutoMapper(cfg =>
        {
            cfg.LicenseKey = licenseKey;
            cfg.AddMaps(assembly);
        });

        return services;
    }
}
