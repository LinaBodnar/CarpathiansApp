using System.Diagnostics;
using System.Text.Json;
using Carpathians.Platform.EventBus.Diagnostics;
using Carpathians.Platform.EventBus.Messaging;
using Carpathians.Platform.EventBus.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Carpathians.Platform.EventBus.Consuming;

/// <summary>
/// Довготривалий споживач черги (BackgroundService) з ручним підтвердженням:
/// <list type="number">
/// <item>канал, топологія черги (основна + .retry + .dlq), BasicQos(prefetch), AsyncEventingBasicConsumer,
/// BasicConsumeAsync(autoAck: false);</item>
/// <item>кожне повідомлення: span Consumer із відновленого traceparent, CorrelationId, DI-scope, десеріалізація,
/// обробник за типом події;</item>
/// <item>рішення щодо підтвердження (рис. 2 методички):
///   успіх — <c>BasicAckAsync</c>;
///   тимчасовий збій — повторна публікація в чергу .retry з x-retry-count + 1 і ack оригіналу
///   (requeue повернув би повідомлення без змін — лічильник не збільшити);
///   вичерпані спроби або постійний збій (некоректний JSON, невідомий тип, несумісна версія, некоректні дані) —
///   <c>BasicNackAsync(requeue: false)</c> -> dead letter exchange -> DLQ.</item>
/// </list>
/// Без ack брокер не видаляє повідомлення: якщо сервіс зупиниться посеред обробки, повідомлення буде доставлене знову.
/// </summary>
public sealed class RabbitMqEventConsumer(
    ConsumerQueue queue,
    EventHandlerRegistry handlers,
    IConnection connection,
    IServiceScopeFactory scopeFactory,
    MessagingMetrics metrics,
    ILogger<RabbitMqEventConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DepthPollInterval = TimeSpan.FromSeconds(15);

    // publisher confirms: повторна публікація в .retry підтверджена брокером ДО ack оригіналу — повідомлення не губиться
    private static readonly CreateChannelOptions ChannelOptions =
        new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = PollQueueDepthsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilChannelClosedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consumer of {Queue} failed, restarting in {RestartDelaySeconds} s",
                    queue.Name, RestartDelay.TotalSeconds);
            }

            await Task.Delay(RestartDelay, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    private async Task ConsumeUntilChannelClosedAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connection.CreateChannelAsync(ChannelOptions, stoppingToken);

        await RabbitMqTopology.DeclareConsumerQueueAsync(channel, queue, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: queue.PrefetchCount, global: false, stoppingToken);

        // Канал закрито через помилку рівня каналу (з'єднання живе) — перезапуск споживача.
        // Обрив з'єднання відновлює автоматичне відновлення клієнта (канали й споживачі разом із ним).
        var channelLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, args) =>
        {
            if (args.Initiator != ShutdownInitiator.Application && connection.IsOpen)
                channelLost.TrySetResult();
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => OnMessageAsync(channel, ea);

        var consumerTag = await channel.BasicConsumeAsync(queue.Name, autoAck: false, consumer, stoppingToken);
        logger.LogInformation(
            "Consumer started on {Queue} (prefetch {Prefetch}, max retries {MaxRetries}), events {EventTypes}",
            queue.Name, queue.PrefetchCount, queue.MaxRetries, handlers.EventTypes.ToArray());

        var stopped = Task.Delay(Timeout.Infinite, stoppingToken);
        await Task.WhenAny(channelLost.Task, stopped);

        if (stoppingToken.IsCancellationRequested)
        {
            // коректна зупинка: спершу припиняємо отримання, потім закриваємо канал;
            // непідтверджені повідомлення брокер поверне в чергу
            try
            {
                await channel.BasicCancelAsync(consumerTag, noWait: false, CancellationToken.None);
                await channel.CloseAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Consumer of {Queue} stopped with an already closed channel", queue.Name);
            }

            logger.LogInformation("Consumer of {Queue} stopped", queue.Name);
            stoppingToken.ThrowIfCancellationRequested();
        }

        logger.LogWarning("Channel of {Queue} was closed by the broker, restarting consumer", queue.Name);
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var eventType = props.Type;
        var messageId = props.MessageId;
        var retryCount = ReadInt(props.Headers, MessageHeaders.RetryCount);
        var correlationId = (props.Headers is { } headers && headers.TryGetValue(MessageHeaders.CorrelationId, out var header)
                                ? TracePropagation.ReadHeader(header)
                                : null)
                            ?? props.CorrelationId;

        using var activity = TracePropagation.StartConsumerActivity(props.Headers, queue.Name, messageId, eventType, retryCount);
        activity?.SetTag("correlation.id", correlationId);
        using var correlationScope = CorrelationContext.BeginMessageScope(correlationId);
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["EventId"] = messageId,
            ["EventType"] = eventType,
            ["CorrelationId"] = correlationId,
            ["Queue"] = queue.Name,
            ["RetryCount"] = retryCount
        });

        var started = Stopwatch.GetTimestamp();
        string outcome;
        try
        {
            logger.LogInformation("Received {EventType} {EventId} from {Queue} (attempt {Attempt})",
                eventType, messageId, queue.Name, retryCount + 1);

            await DispatchAsync(ea, eventType);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            outcome = MessageOutcome.Ack;
            logger.LogInformation("Processed {EventType} {EventId}, ack", eventType, messageId);
        }
        catch (Exception ex) when (IsPermanent(ex))
        {
            // постійний збій: повтор не допоможе — одразу в DLQ (x-death з причиною додасть RabbitMQ)
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex,
                "Permanent failure for {EventType} {EventId} in {Queue}: {Reason}. Sending to DLQ {DeadLetterQueue}",
                eventType, messageId, queue.Name, ex.Message, queue.DeadLetterQueue);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            outcome = MessageOutcome.DeadLetter;
        }
        catch (Exception ex)
        {
            // тимчасовий збій (БД недоступна, таймаут): повтор із лічильником або DLQ після вичерпання спроб
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            outcome = await RetryOrDeadLetterAsync(channel, ea, retryCount, ex);
        }

        metrics.Processed(queue.Name, eventType, outcome, Stopwatch.GetElapsedTime(started));
    }

    private async Task DispatchAsync(BasicDeliverEventArgs ea, string? eventType)
    {
        if (!handlers.TryGet(eventType, out var registration))
            throw new PermanentEventException($"Unsupported event type '{eventType ?? "<none>"}' for queue {queue.Name}");

        var version = ReadInt(ea.BasicProperties.Headers, MessageHeaders.EventVersion, defaultValue: 1);
        if (version > registration.MaxSupportedVersion)
            throw new PermanentEventException(
                $"Unsupported schema version {version} of {eventType} (max {registration.MaxSupportedVersion})");

        var @event = registration.Deserialize(ea.Body);         // JsonException -> DLQ

        await using var scope = scopeFactory.CreateAsyncScope();
        // CancellationToken.None: розпочата обробка завершується навіть під час зупинки сервісу
        await registration.Invoke(scope.ServiceProvider, @event, CancellationToken.None);
    }

    private async Task<string> RetryOrDeadLetterAsync(IChannel channel, BasicDeliverEventArgs ea, int attempt, Exception ex)
    {
        var props = ea.BasicProperties;

        if (attempt >= queue.MaxRetries)
        {
            logger.LogError(ex,
                "{EventType} {EventId} failed after {Attempts} retries in {Queue}, sending to DLQ {DeadLetterQueue}",
                props.Type, props.MessageId, attempt, queue.Name, queue.DeadLetterQueue);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);   // -> DLX -> DLQ
            return MessageOutcome.DeadLetter;
        }

        // Заголовки отриманого повідомлення змінити не можна — публікуємо копію зі збільшеним лічильником
        // у чергу затримки .retry (через default exchange), звідки після TTL вона повернеться в основну чергу.
        var retryProps = new BasicProperties(props)
        {
            Persistent = true,
            Headers = new Dictionary<string, object?>(props.Headers ?? new Dictionary<string, object?>())
            {
                [MessageHeaders.RetryCount] = attempt + 1,
                [MessageHeaders.LastError] = $"{ex.GetType().Name}: {Truncate(ex.Message, 500)}"
            }
        };

        try
        {
            await channel.BasicPublishAsync(exchange: "", routingKey: queue.RetryQueue, mandatory: false,
                retryProps, ea.Body);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        }
        catch (Exception publishError)
        {
            // повтор не вдалося запланувати — повертаємо оригінал у чергу (лічильник не зміниться, але й втрати немає)
            logger.LogError(publishError, "Failed to schedule retry of {EventType} {EventId}, requeueing",
                props.Type, props.MessageId);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            return MessageOutcome.Retry;
        }

        logger.LogWarning(ex,
            "{EventType} {EventId} failed in {Queue}: {Reason}. Retry {Attempt}/{MaxRetries} in {RetryDelaySeconds} s",
            props.Type, props.MessageId, queue.Name, ex.Message, attempt + 1, queue.MaxRetries,
            queue.RetryDelay.TotalSeconds);
        return MessageOutcome.Retry;
    }

    /// <summary>Глибина основної черги, черги повторів і DLQ — для gauge messaging.queue.depth.</summary>
    private async Task PollQueueDepthsAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(DepthPollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false) is true)
        {
            try
            {
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                foreach (var name in new[] { queue.Name, queue.RetryQueue, queue.DeadLetterQueue })
                    metrics.SetQueueDepth(name, await channel.MessageCountAsync(name, stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to read depth of {Queue}", queue.Name);
            }
        }
    }

    /// <summary>
    /// Постійні збої — повтор не допоможе: некоректний JSON, невідомий тип або версія події,
    /// некоректні бізнес-дані (<see cref="PermanentEventException"/>).
    /// </summary>
    private static bool IsPermanent(Exception ex) => ex is JsonException or PermanentEventException or NotSupportedException;

    private static int ReadInt(IDictionary<string, object?>? headers, string key, int defaultValue = 0)
    {
        if (headers is null || !headers.TryGetValue(key, out var value) || value is null)
            return defaultValue;

        return value switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
