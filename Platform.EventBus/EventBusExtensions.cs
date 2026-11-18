using Carpathians.Platform.EventBus.Messaging;
using Carpathians.Platform.EventBus.Publishing;
using Carpathians.Platform.EventBus.Topology;
using Carpathians.Platform.ServiceDefaults.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Carpathians.Platform.EventBus;

/// <summary>
/// Підключення сервісу до брокера повідомлень:
/// <code>
/// builder.AddEventBus()
///     .DeclareExchange(CatalogTopology.Exchange)                 // publisher
///     .DeclareConsumerQueue(CatalogTopology.BookingCatalogSync); // consumer
/// </code>
/// </summary>
public static class EventBusExtensions
{
    /// <summary>Ім'я ресурсу RabbitMQ в AppHost = ім'я рядка підключення (ConnectionStrings:messaging).</summary>
    public const string ConnectionName = "messaging";

    /// <summary>
    /// IConnection — один на весь застосунок (singleton, його реєструє Aspire.RabbitMQ.Client);
    /// канали (IChannel) створюються окремо для кожної операції або кожного споживача.
    /// Aspire додає повторні спроби підключення й health check брокера.
    /// </summary>
    public static EventBusBuilder AddEventBus(this IHostApplicationBuilder builder)
    {
        builder.AddRabbitMQClient(ConnectionName, settings =>
        {
            // Трасування через брокер реалізовано вручну (TracePropagation, ActivitySource "Platform.Messaging"),
            // тому вбудовані spans клієнта не вмикаємо — без дублювання в Dashboard.
            settings.DisableTracing = true;
        });

        // Health check брокера від Aspire — також у readiness (/health/ready): без брокера сервіс
        // не публікує й не отримує події.
        builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            foreach (var registration in options.Registrations)
            {
                if (registration.Name.StartsWith("RabbitMQ", StringComparison.OrdinalIgnoreCase))
                {
                    registration.Tags.Add(HealthTags.Ready);
                    registration.Tags.Add("messaging");
                }
            }
        });

        builder.Services.AddOptions<TopologyOptions>();
        builder.Services.AddHostedService<TopologyInitializer>();

        // Publisher — singleton (тримає лише IConnection; канал створюється на кожну публікацію)
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<CorrelationContext>();
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        return new EventBusBuilder(builder);
    }
}

/// <summary>Налаштування топології сервісу (ланцюжок після AddEventBus).</summary>
public sealed class EventBusBuilder(IHostApplicationBuilder builder)
{
    public IHostApplicationBuilder Builder { get; } = builder;

    public IServiceCollection Services => Builder.Services;

    /// <summary>Exchange, у який сервіс публікує події (оголошується під час старту).</summary>
    public EventBusBuilder DeclareExchange(ExchangeDefinition exchange)
    {
        Services.Configure<TopologyOptions>(o => o.Exchanges.Add(exchange));
        return this;
    }

    /// <summary>Черга сервісу-споживача з bindings, чергою повторів і DLQ (оголошується під час старту).</summary>
    public EventBusBuilder DeclareConsumerQueue(ConsumerQueue queue)
    {
        Services.Configure<TopologyOptions>(o => o.ConsumerQueues.Add(queue));
        return this;
    }
}
