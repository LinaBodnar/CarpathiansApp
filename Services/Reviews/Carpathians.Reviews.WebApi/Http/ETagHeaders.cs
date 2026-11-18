using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace Carpathians.Reviews.WebApi.Http;

/// <summary>
/// ETag / If-Match для оптимістичної конкурентності: GET повертає версію документа в ETag,
/// PUT/DELETE передають очікувану версію в If-Match.
/// </summary>
public static class ETagHeaders
{
    public static string Format(int version) => $"\"{version}\"";

    public static void SetETag(this HttpResponse response, int version)
        => response.Headers[HeaderNames.ETag] = Format(version);

    /// <summary>
    /// Очікувана версія з If-Match; null — заголовка немає або він «*» (будь-яка версія).
    /// Некоректне значення — FormatException (перетворюється на 400).
    /// </summary>
    public static int? GetIfMatchVersion(this HttpRequest request)
    {
        var raw = request.Headers[HeaderNames.IfMatch].ToString().Trim();
        if (string.IsNullOrEmpty(raw) || raw == "*")
            return null;

        var value = raw.StartsWith("W/", StringComparison.Ordinal) ? raw[2..] : raw;
        value = value.Trim('"');

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : throw new FormatException("If-Match header must contain the document version, e.g. \"3\"");
    }
}
