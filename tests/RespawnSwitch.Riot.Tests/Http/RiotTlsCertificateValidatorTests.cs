using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using RespawnSwitch.Riot.Http;

namespace RespawnSwitch.Riot.Tests.Http;

public sealed class RiotTlsCertificateValidatorTests
{
    [Fact]
    public void Validate_AcceptsOnlyCurrentServerCertificateOnAllowedOrigin()
    {
        var validator = new RiotTlsCertificateValidator();
        using var valid = Certificate(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5), serverAuth: true);
        using var expired = Certificate(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(-1), serverAuth: true);
        using var clientOnly = Certificate(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5), serverAuth: false);
        using var allowed = new HttpRequestMessage(HttpMethod.Get, "https://127.0.0.1:2999/liveclientdata/playerlist");
        using var wrongHost = new HttpRequestMessage(HttpMethod.Get, "https://localhost:2999/liveclientdata/playerlist");

        Assert.True(validator.Validate(allowed, valid, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(validator.Validate(wrongHost, valid, null, SslPolicyErrors.None));
        Assert.False(validator.Validate(allowed, expired, null, SslPolicyErrors.None));
        Assert.False(validator.Validate(allowed, clientOnly, null, SslPolicyErrors.None));
        Assert.False(validator.Validate(allowed, null, null, SslPolicyErrors.None));
    }

    private static X509Certificate2 Certificate(
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool serverAuth)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var usages = new OidCollection
        {
            new(serverAuth ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2")
        };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, critical: true));
        return request.CreateSelfSigned(notBefore, notAfter);
    }
}
