using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carpathians.Platform.EventBus.Messaging;

/// <summary>
/// Єдині правила серіалізації для publisher і consumer (System.Text.Json):
/// camelCase, enum — рядком (стійко до зміни порядку значень), дати — ISO 8601 в UTC,
/// невідомі поля ігноруються (нові поля нової версії не ламають старого споживача).
/// </summary>
public static class EventJson
{
    public const string ContentType = "application/json";

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static byte[] Serialize<T>(T @event) => JsonSerializer.SerializeToUtf8Bytes(@event, Options);

    /// <summary>Порожнє тіло або <c>null</c> — теж некоректне повідомлення (<see cref="JsonException"/>).</summary>
    public static T Deserialize<T>(ReadOnlySpan<byte> body)
        => JsonSerializer.Deserialize<T>(body, Options) ?? throw new JsonException("Message body is empty");
}
