using System.Collections.Concurrent;

namespace Carpathians.Auth.Api.Services;

/// <summary>Надсилання листів (підтвердження email, скидання пароля).</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, string? actionUrl, CancellationToken ct);
}

/// <summary>Лист, «надісланий» mock-сервісом.</summary>
public sealed record SentEmail(DateTime SentAt, string To, string Subject, string Body, string? ActionUrl);

/// <summary>
/// Mock email service для розробки: лист не надсилається, а пишеться в лог (з посиланням і токеном)
/// і зберігається в пам'яті — останні листи адресата видно на GET /auth/dev/emails?to=... (лише Development).
/// У production тут була б реалізація через SMTP або поштовий API.
/// </summary>
public sealed class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger, TimeProvider time) : IEmailSender
{
    private const int MaxStored = 200;
    private readonly ConcurrentQueue<SentEmail> _outbox = new();

    public Task SendAsync(string to, string subject, string body, string? actionUrl, CancellationToken ct)
    {
        _outbox.Enqueue(new SentEmail(time.GetUtcNow().UtcDateTime, to, subject, body, actionUrl));
        while (_outbox.Count > MaxStored && _outbox.TryDequeue(out _)) { }

        logger.LogInformation("Mock email to {To}: {Subject}. Link: {ActionUrl}", to, subject, actionUrl);
        return Task.CompletedTask;
    }

    public IReadOnlyList<SentEmail> For(string to)
        => _outbox.Where(e => string.Equals(e.To, to, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.SentAt)
            .ToList();
}
