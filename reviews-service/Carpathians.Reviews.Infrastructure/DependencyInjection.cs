using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Infrastructure.Persistence;
using Carpathians.Reviews.Infrastructure.Persistence.Mappings;
using Carpathians.Reviews.Infrastructure.Persistence.Queries;
using Carpathians.Reviews.Infrastructure.Persistence.Repositories;
using Carpathians.Reviews.Infrastructure.Seeding;
using Carpathians.Reviews.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carpathians.Reviews.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Реєстрація шару Infrastructure: BSON-мапінг, MongoDbContext, репозиторії (запис),
    /// реалізації IReviewQueries/IDiscussionQueries (читання), сервіс рейтингів,
    /// автоматичне створення індексів і сідінг під час старту.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        MongoMappings.Register();                      // до першого звернення до БД

        services.Configure<MongoDbOptions>(config.GetSection(MongoDbOptions.SectionName));
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();
        services.AddScoped<IDiscussionQueries, DiscussionQueries>();
        services.AddScoped<IRatingService, RatingService>();

        // Ідемпотентний сідінг (по одному сідеру на колекцію)
        services.AddSingleton<IDataSeeder, DiscussionSeeder>();
        services.AddSingleton<IDataSeeder, ReviewSeeder>();
        services.AddSingleton<IDataSeeder, RatingSeeder>();

        // Hosted services стартують у порядку реєстрації: спершу індекси, потім сідінг
        services.AddHostedService<IndexCreationService>();
        services.AddHostedService<DataSeedingService>();

        return services;
    }
}
