namespace RespawnSwitch.Riot.Http;
public static class RiotHttpClientFactory
{
    public static HttpClientHandler CreateHandler(RiotTlsCertificateValidator validator) => new()
    { AllowAutoRedirect = false, UseProxy = false, Proxy = null, ServerCertificateCustomValidationCallback = validator.Validate };
}
