using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Carpathians.Platform.EventBus.Topology;

/// <summary>Що оголошує сервіс під час старту: exchanges, у які він публікує, і черги, з яких споживає.</summary>
public sealed class TopologyOptions
{
    public List<ExchangeDefinition> Exchanges { get; } = [];

    public List<ConsumerQueue> ConsumerQueues { get; } = [];
}

/// <summary>
/// Оголошує топологію сервісу під час старту застосунку. Працює у фоні: якщо брокер ще недоступний,
/// повторює спробу, не блокуючи запуск HTTP/gRPC-endpoint-ів сервісу.
/// </summary>
public sealed class TopologyInitializer(
    IConnection connection,
    IOptions<TopologyOptions> options,
    ILogger<TopologyInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topology = options.Value;
        if (topology.Exchanges.Count == 0 && topology.ConsumerQueues.Count == 0)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                foreach (var exchange in topology.Exchanges)
                    await RabbitMqTopology.DeclareExchangeAsync(channel, exchange, stoppingToken);

                foreach (var queue in topology.ConsumerQueues)
                    await RabbitMqTopology.DeclareConsumerQueueAsync(channel, queue, stoppingToken);

                logger.LogInformation(
                    "Messaging topology declared: exchanges {Exchanges}, queues {Queues}",
                    topology.Exchanges.Select(e => e.Name).ToArray(),
                    topology.ConsumerQueues.Select(q => q.Name).ToArray());
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to declare messaging topology, next attempt in {RetryIntervalSeconds} s",
                    RetryInterval.TotalSeconds);
                await Task.Delay(RetryInterval, stoppingToken);
            }
        }
    }
}
