using Carpathians.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Seeding;

/// <summary>
/// Запускає всі <see cref="IDataSeeder"/> по порядку після створення індексів.
/// Вимикається параметром MongoDb:SeedOnStartup (false в appsettings.Production.json).
/// </summary>
public class DataSeedingService(
    IEnumerable<IDataSeeder> seeders,
    IOptions<MongoDbOptions> options,
    ILogger<DataSeedingService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!options.Value.SeedOnStartup)
        {
            logger.LogInformation("MongoDB seeding is disabled");
            return;
        }

        try
        {
            foreach (var seeder in seeders.OrderBy(s => s.Order))
                await seeder.SeedAsync(ct);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogError(ex, "Failed to seed MongoDB");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
