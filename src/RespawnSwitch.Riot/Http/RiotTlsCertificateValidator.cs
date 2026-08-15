using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace RespawnSwitch.Riot.Http;

public sealed class RiotTlsCertificateValidator
{
    public bool Validate(HttpRequestMessage request, X509Certificate2? certificate, X509Chain? _, SslPolicyErrors __)
    {
        if (request.RequestUri is null || !RiotEndpoint.Allows(request.RequestUri) || certificate is null ||
            DateTimeOffset.UtcNow < certificate.NotBefore || DateTimeOffset.UtcNow > certificate.NotAfter)
            return false;
        foreach (var extension in certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>())
            return extension.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>().Any(x => x.Value == "1.3.6.1.5.5.7.3.1");
        return false;
    }
}
