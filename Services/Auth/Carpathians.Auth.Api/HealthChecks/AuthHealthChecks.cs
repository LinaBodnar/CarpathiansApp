using Carpathians.Platform.ServiceDefaults.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Carpathians.Auth.Api.HealthChecks;

/// <summary>Health check Auth Service: identity-db (SQL Server, SELECT 1, 5 с) — без бази вхід неможливий.</summary>
public static class AuthHealthChecks
{
    public const string DatabaseCheck = "identity-db";

    public static IHostApplicationBuilder AddAuthHealthChecks(this IHostApplicationBuilder builder, string connectionString)
    {
        builder.Services.AddHealthChecks()
            .AddSqlServer(
                connectionString: connectionString,
                healthQuery: "SELECT 1;",
                name: DatabaseCheck,
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthTags.Database, "sql", HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(5));
        return builder;
    }
}
