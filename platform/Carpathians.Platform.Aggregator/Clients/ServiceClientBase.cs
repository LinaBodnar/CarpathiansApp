using System.Diagnostics;
using System.Net;

namespace Carpathians.Platform.Aggregator.Clients;

/// <summary>
/// Спільна логіка typed HttpClient: логування кожного виклику та його тривалості,
/// 404 -> null (ресурсу немає — це не відмова сервісу), інші помилки -> виняток.
/// Адреса — логічне ім'я сервісу (service discovery), CorrelationId додає CorrelationIdHandler
/// з ServiceDefaults, resilience — стандартний обробник з ServiceDefaults.
/// </summary>
public abstract class ServiceClientBase(HttpClient http, ILogger logger, string serviceName)
{
    public string ServiceName { get; } = serviceName;

    protected async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        logger.LogInformation("Calling {Service} {Path}", ServiceName, path);
        var sw = Stopwatch.StartNew();

        using var response = await http.GetAsync(path, ct);
        sw.Stop();

        logger.LogInformation("{Service} responded {StatusCode} in {ElapsedMs} ms for {Path}",
            ServiceName, (int)response.StatusCode, sw.ElapsedMilliseconds, path);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }
}
