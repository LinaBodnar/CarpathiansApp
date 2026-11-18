using Carpathians.Reviews.Application.Common.Behaviors;
using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carpathians.Reviews.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Реєстрація шару Application: MediatR (команди, запити, обробники, Pipeline Behaviors),
    /// валідатори FluentValidation та AutoMapper.
    /// </summary>
    /// <param name="mappingAssemblies">Додаткові збірки з профілями AutoMapper (наприклад, WebApi).</param>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration config, params Assembly[] mappingAssemblies)
        => services.AddApplication(config, configureOuterBehaviors: null, mappingAssemblies);

    /// <param name="configureOuterBehaviors">
    /// Behaviors рівня хоста, що виконуються ЗОВНІ від behaviors шару Application (наприклад, метрики WebApi
    /// мають бачити й невдалу валідацію). Application про них не знає — лише надає точку розширення.
    /// </param>
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration config,
        Action<MediatRServiceConfiguration>? configureOuterBehaviors, params Assembly[] mappingAssemblies)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        var licenseKey = config["LuckyPenny:LicenseKey"];

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = licenseKey;
            cfg.RegisterServicesFromAssembly(assembly);

            configureOuterBehaviors?.Invoke(cfg);                         // 0 — behaviors хоста (метрики)

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
            cfg.AddMaps(new[] { assembly }.Concat(mappingAssemblies).Distinct());
        });

        return services;
    }
}
