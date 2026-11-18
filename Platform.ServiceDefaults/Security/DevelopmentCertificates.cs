using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Carpathians.Platform.ServiceDefaults.Security;

/// <summary>
/// Development: довіра сертифікату розробника ASP.NET Core (dotnet dev-certs) для службових HTTPS-викликів
/// (discovery document IdentityServer, токени client credentials), навіть якщо він не доданий у довірені.
/// В інших середовищах — лише стандартна перевірка сертифіката.
/// </summary>
public static class DevelopmentCertificates
{
    /// <summary>OID розширення, яким позначено сертифікат розробника ASP.NET Core.</summary>
    private const string AspNetCoreDevelopmentCertificateOid = "1.3.6.1.4.1.311.84.1.1";

    public static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        SslOptions =
        {
            RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                errors == SslPolicyErrors.None || IsDevelopmentCertificate(certificate)
        }
    };

    public static bool IsDevelopmentCertificate(X509Certificate? certificate)
        => certificate is not null
           && new X509Certificate2(certificate).Extensions.Any(e => e.Oid?.Value == AspNetCoreDevelopmentCertificateOid);
}
